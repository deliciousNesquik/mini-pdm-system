using Dapper;
using MiniPdm.Core.Cad;
using MiniPdm.Core.Domain;
using MiniPdm.Core.Import;
using Npgsql;

namespace MiniPdm.Data;

/// <summary>
/// Сервис импорта CAD-документов в базу данных.
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

    /// <summary>
    /// Импортирует CAD-документы из указанной папки в базу данных.
    /// </summary>
    /// <param name="folder">Путь к папке с CAD-документами</param>
    /// <param name="progress">Индикатор прогресса</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>Результат анализа импорта</returns>
    /// <exception cref="InvalidOperationException">Возникает при ошибке импорта</exception>
    public async Task<ImportAnalysis> ImportFolderAsync(
        string folder,
        IProgress<ImportProgress>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report(new ImportProgress(ImportPhase.Reading, 0, null, null));
        var paths = await _reader.ListDocumentsAsync(folder, ct);
        var documents = new List<CadDocument>(paths.Count);
        var readIssues = new List<ImportIssue>();

        for (var i = 0; i < paths.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var path = paths[i];
            try
            {
                documents.Add(await _reader.ReadAsync(path, ct));
            }
            catch (CadDocumentException e)
            {
                readIssues.Add(new ImportIssue(Path.GetFileName(path), ImportSeverity.Error, e.Message));
            }

            progress?.Report(new ImportProgress(
                ImportPhase.Reading, i + 1, paths.Count, Path.GetFileName(path)));
        }
        
        progress?.Report(new ImportProgress(ImportPhase.Analyzing, 0, null, null));
        ct.ThrowIfCancellationRequested();
        var analysis = _analyzer.Analyze(documents);
        var docsByName = analysis.Importable.ToDictionary(d => d.FileName, StringComparer.Ordinal);

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            progress?.Report(new ImportProgress(ImportPhase.Writing, 0, analysis.Importable.Count, null));
            var snapshots = await _snapshots.LoadAllAsync(conn, tx);
            
            var actions = new Dictionary<string, ReImportAction>(StringComparer.Ordinal);
            var dbIssues = new List<ImportIssue>();
            foreach (var doc in analysis.Importable)
            {
                ct.ThrowIfCancellationRequested();
                snapshots.ByIdentity.TryGetValue(IdentityOf(doc), out var existing);
                var action = ReImportRules.Decide(doc, existing, docsByName);
                if (action == ReImportAction.TypeConflict)
                {
                    dbIssues.Add(new ImportIssue(doc.FileName, ImportSeverity.Error,
                        $"в базе уже есть объект с такой идентичностью другого типа ({existing!.Type})"));
                }
                else
                {
                    actions[doc.FileName] = action;
                }
            }
            
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
            
            var newIds = new Dictionary<string, long>(StringComparer.Ordinal);
            var versionIds = new Dictionary<string, long>(StringComparer.Ordinal);
            var position = 0;
            foreach (var doc in analysis.Importable)
            {
                ct.ThrowIfCancellationRequested();
                position++;
                if (!actions.TryGetValue(doc.FileName, out var action) ||
                    action != ReImportAction.CreateObject)
                {
                    continue;
                }

                var id = await _objects.CreateAsync(doc.Type, doc.Designation, doc.Name, tx);
                newIds[IdentityOf(doc)] = id;
                versionIds[IdentityOf(doc)] =
                    await _versions.CreateInitialVersionAsync(id, doc.Material, doc.MassKg, tx);

                progress?.Report(new ImportProgress(
                    ImportPhase.Writing, position, analysis.Importable.Count, doc.FileName));
            }

            long IdOf(CadDocument d) =>
                newIds.TryGetValue(IdentityOf(d), out var id)
                    ? id
                    : snapshots.IdByIdentity[IdentityOf(d)];

            long VersionIdOf(CadDocument d) =>
                versionIds.TryGetValue(IdentityOf(d), out var versionId)
                    ? versionId
                    : snapshots.CurrentVersionIdByIdentity[IdentityOf(d)]
                      ?? throw new InvalidOperationException(
                          $"У объекта «{d.FileName}» нет действующей версии для записи состава.");
            
            foreach (var doc in analysis.Importable)
            {
                ct.ThrowIfCancellationRequested();
                if (!actions.TryGetValue(doc.FileName, out var action)) continue;
                if (action == ReImportAction.Unchanged) continue;

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

                progress?.Report(new ImportProgress(
                    ImportPhase.Writing, 0, analysis.Importable.Count, doc.FileName));
            }
            
            var failedAtDb = dbIssues.Select(i => i.FileName).ToHashSet(StringComparer.Ordinal);
            var finalImportable = analysis.Importable
                .Where(d => !failedAtDb.Contains(d.FileName)).ToList();
            var finalIssues = analysis.Issues.Concat(readIssues).Concat(dbIssues).ToList();

            progress?.Report(new ImportProgress(ImportPhase.Committing, 0, null, null));
            await WriteImportLogAsync(tx, finalImportable, finalIssues, actions);
            await tx.CommitAsync(ct);

            progress?.Report(new ImportProgress(ImportPhase.Done, 0, null, null));
            return new ImportAnalysis(finalImportable, finalIssues, analysis.Cycles);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Возвращает уникальный идентификатор CAD-документа на основе его типа, обозначения и имени.
    /// </summary>
    /// <param name="doc">CAD-документ</param>
    /// <returns>Уникальный идентификатор</returns>
    private static string IdentityOf(CadDocument doc) =>
        ObjectIdentity.Key(doc.Type, doc.Designation, doc.Name);

    /// <summary>
    /// Записывает лог импорта в базу данных.
    /// </summary>
    /// <param name="tx">Транзакция базы данных</param>
    /// <param name="importable">Список импортируемых документов</param>
    /// <param name="issues">Список проблем импорта</param>
    /// <param name="actions">Словарь действий импорта</param>
    private static async Task WriteImportLogAsync(
        NpgsqlTransaction tx,
        IReadOnlyList<CadDocument> importable,
        IReadOnlyList<ImportIssue> issues,
        IReadOnlyDictionary<string, ReImportAction> actions)
    {
        var rows = new List<(string FileName, string Severity, string? Reason)>();

        foreach (var group in issues.GroupBy(i => i.FileName, StringComparer.Ordinal))
        {
            rows.Add((
                group.Key,
                group.Any(i => i.Severity == ImportSeverity.Error) ? "Error" : "Warning",
                string.Join("; ", group.Select(i => i.Reason))));
        }

        foreach (var doc in importable.Where(d => !issues.Any(i => i.FileName == d.FileName)))
        {
            rows.Add((
                doc.FileName, "Info",
                actions.GetValueOrDefault(doc.FileName) == ReImportAction.Unchanged
                    ? "данные не изменились"
                    : null));
        }

        await tx.Connection!.ExecuteAsync("""
            INSERT INTO import_log (file_name, severity, reason)
            VALUES (@FileName, @Severity, @Reason);
            """, rows.Select(r => new { r.FileName, r.Severity, r.Reason }), tx);
    }
}