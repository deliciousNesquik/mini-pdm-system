using Dapper;
using Npgsql;

namespace MiniPdm.Data;

public sealed record BomLinkRow(long ChildObjectId, int Quantity);

/// <summary>
/// Репозиторий для работы с таблицей bom_link
/// </summary>
public sealed class BomLinkRepository
{
    /// <summary>
    /// Удаляет все строки состава версии и вставляет новые.
    /// </summary>
    /// <param name="versionId">Идентификатор версии</param>
    /// <param name="links">Новые строки состава</param>
    /// <param name="tx">Транзакция</param>
    public async Task ReplaceCompositionAsync(
        long versionId, IReadOnlyCollection<BomLinkRow> links, NpgsqlTransaction tx)
    {
        await tx.Connection!.ExecuteAsync(
            "DELETE FROM bom_link WHERE parent_version_id = @VersionId;",
            new { VersionId = versionId }, tx);          // ← явный алиас

        if (links.Count == 0) return;

        await tx.Connection!.ExecuteAsync("""
                                         INSERT INTO bom_link (parent_version_id, child_object_id, quantity)
                                         SELECT @VersionId, c.child_id, c.quantity
                                           FROM unnest(@ChildIds::bigint[], @Quantities::int[]) AS c(child_id, quantity);
                                         """, new
        {
            VersionId = versionId,                       // ← здесь была та же ошибка
            ChildIds = links.Select(l => l.ChildObjectId).ToArray(),
            Quantities = links.Select(l => l.Quantity).ToArray()
        }, tx);
    }
}