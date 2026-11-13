using System.Reflection;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// Every map editor operation undoes cleanly: on a rich map built through the editor, the map file and the state digest after Undo are exactly
/// those before the operation, and after Redo exactly those right after it. A completeness check fails when an editor operation has no case here.
/// </summary>
public class EditorUndoTests
{
    private static readonly PlayerId Blue = new(0);
    private static readonly PlayerId Red = new(1);

    /// <summary>The map, its objects and their ids, built through the editor with an empty history.</summary>
    public sealed class Fixture
    {
        public Fixture()
        {
            Editor = new MapEditorSession(BlankScenario.Create(40, 30));
            var e = Editor;
            Hero = new HeroId(1);
            RedHero = new HeroId(2);

            Town = Put(BuiltInMapObjects.TownId(TownIds.Haven));
            Ok(e.Towns.SetOwner(Town, Blue));
            Ok(e.Towns.SetName(Town, "Brightwater"));
            Ok(e.Towns.SetDwelling(Town, BuiltInCreatures.Pikeman, 1));
            Ok(e.Towns.SetGarrisonSlot(Town, 0, new CreatureStack(BuiltInCreatures.Pikeman, 5)));
            Ok(e.Towns.SetGuildSpells(Town, [HavenSpells(1)[0]]));

            Ok(e.Heroes.SetArmySlot(Hero, 0, new CreatureStack(BuiltInCreatures.Pikeman, 10)));
            Ok(e.Heroes.SetArtifacts(Hero, [], ["iron-band"]));

            Monster = Ok(e.Monsters.Place(new MapPosition(20, 14), BuiltInCreatures.Wolf, 10, GuardDisposition.Aggressive, false, out var monster), monster);
            Chest = Put(BuiltInMapObjects.TreasureChest);
            Pickup = Put(BuiltInMapObjects.ArtifactId(ArtifactTier.Minor));
            Sign = Put(BuiltInMapObjects.Sign);
            Ok(e.Contents.SetSignMessage(Sign, "Welcome."));
            Box = Put(BuiltInMapObjects.PandorasBox);
            Ok(e.Contents.SetPandorasBox(Box, new BoxContents { Experience = 100 }, [], null));
            SeerHut = Put(BuiltInMapObjects.SeerHut);
            Sphinx = Put(BuiltInMapObjects.Sphinx);
            WitchHut = Put(BuiltInMapObjects.WitchHut);
            Shrine = Put(BuiltInMapObjects.ShrineId(2));
            Prison = Put(BuiltInMapObjects.Prison);
            GateA = Put(BuiltInMapObjects.TwoWayMonolith);
            GateB = Put(BuiltInMapObjects.TwoWayMonolith);
            GateC = Put(BuiltInMapObjects.TwoWayMonolith);
            Ok(e.Objects.LinkGates(GateA, GateB));
            Entrance = Put(BuiltInMapObjects.OneWayMonolithEntrance);
            Exit = Put(BuiltInMapObjects.OneWayMonolithExit);
            Ok(e.Objects.SetGateExit(Entrance, Exit));
            Mine = Put(BuiltInMapObjects.MineId(ResourceType.Gold));
            Provider = Put(BuiltInMapObjects.Windmill);
            Garrison = Put(BuiltInMapObjects.Garrison);
            Ok(e.Towns.SetGarrisonSlot(Garrison, 0, new CreatureStack(BuiltInCreatures.Wolf, 3)));
            Dwelling = Put(BuiltInMapObjects.Dwelling);

            Ok(e.Players.Add("Green", false, out _));

            Ok(e.MapEvents.Add(new MapEventDefinition("hero event", MapEventKind.Hero, [])));
            Ok(e.MapEvents.Add(new MapEventDefinition("quest", MapEventKind.Quest, []) { Stages = [QuestForms.Level(2, "Grow up.")] }));
            Ok(e.MapEvents.Add(new MapEventDefinition("player event", MapEventKind.Player, [])));
            Ok(e.MapEvents.SetCell(EventCell, "hero event"));
            Ok(e.MapEvents.SetVariable("v", 1));
            Ok(e.MapEvents.SetCampaignVariable("v", true, false));
            Ok(e.Contents.SetPandorasBoxEvent(Box, "hero event"));
            Ok(e.Contents.SetQuestSiteQuest(SeerHut, "quest"));

            e.Map.SetProperties("Fixture", "Built by the editor.");
            Assert.Equal(1, e.Terrain.PaintRoad(new MapPosition(30, 24), RoadType.Dirt));
            Assert.True(e.Heights.Raise(Hill, 1, 8) > 0);
            e.History.Clear();
        }

