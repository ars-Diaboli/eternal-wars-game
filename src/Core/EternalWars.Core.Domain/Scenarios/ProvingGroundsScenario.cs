using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Scenarios;

/// <summary>
/// The first shipped arena map (ADR-180): <b>Proving Grounds</b>, 34 × 18, two players. Each picks a faction: a random town and a random hero
/// of it, on two halves that mirror each other left to right, behind a rock wall. The combat place is in a sealed glade in the wall.
/// </summary>
public static class ProvingGroundsScenario
{
    public const string Name = "Proving Grounds";

    /// <summary>The west half; the east half is its mirror image.</summary>
    private static readonly string[] West =
    [
        "ggggggggggggggg",
        "ggggggggggggrgg",
        "gggggggggggrrgg",
        "ggggggggggggggg",
        "ggggggggggggggg",
        "ggggggggggggggg",
        "ggggggggggggggg",
        "ggggggggggggggg",
        "ggggggggggggggg",
        "ggggggdddgggggg",
        "gggggddddgggggg",
        "ggggggdddgggg~~",
        "gggggggggggg~~~",
        "ggrrgggggggg~~g",
        "grrrggggggggggg",
        "ggrggggggggggg#",
        "gggggggggggg###",
        "ggggggggggg####",
    ];

    /// <summary>The wall between the halves, with the glade of the combat place (row 8).</summary>
    private static readonly string[] Wall = [.. Enumerable.Range(0, 18).Select(y => y == 8 ? "#gg#" : "####")];

    public static GameState Create(ContentCatalogs? content = null)
    {
        var sketch = new MapSketch(
            content ?? ContentCatalogs.Default,
            Name,
            "Arena. Two realms of your choosing face each other across a wall of rock. Spend the first day building, hiring and gathering; on the second the champions meet in the sealed glade between them.",
            MapSketch.MirroredX(West, Wall));
        var players = new[] { new Player(new PlayerId(0), "Blue", isHuman: true), new Player(new PlayerId(1), "Red", isHuman: false) };
        var heroes = new List<Hero>();

        sketch.Place(BuiltInMapObjects.CombatPlace, 16, 8);
        Side(players[0].Id, "Westmarch", 1);
        sketch.Mirror = MirrorAxis.X;
        Side(players[1].Id, "Eastmarch", 2);
        return sketch.BuildArena(players, heroes);

        void Side(PlayerId owner, string town, int hero)
        {
            sketch.AddTown(null, 1, 1, owner, town);
            heroes.Add(sketch.AddRandomHero(hero, owner, 4, 8, Army.Of(new(BuiltInCreatures.Wolf, 24), new("neutral.bandit", 6))));
            sketch.Pile(ResourceType.Gold, 9, 1, 2500);
            sketch.Pile(ResourceType.Gold, 13, 9, 2500);
            sketch.Place(BuiltInMapObjects.TreasureChest, 11, 3);
            sketch.Box(13, 1, new BoxContents { Experience = 2000 });
            sketch.Box(1, 15, new BoxContents { Creatures = [new CreatureStack("neutral.troll", 4)] }, new CreatureStack(BuiltInCreatures.Wolf, 20));
            sketch.WitchHut(9, 5, BuiltInSkills.Leadership, BuiltInSkills.Armor);
            sketch.Place(BuiltInMapObjects.ShrineId(1), 12, 5);
            sketch.Place(BuiltInMapObjects.Arena, 9, 12);
            sketch.Place(BuiltInMapObjects.Temple, 5, 12);
            sketch.Place(BuiltInMapObjects.ArtifactId(ArtifactTier.Minor), 3, 10);
            sketch.Monster(BuiltInCreatures.Wolf, 16, 8, 15);
            sketch.Place(BuiltInMapObjects.ArtifactId(ArtifactTier.Major), 7, 16);
        }
    }
}
