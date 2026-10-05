using System.Globalization;
using System.Resources;

namespace MiniPdm.App.Localization;

/// <summary>
/// Класс для получения локализованных строк из ресурсов. Использует ResourceManager для доступа к ресурсам.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("MiniPdm.App.Localization.Strings", typeof(Strings).Assembly);

    /// <summary>
    /// Текущая культура, используемая для локализации
    /// </summary>
    public static CultureInfo Culture { get; set; } = CultureInfo.CurrentUICulture;

    /// <summary>
    /// Возвращает локализованную строку по ключу. Если ключ не найден — возвращает "[ключ]".
    /// </summary>
    /// <param name="key">Ключ строки</param>
    /// <returns>Локализованная строка</returns>
    public static string Get(string key) => Manager.GetString(key, Culture) ?? $"[{key}]";

    public static string App_Title => Get("App_Title");
    public static string Common_Close => Get("Common_Close");
    public static string Error_Title => Get("Error_Title");
    public static string Import_Folder => Get("Import_Folder");
    public static string Import_Folder_Title => Get("Import_Folder_Title");
    public static string Import_Cancel => Get("Import_Cancel");
    public static string Import_Cancelled => Get("Import_Cancelled");
    public static string Import_Report_Title => Get("Import_Report_Title");
    public static string Import_Accepted => Get("Import_Accepted");
    public static string Import_Rejected => Get("Import_Rejected");
    public static string Import_Warnings => Get("Import_Warnings");
    public static string Phase_Reading => Get("Phase_Reading");
    public static string Phase_Analyzing => Get("Phase_Analyzing");
    public static string Phase_Writing => Get("Phase_Writing");
    public static string Phase_Committing => Get("Phase_Committing");
    public static string Phase_Done => Get("Phase_Done");
    public static string Calc_Mass => Get("Calc_Mass");
    public static string Calc_Summary => Get("Calc_Summary");
    public static string Mass_Result_Title => Get("Mass_Result_Title");
    public static string Mass_Total => Get("Mass_Total");
    public static string Mass_Incomplete_Title => Get("Mass_Incomplete_Title");
    public static string Mass_NoSelection => Get("Mass_NoSelection");
    public static string Mass_Problem_MissingMass => Get("Mass_Problem_MissingMass");
    public static string Mass_Problem_NoActiveVersion => Get("Mass_Problem_NoActiveVersion");
    public static string Search_Watermark => Get("Search_Watermark");
    public static string State_Approve => Get("State_Approve");
    public static string State_Annull => Get("State_Annull");
    public static string State_InWork => Get("State_InWork");
    public static string State_Approved => Get("State_Approved");
    public static string State_Annulled => Get("State_Annulled");
    public static string Type_Assembly => Get("Type_Assembly");
    public static string Type_Part => Get("Type_Part");
    public static string Type_StandardPart => Get("Type_StandardPart");
    public static string Card_Label_Type => Get("Card_Label_Type");
    public static string Card_Label_Designation => Get("Card_Label_Designation");
    public static string Card_Label_Name => Get("Card_Label_Name");
    public static string Card_Label_Version => Get("Card_Label_Version");
    public static string Card_Label_State => Get("Card_Label_State");
    public static string Card_Label_Mass => Get("Card_Label_Mass");
    public static string Card_Label_Material => Get("Card_Label_Material");
    public static string Card_NoActiveVersion => Get("Card_NoActiveVersion");
    public static string Card_Mass_Hint => Get("Card_Mass_Hint");
    public static string Card_Composition_Title => Get("Card_Composition_Title");
    public static string Left_Panel_Title => Get("Left_Panel_Title");
    public static string Status_Objects => Get("Status_Objects");
    public static string Status_LastImport => Get("Status_LastImport");
    public static string Tree_Empty => Get("Tree_Empty");
    public static string Report_File => Get("Report_File");
    public static string Report_Result => Get("Report_Result");
    public static string Report_Reason => Get("Report_Reason");
    public static string Report_Error => Get("Report_Error");
    public static string Report_Warning => Get("Report_Warning");
    public static string Summary_Column_Designation => Get("Summary_Column_Designation");
    public static string Summary_Column_Name => Get("Summary_Column_Name");
    public static string Summary_Column_Quantity => Get("Summary_Column_Quantity");
    public static string Summary_Column_UnitMass => Get("Summary_Column_UnitMass");
    public static string Summary_Column_TotalMass => Get("Summary_Column_TotalMass");

}