using Dapper;
using MiniPdm.Core.Bom;
using MiniPdm.Core.Domain;
using Npgsql;

namespace MiniPdm.Data;

/// <summary>Чтение дерева состава одним рекурсивным CTE (ТЗ п.2.3 — один запрос,
/// а не цикл из C#).</summary>
public sealed class BomRepository
{
    /// <summary>Дерево от корневой сборки (её текущая версия).
    /// ADR 0009: LEFT JOIN финальной выборки — узел без действующей версии
    /// остаётся строкой (HasActiveVersion = false); внутренний JOIN шага
    /// рекурсии не раскрывает его состав. Guard NOT = ANY(path) — защита от
    /// зацикливания при ручной правке БД (импортируется только ациклический граф).
    /// У корня без действующей версии дерево пусто.</summary>
    public async Task<IReadOnlyList<BomRow>> LoadTreeAsync(
        long rootObjectId, NpgsqlConnection conn, NpgsqlTransaction? tx = null)
    {
        const string sql = """
            WITH RECURSIVE tree AS (
                SELECT bl.child_object_id AS object_id,
                       bl.quantity        AS quantity_on_path,
                       ARRAY[bl.child_object_id::bigint] AS path
                  FROM bom_link bl
                 WHERE bl.parent_version_id =
                       (SELECT current_version_id FROM pdm_object WHERE id = @RootObjectId)
                UNION ALL
                SELECT bl.child_object_id,
                       t.quantity_on_path * bl.quantity,
                       t.path || bl.child_object_id::bigint
                  FROM tree t
                  JOIN pdm_object po ON po.id = t.object_id
                  JOIN bom_link  bl ON bl.parent_version_id = po.current_version_id
                 WHERE NOT (bl.child_object_id = ANY(t.path))
            )
            SELECT t.path              AS Path,
                   po.object_type      AS Type,
                   po.designation      AS Designation,
                   po.name             AS Name,
                   ov.state            AS State,
                   t.quantity_on_path  AS QuantityOnPath,
                   (ov.id IS NOT NULL) AS HasActiveVersion,
                   ov.mass_kg          AS UnitMassKg
              FROM tree t
              JOIN pdm_object po ON po.id = t.object_id
              LEFT JOIN object_version ov ON ov.id = po.current_version_id
             ORDER BY t.path;
            """;

        // Сырой класс с примитивными свойствами: Dapper присваивает без конверсий
        // (позиционный record падает: типы колонок — строки, не enum).
        var raw = await conn.QueryAsync<TreeRowRaw>(sql, new { RootObjectId = rootObjectId }, tx);

        return raw
            .Select(r => new BomRow(
                r.Path,
                Enum.Parse<ObjectType>(r.Type),
                r.Designation,
                r.Name,
                r.State is null ? null : Enum.Parse<ObjectState>(r.State),
                r.QuantityOnPath,
                r.HasActiveVersion,
                r.UnitMassKg))
            .ToList();
    }

    private sealed class TreeRowRaw
    {
        public long[] Path { get; set; } = [];
        public string Type { get; set; } = "";
        public string? Designation { get; set; }
        public string Name { get; set; } = "";
        public string? State { get; set; }
        public int QuantityOnPath { get; set; }
        public bool HasActiveVersion { get; set; }
        public decimal? UnitMassKg { get; set; }
    }
}