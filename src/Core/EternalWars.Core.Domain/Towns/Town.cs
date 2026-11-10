using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Towns;

/// <summary>
/// A town on the adventure map (ADR-046): 7×7 with the corners free, entered through the bottom-middle cell. Its
/// definition names the town type (<see cref="MapObjectDefinition.Town"/>), whose creature lines are its nine dwellings
/// (ADR-030). Per instance: owner, name, building levels, dwellings (base creature id → 1 built, 2 upgraded), creatures
/// waiting in each dwelling, the garrison, and the day it last built something (one build per day).
/// </summary>
public sealed class Town : MapObject, IFlaggable
{
    /// <summary>
    /// Owned towns reveal and watch this far around their centre cell (<see cref="SightCenter"/>): 7 cells past the walls (ADR-141).
    /// </summary>
    public const int TownScoutingRadius = 10;

    /// <summary>An Academy town with the Observatory sees this far (ADR-143).</summary>
    public const int ObservatoryScoutingRadius = 24;

    /// <summary>The Academy's Observatory (ADR-143): its town sees <see cref="ObservatoryScoutingRadius"/> cells.</summary>
    public const string Observatory = "observatory";

    private readonly Dictionary<string, int> _buildings;
    private readonly Dictionary<string, int> _dwellings;
    private readonly Dictionary<string, int> _available;

    public Town(
        MapObjectId id,
        MapObjectDefinition definition,
        MapPosition position,
        PlayerId? owner = null,
        string? name = null,
        IReadOnlyDictionary<string, int>? buildings = null,
        IReadOnlyDictionary<string, int>? dwellings = null,
        IReadOnlyDictionary<string, int>? available = null,
        Army? garrison = null,
        GameDate? lastBuilt = null,
        IReadOnlyList<string>? fixedGuildSpells = null,
        bool isRandom = false)
        : base(id, definition, position, MapObjectKind.Town)
    {
        Owner = owner;
        Name = name ?? definition.Name;
        _buildings = Copy(buildings ?? BuiltInBuildings.DefaultStart);
        _dwellings = Copy(dwellings);
        _available = Copy(available);
        Garrison = garrison ?? new Army();
        LastBuilt = lastBuilt;
        FixedGuildSpells = fixedGuildSpells?.ToList() ?? [];
        IsRandom = isRandom;
    }

    /// <inheritdoc/>
    /// <remarks>A town with a garrison (troops or a garrison hero) is defended against its owner's enemies, and an unowned one against anyone.</remarks>
    public override bool IsDefendedAgainst(GameState state, PlayerId visitor) =>
        (!Garrison.IsEmpty || GarrisonHero is not null) && (Owner is not { } owner || state.AreEnemies(visitor, owner));

    /// <inheritdoc/>
    internal override string DigestState() =>
        $"owner {Owner?.Value} governor {Governor?.Value} built {GameStateDigest.Pairs(Buildings)} dwellings {GameStateDigest.Pairs(Dwellings)} available {GameStateDigest.Pairs(Available)} "
        + $"garrison {GameStateDigest.Slots(Garrison)} last {LastBuilt?.AbsoluteDay}"
        + (GuildSpells.Count == 0 ? "" : $" guild {string.Join(';', GuildSpells.OrderBy(l => l.Key).Select(l => string.Join(',', l.Value)))}")
        + (SchoolSpells.Count == 0 ? "" : $" schools {string.Join(';', SchoolSpells.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => $"{s.Key}:{string.Join(',', s.Value)}"))}")
        + (FixedGuildSpells.Count == 0 ? "" : $" fixed {string.Join(',', FixedGuildSpells)}");

    /// <summary>Town type (<see cref="TownIds"/>).</summary>
    public string TownType => Definition.Town!;

    public PlayerId? Owner { get; internal set; }

    /// <summary>
    /// A random town (ADR-126): its faction is drawn when the game starts (<see cref="Game.PlaceholderRules"/>), an owned one taking its owner's
    /// chosen faction; until then its definition only shows it.
    /// </summary>
    public bool IsRandom { get; internal set; }

