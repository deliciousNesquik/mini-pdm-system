using Dapper;
using MiniPdm.Core.Bom;

namespace MiniPdm.Data;

/// <summary>
/// Сервис чтения данных для UI.
/// </summary>
public sealed class UiReadService
{
    private readonly Db _db;
    private readonly PdmObjectRepository _objects = new();
    private readonly BomRepository _bom = new();

    public UiReadService(Db db) => _db = db;

    /// <summary>
    /// Список корневых объектов - для ленивого дерева.
    /// </summary>
    /// <param name="ct">Токен отмены</param>
    /// <returns>Список корневых объектов</returns>
    public Task<IReadOnlyList<PdmObjectListItem>> GetRootsAsync(CancellationToken ct = default) =>
        _db.ExecuteAsync(conn => _objects.ListRootsAsync(conn), ct);

    /// <summary>
    /// Поиск по подстроке обозначения или наименования — по всем объектам,
    /// не только корням: деталь из глубины состава тоже должна находиться (ТЗ).
    /// </summary>
    /// <param name="search">Строка поиска</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>Список найденных объектов</returns>
    public Task<IReadOnlyList<PdmObjectListItem>> SearchAsync(string search, CancellationToken ct = default) =>
        _db.ExecuteAsync(conn => _objects.ListAsync(search, conn), ct);

    /// <summary>
    /// Один уровень состава узла — для ленивого дерева.
    /// </summary>
    /// <param name="parentObjectId">Идентификатор родительского объекта</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>Список дочерних объектов</returns>
    public Task<IReadOnlyList<BomRow>> GetChildrenAsync(long parentObjectId, CancellationToken ct = default) =>
        _db.ExecuteAsync(conn => _bom.LoadChildrenAsync(parentObjectId: parentObjectId, conn), ct);

    /// <summary>
    /// Весь подграф состава сборки — для расчётов массы и сводной.
    /// </summary>
    /// <param name="rootObjectId">Идентификатор корневого объекта</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>Список строк состава</returns>
    public Task<IReadOnlyList<BomRow>> GetTreeAsync(long rootObjectId, CancellationToken ct = default) =>
        _db.ExecuteAsync(conn => _bom.LoadTreeAsync(rootObjectId, conn), ct);

    /// <summary>
    /// Карточка объекта для правой панели.
    /// </summary>
    /// <param name="objectId">Идентификатор объекта</param>
    /// <param name="ct">Токен отмены</param>
    /// <returns>Карточка объекта</returns>
    public Task<ObjectCard> GetCardAsync(long objectId, CancellationToken ct = default) =>
        _db.ExecuteAsync(conn => _objects.GetCardAsync(objectId, conn), ct);
    
    
    /// <summary>
    /// Возвращает статус приложения: количество объектов и дату последнего импорта.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public Task<AppStatus> GetStatusAsync(CancellationToken ct = default) =>
        _db.ExecuteAsync(async conn =>
        {
            const string sql = """
                               SELECT (SELECT COUNT(*)::int FROM pdm_object)   AS ObjectsCount,
                                      (SELECT MAX(started_at) FROM import_log) AS LastImportAt;
                               """;
            var row = await conn.QuerySingleAsync<StatusRaw>(sql);
            return new AppStatus("PostgreSQL", _db.ConnectionSummary, row.ObjectsCount, row.LastImportAt);
        }, ct);
}