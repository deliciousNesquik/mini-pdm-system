using MiniPdm.Core.Domain;
using MiniPdm.Data.Infrastructure;

namespace MiniPdm.Data.Services;

/// <summary>
/// Сервис для атомарного изменения состояния версии и отката указателя current_version_id при аннулировании текущей версии (ADR 0004).
/// </summary>
public sealed class PdmStateService
{
    private readonly Db _db;
    private readonly VersionService _versions;

    public PdmStateService(Db db, VersionService versions)
    {
        _db = db;
        _versions = versions;
    }

    /// <summary>
    /// Атомарно изменяет состояние версии. Если версия является текущей, то при аннулировании указатель current_version_id будет откатан на предыдущую версию.
    /// </summary>
    /// <param name="versionId">Идентификатор версии</param>
    /// <param name="to">Новое состояние</param>
    /// <param name="ct">Токен отмены</param>
    public async Task ChangeStateAsync(long versionId, ObjectState to, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            await _versions.SetStateAsync(versionId, to, tx);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}