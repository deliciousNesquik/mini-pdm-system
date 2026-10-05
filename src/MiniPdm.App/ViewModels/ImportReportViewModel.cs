using System.Collections.Generic;
using System.Linq;
using MiniPdm.Core.Import;

namespace MiniPdm.App.ViewModels;

/// <summary>Модель окна отчёта об импорте (рисунок 2 ТЗ). Строки таблицы —
/// проекция ImportIssue в отображаемые данные: тексты результата («Ошибка» /
/// «Предупреждение») готовятся здесь, ядро не знает про UI-формулировки.</summary>
public sealed class ImportReportViewModel
{
    public ImportReportViewModel(ImportAnalysis analysis)
    {
        Issues = analysis.Issues
            .Select(i => new IssueRow(i.FileName, SeverityText(i.Severity), i.Reason))
            .ToList();
        Accepted = analysis.AcceptedCount;
        Rejected = analysis.RejectedCount;
        Warnings = analysis.WarningCount;
    }

    public IReadOnlyList<IssueRow> Issues { get; }
    public int Accepted { get; }
    public int Rejected { get; }
    public int Warnings { get; }

    public static string SeverityText(ImportSeverity severity) => severity switch
    {
        ImportSeverity.Error => Localization.Strings.Report_Error,
        _ => Localization.Strings.Report_Warning
    };

    /// <summary>Строка таблицы отчёта: файл, результат, причина.</summary>
    public sealed record IssueRow(string FileName, string Result, string Reason);
}