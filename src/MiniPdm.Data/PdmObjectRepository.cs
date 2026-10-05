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
    /// Создает новый объект PDM в базе данных.
    /// </summary>
    /// <param name="type">Тип объекта</param>
    /// <param name="designation">Обозначение</param>
    /// <param name="name">Наименование</param>
    /// <param name="tx">Транзакция</param>
    /// <returns>ID созданного объекта</returns>
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
            Type = type.ToString(), // ADR 0001: значения CHECK — строки; запись enum — явной строкой
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
    /// <returns>Список объектов PDM, соответствующих заданному поисковому запросу</returns>
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

        var raw = await conn.QueryAsync<ListItemRaw>(sql,
            hasSearch ? new { Pattern = "%" + search!.Trim() + "%" } : null, tx);
        return raw.Select(Map).ToList();
    }

    /// <summary>
    /// Возвращает список корневых объектов PDM (объектов типа "Assembly", не имеющих родительских связей).
    /// </summary>
    /// <param name="conn">Подключение к базе данных</param>
    /// <param name="tx">Транзакция</param>
    /// <returns>Список корневых объектов PDM</returns>
    public async Task<IReadOnlyList<PdmObjectListItem>> ListRootsAsync(
        NpgsqlConnection conn, NpgsqlTransaction? tx = null)
    {
        const string sql = """
                           SELECT po.id AS Id, po.object_type AS Type, po.designation AS Designation,
                                  po.name AS Name, ov.version_no AS CurrentVersionNo, ov.state AS CurrentState
                             FROM pdm_object po
                             LEFT JOIN object_version ov ON ov.id = po.current_version_id
                            WHERE po.object_type = 'Assembly'
                              AND NOT EXISTS (SELECT 1 FROM bom_link bl WHERE bl.child_object_id = po.id)
                            ORDER BY po.designation NULLS LAST, po.name;
                           """;
        var raw = await conn.QueryAsync<ListItemRaw>(sql, transaction: tx);
        return raw.Select(Map).ToList();
    }

    /// <summary>
    /// Возвращает карточку объекта PDM по его идентификатору.
    /// </summary>
    /// <param name="objectId">Идентификатор объекта PDM</param>
    /// <param name="conn">Подключение к базе данных</param>
    /// <param name="tx">Транзакция</param>
    /// <returns>Карточка объекта PDM</returns>
    public async Task<ObjectCard> GetCardAsync(
        long objectId, NpgsqlConnection conn, NpgsqlTransaction? tx = null)
    {
        const string sql = """
                           SELECT po.id AS Id, po.object_type AS Type, po.designation AS Designation,
                                  po.name AS Name,
                                  po.current_version_id AS CurrentVersionId,
                                  ov.version_no AS CurrentVersionNo, ov.state AS CurrentState,
                                  ov.material AS Material, ov.mass_kg AS MassKg
                             FROM pdm_object po
                             LEFT JOIN object_version ov ON ov.id = po.current_version_id
                            WHERE po.id = @ObjectId;
                           """;
        var raw = await conn.QuerySingleOrDefaultAsync<CardRaw>(sql, new { ObjectId = objectId }, tx);

        // Неизвестный id (плейсхолдер, гонка удаления) — легитимный пустой результат,
        // а не исключение: VM интерпретирует null как «нет карточки».
        return raw is null
            ? ObjectCard.Empty
            : new ObjectCard(
                raw.Id,
                Enum.Parse<ObjectType>(raw.Type),
                raw.Designation,
                raw.Name,
                raw.CurrentVersionId,
                raw.CurrentVersionNo,
                raw.CurrentState is null ? null : Enum.Parse<ObjectState>(raw.CurrentState),
                raw.Material,
                raw.MassKg);
    }

    /// <summary>
    /// Преобразует объект ListItemRaw в объект PdmObjectListItem.
    /// </summary>
    /// <param name="r">Сырой объект списка</param>
    /// <returns>Объект списка PDM</returns>
    private static PdmObjectListItem Map(ListItemRaw r) => new(
        r.Id,
        Enum.Parse<ObjectType>(r.Type),
        r.Designation,
        r.Name,
        r.CurrentVersionNo,
        r.CurrentState is null ? null : Enum.Parse<ObjectState>(r.CurrentState));

}