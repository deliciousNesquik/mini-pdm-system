namespace MiniPdm.Tests.Data;

internal static class TestData
{
    // строка подключения
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("MINIPDM_DB")
        ?? "Host=localhost;Port=5432;Database=minipdm;Username=minipdm;Password=minipdm;Include Error Detail=true";

    private static int _counter;

    // генератор уникальных валидных обозначений
    public static string NextDesignation() =>
        $"ТЕСТ.{Random.Shared.Next(100000, 999999):D6}.{Interlocked.Increment(ref _counter):D3}";
}