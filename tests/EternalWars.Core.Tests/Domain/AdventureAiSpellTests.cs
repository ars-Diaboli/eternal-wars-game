using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The adventure spells the computer casts (ADR-184).</summary>
public class AdventureAiSpellTests
{
    private static AdventureAi Ai(GameSession session) => new(session, controlHumanSlots: true);

    private static Hero Mage(GameState state, params string[] spells)
    {
        var hero = state.GetHero(A);
        hero.HasSpellBook = true;
        hero.Knowledge = 10;
        foreach (var spell in spells)
        {
            hero.Learn(spell);
        }

        Steady(state);
        return hero;
    }

    private static IEnumerable<string> Casts(GameSession session) =>
        session.CommandLog.OfType<CastAdventureSpellCommand>().Select(c => c.Spell);

    [Fact]
    public void DimensionDoor_JumpsAWallTheWalkAroundWouldTakeDaysFor()
    {
        // A pile behind a rock wall: on foot the long way round, through the door 6 tiles east.
        var state = Create(
            "A....#........................",
            ".....#........................",
            ".....#........................",
            ".....#........................",
            ".....#........................",
            ".....#........................",
            ".....#........................",
            ".....#........................",
            ".....#........................",
            ".....#........................",
            "............................B.");
        var pile = Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 8, 0);
        var hero = Mage(state, AdventureMagic.DimensionDoor);
        var session = Start(state);
        hero.Mana = 100;

        var ai = Ai(session);
        ai.PlayTurn();

        Assert.Contains(AdventureMagic.DimensionDoor, Casts(session));
        Assert.DoesNotContain(state.Objects, o => o == pile);
        Assert.Equal(0, ai.Refused);
    }

    [Fact]
    public void DimensionDoor_IsNotCast_WhenTheWalkIsShort()
    {
        var state = Create("A.......B");
        Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 4, 0);
        var hero = Mage(state, AdventureMagic.DimensionDoor);
        var session = Start(state);
        hero.Mana = 100;

        Ai(session).PlayTurn();

        Assert.Empty(Casts(session));
    }

    [Fact]
    public void ViewTerrain_IsCast_OverUnexploredGround()
    {
        var state = Create(new[] { "A............................." }.Concat(Enumerable.Repeat("..............................", 29)).Append(".............................B").ToArray());
        var hero = Mage(state, AdventureMagic.ViewTerrain);
        var session = Start(state, revealAll: false);
        hero.Mana = 100;

        var ai = Ai(session);
        ai.PlayTurn();

        Assert.Equal([AdventureMagic.ViewTerrain], Casts(session));
        Assert.Equal(0, ai.Refused);
    }

    [Fact]
    public void PathfindersCall_IsCast_WhenTodaysRouteCrossesSwamp_AndNotOnOpenGround()
    {
        var swamp = Create("Asssssssssssssssssss.", "ssssssssssssssssssssB");
        Add<ResourcePile>(swamp, BuiltInMapObjects.PileId(ResourceType.Gold), 20, 0);
        var hero = Mage(swamp, AdventureMagic.PathfindersCall);
        var session = Start(swamp);
        hero.Mana = 100;
        hero.MovementPoints = 3000;

        Ai(session).PlayTurn();

        Assert.Equal([AdventureMagic.PathfindersCall], Casts(session));

        var open = Create("A....................", "....................B");
        Add<ResourcePile>(open, BuiltInMapObjects.PileId(ResourceType.Gold), 20, 0);
        var walker = Mage(open, AdventureMagic.PathfindersCall);
        var openSession = Start(open);
        walker.Mana = 100;

        Ai(openSession).PlayTurn();

        Assert.Empty(Casts(openSession));
    }

    [Fact]
    public void ConvenienceSpells_KeepTheManaForTheDearestBattleSpell()
    {
        var state = Create(new[] { "A............................." }.Concat(Enumerable.Repeat("..............................", 29)).Append(".............................B").ToArray());
        var hero = Mage(state, AdventureMagic.ViewTerrain, "light.bless");
        var session = Start(state, revealAll: false);
        hero.Mana = AdventureMagic.Cost(state, hero, AdventureMagic.ViewTerrain) + ManaRules.SpellCost(hero, state.Spells.Get("light.bless").Mana) - 1;

        Ai(session).PlayTurn();

        Assert.Empty(Casts(session));
    }

    [Fact]
    public void GraveWhisper_IsCast_ByANecromancer()
    {
        var state = Create("A.......B");
        var hero = Mage(state, AdventureMagic.GraveWhisper);
        hero.Skills.Set(NecromancyRules.Skill, SkillLevel.Basic);
        var session = Start(state);
        hero.Mana = 100;

        Ai(session).PlayTurn();

        Assert.Equal([AdventureMagic.GraveWhisper], Casts(session));
        Assert.True(AdventureMagic.NecromancyPercent(hero) > 0);
    }

    [Fact]
    public void TownPortal_TakesTheHeroHome_ToATownItCouldNotWalkToToday()
    {
        // The garrison (40) alone cannot hold against the footmen next to it (a horde, taken for 74: ADR-125), the hero (50) and the garrison
        // together can; the hero is three days' walk away.
        var state = Create(
            "............................................................", "............................................................",
            "............................................................", "............................................................",
            "............................................................", "............................................................",
            "..........B................................................A", "............................................................");
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.SetLevel(BuiltInBuildings.Tavern, 0);
        town.LastBuilt = state.Date;
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Footman, 40));
        var hero = Mage(state, AdventureMagic.TownPortal);
        hero.Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 50));
        state.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 74));
        var session = Start(state);
        hero.Mana = 100;

        var ai = Ai(session);
        ai.PlayTurn();

        Assert.Contains(AdventureMagic.TownPortal, Casts(session));
        Assert.Equal(town.VisitPosition, hero.Position);
        Assert.Equal(0, ai.Refused);
    }
}
