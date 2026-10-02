using MiniPdm.Core.Cad;
using MiniPdm.Core.Domain;
using MiniPdm.Core.Import;

namespace MiniPdm.Tests.Import;

public sealed class ImportAnalyzerTests
{
    private static CadDocument Part(
        string file, string designation,
        decimal? mass = 1.0m, string? material = "Сталь 40Х") => new()
    {
        FileName = file, Type = ObjectType.Part,
        Designation = designation, Name = file.Replace(".m3d", ""),
        Material = material, MassKg = mass
    };

    private static CadDocument StandardPart(
        string file, string? name = null, decimal? mass = 0.1m, string? designation = null) => new()
    {
        FileName = file, Type = ObjectType.StandardPart,
        Designation = designation, Name = name ?? file.Replace(".m3d", ""),
        MassKg = mass
    };

    private static CadDocument Assembly(
        string file, string designation, params CadComponent[] components) => new()
    {
        FileName = file, Type = ObjectType.Assembly,
        Designation = designation, Name = file.Replace(".a3d", ""),
        Components = components
    };

    private static bool Rejected(ImportAnalysis a, string file) =>
        a.Importable.All(d => d.FileName != file) &&
        a.Issues.Any(i => i.FileName == file && i.Severity == ImportSeverity.Error);

    [Theory]
    [InlineData("PДЦЛ.304112.601")]   // латинская P — реальный случай набора
    [InlineData("РДЦЛ.30411.602")]    // пять цифр — реальный случай набора
    public void Invalid_designation_is_rejected(string designation)
    {
        var analysis = new ImportAnalyzer().Analyze([Part("Деталь.m3d", designation)]);

        Assert.True(Rejected(analysis, "Деталь.m3d"));
        Assert.Contains(analysis.Issues, i => i.Reason.Contains("формату"));
    }

    [Fact]
    public void Duplicate_designation_rejects_all_copies()
    {
        var analysis = new ImportAnalyzer().Analyze(
        [
            Part("Шайба стопорная.m3d", "РДЦЛ.304112.710"),
            Part("Шайба упорная.m3d", "РДЦЛ.304112.710")
        ]);

        Assert.Empty(analysis.Importable);
        Assert.True(Rejected(analysis, "Шайба стопорная.m3d"));
        Assert.True(Rejected(analysis, "Шайба упорная.m3d"));
    }

    [Fact]
    public void Duplicate_standard_part_name_rejects_all_copies()
    {
        var analysis = new ImportAnalyzer().Analyze(
        [
            StandardPart("Кольцо А40.m3d"),
            StandardPart("Кольцо А60.m3d", name: "Кольцо А40")
        ]);

        Assert.Empty(analysis.Importable);
    }

    [Fact]
    public void Duplicate_document_identity_rejects_both()
    {
        var analysis = new ImportAnalyzer().Analyze(
        [
            Part("Шайба.m3d", "РДЦЛ.304112.710"),
            Part("Шайба.m3d", "РДЦЛ.304112.710")
        ]);

        Assert.Empty(analysis.Importable);
    }

    [Fact]
    public void Missing_component_reference_rejects_assembly()
    {
        var analysis = new ImportAnalyzer().Analyze(
            [Assembly("Привод.a3d", "РДЦЛ.304112.100", new CadComponent("Насос НШ-10.m3d", 1))]);

        Assert.True(Rejected(analysis, "Привод.a3d"));
        Assert.Contains(analysis.Issues, i => i.Reason.Contains("Насос НШ-10"));
    }

    [Fact]
    public void Non_positive_quantity_rejects_assembly()
    {
        var analysis = new ImportAnalyzer().Analyze(
            [Assembly("Пробка.a3d", "РДЦЛ.304112.400", new CadComponent("Пробка.m3d", 0))]);

        Assert.True(Rejected(analysis, "Пробка.a3d"));
    }

    [Fact]
    public void Part_without_mass_is_accepted_with_warning()
    {
        var analysis = new ImportAnalyzer().Analyze(
            [Part("Прокладка маслоуказателя.m3d", "РДЦЛ.304112.604", mass: null)]);

        Assert.Contains(analysis.Importable, d => d.FileName == "Прокладка маслоуказателя.m3d");
        Assert.Contains(analysis.Issues, i =>
            i.FileName == "Прокладка маслоуказателя.m3d" && i.Severity == ImportSeverity.Warning);
        Assert.Equal(1, analysis.AcceptedCount);
        Assert.Equal(1, analysis.WarningCount);
        Assert.Equal(0, analysis.RejectedCount);
    }

    [Fact]
    public void Part_without_material_is_rejected()
    {
        var analysis = new ImportAnalyzer().Analyze(
            [Part("Деталь.m3d", "РДЦЛ.304112.301", material: null)]);

        Assert.True(Rejected(analysis, "Деталь.m3d"));
    }

    [Fact]
    public void Standard_part_with_designation_is_rejected()
    {
        var analysis = new ImportAnalyzer().Analyze(
            [StandardPart("Кольцо.m3d", designation: "РДЦЛ.304112.700")]);

        Assert.True(Rejected(analysis, "Кольцо.m3d"));
    }

    [Fact]
    public void Rejection_propagates_up_the_tree()
    {
        var analysis = new ImportAnalyzer().Analyze(
        [
            Assembly("Привод.a3d", "РДЦЛ.304112.100", new CadComponent("Узел.a3d", 1)),
            Assembly("Узел.a3d", "РДЦЛ.304112.200", new CadComponent("Муфта МУВП-32.m3d", 1)),
            Part("Болт.m3d", "РДЦЛ.304112.310")
        ]);

        Assert.True(Rejected(analysis, "Узел.a3d"));     // ссылка на отсутствующую Муфту
        Assert.True(Rejected(analysis, "Привод.a3d"));   // каскад
        Assert.Contains(analysis.Importable, d => d.FileName == "Болт.m3d");
    }

    [Fact]
    public void Cycle_participants_are_rejected_and_cascade_upwards()
    {
        var analysis = new ImportAnalyzer().Analyze(
        [
            Assembly("Механизм.a3d", "РДЦЛ.304112.900", new CadComponent("Рычаг.a3d", 1)),
            Assembly("Рычаг.a3d", "РДЦЛ.304112.901", new CadComponent("Тяга.a3d", 1)),
            Assembly("Тяга.a3d", "РДЦЛ.304112.902", new CadComponent("Рычаг.a3d", 1)),
            Assembly("Коробка.a3d", "РДЦЛ.304112.903", new CadComponent("Механизм.a3d", 1))
        ]);

        foreach (var file in new[] { "Механизм.a3d", "Рычаг.a3d", "Тяга.a3d", "Коробка.a3d" })
            Assert.True(Rejected(analysis, file));
        Assert.Single(analysis.Cycles);                  // каноническая форма — без дублей
    }

    [Fact]
    public void Clean_set_is_fully_accepted()
    {
        var analysis = new ImportAnalyzer().Analyze(
        [
            Assembly("Узел.a3d", "РДЦЛ.304112.200",
                new CadComponent("Болт.m3d", 4),
                new CadComponent("Гайка М12.m3d", 4)),   // имя файла, а не выдуманное
            Part("Болт.m3d", "РДЦЛ.304112.310"),
            StandardPart("Гайка М12.m3d")
        ]);

        Assert.Equal(3, analysis.Importable.Count);
        Assert.Empty(analysis.Issues);
        Assert.Empty(analysis.Cycles);
        Assert.Equal(3, analysis.AcceptedCount);
    }
}