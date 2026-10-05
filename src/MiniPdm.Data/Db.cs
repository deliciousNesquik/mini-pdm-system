using Npgsql;

namespace MiniPdm.Data;

/// <summary>
/// Обертка над подключением к PostgreSQL с регистрацией TypeHandler Dapper для enum.
/// </summary>
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