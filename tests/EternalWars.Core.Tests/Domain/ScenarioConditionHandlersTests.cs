using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Game.Conditions;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// Every scenario condition kind has its one handler (<see cref="ScenarioConditionHandlers"/>): the game's end, the sentences and the validator
/// dispatch through it, so a kind left out fails here, not when a game should end.
/// </summary>
public sealed class ScenarioConditionHandlersTests
{
    public static TheoryData<ConditionKind> Kinds() => [.. Enum.GetValues<ConditionKind>()];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryKind_HasExactlyOneHandler(ConditionKind kind)
    {
        Assert.Single(ScenarioConditionHandlers.All, h => h.Kind == kind);
        Assert.Equal(kind, ScenarioConditionHandlers.For(kind).Kind);
    }

    [Fact]
    public void TheRegistry_HoldsNothingElse() =>
        Assert.Equal(Enum.GetValues<ConditionKind>(), ScenarioConditionHandlers.All.Select(h => h.Kind));

    [Fact]
    public void OnlySurvivingHasNoDeadline_OnlyTheTimeLimitDefeatsEveryone()
    {
        Assert.Equal([ConditionKind.SurviveUntilDay], ScenarioConditionHandlers.All.Where(h => h.IsVictory && !h.CanHaveDeadline).Select(h => h.Kind));
        Assert.DoesNotContain(ScenarioConditionHandlers.All, h => !h.IsVictory && h.CanHaveDeadline);
        Assert.Equal([ConditionKind.TimeLimit], ScenarioConditionHandlers.All.Where(h => h.DefeatsEveryone).Select(h => h.Kind));
    }

    [Fact]
    public void AnUnknownKind_ReadsAsItsNumber_AndIsNoVictory()
    {
        var unknown = new ScenarioCondition((ConditionKind)99);
        Assert.False(unknown.IsVictory);
        Assert.False(unknown.CanHaveDeadline);
        Assert.Throws<ArgumentOutOfRangeException>(() => ScenarioConditionHandlers.For(unknown.Kind));
    }
}
