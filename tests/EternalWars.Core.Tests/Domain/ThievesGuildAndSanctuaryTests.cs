using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The Thieves' Guild, the Den of Thieves (TOWN-11) and the sanctuary (MAP-28; ADR-079).</summary>
public class ThievesGuildAndSanctuaryTests
{
    [Fact]
    public void TheRows_GrowWithTheTaverns_LikeHomm3()
    {
        Assert.Empty(ThievesGuild.RowsFor(0));
        Assert.Equal([ThievesGuildRow.Towns, ThievesGuildRow.Heroes], ThievesGuild.RowsFor(1));
        Assert.Equal(5, ThievesGuild.RowsFor(2).Count);
        Assert.Equal([.. ThievesGuild.RowsFor(2), ThievesGuildRow.Artifacts, ThievesGuildRow.Income], ThievesGuild.RowsFor(3));
        Assert.Equal(Enum.GetValues<ThievesGuildRow>(), ThievesGuild.RowsFor(4));
        Assert.Equal(Enum.GetValues<ThievesGuildRow>(), ThievesGuild.RowsFor(7));
    }

    [Fact]
    public void EveryPlayerIsRanked_EqualValuesShareAPlace_AndTheBestHeroAndCreatureAreNamed()
    {
        var state = Create("....................", "....................", "....................", "....................",
            "....................", "....................", "....................", "....................", "A..C..............B.");
        var first = AddTown(state, TownIds.Haven, 0, 0, Blue);
        AddTown(state, TownIds.Haven, 8, 0, Blue);
        first.SetLevel(BuiltInBuildings.Tavern, 0);
        state.GetHero(B).Level = 7;
        state.GetHero(B).Army.Set(1, new CreatureStack(BuiltInCreatures.Champion, 1));
        var session = Start(state);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 500); // after the first day's income
        state.GetPlayer(Red).Resources = new ResourceSet(Gold: 500);

        Assert.Equal(1, ThievesGuild.Taverns(state, Blue));
        Assert.Equal(0, ThievesGuild.Taverns(state, Red));

        var towns = ThievesGuild.Rank(state, ThievesGuildRow.Towns);
        Assert.Equal([(Blue, 1, 2L), (Red, 2, 0L)], towns.Select(e => (e.Player, e.Place, e.Value)));
        var gold = ThievesGuild.Rank(session.State, ThievesGuildRow.Gold);
        Assert.All(gold, e => Assert.Equal(1, e.Place));

        var best = ThievesGuild.Rank(state, ThievesGuildRow.BestHero);
        Assert.Equal((Red, 1, "Bravo (level 7)"), (best[0].Player, best[0].Place, best[0].Detail));
        var creature = ThievesGuild.Rank(state, ThievesGuildRow.BestCreature);
        Assert.Equal((Red, state.Creatures.Get(BuiltInCreatures.Champion).Name), (creature[0].Player, creature[0].Detail));

        Assert.Equal(2, ThievesGuild.Table(state, 1).Count);
        Assert.Equal(ThievesGuild.RowsFor(ThievesGuild.FullTable).Count, ThievesGuild.Table(state).Count);
    }

    [Fact]
    public void AVisitToTheDenOfThieves_OpensTheWholeTable_AndBothObjectsAreInTheMapFormat()
    {
        var state = Create(".........", ".........", "...A.....", ".........", "C.......B");
        var den = Add<DenOfThieves>(state, BuiltInMapObjects.DenOfThieves, 2, 0);
        Add<Sanctuary>(state, BuiltInMapObjects.Sanctuary, 5, 3);
        var session = Start(state);
        var visits = new List<ThievesDenVisited>();
        session.Events.Subscribe<ThievesDenVisited>(visits.Add);

        Assert.Equal(MoveResult.Moved, session.Step(A, Direction.North));
        Assert.Equal([new ThievesDenVisited(A, den.Id)], visits);

        var json = MapSerializer.Serialize(MapConverter.ToDocument(state));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));
        Assert.Contains("\"kind\": \"denOfThieves\"", json, StringComparison.Ordinal);
        Assert.Single(restored.ObjectsOf<DenOfThieves>());
        Assert.Single(restored.ObjectsOf<Sanctuary>());
    }

    // The sanctuary's 3×2 footprint at (2, 0) puts its entrance at (3, 1); hero A starts below it.
    private static (GameSession Session, Sanctuary Sanctuary) SanctuaryWorld()
    {
        var state = Create(".........", ".........", "...A.....", ".........", "C.......B");
        var sanctuary = Add<Sanctuary>(state, BuiltInMapObjects.Sanctuary, 2, 0);
        return (Start(state), sanctuary);
    }

    [Fact]
    public void AHeroInTheSanctuary_CannotBeAttacked()
    {
        var (session, sanctuary) = SanctuaryWorld();
        var state = session.State;
        state.GetHero(B).Position = sanctuary.VisitPosition;
        var refused = new List<MapObjectRefused>();
        session.Events.Subscribe<MapObjectRefused>(refused.Add);

        Assert.True(Sanctuary.Protects(state, sanctuary.VisitPosition));
        Assert.Equal(MoveResult.Occupied, session.Step(A, Direction.North));

        Assert.Null(state.Battle);
        Assert.Equal(new MapPosition(3, 2), state.GetHero(A).Position);
        Assert.Contains("safe in the sanctuary", Assert.Single(refused).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AHero_EntersAnEmptySanctuary_AndStaysAsLongAsItLikes()
    {
        var (session, sanctuary) = SanctuaryWorld();

        Assert.Equal(MoveResult.Moved, session.Step(A, Direction.North));
        Assert.Equal(sanctuary.VisitPosition, session.State.GetHero(A).Position);
        session.EndTurn();
        session.EndTurn();
        Assert.Equal(sanctuary.VisitPosition, session.State.GetHero(A).Position);
    }

    [Fact]
    public void TheAi_DoesNotAttackAHeroInTheSanctuary()
    {
        var (session, sanctuary) = SanctuaryWorld();
        var state = session.State;
        state.GetHero(B).Position = sanctuary.VisitPosition;
        state.GetHero(A).Army.Set(1, new CreatureStack(BuiltInCreatures.Champion, 20));

        new AdventureAi(session, controlHumanSlots: true).PlayTurn();

        Assert.Null(state.Battle);
        Assert.Contains(state.Heroes, h => h.Id == B);
    }
}
