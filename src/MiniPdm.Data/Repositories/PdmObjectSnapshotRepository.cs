using Dapper;
using MiniPdm.Core.Domain;
using MiniPdm.Core.Import;
using MiniPdm.Data.Models;
using Npgsql;

namespace MiniPdm.Data.Repositories;

/// <summary>
/// Репозиторий для загрузки снимка состояния объектов PDM из базы данных.
/// </summary>
public sealed class PdmObjectSnapshotRepository
{
    
    /// <summary>
    /// Загружает снимок состояния всех объектов PDM из базы данных.
    /// </summary>
    /// <param name="conn">Подключение к базе данных</param>
    /// <param name="tx">Транзакция</param>
    /// <returns>Снимок состояния всех объектов PDM</returns>
    public async Task<ObjectSnapshotSet> LoadAllAsync(NpgsqlConnection conn, NpgsqlTransaction tx)
    {
        const string objectsSql = """
            SELECT id AS Id, object_type AS Type, designation AS Designation, name AS Name
              FROM pdm_object;
            """;
        // LEFT JOIN объект без действующей версии даёт строку с NULL-версией (ADR 0009/0004).
        const string currentVersionsSql = """
            SELECT po.id AS ObjectId, ov.id AS VersionId, ov.state AS State,
                   ov.material AS Material, ov.mass_kg AS MassKg
              FROM pdm_object po
              LEFT JOIN object_version ov ON ov.id = po.current_version_id;
            """;
        // Состав только текущих версий inner JOIN отсекает объекты без указателя.
        const string compositionSql = """
            SELECT po.id AS ParentObjectId, ch.object_type AS ChildType,
                   ch.designation AS ChildDesignation, ch.name AS ChildName,
                   bl.quantity AS Quantity
              FROM pdm_object po
              JOIN bom_link bl ON bl.parent_version_id = po.current_version_id
              JOIN pdm_object ch ON ch.id = bl.child_object_id;
            """;

        var objects = (await conn.QueryAsync<ObjectRow>(objectsSql, transaction: tx)).ToList();
        var versions = (await conn.QueryAsync<CurrentVersionRow>(currentVersionsSql, transaction: tx))
            .ToDictionary(v => v.ObjectId);
        var composition = (await conn.QueryAsync<CompositionRow>(compositionSql, transaction: tx))
            .GroupBy(c => c.ParentObjectId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ExistingChild>)g
                    .Select(c => new ExistingChild(c.ChildType, c.ChildDesignation, c.ChildName, c.Quantity))
                    .ToList());

        var byIdentity = new Dictionary<string, ExistingObjectSnapshot>(StringComparer.Ordinal);
        var idByIdentity = new Dictionary<string, long>(StringComparer.Ordinal);
        var versionIdByIdentity = new Dictionary<string, long?>(StringComparer.Ordinal);

        foreach (var o in objects)
        {
            var key = ObjectIdentity.Key(o.Type, o.Designation, o.Name);
            idByIdentity[key] = o.Id;
            versions.TryGetValue(o.Id, out var v);
            versionIdByIdentity[key] = v?.VersionId;
            byIdentity[key] = new ExistingObjectSnapshot(
                o.Type, o.Designation, o.Name,
                v?.State, v?.Material, v?.MassKg,
                composition.TryGetValue(o.Id, out var comp) ? comp : []);
        }

        return new ObjectSnapshotSet(byIdentity, idByIdentity, versionIdByIdentity);
    }
}