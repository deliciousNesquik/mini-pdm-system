using Dapper;
using Npgsql;

namespace MiniPdm.Tests;

/// <summary>
/// Единственный тест, требующий БД. Проверяет соответствие
/// живой схемы схеме из ТЗ и работоспособность строки подключения.
/// </summary>
[Trait("Category", "RequiresDatabase")]
public sealed class DatabaseSmokeTests
{
    [Fact]
    public async Task Schema_tables_exist()
    {
        var cs = Environment.GetEnvironmentVariable("MINIPDM_DB")
                 ?? "Host=localhost;Port=5432;Database=minipdm;Username=minipdm;Password=minipdm";

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        var tables = await conn.QueryAsync<string>(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'");

        Assert.Contains("pdm_object", tables);
        Assert.Contains("object_version", tables);
        Assert.Contains("bom_link", tables);
        Assert.Contains("import_log", tables);
    }
}