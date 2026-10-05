using Dapper;
using MiniPdm.Core.Domain;
using Npgsql;

namespace MiniPdm.Data;

/// <summary>
/// Сервис управления версиями объектов. Создаёт, обновляет, аннулирует версии и управляет указателем текущей версии.
/// </summary>
public sealed class VersionService
{
    /// <summary>
    /// Создаёт начальную версию объекта (version_no = 1, state = InWork) и устанавливает указатель текущей версии.
    /// </summary>
    /// <param name="objectId">Идентификатор объекта</param>
    /// <param name="material">Материал</param>
    /// <param name="massKg">Масса, кг</param>
    /// <param name="tx">Транзакция</param>
    /// <returns>Версию</returns>>
    /// <exception cref="InvalidOperationException">Если у объекта уже есть версия</exception>
    public async Task<long> CreateInitialVersionAsync(
        long objectId, string? material, decimal? massKg, NpgsqlTransaction tx)
    {
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

    /// <summary>
    /// Создаёт новую версию объекта (version_no = max + 1, state = InWork) и устанавливает указатель текущей версии.
    /// </summary>
    /// <param name="objectId">Идентификатор объекта</param>
    /// <param name="material">Материал</param>
    /// <param name="massKg">Масса, кг</param>
    /// <param name="tx">Транзакция</param>
    /// <returns>Версию</returns>
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

    /// <summary>
    /// Обновляет материал и массу версии в состоянии InWork. Требует активной транзакции - атомарность гарантирует вызывающий.
    /// </summary>
    /// <param name="versionId">Идентификатор версии</param>
    /// <param name="material">Материал</param>
    /// <param name="massKg">Масса, кг</param>
    /// <param name="tx">Транзакция</param>
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

    /// <summary>
    /// Меняет состояние версии. Если новая версия аннулируется, указатель текущей версии объекта будет перенаправлен на другую версию или обнулён, если других версий нет
    /// </summary>
    /// <param name="versionId">Идентификатор версии</param>
    /// <param name="to">Новое состояние</param>
    /// <param name="tx">Транзакция</param>
    /// <exception cref="InvalidOperationException">Если переход недопустим</exception>
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
        
        // Указатель меняется только при аннулировании ТЕКУЩЕЙ версии, остальные
        // случаи (утверждение, аннулирование не текущей) его не трогают.
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

    /// <summary>
    /// Устанавливает указатель текущей версии объекта на указанную версию
    /// </summary>
    /// <param name="tx">Транзакция</param>
    /// <param name="objectId">Идентификатор объекта</param>
    /// <param name="versionId">Идентификатор версии</param>
    private static async Task SetPointerAsync(NpgsqlTransaction tx, long objectId, long versionId)
    {
        const string sql = "UPDATE pdm_object SET current_version_id = @VersionId WHERE id = @ObjectId;";
        await tx.Connection!.ExecuteAsync(sql, new { VersionId = versionId, ObjectId = objectId }, tx);
    }
}