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
/// The third shipped arena map (ADR-180): <b>Four Banners</b>, 40 × 24, two teams of two (Blue and Green in the west, Red and Yellow in the
/// east), each player with a random town and hero of its faction. Allies share their half and may trade troops; each side's strongest hero
/// fights. Each half has its own combat place: the first player in turn order with a hero standing on one defends there.
/// </summary>
public static class FourBannersScenario
{
    public const string Name = "Four Banners";

    /// <summary>The west half; the east half is its mirror image.</summary>
    private static readonly string[] West =
    [
        "gggggggggggggggggg",
        "gggggggggggggggggg",
        "gggggggggggggggggg",
        "gggggggggggggggggg",
        "ggggggggggggggggrr",
        "ggggggggggggggggrr",
        "gggggggggggggggggg",
        "gggggggggggggggggg",
        "gggggggggggggggggg",
        "gggggggggdddgggggg",
        "ggggggggdddddddggg",
        "gggggggddddddddggg",
        "ggggggggdddddddggg",
        "gggggggggdddgggggg",
        "gggggggggggggggggg",
        "gggggggggggggggggg",
        "gggggggggggggggggg",
        "gggggggggggggggggg",
        "gggggggggggggggggg",
        "ggggggggggggggggrr",
        "ggggggggggggggggrr",
        "gggggggggggggggggg",
        "gggggggggggggggggg",
        "gggggggggggggggggg",
    ];

    public static GameState Create(ContentCatalogs? content = null)
    {
        var sketch = new MapSketch(
            content ?? ContentCatalogs.Default,
            Name,
            "Arena, two against two. Blue and Green hold the west, Red and Yellow the east, each with a realm of its choosing. Allies share their land and may hand each other troops; on the second day each side's strongest hero fights. Stand on your side's combat place to defend on your own ground.",
            MapSketch.MirroredX(West, [.. Enumerable.Repeat("####", West.Length)]));
        var west = new TeamId(0);
        var east = new TeamId(1);
        var players = new[]
        {
            new Player(new PlayerId(0), "Blue", isHuman: true, west),
            new Player(new PlayerId(1), "Red", isHuman: false, east),
            new Player(new PlayerId(2), "Green", isHuman: false, west),
            new Player(new PlayerId(3), "Yellow", isHuman: false, east),
        };
        var heroes = new List<Hero>();

        Half(players[0].Id, players[2].Id, "Northwatch", "Southwatch", 1);
        sketch.Mirror = MirrorAxis.X;
        Half(players[1].Id, players[3].Id, "Dawnspire", "Duskspire", 2);
        return sketch.BuildArena(players, heroes);

        static Army StartingArmy() => Army.Of(new(BuiltInCreatures.Wolf, 20), new("neutral.bandit", 5));

        void Half(PlayerId north, PlayerId south, string northTown, string southTown, int firstHero)
        {
            sketch.AddTown(null, 1, 1, north, northTown);
            heroes.Add(sketch.AddRandomHero(firstHero, north, 4, 8, StartingArmy()));
            sketch.AddTown(null, 1, 15, south, southTown);
            heroes.Add(sketch.AddRandomHero(firstHero + 2, south, 4, 22, StartingArmy()));
            sketch.Place(BuiltInMapObjects.CombatPlace, 12, 11);

            sketch.Pile(ResourceType.Gold, 10, 2, 2500);
            sketch.Pile(ResourceType.Gold, 10, 21, 2500);
            sketch.Pile(ResourceType.Lumber, 16, 7, 10);
            sketch.Pile(ResourceType.Ore, 16, 16, 10);
            sketch.Place(BuiltInMapObjects.TreasureChest, 14, 2);
            sketch.Place(BuiltInMapObjects.TreasureChest, 14, 21);
            sketch.Box(9, 7, new BoxContents { Attack = 1, Defense = 1, SpellPower = 1, Knowledge = 1 }, new CreatureStack(BuiltInCreatures.Wolf, 25));
            sketch.Box(9, 16, new BoxContents { Creatures = [new CreatureStack("neutral.troll", 6)] }, new CreatureStack("neutral.bandit", 15));
            sketch.WitchHut(12, 4, BuiltInSkills.Leadership, BuiltInSkills.Logistics);
            sketch.WitchHut(12, 18, BuiltInSkills.Archery, BuiltInSkills.Armor);
            sketch.Place(BuiltInMapObjects.ShrineId(2), 15, 9);
            sketch.Place(BuiltInMapObjects.ShrineId(1), 15, 13);
            sketch.Place(BuiltInMapObjects.Arena, 6, 10);
            sketch.Place(BuiltInMapObjects.Temple, 2, 10);
            sketch.Place(BuiltInMapObjects.ArtifactId(ArtifactTier.Minor), 8, 4);
            sketch.Monster("neutral.troll", 8, 16, 23);
            sketch.Place(BuiltInMapObjects.ArtifactId(ArtifactTier.Major), 17, 23);
        }
    }
}
