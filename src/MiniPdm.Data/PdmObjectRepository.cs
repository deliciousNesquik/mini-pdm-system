using Dapper;
using MiniPdm.Core.Domain;
using Npgsql;

namespace MiniPdm.Data;

/// <summary>
/// Репозиторий для работы с объектами PDM (Product Data Management) в базе данных.
/// </summary>
public sealed class PdmObjectRepository
{
    /// <summary>
    /// Создает новый объект PDM в базе данных и возвращает его идентификатор.
    /// </summary>
    /// <param name="type">Тип объекта</param>
    /// <param name="designation">Обозначение</param>
    /// <param name="name">Наименование</param>
    /// <param name="tx">Транзакция</param>
    /// <returns>Идентификатор созданного объекта</returns>
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
            Type = type.ToString(),
            Designation = designation,
            Name = name
        }, tx);
    }

    /// <summary>
    /// Возвращает список объектов PDM, соответствующих заданному поисковому запросу.
    /// </summary>
    /// <param name="search">Поисковый запрос</param>
    /// <param name="conn">Подключение к базе данных</param>
    /// <param name="tx">Транзакция</param>
    /// <returns>Список объектов PDM, соответствующих поисковому запросу</returns>
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