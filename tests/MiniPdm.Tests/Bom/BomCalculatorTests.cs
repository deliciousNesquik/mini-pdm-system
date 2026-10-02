using MiniPdm.Core.Bom;
using MiniPdm.Core.Domain;

namespace MiniPdm.Tests.Bom;

public sealed class BomCalculatorTests
{
    private static BomRow Row(
        ObjectType type, string? designation, string name,
        int qty, decimal? unitMass = null,
        bool hasActiveVersion = true, params long[] path) =>
        new(path.Length == 0 ? [1] : path, type, designation, name,
            ObjectState.InWork, qty, hasActiveVersion, unitMass);

    [Fact]
    public void Mass_multiplies_quantities_along_path()
    {
        // Корень → подузел (×2) → деталь (×3) ⇒ деталь в дереве с количеством 6.
        var rows = new[]
        {
            Row(ObjectType.Assembly, "РДЦЛ.304112.100", "Привод", 1, path: [1]),
            Row(ObjectType.Assembly, "РДЦЛ.304112.200", "Узел", 2, path: [1, 2]),
            Row(ObjectType.Part, "РДЦЛ.304112.302", "Колесо", 6, 5.86m, path: [1, 2, 3])
        };

        var calc = BomCalculator.TotalMass(rows);

        Assert.Empty(calc.Problems);
        Assert.Equal(35.16m, calc.TotalMassKg);          // 5.86 × 6
    }

    [Fact]
    public void Missing_mass_names_culprit_instead_of_partial_sum()   // ТЗ + ADR 0003
    {
        var rows = new[]
        {
            Row(ObjectType.Part, "РДЦЛ.304112.301", "Вал", 1, 3.42m),
            Row(ObjectType.Part, "РДЦЛ.304112.604", "Прокладка", 1, null)
        };

        var calc = BomCalculator.TotalMass(rows);

        Assert.Null(calc.TotalMassKg);
        var problem = Assert.Single(calc.Problems);
        Assert.Equal(MassProblemKind.MissingMass, problem.Kind);
        Assert.Contains("Прокладка", problem.Display);
    }

    [Fact]
    public void No_active_version_reports_problem()   // ADR 0009 / п.10 само-ревью
    {
        var rows = new[]
        {
            Row(ObjectType.Part, "РДЦЛ.304112.301", "Вал", 1, 3.42m),
            Row(ObjectType.Assembly, "РДЦЛ.304112.200", "Узел", 1, hasActiveVersion: false)
        };

        var calc = BomCalculator.TotalMass(rows);

        Assert.Null(calc.TotalMassKg);
        Assert.Contains(calc.Problems, p => p.Kind == MassProblemKind.NoActiveVersion);
    }

    [Fact]
    public void Assembly_own_mass_is_not_counted()
    {
        var rows = new[] { Row(ObjectType.Assembly, "РДЦЛ.304112.100", "Привод", 1) };

        var calc = BomCalculator.TotalMass(rows);

        Assert.Empty(calc.Problems);
        Assert.Equal(0m, calc.TotalMassKg);
    }

    [Fact]
    public void Summary_groups_by_identity_and_sums_quantities()
    {
        // Деталь на двух уровнях: 1 + 2 = 3 шт., масса 5.86 × 3.
        var rows = new[]
        {
            Row(ObjectType.Assembly, "РДЦЛ.304112.100", "Привод", 1),
            Row(ObjectType.Part, "РДЦЛ.304112.302", "Колесо", 1, 5.86m),
            Row(ObjectType.Part, "РДЦЛ.304112.302", "Колесо", 2, 5.86m),
            Row(ObjectType.StandardPart, null, "Гайка М12", 1, 0.03m)
        };

        var summary = BomCalculator.Summary(rows);

        Assert.Equal(2, summary.Count);                  // сборки исключены
        var wheel = summary.Single(l => l.Designation == "РДЦЛ.304112.302");
        Assert.Equal(3, wheel.TotalQuantity);
        Assert.Equal(17.58m, wheel.TotalMassKg);
    }

    [Fact]
    public void Summary_line_without_mass_shows_empty_mass()   // ТЗ: строка с пустой массой
    {
        var summary = BomCalculator.Summary(
            [Row(ObjectType.Part, "РДЦЛ.304112.604", "Прокладка", 2, null)]);

        var line = Assert.Single(summary);
        Assert.Null(line.UnitMassKg);
        Assert.Null(line.TotalMassKg);
        Assert.Equal(2, line.TotalQuantity);
    }
}