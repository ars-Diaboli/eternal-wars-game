using System.Text;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Turns;

namespace EternalWars.Core.Data.Maps;

/// <summary>Maps between <see cref="MapDocument"/> and domain objects.</summary>
public static partial class MapConverter
{
    public static MapDocument ToDocument(GameState state) =>
        ToDocument(state.Map) with
        {
            Players = state.Players
                .Select(p => new PlayerSlotDocument(p.Name, p.IsHuman, p.Team.Value == p.Id.Value ? null : p.Team.Value, p.MaxHeroes))
                .ToList(),
            TeamsLocked = state.TeamsLocked,
            MinTeams = state.MinTeams,
            MaxTeams = state.MaxTeams,
            Rules = state.Rules.IsDefault ? null : ToDocument(state.Rules),
            Heroes = state.Heroes
                .Select(h => new HeroPlacementDocument(
                    h.Id.Value, h.Name, h.Owner.Value, h.Position.X, h.Position.Y, h.Position.Level,
                    h.Attack, h.Defense,
                    h.Army.IsEmpty ? null : h.Army.Stacks.Select(s => new ArmySlotDocument(s.Slot, s.Stack.Creature, s.Stack.Count)).ToList(),
                    h.Definition,
                    h.Skills.Levels.Count == 0 ? null : h.Skills.Levels.OrderBy(s => s.Key, StringComparer.Ordinal).ToDictionary(s => s.Key, s => s.Value),
                    h.Morale == 0 ? null : h.Morale,
                    h.Luck == 0 ? null : h.Luck,
                    h.SpellPower == 0 ? null : h.SpellPower,
                    h.Knowledge == 0 ? null : h.Knowledge,
                    h.WarMachines.SetEquals(WarMachines.StartingSet) ? null : h.WarMachines.Order().ToList(),
                    h.Artifacts.AllWorn.Any() ? h.Artifacts.AllWorn.Select(w => w.Id).ToList() : null,
                    h.Artifacts.Backpack.Count == 0 ? null : h.Artifacts.Backpack.ToList(),
                    h.HasSpellBook,
                    h.HasSpellBook ? h.Spells.Keys.ToList() : null,
                    h.IsPlaceholder ? true : null))
                .ToList(),
            Objects = state.Objects.Select(ToDocument).ToList(),
            Events = state.MapEvents.Set.IsEmpty ? null : state.MapEvents.Set,
            Grail = state.Grail.Position is { } grail ? new CellDocument(grail.X, grail.Y, grail.Level) : null,
        };

    public static MapDocument ToDocument(AdventureMap map) => new()
    {
        Name = map.Name,
        Description = map.Description,
        Width = map.Width,
        Height = map.Height,
        Terrain = WriteLayer(map, p => TerrainCodes.ToCode(map.GetTerrain(p))),
        Roads = map.HasRoads ? WriteLayer(map, p => RoadCodes.ToCode(map.GetRoad(p))) : null,
        Rivers = map.HasRivers ? WriteLayer(map, p => RiverCodes.ToCode(map.GetRiver(p))) : null,
        Heights = map.HasGroundHeights ? WriteHeights(map) : null,
    };

    public static AdventureMap ToMap(MapDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.FormatVersion > MapDocument.CurrentFormatVersion)
        {
            throw new NotSupportedException(
                $"Map format v{document.FormatVersion} is newer than supported v{MapDocument.CurrentFormatVersion}.");
        }

        var map = new AdventureMap(document.Width, document.Height, document.Terrain.Count)
        {
            Name = document.Name,
            Description = document.Description,
        };
        ReadLayer(document, document.Terrain, "Terrain", (p, c) => map.SetTerrain(p, TerrainCodes.FromCode(c)));
        if (document.Roads is { } roads)
        {
            ReadLayer(document, roads, "Roads", (p, c) => map.SetRoad(p, RoadCodes.FromCode(c)));
        }

        if (document.Rivers is { } rivers)
        {
            ReadLayer(document, rivers, "Rivers", (p, c) => map.SetRiver(p, RiverCodes.FromCode(c)));
        }

        if (document.Heights is { } heights)
        {
            ReadHeights(document, heights, map);
        }

