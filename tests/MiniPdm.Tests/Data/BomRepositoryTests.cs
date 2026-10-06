using Dapper;
using MiniPdm.Core.Bom;
using MiniPdm.Core.Cad;
using MiniPdm.Core.Domain;
using MiniPdm.Data.Infrastructure;
using MiniPdm.Data.Repositories;
using MiniPdm.Data.Services;
using Npgsql;

namespace MiniPdm.Tests.Data;

[Collection("Database")]
public sealed class BomRepositoryTests : IAsyncLifetime
{
    private readonly Db _db = new(TestData.ConnectionString);
    private readonly VersionService _versions = new();
    private NpgsqlConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = await _db.OpenConnectionAsync();
        await _connection.ExecuteAsync("""
            DELETE FROM bom_link WHERE child_object_id IN (
                       SELECT id FROM pdm_object
                        WHERE designation LIKE 'ТЕСТ.%'
                           OR (object_type = 'StandardPart' AND name LIKE 'ТЕСТ%'));
            DELETE FROM pdm_object
             WHERE designation LIKE 'ТЕСТ.%'
                OR (object_type = 'StandardPart' AND name LIKE 'ТЕСТ%');
            """);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Tree_and_mass_after_import()
    {
        var wheel = new CadDocument { FileName = "Колесо.m3d", Type = ObjectType.Part,
            Designation = TestData.NextDesignation(), Name = "Колесо", Material = "Сталь", MassKg = 5.86m };
        var root = new CadDocument { FileName = "Привод.a3d", Type = ObjectType.Assembly,
            Designation = TestData.NextDesignation(), Name = "Привод",
            Components = [new CadComponent("Колесо.m3d", 2)] };

        await new ImportService(new FakeCadDocumentReader([root, wheel]), _db).ImportFolderAsync("any");

        var rootId = await _connection.QuerySingleAsync<long>(
            "SELECT id FROM pdm_object WHERE designation = @D", new { D = root.Designation });

        var tree = await new BomRepository().LoadTreeAsync(rootId, _connection);

        var row = Assert.Single(tree);
        Assert.True(row.HasActiveVersion);
        Assert.Equal(2, row.QuantityOnPath);
        Assert.Equal(ObjectState.InWork, row.State);

        var mass = BomCalculator.TotalMass(tree);
        Assert.Equal(11.72m, mass.TotalMassKg);   // 5.86 × 2 — Core поверх Data
    }
}