        public MapEditorSession Editor { get; }

        public HeroId Hero { get; }

        public HeroId RedHero { get; }

        public MapObjectId Town { get; }

        public MapObjectId Monster { get; }

        public MapObjectId Chest { get; }

        public MapObjectId Pickup { get; }

        public MapObjectId Sign { get; }

        public MapObjectId Box { get; }

        public MapObjectId SeerHut { get; }

        public MapObjectId Sphinx { get; }

        public MapObjectId WitchHut { get; }

        public MapObjectId Shrine { get; }

        public MapObjectId Prison { get; }

        public MapObjectId GateA { get; }

        public MapObjectId GateB { get; }

        public MapObjectId GateC { get; }

        public MapObjectId Entrance { get; }

        public MapObjectId Exit { get; }

        public MapObjectId Mine { get; }

        public MapObjectId Provider { get; }

        public MapObjectId Garrison { get; }

        public MapObjectId Dwelling { get; }

        public static MapPosition EventCell { get; } = new(22, 25);

        /// <summary>A raised bump of ground, for the tools that need one (smooth, heights from terrain).</summary>
        public static MapPosition Hill { get; } = new(35, 27);

        public string Spell(int level) => ShrineRules.Candidates(Editor.Scenario.Spells, level).First().Id;

        /// <summary>Places the object at the first free place, row by row from the top (the bottom rows stay free for the cases).</summary>
        private MapObjectId Put(string definitionId)
        {
            var definition = Editor.Catalog.Get(definitionId);
            var at = Editor.Scenario.Map.Positions(0).First(p => p.Y < 14 && Editor.Objects.CanPlace(definition, p) == EditResult.Ok);
            Ok(Editor.Objects.Place(definitionId, at, out var id));
            return id;
        }

        private static void Ok(EditResult result) => Assert.Equal(EditResult.Ok, result);

        private static T Ok<T>(EditResult result, T value)
        {
            Ok(result);
            return value;
        }
    }

    private static IReadOnlyList<string> HavenSpells(int level) => [.. SpellCatalog.Default.Of(TownIds.Haven, level).Select(s => s.Id)];

    private static bool Ok(EditResult result) => result == EditResult.Ok;

