using Dapper;
using MiniPdm.Core.Domain;
using Npgsql;

namespace MiniPdm.Data;

public sealed record PdmObjectListItem(
    long Id, ObjectType Type, string? Designation, string Name,
    int? CurrentVersionNo, ObjectState? CurrentState);

/// <summary>pdm_object: создание (ADR 0011, Р.2a) и чтение списка/поиск для UI.</summary>
public sealed class PdmObjectRepository
{
    /// <summary>Создаёт объект, возвращает id. Уникальность защищают частичные
    /// индексы схемы; 23505 сюда дойти не должен — ReImportRules ловит конфликт раньше.</summary>
    public async Task<long> CreateAsync(
        ObjectType type, string? designation, string name, NpgsqlTransaction tx)
    {
        const string sql = """
            INSERT INTO pdm_object (object_type, designation, name)
            VALUES (@Type, @Designation, @Name)
            RETURNING id;
            """;
        
        return await tx.Connection!.QuerySingleAsync<long>(sql, new
        {
            Type = type.ToString(),   // ADR 0001: значения CHECK — строки; явная конвертация,
            Designation = designation,
            Name = name
        }, tx);
    }

    /// <summary>Список объектов для дерева-корней UI; search — подстрока
    /// обозначения или наименования. SQL собирается условно: Npgsql не любит
    /// NULL-параметры в ILIKE-сравнениях без явного типа.</summary>
    public async Task<IReadOnlyList<PdmObjectListItem>> ListAsync(
        string? search, NpgsqlConnection conn, NpgsqlTransaction? tx = null)
    {
        const string baseSql = """
            SELECT po.id AS Id, po.object_type AS Type, po.designation AS Designation,
                   po.name AS Name, ov.version_no AS CurrentVersionNo, ov.state AS CurrentState
              FROM pdm_object po
              LEFT JOIN object_version ov ON ov.id = po.current_version_id
            """;
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var sql = hasSearch
            ? baseSql + " WHERE po.designation ILIKE @Pattern OR po.name ILIKE @Pattern\n"
            : baseSql;
        sql += " ORDER BY po.designation NULLS LAST, po.name;";

        var rows = await conn.QueryAsync<PdmObjectListItem>(sql,
            hasSearch ? new { Pattern = "%" + search!.Trim() + "%" } : null, tx);
        return rows.ToList();
    }
}