using System.Collections.Generic;
using System.Linq;
using MiniPdm.Core.Bom;

namespace MiniPdm.App.ViewModels;

/// <summary>Модель окна сводной спецификации. Строки — проекция SummaryLine:
/// масса форматируется здесь; null = пустая строка (ТЗ: строка показывается
/// с пустой массой), ядро остаётся доменным.</summary>
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

    public IReadOnlyList<SummaryRow> Lines { get; }

    /// <summary>Строка таблицы сводной: обозначение, наименование, кол-во, массы.</summary>
    public sealed record SummaryRow(
        string Designation, string Name, int TotalQuantity, string UnitMass, string TotalMass);
}