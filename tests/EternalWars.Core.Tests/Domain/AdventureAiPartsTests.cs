using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The parts of the adventure AI (<see cref="AdventureAi"/>): its weights, threat map, target valuer, town manager and decision responder.</summary>
public class AdventureAiPartsTests
{
    private static AiContext Context(GameSession session, AiWeights? weights = null) => new(session, weights ?? AiWeights.Default, controlHumanSlots: true);

    // Weights ------------------------------------------------------------------------------------------

    [Fact]
    public void TheDefaultWeights_AreTheOnesTheAiPlaysWith()
    {
        var weights = AiWeights.Default;

        Assert.Equal(new AiWeights(), weights);
        Assert.Equal(10, weights.CandidatesChecked);
        Assert.Equal(40, weights.FightsChecked);
        Assert.Equal(8, weights.FrontierChecked);
        Assert.Equal(0.08, weights.DistancePenalty);
        Assert.Equal(70, weights.PlausiblePercent);
        Assert.Equal(0.4, weights.MonsterLoss);
        Assert.Equal(0.15, weights.ArenaLoss);
        Assert.Equal(0.8, weights.SafeShare);
        Assert.Equal(1000, weights.CarryThreshold);
        Assert.Equal(4000, weights.TownValue);
        Assert.Equal(2000, weights.EnemyTownValue);
        Assert.Equal(500, weights.TownDwellingValue);
        Assert.Equal(6000, weights.DefendTownValue);
        Assert.Equal(2000, weights.EnemyHeroValue);
        Assert.Equal(0.8, weights.HeroArmyShare);
        Assert.Equal(0.35, weights.GuardArmyShare);
        Assert.Equal(900, weights.ExploreValue);
        Assert.Equal(0.1, weights.HuntShare);
        Assert.Equal(3, weights.WayRadius);
        Assert.Equal(500, weights.GuardOpened);
        Assert.Equal(800, weights.ArtifactValue);
        Assert.Equal(80, weights.ArtifactScoreValue);
        Assert.Equal(1500, weights.BankArtifactValue);
        Assert.Equal(1500, weights.ChestValue);
        Assert.Equal(0.85, weights.BoxCreatureShare);
        Assert.Equal(60, weights.TroopsTripPercent);
        Assert.Equal(0.3, weights.TroopsTripShare);
        Assert.Equal(1000, weights.TroopsTripFloor);
        Assert.Equal(1200, weights.LessonValue);
        Assert.Equal(1000, weights.ObeliskValue);
        Assert.Equal(12000, weights.GrailValue);
        Assert.Equal(1500, weights.WarMachineCushion);
        Assert.Equal(3000, weights.TrainingCushion);
        Assert.Equal(3, weights.KeptGoldPart);
        Assert.Equal(3, weights.CartographerGoldTimes);
        Assert.Equal(3, weights.BuildGoldDays);
        Assert.Equal(7, weights.BuildResourceDays);
        Assert.Equal(4, weights.MostHeroes);
        Assert.Equal(50, weights.TradeTreasuryPercent);
        Assert.Equal(2, weights.TradeWaitDays);
        Assert.Equal(3, weights.SavingDays);
        Assert.Equal(20, weights.KeepBasic);
        Assert.Equal(10, weights.KeepMagic);
        Assert.Equal(300, weights.DoorSaving);
        Assert.Equal(3, weights.DoorChecksPerHero);
        Assert.Equal(0.3, weights.ViewTerrainShare);
        Assert.Equal(100, weights.NativeWalkSaving);
        Assert.Equal(47, AiWeights.Names.Count);
    }

    [Fact]
    public void AWeight_IsSetByName_AndTheDefaultsStayAsTheyAre()
    {
        var weights = AiWeights.Default.With("MonsterLoss", "0.25").With("CandidatesChecked=3").With(" CarryThreshold = 5000 ");

        Assert.Equal(0.25, weights.MonsterLoss);
        Assert.Equal(3, weights.CandidatesChecked);
        Assert.Equal(5000, weights.CarryThreshold);
        Assert.Equal(0.4, AiWeights.Default.MonsterLoss);
        Assert.Equal(AiWeights.Default with { MonsterLoss = 0.25, CandidatesChecked = 3, CarryThreshold = 5000 }, weights);
    }

