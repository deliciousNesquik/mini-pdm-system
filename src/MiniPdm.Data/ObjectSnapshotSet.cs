using MiniPdm.Core.Import;

namespace MiniPdm.Data;

/// <summary>
/// Результат загрузки снимков для Core - снимки без id
/// </summary>
/// <param name="ByIdentity">Снимки объектов, сгруппированные по идентификатору</param>
/// <param name="IdByIdentity">Идентификаторы объектов по их идентификатору</param>
/// <param name="CurrentVersionIdByIdentity">Идентификаторы текущих версий объектов по их идентификатору</param>
public sealed record ObjectSnapshotSet(
    IReadOnlyDictionary<string, ExistingObjectSnapshot> ByIdentity,
    IReadOnlyDictionary<string, long> IdByIdentity,
    IReadOnlyDictionary<string, long?> CurrentVersionIdByIdentity);