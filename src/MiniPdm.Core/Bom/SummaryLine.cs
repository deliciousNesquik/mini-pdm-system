using MiniPdm.Core.Domain;

namespace MiniPdm.Core.Bom;

/// <summary>Строка сводной спецификации: суммарное количество по всему дереву и масса.</summary>
public sealed record SummaryLine(
    ObjectType Type,
    string? Designation,
    string Name,
    int TotalQuantity,
    decimal? UnitMassKg,
    decimal? TotalMassKg)
{
    public string Display => Designation is null ? $"«{Name}»" : $"{Designation} «{Name}»";
}