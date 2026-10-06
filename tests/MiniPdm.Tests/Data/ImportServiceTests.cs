using Dapper;
using MiniPdm.Core.Cad;
using MiniPdm.Core.Domain;
using MiniPdm.Core.Import;
using MiniPdm.Data.Infrastructure;
using MiniPdm.Data.Services;
using Npgsql;

namespace MiniPdm.Tests.Data;

/// <summary>Сценарии импорта против живой БД (готовые кейсы для README).
/// Пространство данных — ТЕСТ.* (Р.1a); очистка перед каждым тестом.</summary>
[Collection("Database")]
public sealed class ImportServiceTests : IAsyncLifetime
{
    private readonly Db _db = new(TestData.ConnectionString);
    private readonly VersionService _versions = new();
    private NpgsqlConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = await _db.OpenConnectionAsync();

        // bom_link удаляем первыми: child_object_id имеет RESTRICT,
        // связи родителя удалятся каскадом вместе с версиями объекта.
        await _connection.ExecuteAsync("""
            DELETE FROM bom_link
             WHERE child_object_id IN (SELECT id FROM pdm_object
                                        WHERE designation LIKE 'ТЕСТ.%'
                                           OR (object_type = 'StandardPart' AND name LIKE 'ТЕСТ%'));
            DELETE FROM pdm_object
             WHERE designation LIKE 'ТЕСТ.%'
                OR (object_type = 'StandardPart' AND name LIKE 'ТЕСТ%');
            """);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    // ---------- хелперы ----------

    private ImportService Service(IReadOnlyList<CadDocument> docs, IReadOnlyList<string>? broken = null) =>
        new(new FakeCadDocumentReader(docs, broken), _db);

    private static CadDocument Part(string name, string designation, decimal? mass = 1m) => new()
    {
        FileName = name + ".m3d",
        Type = ObjectType.Part,
        Designation = designation,
        Name = name,
        Material = "Сталь 40Х",
        MassKg = mass
    };

    private static CadDocument StdPart(string name, decimal mass = 0.1m) => new()
    {
        FileName = name + ".m3d",
        Type = ObjectType.StandardPart,
        Name = name,                       // имя = идентичность стандартного изделия
        MassKg = mass
    };

    private static CadDocument Assembly(string name, string designation, params CadComponent[] comps) => new()
    {
        FileName = name + ".a3d",
        Type = ObjectType.Assembly,
        Designation = designation,
        Name = name,
        Components = comps
    };

    private Task<long> ObjectIdByDesignationAsync(string designation) =>
        _connection.QuerySingleAsync<long>(
            "SELECT id FROM pdm_object WHERE designation = @D", new { D = designation });

    private static bool Rejected(ImportAnalysis a, string file) =>
        a.Importable.All(d => d.FileName != file) &&
        a.Issues.Any(i => i.FileName == file && i.Severity == ImportSeverity.Error);

    private sealed record VersionRow(int VersionNo, ObjectState State, decimal? MassKg);

    // ---------- тесты ----------

    [Fact]
    public async Task Clean_set_creates_objects_versions_and_composition()
    {
        var bolt = Part("Болт", TestData.NextDesignation());
        var nut = StdPart("ТЕСТ Гайка");
        var node = Assembly("Узел", TestData.NextDesignation(),
            new CadComponent(bolt.FileName, 4),
            new CadComponent(nut.FileName, 2));

        var result = await Service([node, bolt, nut]).ImportFolderAsync("any");

        Assert.Equal(3, result.AcceptedCount);
        Assert.Empty(result.Issues);

        var nodeId = await ObjectIdByDesignationAsync(node.Designation!);
        var links = (await _connection.QueryAsync<(long ChildId, int Qty)>(
            """
            SELECT bl.child_object_id AS ChildId, bl.quantity AS Qty
              FROM bom_link bl
             WHERE bl.parent_version_id =
                   (SELECT current_version_id FROM pdm_object WHERE id = @Id)
            """, new { Id = nodeId })).ToList();

        Assert.Equal(2, links.Count);                       // состав первой версии записан
        Assert.Contains(links, l => l.Qty == 4);
        Assert.Contains(links, l => l.Qty == 2);

        var severities = await _connection.QueryAsync<string>(
            "SELECT severity FROM import_log WHERE file_name = @F", new { F = node.FileName });
        Assert.Contains("Info", severities);
    }

