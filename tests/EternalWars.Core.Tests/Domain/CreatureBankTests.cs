using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Creature banks (ADR-068): rolled guards that always fight, rewards for the winner, refills after four weeks.</summary>
public class CreatureBankTests
{
    // A 3×2 bank at (2, 0) has its entrance at (3, 1); hero A starts below it.
    private static (GameSession Session, CreatureBank Bank) World(string bank, bool strong = true, ulong seed = 1, int size = 12)
    {
        var rows = Enumerable.Range(0, size).Select(_ => new string('.', size).ToCharArray()).ToArray();
        rows[2][3] = 'A';
        rows[size - 1][0] = 'C';
        rows[size - 1][size - 1] = 'B';
        var state = Create(new GameOptions(Seed: seed), rows.Select(r => new string(r)).ToArray());
        if (strong)
        {
            state.GetHero(A).Army.Set(0, new CreatureStack("dragon-utopia.black-dragon", 60));
        }

        var created = Add<CreatureBank>(state, bank, 2, 0);
        return (Start(state, revealAll: size <= 12), created);
    }

    [Fact]
    public void TheGuardAndReward_AreRolledFromTheTable_WhenTheGameStarts()
    {
        var (_, churchyard) = World(BuiltInMapObjects.Churchyard);
        Assert.Equal(new[] { new CreatureStack("necropolis.ghoul", 90) }, churchyard.Guard);
        Assert.Equal(new ResourceSet(Gold: 2500), churchyard.Reward);
        Assert.Equal(ArtifactTier.Minor, churchyard.RewardArtifact);

        var mansions = Enumerable.Range(1, 60).Select(s => World(BuiltInMapObjects.Mansion, seed: (ulong)s).Bank).ToList();
        Assert.All(mansions, m => Assert.Contains(m.Guard.Single().Count, new[] { 40, 60, 80, 100 }));
        Assert.All(mansions, m => Assert.Equal(ArtifactTier.Major, m.RewardArtifact));
        Assert.Contains(mansions, m => m.Reward == new ResourceSet(Gold: 5000, Crystal: 3, Mercury: 3, Sulfur: 3));
    }

    [Fact]
    public void HalfTheTime_AFifthOfTheGuardIsUpgraded()
    {
        var caches = Enumerable.Range(1, 80).Select(s => World(BuiltInMapObjects.ImpCache, seed: (ulong)s).Bank).ToList();
        var upgraded = caches.Where(c => c.Guard.Any(s => s.Creature == "inferno.familiar")).ToList();

        Assert.InRange(upgraded.Count, 25, 55);
        Assert.All(upgraded, c =>
        {
            var familiars = c.Guard.Single(s => s.Creature == "inferno.familiar").Count;
            var imps = c.Guard.Single(s => s.Creature == "inferno.imp").Count;
            Assert.Equal(familiars * 5, familiars + imps);
        });
    }

    [Fact]
    public void BeatingTheGuard_PaysTheWinner_AndLeavesTheBankEmpty()
    {
        var (session, bank) = World(BuiltInMapObjects.Churchyard);
        var state = session.State;
        var gold = state.GetPlayer(Blue).Resources.Gold;
        var plundered = new List<CreatureBankPlundered>();
        session.Events.Subscribe<CreatureBankPlundered>(plundered.Add);

        session.Step(A, Direction.North);
        Assert.Equal(BattlePhase.Fighting, state.Battle!.Phase);
        session.AutoResolve();

        Assert.Equal(gold + 2500, state.GetPlayer(Blue).Resources.Gold);
        var artifact = Assert.Single(state.GetHero(A).Artifacts.Backpack);
        Assert.Equal(ArtifactTier.Minor, state.Artifacts.Get(artifact).Tier);
        Assert.Equal(artifact, Assert.Single(plundered).Artifact);
        Assert.False(bank.IsGuarded);
        Assert.Equal(GameDate.First, bank.PlunderedOn);

        session.Interactions.Visit(state.GetHero(A), bank); // a later visit: no fight, and the Churchyard's morale penalty
        Assert.Null(state.Battle);
        Assert.Equal(CreatureBankRules.ChurchyardMorale, state.GetHero(A).TotalMorale);
    }

