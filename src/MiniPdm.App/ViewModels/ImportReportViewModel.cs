using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniPdm.App.Localization;
using MiniPdm.App.Models;
using MiniPdm.Core.Import;

namespace MiniPdm.App.ViewModels;

/// <summary>
///     ViewModel для отчёта импорта. Содержит список всех строк отчёта (Issues) и фильтрованный список (FilteredIssues),
/// </summary>
public partial class ImportReportViewModel : ViewModelBase
{
    public ImportReportViewModel(ImportAnalysis analysis, string folder)
    {
        Issues = analysis.Issues
            .Select(i => new IssueRow(i.FileName, SeverityText(i.Severity), i.Reason, i.Severity))
            .ToList();
        Accepted = analysis.AcceptedCount;
        Rejected = analysis.RejectedCount;
        Warnings = analysis.WarningCount;
        Folder = folder;
    }

    [ObservableProperty]
    private string _folder;
    
    /// <summary>
    ///     Индекс фильтра
    /// </summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FilteredIssues))]
    private int _filterIndex;

    /// <summary>
    ///     Список ошибок и предупреждений, полученных при анализе импорта. Не фильтруется!!
    /// </summary>
    public IReadOnlyList<IssueRow> Issues { get; }

    /// <summary>
    ///     Фильтрованный список строк отчёта, пересобирается при смене FilterIndex, используется в UI
    /// </summary>
    public IReadOnlyList<IssueRow> FilteredIssues => FilterIndex switch
    {
        1 => Issues.Where(r => r.Severity == ImportSeverity.Error).ToList(),
        2 => Issues.Where(r => r.Severity != ImportSeverity.Error).ToList(),
        _ => Issues
    };

    public int Accepted { get; }
    public int Rejected { get; }
    public int Warnings { get; }

    private static string SeverityText(ImportSeverity severity)
    {
        return severity switch
        {
            ImportSeverity.Error => Strings.Report_Error,
            _ => Strings.Report_Warning
        };
    }
}