using System.Globalization;
using System.Resources;

namespace MiniPdm.App.Localization;

/// <summary>
/// Класс для доступа к локализованным строкам приложения.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("MiniPdm.App.Localization.Strings", typeof(Strings).Assembly);

    /// <summary>
    /// Получает или задает текущую культуру для локализации строк
    /// </summary>
    public static CultureInfo Culture { get; set; } = CultureInfo.CurrentUICulture;

    /// <summary>
    /// Возвращает локализованную строку по ключу. Если строка не найдена, возвращает ключ в квадратных скобках.
    /// </summary>
    /// <param name="key">Ключ строки</param>
    /// <returns>Локализованная строка или ключ в квадратных скобках</returns>
    public static string Get(string key) => Manager.GetString(key, Culture) ?? $"[{key}]";

    public static string App_Title => Get("App_Title");
    public static string Import_Folder => Get("Import_Folder");
    public static string Import_Cancel => Get("Import_Cancel");
    public static string Import_Report_Title => Get("Import_Report_Title");
    public static string Import_Accepted => Get("Import_Accepted");
    public static string Import_Rejected => Get("Import_Rejected");
    public static string Import_Warnings => Get("Import_Warnings");
    public static string Calc_Mass => Get("Calc_Mass");
    public static string Calc_Summary => Get("Calc_Summary");
    public static string Search_Watermark => Get("Search_Watermark");
    public static string State_Approve => Get("State_Approve");
    public static string State_Annull => Get("State_Annull");
    public static string Tree_Empty => Get("Tree_Empty");
    public static string Card_NoActiveVersion => Get("Card_NoActiveVersion");
    public static string Error_Title => Get("Error_Title");
}