    [Theory]
    [InlineData("Bogus", "1")]
    [InlineData("monsterloss", "0.5")]
    [InlineData("MonsterLoss", "much")]
    [InlineData("MonsterLoss", "NaN")]
    [InlineData("CandidatesChecked", "1.5")]
    [InlineData("MonsterLoss", "-0.1")]
    [InlineData("CandidatesChecked", "-1")]
    [InlineData("CarryThreshold", "-5")]
    public void AnUnknownWeight_OrABadValue_IsRefused(string name, string value)
    {
        Assert.Throws<FormatException>(() => AiWeights.Default.With(name, value));
    }

    [Fact]
    public void AnAssignmentWithoutAValue_IsRefused()
    {
        Assert.Throws<FormatException>(() => AiWeights.Default.With("MonsterLoss"));
    }

    [Fact]
    public void ChangedWeights_ReachTheAi()
    {
        // Pricing no candidate at all: the AI no longer goes for the pile it collects with the defaults.
        var state = Create("A.......B");
        var pile = Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 3, 0);
        Steady(state);
        var weights = AiWeights.Default with { CandidatesChecked = 0 };
        var ai = new AdventureAi(Start(state), controlHumanSlots: true, weights);

        Assert.True(ai.PlayTurn());

        Assert.Same(weights, ai.Weights);
        Assert.Contains(state.Objects, o => o == pile);
        Assert.Equal(new MapPosition(0, 0), state.GetHero(A).Position);
        Assert.Same(AiWeights.Default, new AdventureAi(Start(Create("A.B"))).Weights);
    }

    [Fact]
    public void TheWeightsOnceInline_ReachTheirParts()
    {
        // ADR-232: the gold kept back at a map object and a Pandora's Box's creatures were numbers in the parts.
        var state = Create("A.B");
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 3000);
        var session = Start(state);
        var box = new BoxContents { Creatures = [new CreatureStack(BuiltInCreatures.Wolf, 10)] };
        var wolves = 10 * state.Creatures.Get(BuiltInCreatures.Wolf).FightValue;
        var shipped = Context(session);
        var tuned = Context(session, AiWeights.Default with { KeptGoldPart = 2, BoxCreatureShare = 0.5 });

        Assert.Equal(2000, shipped.Spare(Blue).Gold);
        Assert.Equal(1500, tuned.Spare(Blue).Gold);
        Assert.Equal(0.85 * wolves, new TargetValuer(shipped, new ThreatMap(shipped)).BoxValue(state.GetHero(A), box), 6);
        Assert.Equal(0.5 * wolves, new TargetValuer(tuned, new ThreatMap(tuned)).BoxValue(state.GetHero(A), box), 6);
        Assert.Equal(0, Context(session, AiWeights.Default with { KeptGoldPart = 0 }).Spare(Blue).Gold); // taken as 1: all the gold is kept
    }

    // Threat map ---------------------------------------------------------------------------------------

    [Fact]
    public void TheThreatMap_FindsTheEnemyHeroesThatCanReachAPlace()
    {
        var state = Create("A" + new string('.', 58) + "B");
        state.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var ai = Context(Start(state));
        var threats = new ThreatMap(ai);
        var enemy = state.GetHero(B);
        var power = ai.ArmyPower(ai.Seen(enemy));

        var threat = Assert.Single(threats.Threats(Blue));

        Assert.Same(enemy, threat.Enemy);
        Assert.Equal(power, threat.Power);
        Assert.Equal(power, threats.Danger(Blue, new MapPosition(57, 0)));
        Assert.Equal(0, threats.Danger(Blue, new MapPosition(1, 0)));
        Assert.Equal(0, threats.Danger(Blue, new MapPosition(57, 0), except: enemy.Position));
        Assert.DoesNotContain(threats.Threats(Red), t => t.Enemy.Id == B);
    }

    [Fact]
    public void APlaceHolds_WithTheSafeShareOfTheDanger()
    {
        var session = Start(Create("A.B"));
        var threats = new ThreatMap(Context(session));
        var cautious = new ThreatMap(Context(session, AiWeights.Default with { SafeShare = 1.5 }));

        Assert.True(threats.Holds(0, 0));
        Assert.True(threats.Holds(80, 100));
        Assert.False(threats.Holds(79, 100));
        Assert.False(cautious.Holds(149, 100));
        Assert.True(cautious.Holds(150, 100));
    }

    // Target valuer ------------------------------------------------------------------------------------

    [Fact]
    public void TheTargetValuer_ChoosesAPickup_UnlessAnotherHeroClaimedIt()
    {
        var state = Create("A.......B");
        Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 3, 0);
        Steady(state);
        var ai = Context(Start(state));
        var values = new TargetValuer(ai, new ThreatMap(ai));
        var hero = state.GetHero(A);

        Assert.Equal(new MapPosition(3, 0), values.ChooseTarget(hero, []));
        Assert.Null(values.ChooseTarget(hero, [new MapPosition(3, 0)]));

        ai.Claimed.Add(new MapPosition(3, 0));
        Assert.Null(values.ChooseTarget(hero, []));
    }

    [Fact]
    public void TheTargetValuer_PricesRoutes()
    {
        var state = Create("A.......B");
        var ai = Context(Start(state));
        var values = new TargetValuer(ai, new ThreatMap(ai));
        var hero = state.GetHero(A);

        var near = values.WalkCost(hero, new MapPosition(2, 0));
        var far = values.WalkCost(hero, new MapPosition(6, 0));

        Assert.NotNull(near);
        Assert.True(far > near);
    }

    // Town manager -------------------------------------------------------------------------------------

    [Fact]
    public void TheTownManager_BuildsAndRecruits_WithoutEndingTheTurn()
    {
        var state = TownsScenario.Create();
        var session = Start(state);
        var ai = Context(session);
        var towns = new TownManager(ai, new ThreatMap(ai));

        towns.ManageTowns(Blue);

        Assert.Contains(session.CommandLog, c => c is BuildDwellingCommand);
        Assert.Contains(session.CommandLog, c => c is RecruitCommand);
        Assert.DoesNotContain(session.CommandLog, c => c is EndTurnCommand or MoveHeroCommand);
        Assert.Equal(Blue, state.CurrentPlayer.Id);
        Assert.Equal(0, ai.Refused);
    }

    // Decision responder -------------------------------------------------------------------------------

    [Fact]
    public void TheDecisionResponder_AnswersAChest()
    {
        var state = Create("A...", "...B");
        var chest = Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 2, 0);
        chest.FixedValues = new ChestValues(1500, 1000);
        var session = Start(state);
        session.MoveTo(A, chest.VisitPosition);
        Assert.IsType<TreasureChestDecision>(state.PendingDecision);
        var ai = Context(session);
        var decisions = new DecisionResponder(ai, new TargetValuer(ai, new ThreatMap(ai)));

        Assert.True(decisions.ResolvePending(Blue));

        // The experience brings a level, so the AI takes it and then answers the level-up as well.
        Assert.Null(state.PendingDecision);
        Assert.Equal(new ResolveTreasureChestCommand(Blue, TreasureChestReward.Experience), session.CommandLog[^2]);
        Assert.IsType<ChooseSkillCommand>(session.CommandLog[^1]);
        Assert.DoesNotContain(state.Objects, o => o is TreasureChest);
        Assert.Equal(2, ai.Commands);
    }

    [Fact]
    public void TheDecisionResponder_HasNothingToDo_WithoutADecision()
    {
        var session = Start(Create("A.B"));
        var ai = Context(session);
        var decisions = new DecisionResponder(ai, new TargetValuer(ai, new ThreatMap(ai)));

        Assert.True(decisions.ResolvePending(Blue));

        Assert.Equal(0, ai.Commands);
    }
}
