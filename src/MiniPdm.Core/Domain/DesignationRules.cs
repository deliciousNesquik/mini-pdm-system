using System.Text.RegularExpressions;

namespace MiniPdm.Core.Domain;

/// <summary>
/// Формат обозначения, упрощённый ЕСКД: четыре заглавные кириллические буквы,
/// точка, шесть цифр, точка, три цифры. Пример: АБВГ.301245.001.
/// </summary>
public static partial class DesignationRules
{
    [GeneratedRegex(@"^[А-ЯЁ]{4}\.[0-9]{6}\.[0-9]{3}$")]
    private static partial Regex Pattern();

    /// <summary>Обозначение соответствует маске.</summary>
    public static bool IsValid(string? designation) =>
        designation is not null && Pattern().IsMatch(designation);
}