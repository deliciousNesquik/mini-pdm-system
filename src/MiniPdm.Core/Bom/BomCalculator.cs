using MiniPdm.Core.Domain;

namespace MiniPdm.Core.Bom;

public static class BomCalculator
{
    /// <summary>Масса сборки с учётом вложенности и количеств.</summary>
    public static MassCalculation TotalMass(IReadOnlyList<BomRow> rows)
    {
        var problems = new List<MassProblem>();
        decimal total = 0m;

        foreach (var row in rows)
        {
            if (!row.HasActiveVersion)
            {
                problems.Add(new MassProblem(row.IdentityKey, row.Display, MassProblemKind.NoActiveVersion));
                continue;
            }

            // Собственная масса у сборки не задаётся — считаются только детали и стандартные изделия.
            if (row.Type == ObjectType.Assembly) continue;

            if (row.UnitMassKg is null)
            {
                problems.Add(new MassProblem(row.IdentityKey, row.Display, MassProblemKind.MissingMass));
                continue;
            }

            total += row.UnitMassKg.Value * row.QuantityOnPath;
        }

        return problems.Count == 0
            ? new MassCalculation(total, [])
            : new MassCalculation(null, problems);
    }

    /// <summary>Сводная спецификация: плоский список деталей и стандартных изделий,
    /// количества перемножены по путям и сложены, группировка по идентичности (ТЗ).
    /// Узлы без действующей версии в сводную не попадают — их называет TotalMass.</summary>
    public static IReadOnlyList<SummaryLine> Summary(IReadOnlyList<BomRow> rows) =>
        rows
            .Where(r => r.HasActiveVersion && r.Type != ObjectType.Assembly)
            .GroupBy(r => r.IdentityKey, StringComparer.Ordinal)
            .Select(g =>
            {
                var first = g.First();
                var quantity = g.Sum(r => r.QuantityOnPath);
                return new SummaryLine(
                    first.Type, first.Designation, first.Name,
                    quantity,
                    first.UnitMassKg,
                    first.UnitMassKg * quantity);
            })
            .OrderBy(l => l.Designation is null)          // детали по обозначению, стандартные — в конец
            .ThenBy(l => l.Designation, StringComparer.Ordinal)
            .ThenBy(l => l.Name, StringComparer.Ordinal)
            .ToList();
}