using MiniPdm.Core.Cad;
using MiniPdm.Core.Domain;
using MiniPdm.Core.Import;

namespace MiniPdm.Tests.Import;

public sealed class ReImportRulesTests
{
    private static readonly CadDocument Wheel = new()
    {
        FileName = "Колесо зубчатое.m3d", Type = ObjectType.Part,
        Designation = "РДЦЛ.304112.302", Name = "Колесо зубчатое",
        Material = "Сталь 40Х", MassKg = 5.86m
    };

    private static ExistingObjectSnapshot Snapshot(
        ObjectState? state = ObjectState.InWork,
        decimal? mass = 5.86m,
        IReadOnlyList<ExistingChild>? composition = null) => new(
        ObjectType.Part, "РДЦЛ.304112.302", "Колесо зубчатое",
        state, "Сталь 40Х", mass, composition ?? []);

    private static IReadOnlyDictionary<string, CadDocument> Docs(params CadDocument[] docs) =>
        docs.ToDictionary(d => d.FileName, StringComparer.Ordinal);

    [Fact]
    public void No_existing_object_creates_it() =>
        Assert.Equal(ReImportAction.CreateObject, ReImportRules.Decide(Wheel, null, Docs(Wheel)));

    [Fact]
    public void Same_identity_other_type_is_conflict()
    {
        var snapshot = Snapshot() with { Type = ObjectType.Assembly };
        Assert.Equal(ReImportAction.TypeConflict, ReImportRules.Decide(Wheel, snapshot, Docs(Wheel)));
    }

    [Fact]
    public void No_active_version_creates_new() =>
        Assert.Equal(ReImportAction.CreateNewVersion,
            ReImportRules.Decide(Wheel, Snapshot(state: null), Docs(Wheel)));

    [Fact]
    public void Identical_data_is_unchanged_even_if_approved() =>
        Assert.Equal(ReImportAction.Unchanged,
            ReImportRules.Decide(Wheel, Snapshot(state: ObjectState.Approved), Docs(Wheel)));

    [Fact]
    public void Changed_data_updates_in_work_version() =>
        Assert.Equal(ReImportAction.UpdateInWorkVersion,
            ReImportRules.Decide(Wheel, Snapshot(mass: 5.92m), Docs(Wheel)));   // v2-кейс набора

    [Fact]
    public void Changed_data_creates_new_version_when_approved() =>
        Assert.Equal(ReImportAction.CreateNewVersion,
            ReImportRules.Decide(Wheel, Snapshot(ObjectState.Approved, 5.92m), Docs(Wheel)));

    [Fact]
    public void Decimal_scale_is_not_a_change() =>
        Assert.Equal(ReImportAction.Unchanged,
            ReImportRules.Decide(Wheel, Snapshot(mass: 5.860m), Docs(Wheel)));

    [Fact]
    public void Composition_order_is_not_a_change()     // ключевой тест ADR 0007
    {
        var bolt = Part("Болт.m3d", "РДЦЛ.304112.310");
        var nut = Part("Гайка.m3d", "РДЦЛ.304112.311");
        var assembly = new CadDocument
        {
            FileName = "Узел.a3d", Type = ObjectType.Assembly,
            Designation = "РДЦЛ.304112.200", Name = "Узел",
            Components = [new("Болт.m3d", 1), new("Гайка.m3d", 2)]
        };
        var snapshot = new ExistingObjectSnapshot(
            ObjectType.Assembly, "РДЦЛ.304112.200", "Узел",
            ObjectState.InWork, null, null,
            [new ExistingChild(ObjectType.Part, "РДЦЛ.304112.311", "Гайка", 2),
             new ExistingChild(ObjectType.Part, "РДЦЛ.304112.310", "Болт", 1)]);  // другой порядок

        Assert.Equal(ReImportAction.Unchanged,
            ReImportRules.Decide(assembly, snapshot, Docs(assembly, bolt, nut)));
    }

    [Fact]
    public void Composition_quantity_change_updates_version()
    {
        var bolt = Part("Болт.m3d", "РДЦЛ.304112.310");
        var assembly = new CadDocument
        {
            FileName = "Узел.a3d", Type = ObjectType.Assembly,
            Designation = "РДЦЛ.304112.200", Name = "Узел",
            Components = [new("Болт.m3d", 4)]
        };
        var snapshot = new ExistingObjectSnapshot(
            ObjectType.Assembly, "РДЦЛ.304112.200", "Узел",
            ObjectState.InWork, null, null,
            [new ExistingChild(ObjectType.Part, "РДЦЛ.304112.310", "Болт", 3)]);  // v2-кейс крышки

        Assert.Equal(ReImportAction.UpdateInWorkVersion,
            ReImportRules.Decide(assembly, snapshot, Docs(assembly, bolt)));
    }

    private static CadDocument Part(string file, string designation) => new()
    {
        FileName = file, Type = ObjectType.Part,
        Designation = designation, Name = file.Replace(".m3d", ""),
        Material = "Сталь", MassKg = 0.05m
    };
}