    /// <summary>Each case runs one operation on a fresh fixture and says whether it was accepted. The name before any space is "Type.Method".</summary>
    private static readonly Dictionary<string, Func<Fixture, bool>> Cases = new()
    {
        // Terrain
        ["TerrainEditor.Paint"] = f => f.Editor.Terrain.Paint(new MapPosition(30, 22), TerrainType.Snow, radius: 1) > 0,
        ["TerrainEditor.PaintRoad"] = f => f.Editor.Terrain.PaintRoad(new MapPosition(30, 24), RoadType.Cobblestone, radius: 1) > 0,
        ["TerrainEditor.PaintRoad (erase)"] = f => f.Editor.Terrain.PaintRoad(new MapPosition(30, 24), RoadType.None) > 0,
        ["TerrainEditor.PaintRiver"] = f => f.Editor.Terrain.PaintRiver(new MapPosition(26, 22), RiverType.Clear, radius: 1) > 0,
        ["MapEditorSession.BeginStroke (stroke of several paints)"] = f =>
        {
            f.Editor.BeginStroke("Paint a lake");
            var painted = f.Editor.Terrain.Paint(new MapPosition(30, 22), TerrainType.Water)
                + f.Editor.Terrain.Paint(new MapPosition(31, 22), TerrainType.Water)
                + f.Editor.Terrain.PaintRoad(new MapPosition(33, 22), RoadType.Dirt, radius: 1);
            f.Editor.EndStroke();
            return painted > 0;
        },
        ["TerrainEditor.Paint (Auto height off)"] = f =>
        {
            f.Editor.Heights.AutoHeight = false;
            return f.Editor.Terrain.Paint(new MapPosition(30, 22), TerrainType.Snow, radius: 1) > 0;
        },

        // Heights
        ["HeightEditor.Raise"] = f => f.Editor.Heights.Raise(new MapPosition(5, 27), 1, 3) > 0,
        ["HeightEditor.Lower"] = f => f.Editor.Heights.Lower(new MapPosition(5, 27), 2, 8) > 0,
        ["HeightEditor.Level"] = f => f.Editor.Heights.Level(Fixture.Hill, 2, 2, f.Editor.Heights.HeightAt(new MapPosition(5, 27))) > 0,
        ["HeightEditor.Smooth"] = f => f.Editor.Heights.Smooth(Fixture.Hill, 1, 4) > 0,
        ["HeightEditor.FromTerrain"] = f => f.Editor.Heights.FromTerrain(Fixture.Hill, 1) > 0,
        ["HeightEditor.FromTerrain (whole map)"] = f => f.Editor.Heights.FromTerrain() > 0,
        ["MapEditorSession.BeginStroke (stroke of height dabs)"] = f =>
        {
            f.Editor.BeginStroke("Raise ground");
            var changed = f.Editor.Heights.Raise(new MapPosition(5, 27), 0, 2) + f.Editor.Heights.Raise(new MapPosition(6, 27), 0, 2);
            f.Editor.EndStroke();
            return changed > 0;
        },

        // Objects
        ["ObjectEditor.Place"] = f => Ok(f.Editor.Objects.Place(BuiltInMapObjects.MineId(ResourceType.Ore), new MapPosition(4, 22), out _)),
        ["ObjectEditor.Remove"] = f => Ok(f.Editor.Objects.Remove(f.Chest)),
        ["ObjectEditor.Remove (linked two-way gate)"] = f => Ok(f.Editor.Objects.Remove(f.GateA)),
        ["ObjectEditor.Remove (one-way exit)"] = f => Ok(f.Editor.Objects.Remove(f.Exit)),
        ["ObjectEditor.Remove (town)"] = f => Ok(f.Editor.Objects.Remove(f.Town)),
        ["ObjectEditor.SetGateExit"] = f => Ok(f.Editor.Objects.SetGateExit(f.Entrance, null)),
        ["ObjectEditor.LinkGates"] = f => Ok(f.Editor.Objects.LinkGates(f.GateA, f.GateC)),
        ["ObjectEditor.SetMineOwner"] = f => Ok(f.Editor.Objects.SetMineOwner(f.Mine, Red)),
        ["ObjectEditor.SetMineGuard"] = f => Ok(f.Editor.Objects.SetMineGuard(f.Mine, new GuardStack(BuiltInCreatures.Wolf, 12))),
        ["ObjectEditor.SetProviderOwner"] = f => Ok(f.Editor.Objects.SetProviderOwner(f.Provider, Blue)),
        ["ObjectEditor.SetGarrisonOwner"] = f => Ok(f.Editor.Objects.SetGarrisonOwner(f.Garrison, Red)),
        ["ObjectEditor.SetDwelling"] = f => Ok(f.Editor.Objects.SetDwelling(f.Dwelling, BuiltInCreatures.Pikeman, Blue, 7)),

        // Contents
        ["ObjectContentsEditor.SetChestValues"] = f => Ok(f.Editor.Contents.SetChestValues(f.Chest, new ChestValues(2000, 1500))),
        ["ObjectContentsEditor.SetArtifactPickup"] = f => Ok(f.Editor.Contents.SetArtifactPickup(f.Pickup, "iron-band")),
        ["ObjectContentsEditor.SetSignMessage"] = f => Ok(f.Editor.Contents.SetSignMessage(f.Sign, "Turn back.")),
        ["ObjectContentsEditor.SetPandorasBox"] = f => Ok(f.Editor.Contents.SetPandorasBox(f.Box,
            new BoxContents { Resources = new ResourceSet(Gold: 1000), Experience = 500 }, [new CreatureStack(BuiltInCreatures.Wolf, 3)], "Beware.")),
        ["ObjectContentsEditor.SetPandorasBoxEvent"] = f => Ok(f.Editor.Contents.SetPandorasBoxEvent(f.Box, "player event")),
        ["ObjectContentsEditor.SetQuestSiteQuest"] = f => Ok(f.Editor.Contents.SetQuestSiteQuest(f.SeerHut, null)),
        ["ObjectContentsEditor.SetSphinxRiddle"] = f => Ok(f.Editor.Contents.SetSphinxRiddle(f.Sphinx, new Riddle("What walks?", "A man", "A dog", "A fish"))),
        ["ObjectContentsEditor.SetWitchHutSkills"] = f => Ok(f.Editor.Contents.SetWitchHutSkills(f.WitchHut, [BuiltInSkills.Archery, BuiltInSkills.Logistics])),
        ["ObjectContentsEditor.SetShrineSpell"] = f => Ok(f.Editor.Contents.SetShrineSpell(f.Shrine, f.Spell(2))),
        ["ObjectContentsEditor.SetPrison"] = f => Ok(f.Editor.Contents.SetPrison(f.Prison, "inferno.azgaroth", 5)),

        // Monsters
        ["MonsterEditor.Place"] = f => Ok(f.Editor.Monsters.Place(new MapPosition(15, 25), BuiltInCreatures.Griffin, null, GuardDisposition.Hostile, true, out _)),
        ["MonsterEditor.Set"] = f => Ok(f.Editor.Monsters.Set(f.Monster, BuiltInCreatures.Griffin, 20, GuardDisposition.Hostile, true)),
        ["MonsterEditor.SetRandomTier"] = f => Ok(f.Editor.Monsters.SetRandomTier(f.Monster, 3)),
        ["MonsterEditor.SetGuardMode"] = f => Ok(f.Editor.Monsters.SetGuardMode(f.Monster, GuardMode.Area)),

        // Towns
        ["TownEditor.SetOwner"] = f => Ok(f.Editor.Towns.SetOwner(f.Town, Red)),
        ["TownEditor.SetName"] = f => Ok(f.Editor.Towns.SetName(f.Town, "Dawnford")),
        ["TownEditor.SetType"] = f => Ok(f.Editor.Towns.SetType(f.Town, TownIds.Inferno)),
        ["TownEditor.SetRandom"] = f => Ok(f.Editor.Towns.SetRandom(f.Town, true)),
        ["TownEditor.SetBuilding"] = f => Ok(f.Editor.Towns.SetBuilding(f.Town, BuiltInBuildings.MageGuild, 2)),
        ["TownEditor.SetDwelling"] = f => Ok(f.Editor.Towns.SetDwelling(f.Town, BuiltInCreatures.Pikeman, 2)),
        ["TownEditor.SetGarrisonSlot"] = f => Ok(f.Editor.Towns.SetGarrisonSlot(f.Town, 1, new CreatureStack(BuiltInCreatures.Griffin, 2))),
        ["TownEditor.SetGarrisonSlot (map garrison)"] = f => Ok(f.Editor.Towns.SetGarrisonSlot(f.Garrison, 0, null)),
        ["TownEditor.SetGuildSpells"] = f => Ok(f.Editor.Towns.SetGuildSpells(f.Town, [HavenSpells(1)[1], HavenSpells(2)[0]])),

        // Heroes
        ["HeroEditor.Place"] = f => Ok(f.Editor.Heroes.Place(Blue, new MapPosition(10, 25), "Orrin", out _)),
        ["HeroEditor.Remove"] = f => Ok(f.Editor.Heroes.Remove(f.Hero)),
        ["HeroEditor.SetStats"] = f => Ok(f.Editor.Heroes.SetStats(f.Hero, 5, 4, SkillLevel.Advanced, 3, 2)),
        ["HeroEditor.SetWarMachines"] = f => Ok(f.Editor.Heroes.SetWarMachines(f.Hero, new HashSet<WarMachineSlot> { WarMachineSlot.Cart, WarMachineSlot.Tent })),
        ["HeroEditor.SetArtifacts"] = f => Ok(f.Editor.Heroes.SetArtifacts(f.Hero, ["rusted-blade"], ["iron-band", "iron-band"])),
        ["HeroEditor.SetMoraleAndLuck"] = f => Ok(f.Editor.Heroes.SetMoraleAndLuck(f.Hero, 2, -1)),
        ["HeroEditor.SetType"] = f => Ok(f.Editor.Heroes.SetType(f.Hero, "inferno.azgaroth")),
        ["HeroEditor.SetPlaceholder"] = f => Ok(f.Editor.Heroes.SetPlaceholder(f.Hero, true)),
        ["HeroEditor.SetArmySlot"] = f => Ok(f.Editor.Heroes.SetArmySlot(f.Hero, 0, new CreatureStack(BuiltInCreatures.Griffin, 4))),
        ["HeroEditor.SetSpells"] = f => Ok(f.Editor.Heroes.SetSpells(f.Hero, true, [f.Spell(1), f.Spell(2)])),

        // Map events
        ["MapEventEditor.Add"] = f => Ok(f.Editor.MapEvents.Add(new MapEventDefinition("new event", MapEventKind.Player, []))),
        ["MapEventEditor.Replace"] = f => Ok(f.Editor.MapEvents.Replace("player event", new MapEventDefinition("player event", MapEventKind.Player, []) { Enabled = false })),
        ["MapEventEditor.Replace (rename)"] = f => Ok(f.Editor.MapEvents.Replace("hero event", new MapEventDefinition("renamed", MapEventKind.Hero, []))),
        ["MapEventEditor.Replace (rename quest)"] = f => Ok(f.Editor.MapEvents.Replace("quest",
            new MapEventDefinition("other quest", MapEventKind.Quest, []) { Stages = [QuestForms.Level(3, "Grow more.")] })),
        ["MapEventEditor.Remove"] = f => Ok(f.Editor.MapEvents.Remove("hero event")),
        ["MapEventEditor.Remove (quest)"] = f => Ok(f.Editor.MapEvents.Remove("quest")),
        ["MapEventEditor.Move"] = f => Ok(f.Editor.MapEvents.Move("player event", -1)),
        ["MapEventEditor.SetVariable"] = f => Ok(f.Editor.MapEvents.SetVariable("v", 5)),
        ["MapEventEditor.SetVariable (new)"] = f => Ok(f.Editor.MapEvents.SetVariable("w", 0)),
        ["MapEventEditor.RemoveVariable"] = f => Ok(f.Editor.MapEvents.RemoveVariable("v")),
        ["MapEventEditor.SetCampaignVariable"] = f => Ok(f.Editor.MapEvents.SetCampaignVariable("v", false, true)),
        ["MapEventEditor.SetCell"] = f => Ok(f.Editor.MapEvents.SetCell(new MapPosition(25, 25), "hero event")),
        ["MapEventEditor.SetCell (remove)"] = f => Ok(f.Editor.MapEvents.SetCell(Fixture.EventCell, null)),

        // Players
        ["PlayerEditor.Add"] = f => Ok(f.Editor.Players.Add("Tan", true, out _)),
        ["PlayerEditor.RemoveLast"] = f => Ok(f.Editor.Players.RemoveLast()),
        ["PlayerEditor.Update"] = f => Ok(f.Editor.Players.Update(Red, "Crimson", false)),
        ["PlayerEditor.SetHeroLimit"] = f => Ok(f.Editor.Players.SetHeroLimit(Red, 3)),
        ["PlayerEditor.SetTeam"] = f => Ok(f.Editor.Players.SetTeam(Red, new TeamId(3))),
        ["PlayerEditor.SetTeamsLocked"] = f =>
        {
            f.Editor.Players.SetTeamsLocked(true);
            return true;
        },
        ["PlayerEditor.SetTeamCounts"] = f => f.Editor.Players.SetTeamCounts(2, 3),

        // Map properties
        ["MapPropertiesEditor.SetProperties"] = f =>
        {
            f.Editor.Map.SetProperties("Isles", "Two islands");
            return true;
        },
        ["MapPropertiesEditor.SetRules"] = f => Ok(f.Editor.Map.SetRules(f.Editor.Scenario.Rules with { Difficulty = Complexity.Hard, MaxHeroes = 4 })),
        ["MapPropertiesEditor.SetGrail"] = f => Ok(f.Editor.Map.SetGrail(new MapPosition(20, 19))),
    };

