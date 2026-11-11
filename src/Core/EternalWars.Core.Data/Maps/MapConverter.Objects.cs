using System.Text.Json.Serialization.Metadata;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Data.Maps;

/// <summary>
/// The map objects' part of the map format (ADR-166): one codec per instance type of <see cref="MapObjectKinds"/>, holding the JSON
/// <c>kind</c> name, the document type and how to save and load one. The JSON discriminators come from this table too, and a test checks that
/// every registered kind has a codec.
/// </summary>
public static partial class MapConverter
{
    /// <summary>What loading an object needs besides its document.</summary>
    internal sealed record ObjectContext(
        MapObjectId Id, MapObjectDefinition Definition, MapPosition Position, CreatureCatalog Creatures, SpellCatalog Spells);

    /// <summary>
    /// One kind's map format: <paramref name="Name"/> is its <c>kind</c> in the file. <see cref="Save"/> fills a document whose common fields
    /// (id, definition, position) are already set; <see cref="Load"/> throws <see cref="FormatException"/> for data that does not fit.
    /// </summary>
    internal sealed record ObjectCodec(
        string Name,
        Type ObjectType,
        Type DocumentType,
        Func<MapObject, MapObjectDocument, MapObjectDocument> Save,
        Func<MapObjectDocument, ObjectContext, MapObject> Load);