    public int ScoutingRadius => Level(Observatory) > 0 ? ObservatoryScoutingRadius : TownScoutingRadius;

    /// <summary>The middle of the town's footprint, where its sight is measured from (not its entrance, which is on the bottom edge).</summary>
    public MapPosition SightCenter => Position.Offset(Definition.Footprint.Width / 2, Definition.Footprint.Height / 2);

    public string Name { get; internal set; }

    /// <summary>Built level per building id (dwellings are in <see cref="Dwellings"/>); absent = not built.</summary>
    public IReadOnlyDictionary<string, int> Buildings => _buildings;

    /// <summary>Dwelling level per creature line (base creature id): 1 built, 2 upgraded.</summary>
    public IReadOnlyDictionary<string, int> Dwellings => _dwellings;

    /// <summary>Creatures waiting to be recruited per creature line; they accumulate week by week.</summary>
    public IReadOnlyDictionary<string, int> Available => _available;

    public Army Garrison { get; }

    /// <summary>
    /// The hero in the town's garrison (ADR-112), off the map: it commands <see cref="Garrison"/> when the town is attacked, with its skills, spells and war
    /// machines. It is not one of <see cref="Game.GameState.Heroes"/> and does not count towards the hero limit.
    /// </summary>
    public Heroes.Hero? GarrisonHero { get; internal set; }

    /// <summary>The hero appointed governor (ADR-063); valid only while that hero is alive, owned by the town's owner and has Stewardship (<see cref="StewardshipRules.GovernorOf"/>).</summary>
    public HeroId? Governor { get; internal set; }

    /// <summary>The day the town last built something; one building per town per day.</summary>
    public GameDate? LastBuilt { get; internal set; }

    /// <summary>What the town built on <see cref="LastBuilt"/> (the level's name, or the dwelling's), for the town screen; null when not known (a map's town).</summary>
    public string? LastBuiltName { get; internal set; }

    /// <summary>
    /// The spells its mage guild teaches, per spell level (ADR-110): drawn once at game start from the town's school, whether the guild is built
    /// or not; a guild of level L teaches levels 1 to L.
    /// </summary>
    public IReadOnlyDictionary<int, IReadOnlyList<string>> GuildSpells { get; private set; } = new Dictionary<int, IReadOnlyList<string>>();

    internal void SetGuildSpells(IReadOnlyDictionary<int, IReadOnlyList<string>> spells) => GuildSpells = spells;

    /// <summary>
    /// The guild spells the map fixes (map format v30): spells of the town's school, at most as many per spell level as the guild offers
    /// (<see cref="Magic.MagicRules.GuildSpellsPerLevel"/>); the game-start draw fills the rest. Empty = everything drawn.
    /// </summary>
    public IReadOnlyList<string> FixedGuildSpells { get; internal set; }

    /// <summary>
    /// The spells each spell building of another school adds here, by school (ADR-072): one per spell level 1 to 4 in level order, drawn once at
    /// game start for every spell building the town type has, built or not; a guild of level L teaches the first L.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> SchoolSpells { get; private set; } = new Dictionary<string, IReadOnlyList<string>>();

    internal void SetSchoolSpells(IReadOnlyDictionary<string, IReadOnlyList<string>> spells) => SchoolSpells = spells;

    public int Level(string building) => _buildings.GetValueOrDefault(building);

    public int DwellingLevel(string line) => _dwellings.GetValueOrDefault(line);

    public int AvailableOf(string line) => _available.GetValueOrDefault(line);

    internal void SetLevel(string building, int level) => Set(_buildings, building, level);

    internal void SetDwelling(string line, int level) => Set(_dwellings, line, level);

    internal void SetAvailable(string line, int count) => Set(_available, line, count);

    private static void Set(Dictionary<string, int> values, string key, int value)
    {
        if (value <= 0)
        {
            values.Remove(key);
        }
        else
        {
            values[key] = value;
        }
    }

    private static Dictionary<string, int> Copy(IReadOnlyDictionary<string, int>? values) =>
        values?.Where(v => v.Value > 0).ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal)
        ?? new Dictionary<string, int>(StringComparer.Ordinal);
}
