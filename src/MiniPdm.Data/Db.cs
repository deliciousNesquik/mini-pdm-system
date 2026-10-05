using System.Data;
using Dapper;
using MiniPdm.Core.Domain;
using Npgsql;

namespace MiniPdm.Data;

/// <summary>Единая точка создания соединений (ADR 0011, п.8/Р.8).
/// Пулом физических соединений управляет Npgsql; класс владеет только
/// строкой подключения и конфигурацией маппинга.</summary>
public sealed class Db
{
    private readonly string _connectionString;

    static Db() => DataConfig.Register();

    public Db(string connectionString) => _connectionString = connectionString;

    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }
}

internal static class DataConfig
{
    private static readonly Lazy<bool> _registered = new(
        () => { SqlMapper.AddTypeHandler(new ObjectTypeHandler());
                SqlMapper.AddTypeHandler(new ObjectStateHandler());
                return true; },
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>ADR 0001: схема хранит справочники TEXT; Dapper по умолчанию
    /// отправляет enum-параметр как его базовый тип (int) — расхождение с CHECK.
    /// Строковые TypeHandler'ы делают запись и чтение строками в обе стороны.</summary>
    public static void Register() => _ = _registered.Value;
}

internal sealed class ObjectTypeHandler : SqlMapper.TypeHandler<ObjectType>
{
    public override void SetValue(IDbDataParameter parameter, ObjectType value) =>
        parameter.Value = value.ToString();

    public override ObjectType Parse(object value) =>
        Enum.TryParse<ObjectType>((string)value, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new InvalidOperationException($"Неизвестное значение object_type в БД: «{value}»");
}

internal sealed class ObjectStateHandler : SqlMapper.TypeHandler<ObjectState>
{
    public override void SetValue(IDbDataParameter parameter, ObjectState value) =>
        parameter.Value = value.ToString();

    public override ObjectState Parse(object value) =>
        Enum.TryParse<ObjectState>((string)value, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new InvalidOperationException($"Неизвестное значение state в БД: «{value}»");
}