    [Fact]
    public async Task Repeat_import_of_same_data_is_noop()
    {
        var wheel = Part("Колесо", TestData.NextDesignation(), 5.86m);
        var service = Service([wheel]);

        await service.ImportFolderAsync("any");
        await service.ImportFolderAsync("any");             // повторный импорт

        var versionCount = await _connection.QuerySingleAsync<int>(
            """
            SELECT COUNT(*)::int
              FROM object_version ov
              JOIN pdm_object po ON po.id = ov.object_id
             WHERE po.designation = @D
            """, new { D = wheel.Designation });
        Assert.Equal(1, versionCount);                      // «ничего не происходит» — версии не создались

        var reason = await _connection.QuerySingleAsync<string?>(
            """
            SELECT reason FROM import_log
             WHERE file_name = @F AND severity = 'Info'
             ORDER BY id DESC LIMIT 1
            """, new { F = wheel.FileName });
        Assert.Equal("данные не изменились", reason);
    }

    [Fact]
    public async Task Approved_object_with_changed_data_gets_version_two()   // v2-кейс колеса
    {
        var wheel = Part("Колесо", TestData.NextDesignation(), 5.86m);
        await Service([wheel]).ImportFolderAsync("any");

        var v1 = await _connection.QuerySingleAsync<long>(
            """
            SELECT ov.id
              FROM object_version ov
              JOIN pdm_object po ON po.id = ov.object_id
             WHERE po.designation = @D AND ov.version_no = 1
            """, new { D = wheel.Designation });

        await using var tx = await _connection.BeginTransactionAsync();
        await _versions.SetStateAsync(v1, ObjectState.Approved, tx);
        await tx.CommitAsync();

        var changed = wheel with { MassKg = 5.92m };
        var result = await Service([changed]).ImportFolderAsync("any");

        Assert.Empty(result.Issues);

        var versions = (await _connection.QueryAsync<VersionRow>(
            """
            SELECT ov.version_no AS VersionNo, ov.state AS State, ov.mass_kg AS MassKg
              FROM object_version ov
              JOIN pdm_object po ON po.id = ov.object_id
             WHERE po.designation = @D
             ORDER BY ov.version_no
            """, new { D = wheel.Designation })).ToList();

        Assert.Equal(2, versions.Count);
        Assert.Equal(1, versions[0].VersionNo);
        Assert.Equal(ObjectState.Approved, versions[0].State);
        Assert.Equal(5.86m, versions[0].MassKg);
        Assert.Equal(2, versions[1].VersionNo);
        Assert.Equal(ObjectState.InWork, versions[1].State);
        Assert.Equal(5.92m, versions[1].MassKg);
    }

    [Fact]
    public async Task Type_conflict_is_reported_and_database_untouched()
    {
        var assembly = Assembly("Механизм", TestData.NextDesignation());
        await Service([assembly]).ImportFolderAsync("any");

        // Та же идентичность (обозначение), другой тип — вердикт «Ошибка», БД не тронута.
        var impostor = Part("Самозванец", assembly.Designation!);
        var result = await Service([impostor]).ImportFolderAsync("any");

        Assert.True(Rejected(result, impostor.FileName));
        Assert.Contains(result.Issues,
            i => i.FileName == impostor.FileName && i.Reason.Contains("другого типа"));

        var type = await _connection.QuerySingleAsync<string>(
            "SELECT object_type FROM pdm_object WHERE designation = @D",
            new { D = assembly.Designation });
        Assert.Equal("Assembly", type);
    }

    [Fact]
    public async Task Db_cascade_conflict_rejects_parent_too()
    {
        // Идентичность сборки/детали — обозначение (ТЗ): самозванец-деталь
        // с обозначением существующей сборки даёт TypeConflict на этапе БД,
        // а узел, ссылающийся на него, снимается каскадом (ТЗ: «и так далее вверх по дереву»).
        var conflicting = Assembly("Конфликт", TestData.NextDesignation());
        await Service([conflicting]).ImportFolderAsync("any");

        var bolt = Part("Болт", TestData.NextDesignation());
        var impostor = Part("Самозванец", conflicting.Designation!);
        var node = Assembly("Узел", TestData.NextDesignation(),
            new CadComponent(bolt.FileName, 1),
            new CadComponent(impostor.FileName, 1));

        var result = await Service([node, bolt, impostor]).ImportFolderAsync("any");

        Assert.True(Rejected(result, impostor.FileName));   // TypeConflict
        Assert.True(Rejected(result, node.FileName));       // каскад
        Assert.Contains(result.Importable, d => d.FileName == bolt.FileName);
    }

    [Fact]
    public async Task Read_error_does_not_block_other_files()
    {
        var good = Part("Деталь", TestData.NextDesignation());
        const string broken = "Битый.m3d";                  // есть в перечислении, падает при чтении

        var result = await Service([good], [broken]).ImportFolderAsync("any");

        Assert.True(Rejected(result, broken));
        Assert.Contains(result.Importable, d => d.FileName == good.FileName);

        var created = await _connection.QuerySingleAsync<int>(
            "SELECT COUNT(*)::int FROM pdm_object WHERE designation = @D",
            new { D = good.Designation });
        Assert.Equal(1, created);                           // остальные файлы импортированы
    }
}