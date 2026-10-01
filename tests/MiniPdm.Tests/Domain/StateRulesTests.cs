using MiniPdm.Core.Domain;

namespace MiniPdm.Tests.Domain;

public sealed class StateRulesTests
{
    [Theory]
    [InlineData(ObjectState.InWork,   ObjectState.Approved,   true)]
    [InlineData(ObjectState.InWork,   ObjectState.Annulled,   true)]
    [InlineData(ObjectState.Approved, ObjectState.Annulled,   true)]
    [InlineData(ObjectState.Approved, ObjectState.InWork,     false)]
    [InlineData(ObjectState.Annulled, ObjectState.InWork,     false)]
    [InlineData(ObjectState.Annulled, ObjectState.Approved,   false)]
    [InlineData(ObjectState.InWork,   ObjectState.InWork,     false)]
    [InlineData(ObjectState.Approved, ObjectState.Approved,   false)]
    [InlineData(ObjectState.Annulled, ObjectState.Annulled,   false)]
    public void Transitions_follow_domain_rules(ObjectState from, ObjectState to, bool expected) =>
        Assert.Equal(expected, StateRules.CanTransition(from, to));

    [Theory]
    [InlineData(ObjectState.InWork,   true)]
    [InlineData(ObjectState.Approved, false)]
    [InlineData(ObjectState.Annulled, false)]
    public void Only_InWork_is_editable(ObjectState state, bool expected) =>
        Assert.Equal(expected, StateRules.IsEditable(state));

    [Theory]
    [InlineData(ObjectState.InWork,   true)]
    [InlineData(ObjectState.Approved, true)]
    [InlineData(ObjectState.Annulled, false)]
    public void Annulled_excluded_from_calculations(ObjectState state, bool expected) =>
        Assert.Equal(expected, StateRules.ParticipatesInCalculations(state));
}