    [Fact]
    public void BankBattles_UseTheDefaultWidth_EvenWithTactics()
    {
        var (session, _) = World(BuiltInMapObjects.NagaBank);
        session.State.GetHero(A).Tactics = SkillLevel.Expert;

        session.Step(A, Direction.North);

        Assert.Equal(BattlePhase.Fighting, session.State.Battle!.Phase);
        Assert.Equal(BattleState.DefaultWidth, session.State.Battle.Width);
    }

    [Fact]
    public void ALostOrFledBattle_LeavesTheGuardWhole()
    {
        var (session, bank) = World(BuiltInMapObjects.MedusaStores, strong: false);
        var guard = bank.Guard.ToList();

        session.Step(A, Direction.North);
        session.Execute(new BattleRetreatCommand(Blue)).ThrowIfRejected();

        Assert.Null(session.State.Battle);
        Assert.Equal(guard, bank.Guard);
        Assert.True(bank.IsGuarded);
    }

    [Fact]
    public void APlunderedBank_FillsAgainFourWeeksLater_WithANewRoll()
    {
        var (session, bank) = World(BuiltInMapObjects.ImpCache);
        session.Step(A, Direction.North);
        session.AutoResolve();
        Assert.False(bank.IsGuarded);
        var refilled = new List<CreatureBankRefilled>();
        session.Events.Subscribe<CreatureBankRefilled>(refilled.Add);

        void EndTurns(int untilDay)
        {
            while (session.State.Date.AbsoluteDay < untilDay)
            {
                // Beating the imps brought level-ups to choose first.
                while (session.State.PendingDecision is EternalWars.Core.Domain.Decisions.LevelUpDecision levelUp)
                {
                    session.Execute(new ChooseSkillCommand(session.State.CurrentPlayer.Id, levelUp.Options[0])).ThrowIfRejected();
                }

                session.EndTurn();
            }
        }

        EndTurns(28);
        Assert.False(bank.IsGuarded);
        EndTurns(29);

        Assert.True(bank.IsGuarded);
        Assert.Single(refilled);
        Assert.Null(bank.PlunderedOn);
        Assert.NotEqual(ResourceSet.Empty, bank.Reward);
    }

    [Fact]
    public void TheRuins_RevealTheirSurroundings_OnEveryVisit()
    {
        var (session, ruins) = World(BuiltInMapObjects.Ruins, size: 40);
        var state = session.State;
        Assert.Equal(5, ruins.Guard.Count); // skeletons, wights, wraiths, skeleton warriors, arch liches
        Assert.False(state.Fog.IsExplored(Blue, new MapPosition(14, 1)));

        session.Step(A, Direction.North);

        Assert.True(state.Fog.IsExplored(Blue, new MapPosition(14, 1)));
        Assert.False(state.Fog.IsExplored(Blue, new MapPosition(20, 1)));
    }

    [Fact]
    public void AHeroWithoutAnArmy_CannotEnter()
    {
        var (session, _) = World(BuiltInMapObjects.Mansion, strong: false);
        var hero = session.State.GetHero(A);
        hero.Army.Set(0, null);

        Assert.Equal(MoveResult.NoArmy, session.Step(A, Direction.North));
    }

    [Fact]
    public void Banks_AreInTheMapFormat_WithoutTheirRoll()
    {
        var editor = new MapEditorSession(BlankScenario.Create(12, 8));
        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.DwarvenTreasury, new(2, 2), out var id));

        var json = MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal(20, MapDocument.CurrentFormatVersion);
        Assert.Contains("\"kind\": \"bank\"", json, StringComparison.Ordinal);
        var bank = Assert.IsType<CreatureBank>(restored.GetObject(id));
        Assert.Empty(bank.Guard);
        Assert.DoesNotContain(MapValidator.Validate(editor.Scenario), i => i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void TheAi_PlundersABankItCanBeat()
    {
        var (session, bank) = World(BuiltInMapObjects.ImpCache);

        var ai = new AdventureAi(session, controlHumanSlots: true);
        ai.PlayTurn();

        Assert.False(bank.IsGuarded);
        Assert.Equal(0, ai.Refused);
    }
}
