using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace MiniPdm.App;

/// <summary>
/// Класс настроек приложения
/// </summary>
public sealed class AppSettings
{
    [JsonPropertyName("ConnectionStrings")]
    public ConnectionStrings ConnectionStrings { get; init; } = new();
}

/// <summary>
/// Класс подключения к базе данных
/// </summary>
public sealed class ConnectionStrings
{
    [JsonPropertyName("Default")]
    public string Default { get; init; } =
        "Host=localhost;Port=5432;Database=minipdm;Username=minipdm;Password=minipdm";
}

/// <summary>
/// Класс для загрузки настроек приложения из файла appsettings.json
/// </summary>
public static class AppSettingsLoader
{
    public static AppSettings Load()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return new AppSettings();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (JsonException e)
        {
            Log.Warning(e, "appsettings.json повреждён - использую значения по умолчанию");
            return new AppSettings();
        }
    }
}