    internal static IReadOnlyList<ObjectCodec> ObjectCodecs { get; } =
    [
        Codec<ResourcePile, ResourcePileDocument>(
            "pile", (pile, d) => d with { Amount = pile.Amount }, (d, c) => new ResourcePile(c.Id, c.Definition, c.Position, d.Amount)),
        Codec<Mine, MineDocument>(
            "mine",
            (mine, d) => d with { Owner = mine.Owner?.Value, Guard = mine.Guard is { } guard ? new GuardDocument(guard.Creature, guard.Count) : null },
            (d, c) => new Mine(
                c.Id, c.Definition, c.Position, d.Owner is { } owner ? new PlayerId(owner) : null,
                d.Guard is { } guard ? new GuardStack(guard.Creature, guard.Count) : null)),
        Codec<TreasureChest, TreasureChestDocument>(
            "chest",
            (chest, d) => d with { Gold = chest.FixedValues?.Gold, Experience = chest.FixedValues?.Experience },
            (d, c) => d switch
            {
                { Gold: { } gold, Experience: { } experience } => new TreasureChest(c.Id, c.Definition, c.Position, new ChestValues(gold, experience)),
                { Gold: null, Experience: null } => new TreasureChest(c.Id, c.Definition, c.Position),
                _ => throw new FormatException("A chest needs both gold and experience, or neither."),
            }),
        Codec<TwoWayGate, GateDocument>(
            "gate", (gate, d) => d with { LinkedGate = gate.LinkedGate?.Value }, (d, c) => new TwoWayGate(c.Id, c.Definition, c.Position, ToId(d.LinkedGate))),
        Codec<OneWayGateEntrance, GateEntranceDocument>(
            "gateEntrance", (entrance, d) => d with { Exit = entrance.Exit?.Value }, (d, c) => new OneWayGateEntrance(c.Id, c.Definition, c.Position, ToId(d.Exit))),
        Codec<OneWayGateExit, GateExitDocument>("gateExit", (_, d) => d, (_, c) => new OneWayGateExit(c.Id, c.Definition, c.Position)),
        Codec<MonsterGuard, MonsterDocument>(
            "monster",
            (monster, d) => d with
            {
                Creature = monster.Creature, Count = monster.Count, Disposition = monster.Disposition, NeverFlees = monster.NeverFlees,
                Mode = monster.Mode == GuardMode.Classic ? null : monster.Mode,
                RandomTier = monster.RandomTier,
            },
            (d, c) => d switch
            {
                { Mode: { } mode } when !Enum.IsDefined(mode) => throw new FormatException($"Monster {d.Id} has an unknown guard mode {mode}."),
                { RandomTier: < 0 or > CreatureDefinition.MaxTier } =>
                    throw new FormatException($"Monster {d.Id} has a random tier outside 0–{CreatureDefinition.MaxTier}."),
                _ => new MonsterGuard(
                    c.Id, c.Definition, c.Position, d.Creature, d.Count, d.Disposition, d.NeverFlees, d.Mode ?? GuardMode.Classic, d.RandomTier),
            }),
        Codec<Town, TownDocument>("town", ToDocument, ToTown),
        Codec<ArtifactPickup, ArtifactPickupDocument>(
            "artifact", (pickup, d) => d with { Artifact = pickup.Artifact }, (d, c) => new ArtifactPickup(c.Id, c.Definition, c.Position, d.Artifact)),
        Codec<CreatureDwelling, DwellingDocument>(
            "dwelling",
            (dwelling, d) => d with { Creature = dwelling.Creature, Owner = dwelling.Owner?.Value, Available = dwelling.Available },
            (d, c) => d switch
            {
                _ when !c.Creatures.Contains(d.Creature) => throw new FormatException($"Dwelling {d.Id} has unknown creature '{d.Creature}'."),
                { Available: < 0 } => throw new FormatException($"Dwelling {d.Id} has a negative number of creatures."),

                // Map dwellings sell base creatures only (ADR-073): an older map's upgraded dwelling loads as its line's base creature.
                _ => new CreatureDwelling(
                    c.Id, c.Definition, c.Position, c.Creatures.BaseOf(d.Creature), d.Owner is { } owner ? new PlayerId(owner) : null, d.Available),
            }),
        Codec<Marketplace, MarketplaceDocument>("marketplace", (_, d) => d, (_, c) => new Marketplace(c.Id, c.Definition, c.Position)),
        Codec<ResourceProvider, ResourceProviderDocument>(
            "provider",
            (provider, d) => d with { Owner = provider.Owner?.Value },
            (d, c) => new ResourceProvider(c.Id, c.Definition, c.Position, d.Owner is { } owner ? new PlayerId(owner) : null)),
        Codec<LootSite, LootSiteDocument>("loot", (_, d) => d, (_, c) => new LootSite(c.Id, c.Definition, c.Position)),
        Codec<BuffSite, BuffSiteDocument>("buff", (_, d) => d, (_, c) => new BuffSite(c.Id, c.Definition, c.Position)),
        Codec<TrainingSite, TrainingSiteDocument>("training", (_, d) => d, (_, c) => new TrainingSite(c.Id, c.Definition, c.Position)),
        Codec<SignPost, SignDocument>(
            "sign", (sign, d) => d with { Message = sign.Message }, (d, c) => new SignPost(c.Id, c.Definition, c.Position, d.Message)),
        Codec<Observatory, ObservatoryDocument>("observatory", (_, d) => d, (_, c) => new Observatory(c.Id, c.Definition, c.Position)),
        Codec<HillFort, HillFortDocument>("hillFort", (_, d) => d, (_, c) => new HillFort(c.Id, c.Definition, c.Position)),
        Codec<TownGate, TownGateDocument>("townGate", (_, d) => d, (_, c) => new TownGate(c.Id, c.Definition, c.Position)),
        Codec<WarMachineFactory, WarMachineFactoryDocument>("factory", (_, d) => d, (_, c) => new WarMachineFactory(c.Id, c.Definition, c.Position)),
        Codec<Prison, PrisonDocument>(
            "prison",
            (prison, d) => d with { Hero = prison.Hero, PrisonerLevel = prison.Level },
            (d, c) => new Prison(c.Id, c.Definition, c.Position, d.Hero, d.PrisonerLevel)),
        Codec<CreatureBank, CreatureBankDocument>("bank", (_, d) => d, (_, c) => new CreatureBank(c.Id, c.Definition, c.Position)),
        Codec<DenOfThieves, DenOfThievesDocument>("denOfThieves", (_, d) => d, (_, c) => new DenOfThieves(c.Id, c.Definition, c.Position)),
        Codec<AstrologersTower, AstrologersTowerDocument>("astrologersTower", (_, d) => d, (_, c) => new AstrologersTower(c.Id, c.Definition, c.Position)),
        Codec<Sanctuary, SanctuaryDocument>("sanctuary", (_, d) => d, (_, c) => new Sanctuary(c.Id, c.Definition, c.Position)),
        Codec<Scenery, SceneryDocument>("scenery", (_, d) => d, (_, c) => new Scenery(c.Id, c.Definition, c.Position)),
        Codec<Cartographer, CartographerDocument>("cartographer", (_, d) => d, (_, c) => new Cartographer(c.Id, c.Definition, c.Position)),
        Codec<ArtifactMerchant, ArtifactMerchantDocument>("artifactMerchant", (_, d) => d, (_, c) => new ArtifactMerchant(c.Id, c.Definition, c.Position)),
        Codec<RefugeeCamp, RefugeeCampDocument>("refugeeCamp", (_, d) => d, (_, c) => new RefugeeCamp(c.Id, c.Definition, c.Position)),
        Codec<SacrificialAltar, SacrificialAltarDocument>("altar", (_, d) => d, (_, c) => new SacrificialAltar(c.Id, c.Definition, c.Position)),
        Codec<Sphinx, SphinxDocument>(
            "sphinx",
            (sphinx, d) => d with
            {
                Question = sphinx.Riddle?.Question, Answer = sphinx.Riddle?.Answer, Wrong1 = sphinx.Riddle?.Wrong1, Wrong2 = sphinx.Riddle?.Wrong2,
            },
            (d, c) => d switch
            {
                { Question: { } q, Answer: { } a, Wrong1: { } w1, Wrong2: { } w2 } => new Sphinx(c.Id, c.Definition, c.Position, new Riddle(q, a, w1, w2)),
                { Question: null, Answer: null, Wrong1: null, Wrong2: null } => new Sphinx(c.Id, c.Definition, c.Position),
                _ => throw new FormatException("A sphinx's riddle needs a question, the answer and two wrong answers, or none of them."),
            }),
        Codec<WitchHut, WitchHutDocument>(
            "witchHut",
            (hut, d) => d with { Skills = hut.Skills.Count == 0 ? null : hut.Skills },
            (d, c) => d switch
            {
                { Skills.Count: not 0 and not WitchHut.SkillCount } => throw new FormatException($"A witch hut has {WitchHut.SkillCount} skills, or none to roll."),
                { Skills: { } skills } when skills.Any(s => !BuiltInSkills.Contains(s)) => throw new FormatException("A witch hut names an unknown skill."),
                _ => new WitchHut(c.Id, c.Definition, c.Position, d.Skills?.Select(BuiltInSkills.Resolve).ToList()),
            }),
        Codec<Shrine, ShrineDocument>(
            "shrine",
            (shrine, d) => d with { Spell = shrine.Spell },
            (d, c) => ShrineRules.SpellRefusal(c.Spells, c.Definition.Amount, d.Spell) is { } wrong
                ? throw new FormatException($"A shrine: {wrong}")
                : new Shrine(c.Id, c.Definition, c.Position, d.Spell)),
        Codec<SkillSchool, SkillSchoolDocument>("skillSchool", (_, d) => d, (_, c) => new SkillSchool(c.Id, c.Definition, c.Position)),
        Codec<Obelisk, ObeliskDocument>("obelisk", (_, d) => d, (_, c) => new Obelisk(c.Id, c.Definition, c.Position)),
        Codec<CombatPlace, CombatPlaceDocument>("combatPlace", (_, d) => d, (_, c) => new CombatPlace(c.Id, c.Definition, c.Position)),
        Codec<Deposit, DepositDocument>("deposit", (_, d) => d, (_, c) => new Deposit(c.Id, c.Definition, c.Position)),
        Codec<MagiWindow, MagiWindowDocument>("magiWindow", (_, d) => d, (_, c) => new MagiWindow(c.Id, c.Definition, c.Position)),
        Codec<MagiEye, MagiEyeDocument>("magiEye", (_, d) => d, (_, c) => new MagiEye(c.Id, c.Definition, c.Position)),
        Codec<KeymasterTent, KeymasterDocument>("keymaster", (_, d) => d, (_, c) => new KeymasterTent(c.Id, c.Definition, c.Position)),
        Codec<BorderPost, BorderPostDocument>("borderPost", (_, d) => d, (_, c) => new BorderPost(c.Id, c.Definition, c.Position)),
        Codec<MapTavern, MapTavernDocument>("tavern", (_, d) => d, (_, c) => new MapTavern(c.Id, c.Definition, c.Position)),
        Codec<MapGarrison, GarrisonDocument>(
            "garrison",
            (garrison, d) => d with
            {
                Owner = garrison.Owner?.Value,
                Troops = garrison.Troops.IsEmpty ? null : garrison.Troops.Stacks.Select(s => new ArmySlotDocument(s.Slot, s.Stack.Creature, s.Stack.Count)).ToList(),
            },
            (d, c) => new MapGarrison(
                c.Id, c.Definition, c.Position, d.Owner is { } owner ? new PlayerId(owner) : null, ToArmy(d.Troops, $"Garrison {d.Id}", c.Creatures))),
        Codec<PandorasBox, PandorasBoxDocument>("pandorasBox", ToDocument, ToBox),
        Codec<QuestSite, QuestSiteDocument>(
            "questSite",
            (site, d) => d with { Quest = site.Quest },
            (d, c) => new QuestSite(c.Id, c.Definition, c.Position, string.IsNullOrEmpty(d.Quest) ? null : d.Quest)),
    ];

