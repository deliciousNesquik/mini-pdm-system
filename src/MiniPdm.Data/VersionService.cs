using Dapper;
using MiniPdm.Core.Domain;
using Npgsql;

namespace MiniPdm.Data;

/// <summary>
/// Единственная точка записи версий и состояний (ADR 0004): любой путь,
/// создающий версию или меняющий состояние, проходит здесь — и только здесь
/// поддерживается указатель current_version_id.
/// </summary>
public sealed class VersionService
{
    /// <summary>Версия 1 «В работе» для нового объекта; указатель → v1.
    /// Нарушение (версии уже есть) — программная ошибка: InvalidOperationException.</summary>
    public async Task<long> CreateInitialVersionAsync(
        long objectId, string? material, decimal? massKg, NpgsqlTransaction tx)
    {
        // COUNT(*) в PostgreSQL возвращает bigint — ::int нужен для QuerySingleAsync<int>.
        const string countSql = "SELECT COUNT(*)::int FROM object_version WHERE object_id = @ObjectId;";
        var existing = await tx.Connection!.QuerySingleAsync<int>(countSql, new { ObjectId = objectId }, tx);
        if (existing > 0)
            throw new InvalidOperationException(
                $"У объекта {objectId} уже есть версии — начальная версия не создаётся.");

        const string insertSql = """
            INSERT INTO object_version (object_id, version_no, state, material, mass_kg)
            VALUES (@ObjectId, 1, @State, @Material, @MassKg)
            RETURNING id;
            """;
        var versionId = await tx.Connection!.QuerySingleAsync<long>(insertSql, new
        {
            ObjectId = objectId,
            State = "InWork",
            Material = material,
            MassKg = massKg
        }, tx);

        await SetPointerAsync(tx, objectId, versionId);
        return versionId;
    }

    /// <summary>Новая версия «В работе»: номер = max(все)+1, включая аннулированные;
    /// указатель → новая версия (таблица поведения ADR 0004).</summary>
    public async Task<long> CreateNextVersionAsync(
        long objectId, string? material, decimal? massKg, NpgsqlTransaction tx)
    {
        const string nextSql = """
            SELECT COALESCE(MAX(version_no), 0) + 1 FROM object_version WHERE object_id = @ObjectId;
            """;
        var nextNo = await tx.Connection!.QuerySingleAsync<int>(nextSql, new { ObjectId = objectId }, tx);

        const string insertSql = """
            INSERT INTO object_version (object_id, version_no, state, material, mass_kg)
            VALUES (@ObjectId, @VersionNo, @State, @Material, @MassKg)
            RETURNING id;
            """;
        var versionId = await tx.Connection!.QuerySingleAsync<long>(insertSql, new
        {
            ObjectId = objectId,
            VersionNo = nextNo,
            State = "InWork",
            Material = material,
            MassKg = massKg
        }, tx);

        await SetPointerAsync(tx, objectId, versionId);
        return versionId;
    }

    /// <summary>Обновляет атрибуты версии «В работе» на месте: id и номер сохраняются,
    /// состав перезаписывает BomLinkRepository.ReplaceCompositionAsync.
    /// Требует активной транзакции — атомарность гарантирует вызывающий (ImportService).</summary>
    public async Task UpdateInWorkVersionAsync(
        long versionId, string? material, decimal? massKg, NpgsqlTransaction tx)
    {
        const string sql = """
            UPDATE object_version SET material = @Material, mass_kg = @MassKg
             WHERE id = @VersionId;
            """;
        await tx.Connection!.ExecuteAsync(sql,
            new { VersionId = versionId, Material = material, MassKg = massKg }, tx);
    }

    /// <summary>Переход состояния с guard'ом StateRules. При аннулировании ТЕКУЩЕЙ
    /// версии указатель откатывается на последнюю неаннулированную или NULL (ADR 0004).
    /// Аннулирование не-текущей версии указатель не трогает.
    /// Требует активной транзакции — атомарность гарантирует вызывающий.</summary>
    public async Task SetStateAsync(long versionId, ObjectState to, NpgsqlTransaction tx)
    {
        const string loadSql = """
            SELECT ov.object_id          AS ObjectId,
                   ov.version_no         AS VersionNo,
                   ov.state              AS State,
                   po.current_version_id AS CurrentVersionId
              FROM object_version ov
              JOIN pdm_object po ON po.id = ov.object_id
             WHERE ov.id = @VersionId;
            """;
        var row = await tx.Connection!.QuerySingleAsync<VersionRow>(loadSql, new { VersionId = versionId }, tx);

        if (!StateRules.CanTransition(row.State, to))
            throw new InvalidOperationException(
                $"Недопустимый переход {row.State} → {to} (версия {row.VersionNo} объекта {row.ObjectId}).");

        const string updateSql = "UPDATE object_version SET state = @To WHERE id = @VersionId;";
        await tx.Connection!.ExecuteAsync(updateSql,
            new { To = to.ToString(), VersionId = versionId }, tx);   // ← было: To = to
        
        // Указатель меняется только при аннулировании ТЕКУЩЕЙ версии; остальные
        // случаи (утверждение, аннулирование не-текущей) его не трогают.
        if (to != ObjectState.Annulled || row.CurrentVersionId != versionId)
            return;

        const string versionsSql = """
            SELECT version_no AS VersionNo, state AS State
              FROM object_version
             WHERE object_id = @ObjectId;
            """;
        var versions = (await tx.Connection!.QueryAsync<VersionNoRow>(
                versionsSql, new { ObjectId = row.ObjectId }, tx))
            .Select(v => (v.VersionNo, v.State))
            .ToList();

        // Решение принимает чистая функция ядра, SQL — только исполнение (ADR 0004).
        var newCurrentNo = CurrentVersionRules.ResolveCurrentVersionNo(versions);

        if (newCurrentNo is null)
        {
            const string nullSql =
                "UPDATE pdm_object SET current_version_id = NULL WHERE id = @ObjectId;";
            await tx.Connection!.ExecuteAsync(nullSql, new { ObjectId = row.ObjectId }, tx);
        }
        else
        {
            const string repointSql = """
                UPDATE pdm_object
                   SET current_version_id = (SELECT id FROM object_version
                                              WHERE object_id = @ObjectId AND version_no = @VersionNo)
                 WHERE id = @ObjectId;
                """;
            await tx.Connection!.ExecuteAsync(repointSql, new
            {
                ObjectId = row.ObjectId,
                VersionNo = newCurrentNo.Value
            }, tx);
        }
    }

    private static async Task SetPointerAsync(NpgsqlTransaction tx, long objectId, long versionId)
    {
        const string sql = "UPDATE pdm_object SET current_version_id = @VersionId WHERE id = @ObjectId;";
        await tx.Connection!.ExecuteAsync(sql, new { VersionId = versionId, ObjectId = objectId }, tx);
    }

    private sealed record VersionRow(long ObjectId, int VersionNo, ObjectState State, long? CurrentVersionId);

    private sealed record VersionNoRow(int VersionNo, ObjectState State);
}