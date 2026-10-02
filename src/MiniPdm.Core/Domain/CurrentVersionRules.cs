namespace MiniPdm.Core.Domain;

/// <summary>
/// Инвариант указателя текущей версии (ADR 0004) версия с максимальным номером
/// среди неаннулированных, если таких нет то null.
/// </summary>
public static class CurrentVersionRules
{
    public static int? ResolveCurrentVersionNo(IReadOnlyList<(int VersionNo, ObjectState State)> versions)
    {
        int? best = null;
        foreach (var (versionNo, state) in versions)
        {
            if (state == ObjectState.Annulled) continue;
            if (best is null || versionNo > best) best = versionNo;
        }
        return best;
    }
}