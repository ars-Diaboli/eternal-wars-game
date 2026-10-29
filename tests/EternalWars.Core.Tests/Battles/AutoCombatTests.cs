using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Battles;

/// <summary>Auto combat (ADR-090, CMB-10): the setting, which battles it covers, and a preview that leaves no trace and that quick combat matches.</summary>
public class AutoCombatTests
{
    /// <summary>Blue's hero A attacks Red's hero B; Red is a computer player unless <paramref name="redIsHuman"/>.</summary>
    private static (GameState State, GameSession Session) Attack(SkillLevel tactics = SkillLevel.None, bool redIsHuman = false, Action<GameState>? setup = null)
    {
        var state = Create("A.B", "...");
        state.GetHero(A).Tactics = tactics;
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 30));
        state.GetHero(A).Army.Set(1, new CreatureStack(BuiltInCreatures.Archer, 12));
        state.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 25));
        state.GetPlayer(Red).IsHuman = redIsHuman;
        setup?.Invoke(state);
        var session = Start(state);
        session.MoveTo(A, new MapPosition(2, 0));
        return (state, session);
    }

    [Theory]
    [InlineData(SkillLevel.None)] // fighting at once
    [InlineData(SkillLevel.Advanced)] // the width to choose, then the deployment
    public void ThePreview_ChangesNothing_AndQuickCombatEndsTheBattleExactlySo(SkillLevel tactics)
    {
        var (state, session) = Attack(tactics);
        var phase = state.Battle!.Phase;
        var before = GameStateDigest.Describe(state);

        var preview = session.Battles.Preview();

        Assert.Equal(before, GameStateDigest.Describe(state));
        Assert.Equal(phase, state.Battle!.Phase);
        Assert.Empty(session.CommandLog.OfType<AutoResolveBattleCommand>());

        var ended = new List<BattleEnded>();
        session.Events.Subscribe<BattleEnded>(ended.Add);
        session.Execute(new AutoResolveBattleCommand(Blue)).ThrowIfRejected();

        var result = Assert.Single(ended);
        Assert.Equal((result.Winner, result.Reason, result.Experience), (preview.Winner, preview.Reason, preview.Experience));
        Assert.Equal(result.Casualties, preview.Casualties);
    }

    [Fact]
    public void ThePreview_InTheMiddleOfABattle_StartsFromWhereItStands()
    {
        var (state, session) = Attack();
        session.Execute(new BattleWaitCommand(session.Battles.Commander(state.Battle!.ActiveUnit!.Side)!.Value)).ThrowIfRejected();

        var preview = session.Battles.Preview();
        var ended = new List<BattleEnded>();
        session.Events.Subscribe<BattleEnded>(ended.Add);
        session.Execute(new AutoResolveBattleCommand(Blue)).ThrowIfRejected();

        Assert.Equal(Assert.Single(ended).Casualties, preview.Casualties);
    }

    [Fact]
    public void ThePreview_ForeseesTheRaisedUndead_AndTheDefeatedHerosArtifacts()
    {
        var (state, session) = Attack(setup: s =>
        {
            s.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Seraph, 30));
            s.GetHero(A).Skills.Set(NecromancyRules.Skill, SkillLevel.Basic);
            s.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 100));
            s.GetHero(B).Artifacts.AddToBackpack("iron-band");
        });

        var preview = session.Battles.Preview();
        var raised = new List<UndeadRaised>();
        session.Events.Subscribe<UndeadRaised>(raised.Add);
        session.Execute(new AutoResolveBattleCommand(Blue)).ThrowIfRejected();

        Assert.Equal(BattleEndReason.Defeated, preview.Reason);
        Assert.NotEmpty(preview.Raised);
        Assert.Equal(Assert.Single(raised).Raised, preview.Raised);
        Assert.Equal(["iron-band"], preview.Artifacts);
        Assert.Contains("iron-band", state.GetHero(A).Artifacts.Backpack);
    }

    [Fact]
    public void AutoCombat_CoversBattlesAgainstTheComputer_NotBetweenTwoHumans()
    {
        Assert.Equal(Blue, Attack().Session.Battles.AutoCombatPlayer());
        Assert.Null(Attack(redIsHuman: true).Session.Battles.AutoCombatPlayer());

        var (_, session) = Attack();
        session.Execute(new AutoResolveBattleCommand(Blue)).ThrowIfRejected();
        Assert.Null(session.Battles.AutoCombatPlayer()); // over
    }

    [Fact]
    public void TheSetting_IsAHumanPlayersAtAnyTime_AndIsSavedWithTheGame()
    {
        var state = Create("A.B", "...");
        state.GetPlayer(Red).IsHuman = true;
        var map = MapConverter.ToDocument(state);
        var session = new GameSession(MapConverter.ToGameState(map));
        session.Start();
        var changes = new List<AutoCombatChanged>();
        session.Events.Subscribe<AutoCombatChanged>(changes.Add);

        Assert.Equal(AutoCombatMode.Off, session.State.GetPlayer(Blue).AutoCombat);
        session.Execute(new SetAutoCombatCommand(Red, AutoCombatMode.Ask)).ThrowIfRejected(); // not Red's turn
        session.Execute(new SetAutoCombatCommand(Blue, AutoCombatMode.Always)).ThrowIfRejected();

        Assert.Equal([new AutoCombatChanged(Red, AutoCombatMode.Ask), new AutoCombatChanged(Blue, AutoCombatMode.Always)], changes);
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(GameReplay.Record(map, session))));
        Assert.Equal(AutoCombatMode.Always, replayed.State.GetPlayer(Blue).AutoCombat);
        Assert.Equal(AutoCombatMode.Ask, replayed.State.GetPlayer(Red).AutoCombat);
    }

    [Fact]
    public void AComputerPlayer_HasNoAutoCombatSetting()
    {
        var state = Create("A.B", "...");
        state.GetPlayer(Red).IsHuman = false;
        var session = Start(state);

        Assert.Contains("human", session.Execute(new SetAutoCombatCommand(Red, AutoCombatMode.Always)).Error);
    }
}
