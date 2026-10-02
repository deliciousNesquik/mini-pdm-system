using MiniPdm.Core.Cad;

namespace MiniPdm.Core.Import;

/// <summary>
/// Результат анализа набора документов. Импортируемые - без ошибок
/// могут нести предупреждения; Issues - только Error или Warning с причинами.
/// Счётчики - по формату отчёта из ТЗ: принятые / отклонённые / предупреждённые
/// в сумме дают размер набора.
/// </summary>
public sealed record ImportAnalysis(
    IReadOnlyList<CadDocument> Importable,
    IReadOnlyList<ImportIssue> Issues,
    IReadOnlyList<IReadOnlyList<string>> Cycles)
{
    /// <summary>Уникальные файлы с ошибкой.</summary>
    public int RejectedCount => Issues
        .Where(i => i.Severity == ImportSeverity.Error)
        .Select(i => i.FileName)
        .Distinct(StringComparer.Ordinal)
        .Count();

    /// <summary>Уникальные файлы, принятые с предупреждением.</summary>
    public int WarningCount => Issues
        .Where(i => i.Severity == ImportSeverity.Warning)
        .Select(i => i.FileName)
        .Distinct(StringComparer.Ordinal)
        .Count();

    /// <summary>Принято: все файлы без ошибок, включая принятые с предупреждением
    /// (деталь без массы импортируется — пример «Крышка смотровая» в ТЗ).
    /// Инвариант: AcceptedCount + RejectedCount = число разобранных документов;
    /// WarningCount — подмножество принятых.</summary>
    public int AcceptedCount => Importable.Count;
}