using MiniPdm.Core.Domain;

namespace MiniPdm.Core.Cad;

/// <summary>
/// Документ CAD-системы в доменном представлении.
/// Идентификатор документа — <see cref="FileName"/> (для файлового источника —
/// имя файла, нормализованное адаптером к NFC, ADR 0005).
/// </summary>
public sealed record CadDocument
{
    /// <summary>Идентификатор документа.</summary>
    public required string FileName { get; init; }

    public required ObjectType Type { get; init; }

    /// <summary>Обозначение; null у StandardPart.</summary>
    public string? Designation { get; init; }

    public required string Name { get; init; }

    /// <summary>Материал; null у Assembly и StandardPart.</summary>
    public string? Material { get; init; }

    /// <summary>
    /// Масса, кг за 1 шт. null = «не задана», ноль невозможен (ADR 0003).
    /// У Assembly всегда null: масса вычисляется по составу.
    /// </summary>
    public decimal? MassKg { get; init; }

    /// <summary>Состав; непустой только у Assembly. Порядок элементов семантики не несёт.</summary>
    public IReadOnlyList<CadComponent> Components { get; init; } = [];
}