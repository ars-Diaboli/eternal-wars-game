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
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Turns;

namespace EternalWars.Core.Data.Maps;

/// <summary>Maps between <see cref="MapDocument"/> and domain objects.</summary>
public static class MapConverter
{
    public static MapDocument ToDocument(GameState state) =>
        ToDocument(state.Map) with
        {
            Players = state.Players
                .Select(p => new PlayerSlotDocument(p.Name, p.IsHuman, p.Team.Value == p.Id.Value ? null : p.Team.Value, p.MaxHeroes))
                .ToList(),
            TeamsLocked = state.TeamsLocked,
            Rules = state.Rules.IsDefault ? null : ToDocument(state.Rules),
            Heroes = state.Heroes
                .Select(h => new HeroPlacementDocument(
                    h.Id.Value, h.Name, h.Owner.Value, h.Position.X, h.Position.Y, h.Position.Level,
                    h.Attack, h.Defense, null,
                    h.Army.IsEmpty ? null : h.Army.Stacks.Select(s => new ArmySlotDocument(s.Slot, s.Stack.Creature, s.Stack.Count)).ToList(),
                    h.Definition,
                    h.Skills.Levels.Count == 0 ? null : h.Skills.Levels.OrderBy(s => s.Key, StringComparer.Ordinal).ToDictionary(s => s.Key, s => s.Value),
                    h.Morale == 0 ? null : h.Morale,
                    h.Luck == 0 ? null : h.Luck,
                    h.SpellPower == 0 ? null : h.SpellPower,
                    h.Knowledge == 0 ? null : h.Knowledge,
                    h.WarMachines.SetEquals(WarMachines.StartingSet) ? null : h.WarMachines.Order().ToList(),
                    h.Artifacts.AllWorn.Any() ? h.Artifacts.AllWorn.Select(w => w.Id).ToList() : null,
                    h.Artifacts.Backpack.Count == 0 ? null : h.Artifacts.Backpack.ToList()))
                .ToList(),
            Objects = state.Objects.Select(ToDocument).ToList(),
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

        return map;
    }

    /// <summary>
    /// Builds the initial state of a scenario. <paramref name="catalog"/> resolves object definitions and
    /// <paramref name="terrains"/> supplies terrain rules (both default to the built-in content);
    /// <paramref name="options"/> are the rules chosen at game creation.
    /// Throws <see cref="FormatException"/> for inconsistent maps.
    /// </summary>
    public static GameState ToGameState(
        MapDocument document,
        MapObjectCatalog? catalog = null,
        GameOptions? options = null,
        TerrainCatalog? terrains = null,
        CreatureCatalog? creatures = null,
        HeroCatalog? heroRoster = null,
        BuildingCatalog? buildings = null,
        ArtifactCatalog? artifacts = null)
    {
        catalog ??= MapObjectCatalog.Default;
        artifacts ??= ArtifactCatalog.Default;
        creatures ??= CreatureCatalog.Default;
        heroRoster ??= HeroCatalog.Default;
        var map = ToMap(document);
        var players = document.Players.Select((p, i) =>
            new Player(new PlayerId(i), p.Name, p.IsHuman, p.Team is { } team ? new TeamId(team) : null));
        var heroes = document.Heroes.Select(h => ToHero(h, creatures, heroRoster, artifacts)).ToList();
        // Before v13 the guard mode was one map-wide setting; it becomes the mode of every monster (ADR-065).
        var legacyGuardMode = document.FormatVersion < MapDocument.PerMonsterGuardModeVersion ? document.GuardMode : null;
        var objects = document.Objects.Select(o => ToObject(o, catalog, creatures, legacyGuardMode));
        try
        {
            var state = new GameState(map, players, heroes, objects, options, terrains, creatures, heroRoster, buildings, artifacts, catalog);
            state.SetTeamsLocked(document.TeamsLocked);
            state.SetRules(ToRules(document.Rules));
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
        rules.WeekEvents.IsDefault ? null : ToDocument(rules.WeekEvents));

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
        };

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

