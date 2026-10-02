namespace MiniPdm.Core.Bom;

/// <summary>Итог массы сборки. Инварианты: Problems никогда не null;
/// TotalMassKg == null тогда и только тогда, когда Problems непуст
/// (неполная сумма не выдаётся — ТЗ; ADR 0003).</summary>
public sealed record MassCalculation(decimal? TotalMassKg, IReadOnlyList<MassProblem> Problems);