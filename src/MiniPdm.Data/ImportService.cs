using Dapper;
using MiniPdm.Core.Cad;
using MiniPdm.Core.Domain;
using MiniPdm.Core.Import;
using Npgsql;

namespace MiniPdm.Data;

/// <summary>
/// Оркестратор импорта (ТЗ п.2.4): чтение файлов и анализ — ДО транзакции,
/// вся запись — в ОДНОЙ транзакции. Единственное место слоя, знающее
/// ICadDocumentReader (ТЗ: логика импорта тестируется с подменой ридера).
/// </summary>
public sealed class ImportService
{
    private readonly ICadDocumentReader _reader;
    private readonly Db _db;
    private readonly ImportAnalyzer _analyzer = new();
    private readonly PdmObjectRepository _objects = new();
    private readonly VersionService _versions = new();
    private readonly BomLinkRepository _links = new();
    private readonly PdmObjectSnapshotRepository _snapshots = new();

    public ImportService(ICadDocumentReader reader, Db db)
    {
        _reader = reader;
        _db = db;
    }

    public async Task<ImportAnalysis> ImportFolderAsync(string folder, CancellationToken ct = default)
    {
        // 1. Чтение: ошибка одного документа — вердикт, остальные продолжаются (ТЗ).
        var paths = await _reader.ListDocumentsAsync(folder, ct);
        var documents = new List<CadDocument>();
        var readIssues = new List<ImportIssue>();
        foreach (var path in paths)
        {
            try
            {
                documents.Add(await _reader.ReadAsync(path, ct));
            }
            catch (CadDocumentException e)
            {
                readIssues.Add(new ImportIssue(Path.GetFileName(path), ImportSeverity.Error, e.Message));
            }
        }

        // 2. Анализ набора — чистая функция ядра.
        var analysis = _analyzer.Analyze(documents);
        var docsByName = analysis.Importable.ToDictionary(d => d.FileName, StringComparer.Ordinal);

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var snapshots = await _snapshots.LoadAllAsync(conn, tx);

            // 3. Решения против состояния БД (повторный импорт, ТЗ «Предметная область»).
            var actions = new Dictionary<string, ReImportAction>(StringComparer.Ordinal);
            var dbIssues = new List<ImportIssue>();
            foreach (var doc in analysis.Importable)
            {
                snapshots.ByIdentity.TryGetValue(IdentityOf(doc), out var existing);
                var action = ReImportRules.Decide(doc, existing, docsByName);
                if (action == ReImportAction.TypeConflict)
                    dbIssues.Add(new ImportIssue(doc.FileName, ImportSeverity.Error,
                        $"в базе уже есть объект с такой идентичностью другого типа ({existing!.Type})"));
                else
                    actions[doc.FileName] = action;
            }

            // 4. Каскад на этапе БД: сборка, чей компонент не импортируется, тоже не импортируется.
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var fileName in actions.Keys.ToList())
                {
                    var bad = docsByName[fileName].Components
                        .Select(c => c.FileName)
                        .FirstOrDefault(f => !actions.ContainsKey(f));
                    if (bad is null) continue;

                    actions.Remove(fileName);
                    dbIssues.Add(new ImportIssue(fileName, ImportSeverity.Error,
                        $"компонент «{bad}» не импортирован"));
                    changed = true;
                }
            }

            // 5. Фаза 1: новые объекты (id по RETURNING, Р.2a) — нужны составам.
            var newIds = new Dictionary<string, long>(StringComparer.Ordinal);
            var versionIds = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var doc in analysis.Importable)
            {
                if (!actions.TryGetValue(doc.FileName, out var phaseOne) ||
                    phaseOne != ReImportAction.CreateObject) continue;

                var id = await _objects.CreateAsync(doc.Type, doc.Designation, doc.Name, tx);
                newIds[IdentityOf(doc)] = id;
                versionIds[IdentityOf(doc)] =
                    await _versions.CreateInitialVersionAsync(id, doc.Material, doc.MassKg, tx);
            }

            long IdOf(CadDocument d) =>
                newIds.TryGetValue(IdentityOf(d), out var id)
                    ? id
                    : snapshots.IdByIdentity[IdentityOf(d)];

            long VersionIdOf(CadDocument d) =>
                versionIds.TryGetValue(IdentityOf(d), out var vid)
                    ? vid
                    : snapshots.CurrentVersionIdByIdentity[IdentityOf(d)]
                      ?? throw new InvalidOperationException(
                          $"У объекта {d.FileName} нет действующей версии для записи состава.");

            // 6. Фаза 2: обновления, новые версии, составы.
            foreach (var doc in analysis.Importable)
            {
                if (!actions.TryGetValue(doc.FileName, out var action)) continue; // снят каскадом
                if (action == ReImportAction.Unchanged) continue;                 // ТЗ: ничего не происходит

                switch (action)
                {
                    case ReImportAction.UpdateInWorkVersion:
                        await _versions.UpdateInWorkVersionAsync(
                            VersionIdOf(doc), doc.Material, doc.MassKg, tx);
                        break;

                    case ReImportAction.CreateNewVersion:
                        versionIds[IdentityOf(doc)] = await _versions.CreateNextVersionAsync(
                            IdOf(doc), doc.Material, doc.MassKg, tx);
                        break;

                    case ReImportAction.CreateObject:
                        break; // версия создана в фазе 1
                }

                if (doc.Type == ObjectType.Assembly)
                {
                    var links = doc.Components
                        .Select(c => new BomLinkRow(IdOf(docsByName[c.FileName]), c.Count))
                        .ToList();
                    await _links.ReplaceCompositionAsync(VersionIdOf(doc), links, tx);
                }
            }

            // 7. Итоговые множества отчёта и журнал.
            var failedAtDb = dbIssues.Select(i => i.FileName).ToHashSet(StringComparer.Ordinal);
            var finalImportable = analysis.Importable
                .Where(d => !failedAtDb.Contains(d.FileName)).ToList();
            var finalIssues = analysis.Issues.Concat(readIssues).Concat(dbIssues).ToList();

            await WriteImportLogAsync(tx, finalImportable, finalIssues, actions);
            await tx.CommitAsync(ct);

            return new ImportAnalysis(finalImportable, finalIssues, analysis.Cycles);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static string IdentityOf(CadDocument doc) =>
        ObjectIdentity.Key(doc.Type, doc.Designation, doc.Name);

    /// <summary>Строка на файл (Р.6а): Error/Warning из итоговых issues,
    /// Info — принятые без замечаний; Unchanged получает причину «данные не изменились».</summary>
    private static async Task WriteImportLogAsync(
        NpgsqlTransaction tx,
        IReadOnlyList<CadDocument> importable,
        IReadOnlyList<ImportIssue> issues,
        IReadOnlyDictionary<string, ReImportAction> actions)
    {
        var rows = new List<(string FileName, string Severity, string? Reason)>();

        foreach (var group in issues.GroupBy(i => i.FileName, StringComparer.Ordinal))
            rows.Add((
                group.Key,
                group.Any(i => i.Severity == ImportSeverity.Error) ? "Error" : "Warning",
                string.Join("; ", group.Select(i => i.Reason))));

        foreach (var doc in importable.Where(d => !issues.Any(i => i.FileName == d.FileName)))
            rows.Add((
                doc.FileName, "Info",
                actions.GetValueOrDefault(doc.FileName) == ReImportAction.Unchanged
                    ? "данные не изменились"
                    : null));

        await tx.Connection!.ExecuteAsync("""
            INSERT INTO import_log (file_name, severity, reason)
            VALUES (@FileName, @Severity, @Reason);
            """, rows.Select(r => new { r.FileName, r.Severity, r.Reason }), tx);
    }
}