        var skills = h.Skills ?? (h.Tactics is { } tactics and not SkillLevel.None
            ? new Dictionary<string, SkillLevel> { [BuiltInSkills.Tactics] = tactics }
            : h.Type is null ? new Dictionary<string, SkillLevel>() : null);
        try
        {
            return Hero.Create(
                new HeroId(h.Id), new PlayerId(h.Owner), new MapPosition(h.X, h.Y, h.Level), ToArmy(h, creatures),
                h.Type is { } id ? heroRoster.Get(id) : null, h.Name, h.Attack, h.Defense, skills, h.Morale ?? 0, h.Luck ?? 0, h.SpellPower, h.Knowledge, h.WarMachines,
                h.Artifacts, h.Backpack, artifacts);
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

    private static MapObjectDocument ToDocument(MapObject mapObject)
    {
        var (id, definition, p) = (mapObject.Id.Value, mapObject.Definition.Id, mapObject.Position);
        return mapObject switch
        {
            ResourcePile pile => new ResourcePileDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, Amount = pile.Amount,
            },
            Mine mine => new MineDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, Owner = mine.Owner?.Value,
                Guard = mine.Guard is { } guard ? new GuardDocument(guard.Creature, guard.Count) : null,
            },
            CreatureDwelling dwelling => new DwellingDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
                Creature = dwelling.Creature, Owner = dwelling.Owner?.Value, Available = dwelling.Available,
            },
            Marketplace => new MarketplaceDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            ResourceProvider provider => new ResourceProviderDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, Owner = provider.Owner?.Value,
            },
            LootSite => new LootSiteDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            BuffSite => new BuffSiteDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            TrainingSite => new TrainingSiteDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            SignPost sign => new SignDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, Message = sign.Message,
            },
            CreatureBank => new CreatureBankDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            Observatory => new ObservatoryDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            DenOfThieves => new DenOfThievesDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            AstrologersTower => new AstrologersTowerDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            Sanctuary => new SanctuaryDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            Scenery => new SceneryDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            Cartographer => new CartographerDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            ArtifactMerchant => new ArtifactMerchantDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            RefugeeCamp => new RefugeeCampDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            SacrificialAltar => new SacrificialAltarDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            Sphinx sphinx => new SphinxDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
                Question = sphinx.Riddle?.Question, Answer = sphinx.Riddle?.Answer, Wrong1 = sphinx.Riddle?.Wrong1, Wrong2 = sphinx.Riddle?.Wrong2,
            },
            WitchHut hut => new WitchHutDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, Skills = hut.Skills.Count == 0 ? null : hut.Skills,
            },
            Deposit => new DepositDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            MagiWindow => new MagiWindowDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            MagiEye => new MagiEyeDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            KeymasterTent => new KeymasterDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            BorderPost => new BorderPostDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            MapTavern => new MapTavernDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            MapGarrison garrison => new GarrisonDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, Owner = garrison.Owner?.Value,
                Troops = garrison.Troops.IsEmpty ? null : garrison.Troops.Stacks.Select(s => new ArmySlotDocument(s.Slot, s.Stack.Creature, s.Stack.Count)).ToList(),
            },
            HillFort => new HillFortDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            TownGate => new TownGateDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            WarMachineFactory => new WarMachineFactoryDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            Prison prison => new PrisonDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, Hero = prison.Hero, PrisonerLevel = prison.Level,
            },
            ArtifactPickup pickup => new ArtifactPickupDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, Artifact = pickup.Artifact,
            },
            TreasureChest chest => new TreasureChestDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, Gold = chest.FixedValues?.Gold, Experience = chest.FixedValues?.Experience,
            },
            TwoWayGate gate => new GateDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, LinkedGate = gate.LinkedGate?.Value,
            },
            OneWayGateEntrance entrance => new GateEntranceDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, Exit = entrance.Exit?.Value,
            },
            OneWayGateExit => new GateExitDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
            },
            MonsterGuard monster => new MonsterDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level,
                Creature = monster.Creature, Count = monster.Count, Disposition = monster.Disposition, NeverFlees = monster.NeverFlees,
                Mode = monster.Mode == GuardMode.Classic ? null : monster.Mode,
            },
            Town town => new TownDocument
            {
                Id = id, Definition = definition, X = p.X, Y = p.Y, Level = p.Level, Owner = town.Owner?.Value,
                Name = town.Name == town.Definition.Name ? null : town.Name,
                Buildings = Sorted(town.Buildings),
                Dwellings = town.Dwellings.Count == 0 ? null : Sorted(town.Dwellings),
                Available = town.Available.Count == 0 ? null : Sorted(town.Available),
                Garrison = town.Garrison.IsEmpty ? null : town.Garrison.Stacks.Select(s => new ArmySlotDocument(s.Slot, s.Stack.Creature, s.Stack.Count)).ToList(),
                LastBuilt = town.LastBuilt?.AbsoluteDay,
            },
            _ => throw new NotSupportedException($"Cannot save {mapObject.GetType().Name}."),
        };
    }

    private static Dictionary<string, int> Sorted(IReadOnlyDictionary<string, int> values) =>
        values.OrderBy(v => v.Key, StringComparer.Ordinal).ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);

    /// <summary>Definition ids older maps use for what has another id now: the undead Ruins are the Crypt since ADR-070.</summary>
    private static readonly Dictionary<string, string> RenamedDefinitions = new(StringComparer.Ordinal)
    {
        ["bank.ruins"] = BuiltInMapObjects.Crypt,
    };

    private static MapObject ToObject(MapObjectDocument document, MapObjectCatalog catalog, CreatureCatalog creatures, GuardMode? legacyGuardMode)
    {
        if (!catalog.TryGet(document.Definition, out var definition)
            && !(RenamedDefinitions.TryGetValue(document.Definition, out var renamed) && catalog.TryGet(renamed, out definition)))
        {
            throw new FormatException($"Object {document.Id} uses unknown definition '{document.Definition}'.");
        }

        var id = new MapObjectId(document.Id);
        var position = new MapPosition(document.X, document.Y, document.Level);
        try
        {
            return document switch
            {
                ResourcePileDocument pile => new ResourcePile(id, definition, position, pile.Amount),
                MineDocument mine => new Mine(
                    id, definition, position, mine.Owner is { } owner ? new PlayerId(owner) : null,
                    mine.Guard is { } guard ? new GuardStack(guard.Creature, guard.Count) : null),
                DwellingDocument dwelling when !creatures.Contains(dwelling.Creature) =>
                    throw new FormatException($"Dwelling {dwelling.Id} has unknown creature '{dwelling.Creature}'."),
                DwellingDocument { Available: < 0 } dwelling => throw new FormatException($"Dwelling {dwelling.Id} has a negative number of creatures."),
                // Map dwellings sell base creatures only (ADR-073): an older map's upgraded dwelling loads as its line's base creature.
                DwellingDocument dwelling => new CreatureDwelling(
                    id, definition, position, creatures.BaseOf(dwelling.Creature), dwelling.Owner is { } dwellingOwner ? new PlayerId(dwellingOwner) : null, dwelling.Available),
                MarketplaceDocument => new Marketplace(id, definition, position),
                ResourceProviderDocument provider => new ResourceProvider(id, definition, position, provider.Owner is { } providerOwner ? new PlayerId(providerOwner) : null),
                LootSiteDocument => new LootSite(id, definition, position),
                BuffSiteDocument => new BuffSite(id, definition, position),
                TrainingSiteDocument => new TrainingSite(id, definition, position),
                SignDocument sign => new SignPost(id, definition, position, sign.Message),
                ObservatoryDocument => new Observatory(id, definition, position),
                DenOfThievesDocument => new DenOfThieves(id, definition, position),
                AstrologersTowerDocument => new AstrologersTower(id, definition, position),
                SanctuaryDocument => new Sanctuary(id, definition, position),
                SceneryDocument => new Scenery(id, definition, position),
                CartographerDocument => new Cartographer(id, definition, position),
                ArtifactMerchantDocument => new ArtifactMerchant(id, definition, position),
                RefugeeCampDocument => new RefugeeCamp(id, definition, position),
                SacrificialAltarDocument => new SacrificialAltar(id, definition, position),
                SphinxDocument { Question: { } q, Answer: { } a, Wrong1: { } w1, Wrong2: { } w2 } => new Sphinx(id, definition, position, new Riddle(q, a, w1, w2)),
                SphinxDocument { Question: null, Answer: null, Wrong1: null, Wrong2: null } => new Sphinx(id, definition, position),
                SphinxDocument => throw new FormatException("A sphinx's riddle needs a question, the answer and two wrong answers, or none of them."),
                WitchHutDocument { Skills: { Count: not 0 and not WitchHut.SkillCount } } => throw new FormatException($"A witch hut has {WitchHut.SkillCount} skills, or none to roll."),
                WitchHutDocument { Skills: { } skills } when skills.Any(s => !BuiltInSkills.Contains(s)) => throw new FormatException("A witch hut names an unknown skill."),
                WitchHutDocument hut => new WitchHut(id, definition, position, hut.Skills?.Select(BuiltInSkills.Resolve).ToList()),
                CreatureBankDocument => new CreatureBank(id, definition, position),
                DepositDocument => new Deposit(id, definition, position),
                MagiWindowDocument => new MagiWindow(id, definition, position),
                MagiEyeDocument => new MagiEye(id, definition, position),
                KeymasterDocument => new KeymasterTent(id, definition, position),
                BorderPostDocument => new BorderPost(id, definition, position),
                MapTavernDocument => new MapTavern(id, definition, position),
                GarrisonDocument garrison => new MapGarrison(
                    id, definition, position, garrison.Owner is { } garrisonOwner ? new PlayerId(garrisonOwner) : null,
                    ToArmy(garrison.Troops, $"Garrison {garrison.Id}", creatures)),
                HillFortDocument => new HillFort(id, definition, position),
                TownGateDocument => new TownGate(id, definition, position),
                WarMachineFactoryDocument => new WarMachineFactory(id, definition, position),
                PrisonDocument prison => new Prison(id, definition, position, prison.Hero, prison.PrisonerLevel),
                ArtifactPickupDocument artifact => new ArtifactPickup(id, definition, position, artifact.Artifact),
                TreasureChestDocument { Gold: { } gold, Experience: { } experience } => new TreasureChest(id, definition, position, new ChestValues(gold, experience)),
                TreasureChestDocument { Gold: null, Experience: null } => new TreasureChest(id, definition, position),
                TreasureChestDocument => throw new FormatException("A chest needs both gold and experience, or neither."),
                MonsterDocument { Mode: { } mode } monster when !Enum.IsDefined(mode) =>
                    throw new FormatException($"Monster {monster.Id} has an unknown guard mode {mode}."),
                MonsterDocument monster => new MonsterGuard(
                    id, definition, position, monster.Creature, monster.Count, monster.Disposition, monster.NeverFlees,
                    monster.Mode ?? legacyGuardMode ?? GuardMode.Classic),
                GateDocument gate => new TwoWayGate(id, definition, position, ToId(gate.LinkedGate)),
                GateEntranceDocument entrance => new OneWayGateEntrance(id, definition, position, ToId(entrance.Exit)),
                GateExitDocument => new OneWayGateExit(id, definition, position),
                TownDocument town => ToTown(town, id, definition, position, creatures),
                _ => throw new FormatException($"Unsupported object kind {document.GetType().Name}."),
            };
        }
        catch (ArgumentException e)
        {
            throw new FormatException($"Object {document.Id}: {e.Message}", e);
        }

        static MapObjectId? ToId(int? value) => value is { } v ? new MapObjectId(v) : null;
    }

    private static Town ToTown(TownDocument town, MapObjectId id, MapObjectDefinition definition, MapPosition position, CreatureCatalog creatures)
    {
        foreach (var line in (town.Dwellings?.Keys ?? []).Concat(town.Available?.Keys ?? []))
        {
            if (!creatures.Contains(line))
            {
                throw new FormatException($"Town {town.Id} has a dwelling for unknown creature '{line}'.");
            }
        }

        if (town.Dwellings?.Values.Any(l => l is < 0 or > 2) == true || town.Buildings?.Values.Any(l => l < 0) == true)
        {
            throw new FormatException($"Town {town.Id} has an invalid building or dwelling level.");
        }

        return new Town(
            id, definition, position, town.Owner is { } owner ? new PlayerId(owner) : null, town.Name, town.Buildings, town.Dwellings,
            town.Available, ToArmy(town.Garrison, $"Town {town.Id}", creatures), town.LastBuilt is { } day ? new GameDate(day) : null);
    }
}
