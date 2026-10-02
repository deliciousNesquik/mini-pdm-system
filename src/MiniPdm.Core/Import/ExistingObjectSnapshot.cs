using MiniPdm.Core.Domain;

namespace MiniPdm.Core.Import;

/// <summary>Снимок состояния объекта в БД для решения о повторном импорте.</summary>
public sealed record ExistingObjectSnapshot(
    ObjectType Type,
    string? Designation,
    string Name,

    /// <summary>Состояние текущей версии; null — действующей версии нет (все аннулированы, ADR 0004).</summary>
    ObjectState? CurrentState,
    string? Material,
    decimal? MassKg,
    IReadOnlyList<ExistingChild> Composition);