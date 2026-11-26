using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using static EternalWars.Core.Domain.Battles.BattleSide;

namespace EternalWars.Core.Tests.Battles;

/// <summary>The battle result's summary (ADR-314): the title for the viewer at this screen, the reason, each side's losses.</summary>
public class BattleSummaryTests
{
    private static readonly CreatureId Wolf = new("wolf"), Archer = new("archer");

    private static string Name(BattleSide side) => side == Attacker ? "Alpha" : "Neutral wolves";

    private static BattleEnded Ended(BattleSide winner, BattleEndReason reason = BattleEndReason.Defeated, params BattleCasualty[] casualties) =>
        new(winner, reason, casualties, 120);

    [Theory]
    [InlineData(new[] { Attacker }, Attacker, "Victory")]
    [InlineData(new[] { Attacker }, Defender, "Defeat")]
    [InlineData(new[] { Defender }, Defender, "Victory")]
    [InlineData(new[] { Attacker, Defender }, Defender, "Neutral wolves wins")] // both sides play at this screen
    [InlineData(new BattleSide[0], Attacker, "Alpha wins")] // neither: two computers, the replay viewer
    public void TheTitle_IsVictoryOrDefeat_ForTheOneSidePlayedHere_ElseWhoWins(BattleSide[] here, BattleSide winner, string title)
    {
        Assert.Equal(title, BattleSummary.Of(Ended(winner), here.ToHashSet(), Name).Title);
    }

    [Theory]
    [InlineData(BattleEndReason.Defeated, "Alpha wins the battle.")]
    [InlineData(BattleEndReason.Retreated, "Neutral wolves retreated.")]
    [InlineData(BattleEndReason.Surrendered, "Neutral wolves surrendered.")]
    public void TheReason_NamesWhoRetreatedOrSurrendered_ElseWhoWon(BattleEndReason reason, string text)
    {
        Assert.Equal(text, BattleSummary.Of(Ended(Attacker, reason), new HashSet<BattleSide>(), Name).Reason);
    }

    [Fact]
    public void TheLosses_AreEachSides_TheAttackersFirst_OneEntryACreature()
    {
        var ended = Ended(
            Attacker, BattleEndReason.Defeated,
            new BattleCasualty(Defender, Wolf, 3), new BattleCasualty(Attacker, Archer, 2), new BattleCasualty(Defender, Wolf, 4),
            new BattleCasualty(Attacker, Wolf, 0));

        var summary = BattleSummary.Of(ended, new HashSet<BattleSide> { Attacker }, Name);

        Assert.Equal(
            [
                new SideLosses(Attacker, "Alpha", [new BattleCasualty(Attacker, Archer, 2)]), // a creature with none lost is left out
                new SideLosses(Defender, "Neutral wolves", [new BattleCasualty(Defender, Wolf, 7)]), // two stacks of wolves, counted once
            ],
            summary.Losses,
            (a, b) => a.Side == b.Side && a.Name == b.Name && a.Lost.SequenceEqual(b.Lost));
        Assert.Equal(120, summary.Experience);
    }

    [Fact]
    public void TheSidesPlayedHere_AreThePeoplesPlayingAtThisScreen()
    {
        var state = TestWorld.Create("A.B");
        var battle = TestBattle.Create(); // Blue attacks, Red defends

        Assert.Equal([Attacker, Defender], BattleSummary.SidesPlayedHere(state, battle.State, null).Order()); // hot seat: both
        Assert.Equal([Attacker], BattleSummary.SidesPlayedHere(state, battle.State, p => p == TestWorld.Blue)); // LAN: Red plays elsewhere

        state.AssignControllers(new Dictionary<PlayerId, bool> { [TestWorld.Red] = false });
        Assert.Equal([Attacker], BattleSummary.SidesPlayedHere(state, battle.State, null)); // the computer's side never is
    }
}
