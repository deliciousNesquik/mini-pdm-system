using MiniPdm.Core.Cad;
using MiniPdm.Core.Domain;

namespace MiniPdm.Tests.Cad;

public sealed class CadDocumentEqualityTests
{
    private static CadDocument MakeDocument() => new()
    {
        FileName = "Вал.m3d",
        Type = ObjectType.Part,
        Designation = "РДЦЛ.304112.301",
        Name = "Вал",
        Material = "Сталь 40Х",
        MassKg = 3.42m
    };

    [Fact]
    public void Same_attribute_values_are_equal() =>
        Assert.Equal(MakeDocument(), MakeDocument());

    [Fact]
    public void Record_equality_does_not_compare_list_contents()
    {
        // record-равенство не заглядывает в содержимое списков.
        // Значит проверка "данные не изменились" при повторном
        // импорте - обязанность планировщика, а не Equals.
        var a = MakeDocument() with { Components = [new CadComponent("Гайка.m3d", 1)] };
        var b = MakeDocument() with { Components = [new CadComponent("Гайка.m3d", 1)] };

        Assert.NotEqual(a, b);
    }
}