    private static readonly Dictionary<Type, ObjectCodec> CodecsByObject = ObjectCodecs.ToDictionary(c => c.ObjectType);

    private static readonly Dictionary<Type, ObjectCodec> CodecsByDocument = ObjectCodecs.ToDictionary(c => c.DocumentType);

    /// <summary>A <see cref="DefaultJsonTypeInfoResolver"/> modifier: the object documents, discriminated by <c>kind</c> (<see cref="ObjectCodecs"/>).</summary>
    internal static void AddObjectTypes(JsonTypeInfo info)
    {
        if (info.Type != typeof(MapObjectDocument))
        {
            return;
        }

        info.PolymorphismOptions = new JsonPolymorphismOptions { TypeDiscriminatorPropertyName = "kind" };
        foreach (var codec in ObjectCodecs)
        {
            info.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(codec.DocumentType, codec.Name));
        }
    }

    private static ObjectCodec Codec<TObject, TDocument>(
        string name, Func<TObject, TDocument, TDocument> save, Func<TDocument, ObjectContext, TObject> load)
        where TObject : MapObject
        where TDocument : MapObjectDocument =>
        new(name, typeof(TObject), typeof(TDocument), (o, d) => save((TObject)o, (TDocument)d), (d, c) => load((TDocument)d, c));

    private static MapObjectDocument ToDocument(MapObject mapObject)
    {
        var codec = CodecsByObject.GetValueOrDefault(mapObject.GetType())
                    ?? throw new NotSupportedException($"Cannot save {mapObject.GetType().Name}: it has no map format codec (ADR-166).");

        // The document types have required members, so the blank one is made by reflection and filled with "with".
        var blank = (MapObjectDocument)Activator.CreateInstance(codec.DocumentType)! with
        {
            Id = mapObject.Id.Value, Definition = mapObject.Definition.Id, X = mapObject.Position.X, Y = mapObject.Position.Y, Level = mapObject.Position.Level,
        };
        return codec.Save(mapObject, blank);
    }

    private static MapObject ToObject(
        MapObjectDocument document, MapObjectCatalog catalog, CreatureCatalog creatures, SpellCatalog spells)
    {
        if (!catalog.TryGet(document.Definition, out var definition))
        {
            throw new FormatException($"Object {document.Id} uses unknown definition '{document.Definition}'.");
        }

        var codec = CodecsByDocument.GetValueOrDefault(document.GetType())
                    ?? throw new FormatException($"Unsupported object kind {document.GetType().Name}.");
        var context = new ObjectContext(
            new MapObjectId(document.Id), definition, new MapPosition(document.X, document.Y, document.Level), creatures, spells);
        try
        {
            return codec.Load(document, context);
        }
        catch (ArgumentException e)
        {
            throw new FormatException($"Object {document.Id}: {e.Message}", e);
        }
    }

    private static MapObjectId? ToId(int? value) => value is { } v ? new MapObjectId(v) : null;

    private static TownDocument ToDocument(Town town, TownDocument document) => document with
    {
        Owner = town.Owner?.Value,
        Name = town.Name == town.Definition.Name ? null : town.Name,
        Buildings = Sorted(town.Buildings),
        Dwellings = town.Dwellings.Count == 0 ? null : Sorted(town.Dwellings),
        Available = town.Available.Count == 0 ? null : Sorted(town.Available),
        Garrison = town.Garrison.IsEmpty ? null : town.Garrison.Stacks.Select(s => new ArmySlotDocument(s.Slot, s.Stack.Creature, s.Stack.Count)).ToList(),
        LastBuilt = town.LastBuilt?.AbsoluteDay,
        GuildSpells = town.FixedGuildSpells.Count == 0 ? null : town.FixedGuildSpells.ToList(),
        Random = town.IsRandom ? true : null,
    };

    private static PandorasBoxDocument ToDocument(PandorasBox box, PandorasBoxDocument document)
    {
        var c = box.Contents;
        static int? Some(int value) => value == 0 ? null : value;
        static List<StackDocument>? Stacks(IReadOnlyList<CreatureStack> stacks) => stacks.Count == 0 ? null : [.. stacks.Select(s => new StackDocument(s.Creature, s.Count))];
        return document with
        {
            Resources = c.Resources.IsEmpty ? null : Enum.GetValues<ResourceType>().Where(r => c.Resources[r] != 0).ToDictionary(r => r, r => c.Resources[r]),
            Experience = Some(c.Experience), Mana = Some(c.Mana), Morale = Some(c.Morale), Luck = Some(c.Luck),
            Attack = Some(c.Attack), Defense = Some(c.Defense), SpellPower = Some(c.SpellPower), Knowledge = Some(c.Knowledge),
            Skills = c.Skills.Count == 0 ? null : c.Skills.OrderBy(s => s.Key, StringComparer.Ordinal).ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal),
            Artifacts = c.Artifacts.Count == 0 ? null : c.Artifacts,
            Creatures = Stacks(c.Creatures),
            Guard = Stacks(box.Guard),
            Message = box.Message,
            Event = box.Event,
        };
    }

    /// <summary>A Pandora's Box from its document (v27); unknown creatures and contents that are not gains are format errors, unknown artifacts the validator's.</summary>
    private static PandorasBox ToBox(PandorasBoxDocument box, ObjectContext context)
    {
        List<CreatureStack> Stacks(IReadOnlyList<StackDocument>? stacks, string what)
        {
            if ((stacks?.Count ?? 0) > Army.SlotCount)
            {
                throw new FormatException($"Pandora's Box {box.Id} has more than {Army.SlotCount} {what} stacks.");
            }

            foreach (var stack in stacks ?? [])
            {
                if (!context.Creatures.Contains(stack.Creature))
                {
                    throw new FormatException($"Pandora's Box {box.Id} has unknown creature '{stack.Creature}'.");
                }
            }

            return [.. (stacks ?? []).Select(s => new CreatureStack(s.Creature, s.Count))];
        }

        if (box.Skills?.Keys.FirstOrDefault(s => !BuiltInSkills.Contains(s)) is { } unknown)
        {
            throw new FormatException($"Pandora's Box {box.Id} names the unknown skill '{unknown}'.");
        }

        var resources = (box.Resources ?? new Dictionary<ResourceType, int>()).Aggregate(ResourceSet.Empty, (sum, r) => sum + ResourceSet.Of(r.Key, r.Value));
        var contents = new BoxContents
        {
            Resources = resources,
            Experience = box.Experience ?? 0, Mana = box.Mana ?? 0, Morale = box.Morale ?? 0, Luck = box.Luck ?? 0,
            Attack = box.Attack ?? 0, Defense = box.Defense ?? 0, SpellPower = box.SpellPower ?? 0, Knowledge = box.Knowledge ?? 0,
            Skills = (box.Skills ?? new Dictionary<string, SkillLevel>()).ToDictionary(s => BuiltInSkills.Resolve(s.Key), s => s.Value, StringComparer.Ordinal),
            Artifacts = box.Artifacts ?? [],
            Creatures = Stacks(box.Creatures, "creature"),
        };
        if (contents.Problem() is { } problem)
        {
            throw new FormatException($"Pandora's Box {box.Id} {problem}");
        }

        var guard = Stacks(box.Guard, "guard");
        return guard.Any(s => s.Count < 1)
            ? throw new FormatException($"Pandora's Box {box.Id} has an empty guard stack.")
            : new PandorasBox(context.Id, context.Definition, context.Position, contents, guard, string.IsNullOrEmpty(box.Message) ? null : box.Message,
                string.IsNullOrEmpty(box.Event) ? null : box.Event);
    }

    private static Dictionary<string, int> Sorted(IReadOnlyDictionary<string, int> values) =>
        values.OrderBy(v => v.Key, StringComparer.Ordinal).ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);

    private static Town ToTown(TownDocument town, ObjectContext context)
    {
        foreach (var line in (town.Dwellings?.Keys ?? []).Concat(town.Available?.Keys ?? []))
        {
            if (!context.Creatures.Contains(line))
            {
                throw new FormatException($"Town {town.Id} has a dwelling for unknown creature '{line}'.");
            }
        }

        if (town.Dwellings?.Values.Any(l => l is < 0 or > 2) == true || town.Buildings?.Values.Any(l => l < 0) == true)
        {
            throw new FormatException($"Town {town.Id} has an invalid building or dwelling level.");
        }

        if (town.GuildSpells is { } spells && MagicRules.GuildSpellsProblem(context.Spells, context.Definition.Town!, spells) is { } wrong)
        {
            throw new FormatException($"Town {town.Id} has wrong guild spells: {wrong}");
        }

        return new Town(
            context.Id, context.Definition, context.Position, town.Owner is { } owner ? new PlayerId(owner) : null, town.Name, town.Buildings, town.Dwellings,
            town.Available, ToArmy(town.Garrison, $"Town {town.Id}", context.Creatures), town.LastBuilt is { } day ? new GameDate(day) : null, town.GuildSpells,
            town.Random == true);
    }
}
