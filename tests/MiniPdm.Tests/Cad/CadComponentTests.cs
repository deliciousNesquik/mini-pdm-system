using MiniPdm.Core.Cad;

namespace MiniPdm.Tests.Cad;

public sealed class CadComponentTests
{
    [Fact]
    public void Equality_is_by_value() =>
        Assert.Equal(new CadComponent("Вал.m3d", 2), new CadComponent("Вал.m3d", 2));

    [Fact]
    public void Different_count_means_not_equal() =>
        Assert.NotEqual(new CadComponent("Вал.m3d", 2), new CadComponent("Вал.m3d", 3));

    [Fact]
    public void Names_compare_ordinally()
    {
        // Сравнение строк как требует ADR 0005 для нормализованных имен.
        Assert.NotEqual(new CadComponent("Шайба.m3d", 1), new CadComponent("шайба.m3d", 1));
    }
}