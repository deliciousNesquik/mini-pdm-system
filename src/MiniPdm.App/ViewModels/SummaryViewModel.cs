using System.Collections.Generic;
using System.Linq;
using MiniPdm.App.Models;
using MiniPdm.Core.Bom;

namespace MiniPdm.App.ViewModels;

/// <summary>
/// ViewModel для отображения сводной информации по спецификации.
/// </summary>
public sealed class SummaryViewModel
{
    public SummaryViewModel(IReadOnlyList<SummaryLine> lines)
    {
        Lines = lines
            .Select(l => new SummaryRow(
                l.Designation ?? "—",
                l.Name,
                l.TotalQuantity,
                l.UnitMassKg?.ToString("0.####") ?? "",
                l.TotalMassKg?.ToString("0.####") ?? ""))
            .ToList();
    }

    /// <summary>
    /// Строки таблицы сводной информации по спецификации.
    /// </summary>
    public IReadOnlyList<SummaryRow> Lines { get; }
}