    /// <summary>Area editor methods that are not edits, so have no undo case.</summary>
    private static readonly Dictionary<string, string> NotUndoable = new()
    {
        ["ObjectEditor.CanPlace"] = "a query: whether Place would succeed",
        ["HeightEditor.HeightAt"] = "a query: the ground's height at a tile",
    };

    private static readonly Type[] AreaEditors =
    [
        typeof(TerrainEditor), typeof(HeightEditor),typeof(ObjectEditor), typeof(ObjectContentsEditor), typeof(MonsterEditor), typeof(TownEditor),
        typeof(HeroEditor), typeof(MapEventEditor), typeof(PlayerEditor), typeof(MapPropertiesEditor),
    ];

    public static IEnumerable<TheoryDataRow<string>> CaseNames =>
        Cases.Keys.Select(name => new TheoryDataRow<string>(name));

    /// <summary>The map file text plus the state digest's text: what the editor saves and what play depends on.</summary>
    private static string Snapshot(MapEditorSession editor) =>
        MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario)) + "\n---\n" + GameStateDigest.Describe(editor.Scenario);

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Operation_Undoes_AndRedoes_Cleanly(string name)
    {
        var fixture = new Fixture();
        var editor = fixture.Editor;
        var before = Snapshot(editor);

        Assert.True(Cases[name](fixture), $"{name} was refused.");
        var after = Snapshot(editor);
        Assert.NotEqual(before, after);
        Assert.True(editor.History.CanUndo);

        Assert.True(editor.Undo());
        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);

        Assert.True(editor.Redo());
        Assert.Equal(after, Snapshot(editor));

        // And once more, so a redo leaves the command able to undo again.
        Assert.True(editor.Undo());
        Assert.Equal(before, Snapshot(editor));
    }

    [Fact]
    public void EveryEditorOperation_HasAnUndoCase()
    {
        var covered = Cases.Keys.Select(k => k.Split(' ')[0]).ToHashSet(StringComparer.Ordinal);
        var operations = AreaEditors
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .Where(m => m.ReturnType == typeof(EditResult) || m.ReturnType == typeof(bool) || m.ReturnType == typeof(int) || m.ReturnType == typeof(void))
                .Select(m => $"{t.Name}.{m.Name}"))
            .Distinct()
            .ToList();
        var missing = operations.Where(o => !covered.Contains(o) && !NotUndoable.ContainsKey(o)).Order(StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0, $"Editor operations without an undo case: {string.Join(", ", missing)}");
        Assert.All(NotUndoable.Keys, k => Assert.Contains(k, operations));
    }
}
