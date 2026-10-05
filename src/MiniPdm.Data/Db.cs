using Npgsql;

namespace MiniPdm.Data;

/// <summary>
/// Обертка над подключением к PostgreSQL с регистрацией TypeHandler Dapper для enum.
/// </summary>
public sealed class Db
{
    private readonly string _connectionString;

    static Db() => DataConfig.Register();

    public Db(string connectionString)
    {
        _connectionString = connectionString;

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        ConnectionSummary = $"{builder.Username ?? "postgres"}@{builder.Host ?? "localhost"}";
    }

    /// <summary>Краткое представление подключения «user@host» — для статус-строки UI.</summary>
    public string ConnectionSummary { get; }

    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    /// <summary>
    /// Открывает подключение к базе данных и выполняет переданную функцию work, передавая ей открытое подключение. После выполнения функции подключение закрывается.
    /// </summary>
    /// <param name="work">Функция, которая будет выполнена с открытым подключением</param>
    /// <param name="ct">Токен отмены</param>
    /// <typeparam name="T">Тип возвращаемого значения</typeparam>
    /// <returns></returns>
    public async Task<T> ExecuteAsync<T>(Func<NpgsqlConnection, Task<T>> work, CancellationToken ct = default)
    {
        await using var conn = await OpenConnectionAsync(ct);
        return await work(conn);
    }
}