        return map;
    }

    /// <summary>
    /// Builds the initial state of a scenario from the game's content tables (<paramref name="content"/>, the built-in tables when left out);
    /// <paramref name="options"/> are the rules chosen at game creation.
    /// Throws <see cref="FormatException"/> for inconsistent maps. The editor passes <paramref name="checkEvents"/> false, so a map whose events do
    /// not fit yet still opens (its validator shows why).
    /// </summary>
    public static GameState ToGameState(MapDocument document, ContentCatalogs? content = null, GameOptions? options = null, bool checkEvents = true)
    {
        content ??= ContentCatalogs.Default;
        var catalog = content.Objects;
        var artifacts = content.Artifacts;
        var creatures = content.Creatures;
        var spells = content.Spells;
        var map = ToMap(document);
        var players = document.Players.Select((p, i) =>
            new Player(new PlayerId(i), p.Name, p.IsHuman, p.Team is { } team ? new TeamId(team) : null));
        var heroes = document.Heroes.Select(h => ToHero(h, creatures, content.Heroes, artifacts)).ToList();
        var objects = document.Objects.Select(o => ToObject(o, catalog, creatures, spells));
        try
        {
            var state = new GameState(map, players, heroes, objects, options, content: content);
            state.SetTeamsLocked(document.TeamsLocked);
            state.SetTeamCounts(document.MinTeams, document.MaxTeams);
            state.SetRules(ToRules(document.Rules));
            state.SetGrail(document.Grail is { } grail ? new MapPosition(grail.X, grail.Y, grail.Level) : null);
            // Quest objects and Pandora's Boxes name their events, so a map with them is checked even without events (EVT-5, MAP-35).
            if (document.Events is { } mapEvents)
            {
                state.SetMapEvents(mapEvents, checkEvents);
            }
            else if (state.ObjectsOf<QuestSite>().Any(q => q.Quest is not null) || state.ObjectsOf<PandorasBox>().Any(b => b.Event is not null))
            {
                state.SetMapEvents(Domain.MapEvents.MapEventSet.Empty, checkEvents);
            }
            for (var i = 0; i < document.Players.Count; i++)
            {
                state.SetHeroLimit(new PlayerId(i), document.Players[i].MaxHeroes);
            }

            return state;
        }
        catch (ArgumentException e)
        {
            throw new FormatException($"Invalid map '{document.Name}': {e.Message}", e);
        }
    }

    private static ScenarioRulesDocument ToDocument(ScenarioRules rules) => new(
        rules.Victory.Count == 0 ? null : rules.Victory.Select(ToDocument).ToList(),
        rules.Defeat.Count == 0 ? null : rules.Defeat.Select(ToDocument).ToList(),
        rules.StandardVictory,
        rules.Elimination,
        rules.GraceDays == ScenarioRules.DefaultGraceDays ? null : rules.GraceDays,
        rules.MaxHeroes == ScenarioRules.HeroLimit ? null : rules.MaxHeroes,
        rules.WeekEvents.IsDefault ? null : ToDocument(rules.WeekEvents),
        rules.Difficulty,
        rules.Bans.IsEmpty ? null : new BansDocument(
            rules.Bans.Heroes.Count == 0 ? null : rules.Bans.Heroes,
            rules.Bans.Artifacts.Count == 0 ? null : rules.Bans.Artifacts,
            rules.Bans.Spells.Count == 0 ? null : rules.Bans.Spells,
            rules.Bans.Skills.Count == 0 ? null : rules.Bans.Skills),
        rules.Arena ? true : null);

    private static WeekEventsDocument ToDocument(WeekEventSettings events) => new(
        events.Enabled,
        events.SkipWeeks == WeekEventSettings.DefaultSkipWeeks ? null : events.SkipWeeks,
        events.WeeklyFactions?.ToList(),
        events.MonthlyFactions?.ToList());

    private static ConditionDocument ToDocument(ScenarioCondition c) => (c.Kind switch
    {
        ConditionKind.CaptureTown or ConditionKind.LoseTown => new ConditionDocument(c.Kind, c.Player?.Value, Town: c.Town?.Value),
        ConditionKind.DefeatHero or ConditionKind.LoseHero => new(c.Kind, c.Player?.Value, Hero: c.Hero?.Value),
        ConditionKind.GatherResources => new(c.Kind, c.Player?.Value, Resource: c.Resource, Amount: c.Amount),
        ConditionKind.CaptureObject or ConditionKind.ReleaseHero => new(c.Kind, c.Player?.Value, Target: c.Target?.Value),
        ConditionKind.ObtainArtifact or ConditionKind.LoseArtifact => new(c.Kind, c.Player?.Value, Artifact: c.Artifact),
        _ => new(c.Kind, c.Player?.Value, Day: c.Day),
    }) with { Deadline = c.Deadline };

    private static ScenarioRules ToRules(ScenarioRulesDocument? document) => document is null
        ? ScenarioRules.Default
        : new ScenarioRules(
            (document.Victory ?? []).Select(ToCondition).ToList(),
            (document.Defeat ?? []).Select(ToCondition).ToList(),
            document.StandardVictory,
            document.Elimination,
            document.GraceDays ?? ScenarioRules.DefaultGraceDays,
            document.MaxHeroes ?? ScenarioRules.HeroLimit)
        {
            WeekEvents = document.WeekEvents is { } events
                ? new WeekEventSettings(events.Enabled, events.SkipWeeks ?? WeekEventSettings.DefaultSkipWeeks, events.WeeklyFactions?.ToList(), events.MonthlyFactions?.ToList())
                : WeekEventSettings.Default,
            Difficulty = document.Difficulty,
            Bans = ToBans(document.Bans),
            Arena = document.Arena == true,
        };

    /// <summary>The map's bans (v35); skill ids are resolved like everywhere else, and each list is sorted without repeats.</summary>
    private static MapBans ToBans(BansDocument? bans)
    {
        static IReadOnlyList<string> Sorted(IEnumerable<string>? ids) => (ids ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return bans is null ? MapBans.None : new MapBans
        {
            Heroes = Sorted(bans.Heroes),
            Artifacts = Sorted(bans.Artifacts),
            Spells = Sorted(bans.Spells),
            Skills = Sorted(bans.Skills?.Select(s => BuiltInSkills.Contains(s) ? BuiltInSkills.Resolve(s) : s)),
        };
    }

    private static ScenarioCondition ToCondition(ConditionDocument c) => new(
        c.Kind,
        c.Player is { } player ? new PlayerId(player) : null,
        c.Town is { } town ? new MapObjectId(town) : null,
        c.Hero is { } hero ? new HeroId(hero) : null,
        c.Resource ?? ResourceType.Gold,
        c.Amount ?? 0,
        c.Day ?? 0,
        c.Target is { } target ? new MapObjectId(target) : null,
        c.Artifact,
        c.Deadline);

    private static Hero ToHero(HeroPlacementDocument h, CreatureCatalog creatures, HeroCatalog heroRoster, ArtifactCatalog artifacts)
    {
        if (h.Type is { } type && !heroRoster.Contains(type))
        {
            throw new FormatException($"Hero {h.Name} is of unknown type '{type}'.");
        }

        var skills = h.Skills ?? (h.Type is null ? new Dictionary<string, SkillLevel>() : null);
        try
        {
            var hero = Hero.Create(
                new HeroId(h.Id), new PlayerId(h.Owner), new MapPosition(h.X, h.Y, h.Level), ToArmy(h, creatures),
                h.Type is { } id ? heroRoster.Get(id) : null, h.Name, h.Attack, h.Defense, skills, h.Morale ?? 0, h.Luck ?? 0, h.SpellPower, h.Knowledge, h.WarMachines,
                h.Artifacts, h.Backpack, artifacts, h.SpellBook, h.Spells, placeholder: h.Placeholder == true);
            return hero;
        }
        catch (Exception e) when (e is KeyNotFoundException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            throw new FormatException($"Hero {h.Name}: {e.Message}", e);
        }
    }

    private static Army ToArmy(HeroPlacementDocument hero, CreatureCatalog creatures) => ToArmy(hero.Army, $"Hero {hero.Id}", creatures);

    private static Army ToArmy(IReadOnlyList<ArmySlotDocument>? slots, string owner, CreatureCatalog creatures)
    {
        var army = new CreatureStack?[Army.SlotCount];
        foreach (var slot in slots ?? [])
        {
            if (!Army.IsValidSlot(slot.Slot) || army[slot.Slot] is not null || slot.Count < 1)
            {
                throw new FormatException($"{owner} has an invalid or duplicate army slot {slot.Slot}.");
            }

            if (!creatures.Contains(slot.Creature))
            {
                throw new FormatException($"{owner} has unknown creature '{slot.Creature}'.");
            }

            army[slot.Slot] = new CreatureStack(slot.Creature, slot.Count);
        }

        return new Army(army);
    }

    private static List<IReadOnlyList<string>> WriteLayer(AdventureMap map, Func<MapPosition, char> code)
    {
        var levels = new List<IReadOnlyList<string>>(map.Levels);
        var row = new StringBuilder(map.Width);
        for (var level = 0; level < map.Levels; level++)
        {
            var rows = new List<string>(map.Height);
            for (var y = 0; y < map.Height; y++)
            {
                row.Clear();
                for (var x = 0; x < map.Width; x++)
                {
                    row.Append(code(new MapPosition(x, y, level)));
                }

                rows.Add(row.ToString());
            }

            levels.Add(rows);
        }

        return levels;
    }

    /// <summary>The corner grid (<see cref="MapDocument.Heights"/>): per level, <c>Height + 1</c> rows of <c>Width + 1</c> codes.</summary>
    internal static List<IReadOnlyList<string>> WriteHeights(AdventureMap map)
    {
        var levels = new List<IReadOnlyList<string>>(map.Levels);
        var row = new StringBuilder(map.Width + 1);
        for (var level = 0; level < map.Levels; level++)
        {
            var rows = new List<string>(map.Height + 1);
            for (var cy = 0; cy <= map.Height; cy++)
            {
                row.Clear();
                for (var cx = 0; cx <= map.Width; cx++)
                {
                    row.Append(HeightCodes.ToCode(map.GetCorner(level, cx, cy)));
                }

                rows.Add(row.ToString());
            }

            levels.Add(rows);
        }

        return levels;
    }

    private static void ReadHeights(MapDocument document, IReadOnlyList<IReadOnlyList<string>> layer, AdventureMap map)
    {
        if (layer.Count != document.Terrain.Count)
        {
            throw new FormatException($"Heights has {layer.Count} levels, expected {document.Terrain.Count}.");
        }

        for (var level = 0; level < layer.Count; level++)
        {
            var rows = layer[level];
            if (rows.Count != document.Height + 1)
            {
                throw new FormatException($"Heights level {level} has {rows.Count} rows, expected {document.Height + 1}.");
            }

            for (var cy = 0; cy < rows.Count; cy++)
            {
                if (rows[cy].Length != document.Width + 1)
                {
                    throw new FormatException($"Heights level {level}, row {cy} has {rows[cy].Length} corners, expected {document.Width + 1}.");
                }

                for (var cx = 0; cx < rows[cy].Length; cx++)
                {
                    map.SetCorner(level, cx, cy, HeightCodes.FromCode(rows[cy][cx]));
                }
            }
        }
    }

    private static void ReadLayer(
        MapDocument document, IReadOnlyList<IReadOnlyList<string>> layer, string name, Action<MapPosition, char> apply)
    {
        if (layer.Count != document.Terrain.Count)
        {
            throw new FormatException($"{name} has {layer.Count} levels, expected {document.Terrain.Count}.");
        }

        for (var level = 0; level < layer.Count; level++)
        {
            var rows = layer[level];
            if (rows.Count != document.Height)
            {
                throw new FormatException($"{name} level {level} has {rows.Count} rows, expected {document.Height}.");
            }

            for (var y = 0; y < rows.Count; y++)
            {
                if (rows[y].Length != document.Width)
                {
                    throw new FormatException($"{name} level {level}, row {y} has {rows[y].Length} tiles, expected {document.Width}.");
                }

                for (var x = 0; x < rows[y].Length; x++)
                {
                    apply(new MapPosition(x, y, level), rows[y][x]);
                }
            }
        }
    }
}
