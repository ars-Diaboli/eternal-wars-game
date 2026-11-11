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
/// The second shipped arena map (ADR-180): <b>Frostfang Pass</b>, 24 × 30, two players and no towns. A Haven mage (Blue, the snowy north)
/// and a Necropolis mage (Red, the wasted south) buy their armies at map dwellings with the gold lying about, and gather spells, skills
/// and Pandora's Boxes. The halves mirror each other top to bottom; the combat place (attacker from the north) is sealed in the ridge.
/// </summary>
public static class FrostfangPassScenario
{
    public const string Name = "Frostfang Pass";

    /// <summary>The north half; the south half is it upside down, in wasteland.</summary>
    private static readonly string[] North =
    [
        "ssssssssssssssssssssssss",
        "ssssssssssssssssssssssss",
        "ssssssssssssssssssssssss",
        "#ssssssssssssssssssssss#",
        "ssssssssssssssssssssssss",
        "ssssss#ssssssssss#ssssss",
        "##ssssssssssssssssssss##",
        "ssssssssssssssssssssssss",
        "ssssssssssssssssssssssss",
        "ssssssssssssssssssssssss",
        "ssssssss#sssssss#sssssss",
        "ssssssssssssssssssssssss",
        "ssssssssssssssssssssssss",
    ];

    /// <summary>The ridge between the halves, with the combat place's two cells (column 11).</summary>
    private static readonly string[] Ridge = [.. Enumerable.Range(0, 4).Select(y => y is 1 or 2 ? "###########s############" : new string('#', 24))];

    /// <summary>One side's troops: its starting army, the map dwellings (tiers 2, 3, 2 and 4) and the creatures in its Pandora's Box.</summary>
    private sealed record Faction(string Hero, Army Army, string[] Dwellings, CreatureStack BoxStack);

    public static GameState Create(ContentCatalogs? content = null)
    {
        var sketch = new MapSketch(
            content ?? ContentCatalogs.Default,
            Name,
            "Arena. A Haven mage holds the frozen north, a Necropolis mage the wasted south; neither has a town. Buy an army at the dwellings with the gold lying about, learn what the shrines and huts teach, and meet in the pass on the second day.",
            MapSketch.MirroredY(North, Ridge, letter => letter == 's' ? 'w' : letter));
        var players = new[] { new Player(new PlayerId(0), "Blue", isHuman: true), new Player(new PlayerId(1), "Red", isHuman: false) };
        var heroes = new List<Hero>();

        sketch.Place(BuiltInMapObjects.VerticalCombatPlace, 11, 14);
        Side(players[0].Id, 1, new Faction(
            "haven.ansel",
            Army.Of(new(BuiltInCreatures.Footman, 30), new(BuiltInCreatures.Archer, 10)),
            ["haven.archer", "haven.griffin", "haven.zealot", "haven.lancer"],
            new CreatureStack("haven.monk", 5)));
        sketch.Mirror = MirrorAxis.Y;
        Side(players[1].Id, 2, new Faction(
            "necropolis.kael",
            Army.Of(new("necropolis.skeleton", 33), new("necropolis.wight", 10)),
            ["necropolis.wight", "necropolis.vampire", "necropolis.ghoul", "necropolis.lich"],
            new CreatureStack("necropolis.death-weaver", 5)));
        return sketch.BuildArena(players, heroes);

        void Side(PlayerId owner, int hero, Faction faction)
        {
            heroes.Add(sketch.AddHero(hero, owner, 11, 3, faction.Hero, faction.Army));
            sketch.Dwelling(faction.Dwellings[0], 1, 1);
            sketch.Dwelling(faction.Dwellings[1], 5, 1);
            sketch.Dwelling(faction.Dwellings[2], 16, 1);
            sketch.Dwelling(faction.Dwellings[3], 20, 1);
            sketch.Pile(ResourceType.Gold, 9, 5, 3000);
            sketch.Pile(ResourceType.Gold, 14, 5, 3000);
            sketch.Pile(ResourceType.Gold, 11, 10, 4000);
            sketch.Place(BuiltInMapObjects.WarMachineFactory, 10, 6);
            sketch.Place(BuiltInMapObjects.ShrineId(2), 3, 4);
            sketch.Place(BuiltInMapObjects.ShrineId(1), 19, 4);
            sketch.Box(1, 8, new BoxContents { Attack = 2, Defense = 1 }, new CreatureStack(BuiltInCreatures.Wolf, 15));
            sketch.Box(22, 8, new BoxContents { Creatures = [faction.BoxStack] }, new CreatureStack("neutral.bandit", 12));
            sketch.Box(7, 11, new BoxContents { Experience = 2500 });
            sketch.WitchHut(14, 9, BuiltInSkills.Tactics, BuiltInSkills.Armor);
            sketch.Place(BuiltInMapObjects.StarAxis, 5, 8);
            sketch.Place(BuiltInMapObjects.MarlettoTower, 17, 8);
            sketch.Place(BuiltInMapObjects.LearningStone, 19, 11);
            sketch.Place(BuiltInMapObjects.ArtifactId(ArtifactTier.Minor), 13, 3);
            sketch.Monster("neutral.troll", 7, 2, 11);
            sketch.Place(BuiltInMapObjects.ArtifactId(ArtifactTier.Major), 1, 12);
        }
    }
}
