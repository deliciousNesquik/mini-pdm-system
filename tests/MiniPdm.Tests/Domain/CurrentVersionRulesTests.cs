using MiniPdm.Core.Domain;

namespace MiniPdm.Tests.Domain;

/// <summary>Таблица поведения указателя из ADR 0004 каждая строка как кейс.</summary>
public sealed class CurrentVersionRulesTests
{
    [Fact]
    public void Empty_list_returns_null() =>
        Assert.Null(CurrentVersionRules.ResolveCurrentVersionNo([]));

    [Fact]
    public void Only_annulled_returns_null() =>
        Assert.Null(CurrentVersionRules.ResolveCurrentVersionNo(
            [(1, ObjectState.Annulled), (2, ObjectState.Annulled)]));

    [Fact]
    public void Max_non_annulled_wins() =>
        Assert.Equal(2, CurrentVersionRules.ResolveCurrentVersionNo(
            [(1, ObjectState.Approved), (2, ObjectState.InWork)]));

    [Fact]
    public void Annulled_latest_rolls_back_to_previous() =>
        Assert.Equal(2, CurrentVersionRules.ResolveCurrentVersionNo(
            [(1, ObjectState.InWork), (2, ObjectState.Approved), (3, ObjectState.Annulled)]));

    [Fact]
    public void Order_independent() =>
        Assert.Equal(2, CurrentVersionRules.ResolveCurrentVersionNo(
            [(3, ObjectState.Annulled), (1, ObjectState.InWork), (2, ObjectState.Approved)]));
}