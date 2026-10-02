using MiniPdm.Core.Domain;

namespace MiniPdm.Core.Bom;

/// <summary>
/// Плоская строка дерева состава — прямая проекция результата рекурсивного CTE.
/// Из <see cref="Path"/> UI собирает дерево; расчётам достаточно QuantityOnPath.
/// </summary>
public sealed record BomRow(
    IReadOnlyList<long> Path,
    ObjectType Type,
    string? Designation,
    string Name,
    ObjectState? State,
    int QuantityOnPath,

    /// <summary>false — у объекта нет действующей версии (все аннулированы):
    /// строка сохранена в дереве, состав не раскрыт (ADR 0009).</summary>
    bool HasActiveVersion,
    decimal? UnitMassKg)
{
    public string IdentityKey => ObjectIdentity.Key(Type, Designation, Name);

    /// <summary>Человекочитаемое представление: "РДЦЛ.x.y "Имя"" или "Имя" для стандартных.</summary>
    public string Display => Designation is null ? $"\"{Name}\"" : $"{Designation} \"{Name}\"";
}