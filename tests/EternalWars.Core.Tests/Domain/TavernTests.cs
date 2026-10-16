using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The tavern (ADR-060): who it offers, upgrading it, hiring, and the AI hiring.</summary>
public class TavernTests
{
    private static GameState World(string townType, int tavernLevel = 1, int gold = 10_000)
    {
        var state = Create("..........", "..........", "..........", "..........", "..........", "..........", "..........", "..........", "B........A");
        var town = AddTown(state, townType, 0, 0, Blue);
        town.SetLevel(BuiltInBuildings.Tavern, tavernLevel);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: gold);
        return state;
    }

    private static Town TownOf(GameState state) => state.ObjectsOf<Town>().Single();

    private static IReadOnlyList<HeroDefinition> Offers(GameState state) => TavernRules.Offers(state, Blue, TownOf(state));

    private static string[] Towns(IEnumerable<HeroDefinition> heroes) => heroes.Select(h => h.Town).ToArray();

    [Fact]
    public void TheRing_AdjacentAreTheNeighbours_AndOpposingTheTownAcross()
    {
        Assert.Equal([TownIds.Inferno, TownIds.Necropolis], TavernRules.Adjacent(TownIds.Haven).Order());
        Assert.Equal(TownIds.Dungeon, TavernRules.Opposing(TownIds.Haven));
        Assert.Equal(TownIds.Academy, TavernRules.Opposing(TownIds.Necropolis));
        Assert.Equal(TownIds.Inferno, TavernRules.Opposing(TownIds.Sylvan));
        Assert.Empty(TavernRules.Adjacent(TownIds.Stronghold));
        Assert.Null(TavernRules.Opposing(TownIds.SwampBestiary));
        foreach (var town in TavernRules.Ring)
        {
            Assert.Equal(BuiltInSkills.Rivals[town].Order(), TavernRules.Adjacent(town).Order()); // the lore rivals are the ring neighbours
        }
    }

    [Fact]
    public void ABasicTavern_OffersOneNativeAndOneAdjacentHero()
    {
        var offers = Offers(World(TownIds.Haven));

        Assert.Equal(2, offers.Count);
        Assert.Equal(TownIds.Haven, offers[0].Town);
        Assert.Contains(offers[1].Town, new[] { TownIds.Necropolis, TownIds.Inferno });
    }

    [Fact]
    public void AnUpgradedTavern_OffersTwoNatives_OneAdjacent_AndOneNotOpposing()
    {
        var offers = Offers(World(TownIds.Haven, tavernLevel: 2));

        Assert.Equal(4, offers.Count);
        Assert.Equal([TownIds.Haven, TownIds.Haven], Towns(offers.Take(2)));
        Assert.Contains(offers[2].Town, new[] { TownIds.Necropolis, TownIds.Inferno });
        Assert.DoesNotContain(offers[3].Town, new[] { TownIds.Haven, TownIds.Necropolis, TownIds.Inferno, TownIds.Dungeon }); // not opposing
        Assert.Equal(4, offers.Select(o => o.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(TownIds.Stronghold)]
    [InlineData(TownIds.SwampBestiary)]
    public void TheWildTowns_OfferNativesAndWarriorsOfOtherTowns(string town)
    {
        var basic = Offers(World(town));
        Assert.Equal(2, basic.Count);
        Assert.Equal(town, basic[0].Town);
        Assert.NotEqual(town, basic[1].Town);
        Assert.Equal(HeroClass.Warrior, basic[1].Class);

        var upgraded = Offers(World(town, tavernLevel: 2));
        Assert.Equal(4, upgraded.Count);
        Assert.All(upgraded.Take(2), h => Assert.Equal(town, h.Town));
        Assert.All(upgraded.Skip(2), h => Assert.True(h.Town != town && h.Class == HeroClass.Warrior));
    }

    [Fact]
    public void WhenARankRunsOut_TheSlotFallsThroughTheOrder()
    {
        var state = World(TownIds.Haven, tavernLevel: 2);
        var id = 100;
        foreach (var native in state.HeroRoster.All.Where(h => h.Town == TownIds.Haven))
        {
            state.AddHero(Hero.Create(new HeroId(id++), Red, new MapPosition(id % 10, 7), null, native));
        }

        var offers = Offers(state);

        Assert.Equal(4, offers.Count);
        Assert.DoesNotContain(offers, h => h.Town == TownIds.Haven); // all natives are on the map
        Assert.Contains(offers.Take(2), h => TavernRules.RankOf(TownIds.Haven, h) == 1); // the native slots took adjacent heroes
    }

    [Fact]
    public void Offers_AreTheSameWithinAWeek_AndChangeWithTheWeekAndThePlayer()
    {
        var state = World(TownIds.Haven, tavernLevel: 2);
        var first = Offers(state).Select(h => h.Id).ToList();
        Assert.Equal(first, Offers(state).Select(h => h.Id));

        var weeks = new HashSet<string> { string.Join(",", first) };
        for (var week = 1; week < 6; week++)
        {
            state.Date = new GameDate((week * 7) + 1);
            weeks.Add(string.Join(",", Offers(state).Select(h => h.Id)));
        }

        Assert.True(weeks.Count > 1);
        Assert.NotEqual(
            string.Join(",", TavernRules.PoolOrder(state, Blue).Select(h => h.Id)),
            string.Join(",", TavernRules.PoolOrder(state, Red).Select(h => h.Id)));
    }

    [Fact]
    public void NoTavern_NoOffers()
    {
        var state = World(TownIds.Haven, tavernLevel: 0);

        Assert.Empty(Offers(state));
        Assert.Contains("no tavern", TavernRules.CheckHire(state, TownOf(state), "haven.aldric")!, StringComparison.Ordinal);
    }

    [Fact]
    public void HiringAHero_PutsItAtTheEntranceWithItsRosterStats_AndTakesTheGold()
    {
        var state = World(TownIds.Haven);
        var session = Start(state);
        var hired = new List<HeroHired>();
        session.Events.Subscribe<HeroHired>(hired.Add);
        var town = TownOf(state);
        var offer = Offers(state)[0];
        var gold = state.GetPlayer(Blue).Resources.Gold;

        var result = session.Execute(new HireHeroCommand(Blue, town.Id, offer.Id));

        Assert.True(result.IsAccepted, result.Error);
        var hero = state.Heroes.Single(h => h.Definition == offer.Id);
        Assert.Equal(town.VisitPosition, hero.Position);
        Assert.Equal(Blue, hero.Owner);
        Assert.Equal(offer.Name, hero.Name);
        Assert.Equal(offer.Attack, hero.Attack);
        Assert.True(hero.Army.IsEmpty);
        Assert.True(hero.MovementPoints > 0);
        Assert.Equal(gold - 2500, state.GetPlayer(Blue).Resources.Gold);
        Assert.Equal([new HeroHired(town.Id, hero.Id, offer.Id)], hired);
        Assert.DoesNotContain(Offers(state), o => o.Id == offer.Id); // it is on the map now
    }

    [Fact]
    public void HiringIsRefused_WhenItCannotWork()
    {
        var state = World(TownIds.Haven, gold: 1000); // the first day's income comes on top
        var session = Start(state);
        var town = TownOf(state);
        var offer = Offers(state)[0];

        Assert.Contains("gold", session.Execute(new HireHeroCommand(Blue, town.Id, offer.Id)).Error!, StringComparison.Ordinal);

        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 10_000);
        Assert.Contains("not offered", session.Execute(new HireHeroCommand(Blue, town.Id, "sylvan.thalion")).Error!, StringComparison.Ordinal);

        var standing = Hero.Create(new HeroId(50), Blue, town.VisitPosition, null, null, "Guard");
        state.AddHero(standing);
        Assert.Contains("entrance", session.Execute(new HireHeroCommand(Blue, town.Id, offer.Id)).Error!, StringComparison.Ordinal);
        state.RemoveHero(standing);

        Assert.False(session.Execute(new HireHeroCommand(Red, town.Id, offer.Id)).IsAccepted); // not Red's town or turn
    }

    [Fact]
    public void APlayerHasAtMostEightHeroes()
    {
        var state = World(TownIds.Haven);
        for (var i = 0; i < 7; i++)
        {
            state.AddHero(Hero.Create(new HeroId(60 + i), Blue, new MapPosition(i, 7), null, null, $"Extra {i}"));
        }

        var session = Start(state); // A plus seven others = 8
        var offer = Offers(state)[0];

        Assert.Contains("at most 8", session.Execute(new HireHeroCommand(Blue, TownOf(state).Id, offer.Id)).Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTavern_IsUpgradedLikeAnyBuilding_WhenTheFortStands()
    {
        var state = World(TownIds.Haven, gold: 20_000);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 20_000, Lumber: 10, Ore: 10);
        var session = Start(state);
        var town = TownOf(state);
        var gold = state.GetPlayer(Blue).Resources.Gold;

        Assert.True(session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.Tavern)).IsAccepted);

        Assert.Equal(2, town.Level(BuiltInBuildings.Tavern));
        Assert.Equal(4, Offers(state).Count);
        Assert.Equal(gold - 2000, state.GetPlayer(Blue).Resources.Gold);

        var blocked = World(TownIds.Haven, tavernLevel: 1);
        TownOf(blocked).SetLevel(BuiltInBuildings.Fort, 0);
        Assert.Contains("Fort", TownRules.CheckBuild(blocked, TownOf(blocked), BuiltInBuildings.Tavern)!, StringComparison.Ordinal);
    }

    [Fact]
    public void AHiredHero_GetsAnIdNoOneUsedBefore()
    {
        var state = World(TownIds.Haven);
        var session = Start(state);
        var town = TownOf(state);
        var first = Offers(state)[0];
        session.Execute(new HireHeroCommand(Blue, town.Id, first.Id)).ThrowIfRejected();
        var hired = state.Heroes.Single(h => h.Definition == first.Id);
        state.RemoveHero(hired);
        state.AddHero(Hero.Create(new HeroId(hired.Id.Value + 1), Blue, new MapPosition(5, 5), null, null, "Later"));
        state.RemoveHero(state.GetHero(new HeroId(hired.Id.Value + 1)));

        Assert.Equal(hired.Id.Value + 2, state.NextUnusedHeroId().Value);
    }

    [Fact]
    public void AHiredHero_ReplaysFromTheCommandLog()
    {
        var state = World(TownIds.Haven);
        var document = MapConverter.ToDocument(state);
        var session = Start(state, revealAll: false);
        var command = new HireHeroCommand(Blue, TownOf(state).Id, Offers(state)[0].Id);
        session.Execute(command).ThrowIfRejected();

        var json = GameRecordSerializer.Serialize(GameReplay.Record(document, session));

        Assert.Equal(command, Assert.IsType<HireHeroCommand>(Assert.Single(GameRecordSerializer.Deserialize(json).Commands)));
    }

    [Fact]
    public void TheAi_HiresAHero_WhenItHasNone()
    {
        var state = Create("..........", "..........", "..........", "..........", "..........", "..........", "..........", "B.........");
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.SetLevel(BuiltInBuildings.Tavern, 1);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 5000);
        var session = Start(state);
        var ai = new AdventureAi(session, controlHumanSlots: true);

        Assert.True(ai.PlayTurn());

        Assert.Contains(session.CommandLog, c => c is HireHeroCommand);
        Assert.Single(state.HeroesOf(Blue));
        Assert.Equal(0, ai.Refused);
    }
}
