namespace MiniPdm.App.Models;

/// <summary>Строка таблицы сводной: обозначение, наименование, кол-во, массы.</summary>
public sealed record SummaryRow(
    string Designation, string Name, int TotalQuantity, string UnitMass, string TotalMass);