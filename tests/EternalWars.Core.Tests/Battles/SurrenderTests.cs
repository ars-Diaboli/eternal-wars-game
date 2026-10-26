using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Battles;

/// <summary>Surrender, the AI's retreat and heroes waiting in the tavern after either (ADR-070, ADR-081).</summary>
public class SurrenderTests
{
    private const string Aldric = "haven.aldric";

    /// <summary>Both players own a town (so losing the hero does not end the game); A at (0, 8) attacks B at (2, 8).</summary>
    private static GameState World()
    {
        var rows = Enumerable.Repeat(new string('.', 20), 8).Append("A.B.................").ToArray();
        var state = Create(rows);
        AddTown(state, TownIds.Haven, 0, 0, Blue).SetLevel(BuiltInBuildings.Tavern, 1);
        AddTown(state, TownIds.Haven, 10, 0, Red).SetLevel(BuiltInBuildings.Tavern, 1);
        return state;
    }

    private static Town TownOf(GameState state, PlayerId owner) => state.ObjectsOf<Town>().Single(t => t.Owner == owner);

    private static (GameState State, GameSession Session, List<IEvent> Events) Battle()
    {
        var state = World();
        state.GetHero(A).Definition = Aldric;
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 5000);
        state.GetPlayer(Red).Resources = new ResourceSet(Gold: 0);
        var session = Start(state);
        var events = new List<IEvent>();
        session.Events.Subscribe<IEvent>(events.Add);
        session.MoveTo(A, new MapPosition(2, 8));
        return (state, session, events);
    }

    [Fact]
    public void Surrender_PaysHalfTheSurvivorsGoldToTheEnemy_AndTheHeroWaitsInTheTavernWithItsArmy()
    {
        var (state, session, events) = Battle();
        var gold = state.GetPlayer(Blue).Resources.Gold;
        var redGold = state.GetPlayer(Red).Resources.Gold;
        var price = ((10 * state.Creatures.Get(BuiltInCreatures.Footman).Cost.Gold) + 1) / 2;
        Assert.Equal(price, SurrenderRules.Price(state.Battle!, BattleSide.Attacker));

        Assert.True(session.Execute(new BattleSurrenderCommand(Blue)).IsAccepted);

        Assert.Equal(gold - price, state.GetPlayer(Blue).Resources.Gold);
        Assert.Equal(redGold + price, state.GetPlayer(Red).Resources.Gold);
        Assert.Contains(new HeroSurrendered(A, Blue, Red, price), events);
        var ended = Assert.Single(events.OfType<BattleEnded>());
        Assert.Equal((BattleSide.Defender, BattleEndReason.Surrendered), (ended.Winner, ended.Reason));
        Assert.Contains(new HeroDefeated(A, Blue, Retreated: true), events);
        Assert.DoesNotContain(state.Heroes, h => h.Id == A);
        var waiting = Assert.Single(state.RetiredHeroesOf(Blue));
        Assert.Equal(10, waiting.Army[0]!.Count);
        Assert.Null(state.Battle);
    }

    [Fact]
    public void Surrender_IsRefused_WithoutTheGold_AndAgainstNeutrals()
    {
        var (state, session, _) = Battle();
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 100);

        Assert.Contains("costs", session.Execute(new BattleSurrenderCommand(Blue)).Error!, StringComparison.Ordinal);
        Assert.NotNull(state.Battle);

        var neutral = Create("A..", "...");
        neutral.GetPlayer(Blue).Resources = new ResourceSet(Gold: 5000);
        neutral.AddObject(new MonsterGuard(neutral.NextObjectId(), MapObjectCatalog.Default.Get(BuiltInMapObjects.Monster), new MapPosition(2, 0), BuiltInCreatures.Wolf, 5));
        var guarded = Start(neutral);
        guarded.MoveTo(A, new MapPosition(2, 0));
        Assert.NotNull(neutral.Battle);
        Assert.Contains("Neutral", guarded.Execute(new BattleSurrenderCommand(Blue)).Error!, StringComparison.Ordinal);
        Assert.True(guarded.Execute(new BattleRetreatCommand(Blue)).IsAccepted); // retreating from neutrals is fine
    }

    [Fact]
    public void ARetreatedHero_ComesBackInEveryTavernOfItsPlayer_AsItWas_WithAStarterStack()
    {
        var (state, session, _) = Battle();
        var hero = state.GetHero(A);
        hero.Experience = 2000;
        hero.Level = 3;
        var town = TownOf(state, Blue);

        session.Execute(new BattleRetreatCommand(Blue)).ThrowIfRejected();

        var waiting = Assert.Single(state.RetiredHeroesOf(Blue));
        Assert.True(waiting.Army.IsEmpty); // a retreat costs the army
        Assert.Equal(Aldric, TavernRules.Offers(state, Blue, town)[0].Id);
        Assert.DoesNotContain(TavernRules.Offers(state, Red, TownOf(state, Red)), h => h.Id == Aldric); // only its own player sees it

        session.Execute(new HireHeroCommand(Blue, town.Id, Aldric)).ThrowIfRejected();

        var back = state.GetHero(A); // the same hero, same id
        Assert.Equal((3, 2000), (back.Level, back.Experience));
        Assert.Equal(town.VisitPosition, back.Position);
        var starter = state.Creatures.Get(state.HeroRoster.Get(Aldric).PreferredCreature);
        Assert.Equal(new CreatureStack(starter.Id, starter.WeeklyGrowth), Assert.Single(back.Army.Stacks).Stack);
        Assert.Empty(state.RetiredHeroes);
    }

    [Fact]
    public void ADefeatedPlayersWaitingHeroes_GoBackToTheRoster()
    {
        var state = Create("A.B", "...");
        state.GetHero(A).Definition = Aldric;
        var session = Start(state);
        session.MoveTo(A, new MapPosition(2, 0));

        session.Execute(new BattleRetreatCommand(Blue)).ThrowIfRejected();

        Assert.True(state.IsOver); // Blue had no other hero and no town
        Assert.Empty(state.RetiredHeroes);
    }

    [Fact]
    public void AComputerPlayer_RetreatsWhenItsArmyIsAlmostBeaten()
    {
        var state = World();
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 40));
        var defender = state.GetHero(B);
        defender.Definition = Aldric;
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            defender.Army.Set(slot, new CreatureStack(BuiltInCreatures.Footman, 5));
        }

        state.GetPlayer(Red).IsHuman = false;
        state.GetPlayer(Red).Resources = new ResourceSet(Gold: 100_000);
        var session = Start(state);
        var events = new List<IEvent>();
        session.Events.Subscribe<IEvent>(events.Add);

        session.MoveTo(A, new MapPosition(2, 8));
        session.AutoResolve();

        var ended = Assert.Single(events.OfType<BattleEnded>());
        Assert.Equal(BattleEndReason.Retreated, ended.Reason);
        Assert.Contains(new HeroDefeated(B, Red, Retreated: true), events);
        Assert.Single(state.RetiredHeroesOf(Red));
        Assert.DoesNotContain(events, e => e is HeroSurrendered); // the AI does not surrender
    }
}
