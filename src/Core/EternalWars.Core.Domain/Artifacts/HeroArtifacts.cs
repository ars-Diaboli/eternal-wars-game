using System.Globalization;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Domain.Artifacts;

/// <summary>
/// What a hero carries (ADR-061): worn artifacts by slot (<see cref="ArtifactSlots.Capacity"/>) and a backpack of
/// <see cref="ArtifactSlots.BackpackSize"/>. Only the artifact service and map loading change it (internal mutators); a
/// change must be followed by <c>GameState.RefreshArtifacts</c> so the hero's <see cref="ArtifactBonuses"/> match.
/// </summary>
public sealed class HeroArtifacts
{
    private readonly Dictionary<ArtifactSlot, List<string>> _worn = [];
    private readonly List<string> _backpack = [];

    public IReadOnlyList<string> Backpack => _backpack;

    public bool BackpackFull => _backpack.Count >= ArtifactSlots.BackpackSize;

    public IReadOnlyList<string> Worn(ArtifactSlot slot) => _worn.TryGetValue(slot, out var list) ? list : [];

    /// <summary>Everything worn, in paper-doll order.</summary>
    public IEnumerable<(ArtifactSlot Slot, string Id)> AllWorn =>
        ArtifactSlots.All.SelectMany(slot => Worn(slot).Select(id => (slot, id)));

    public bool IsEmpty => _backpack.Count == 0 && _worn.Values.All(l => l.Count == 0);

    public bool IsWorn(string id) => AllWorn.Any(w => w.Id == id);

    public bool Has(string id) => _backpack.Contains(id) || IsWorn(id);

    internal bool HasRoomToWear(ArtifactSlot slot) => Worn(slot).Count < ArtifactSlots.Capacity(slot);

    internal bool Wear(ArtifactDefinition artifact)
    {
        if (!HasRoomToWear(artifact.Slot))
        {
            return false;
        }

        if (!_worn.TryGetValue(artifact.Slot, out var list))
        {
            list = [];
            _worn[artifact.Slot] = list;
        }

        list.Add(artifact.Id);
        return true;
    }

    internal bool RemoveWorn(string id)
    {
        foreach (var list in _worn.Values)
        {
            if (list.Remove(id))
            {
                return true;
            }
        }

        return false;
    }

    internal bool AddToBackpack(string id)
    {
        if (BackpackFull)
        {
            return false;
        }

        _backpack.Add(id);
        return true;
    }

    internal bool RemoveFromBackpack(string id) => _backpack.Remove(id);

    /// <summary>
    /// Sets everything at once (map data, the editor): <paramref name="worn"/> go to the slots their definitions name.
    /// Throws <see cref="InvalidOperationException"/> when something does not fit, <see cref="KeyNotFoundException"/> for an
    /// unknown artifact; the hero is unchanged then.
    /// </summary>
    internal void ReplaceWith(IEnumerable<string> worn, IEnumerable<string> backpack, ArtifactCatalog catalog)
    {
        var next = new HeroArtifacts();
        foreach (var id in worn)
        {
            if (!next.Wear(catalog.Get(id)))
            {
                throw new InvalidOperationException($"No free {catalog.Get(id).Slot} slot for '{id}'.");
            }
        }

        foreach (var id in backpack)
        {
            catalog.Get(id);
            if (!next.AddToBackpack(id))
            {
                throw new InvalidOperationException($"The backpack holds at most {ArtifactSlots.BackpackSize} artifacts.");
            }
        }

        _worn.Clear();
        foreach (var (slot, list) in next._worn)
        {
            _worn[slot] = list;
        }

        _backpack.Clear();
        _backpack.AddRange(next._backpack);
    }

    /// <summary>The state digest and logs: <c>head:a,ring:b|backpack:c</c>.</summary>
    public override string ToString() =>
        string.Join(",", AllWorn.Select(w => $"{w.Slot.ToString().ToLowerInvariant()}:{w.Id}")) + "|" + string.Join(",", _backpack);
}

/// <summary>
/// The sum of a hero's worn artifacts (and complete sets): what the rules read instead of looking at the artifacts
/// (ADR-061). Kept on the hero by <c>GameState.RefreshArtifacts</c>.
/// </summary>
public sealed record ArtifactBonuses
{
    public static ArtifactBonuses None { get; } = new();

    public int Attack { get; init; }

    public int Defense { get; init; }

    public int SpellPower { get; init; }

    public int Knowledge { get; init; }

    public int Morale { get; init; }

    public int Luck { get; init; }

    public int MovementPercent { get; init; }

    public int MovementFlat { get; init; }

    public int BattleSpeed { get; init; }

    public int MeleePercent { get; init; }

    public int RangedPercent { get; init; }

    public int DamageTakenReductionPercent { get; init; }

    public int RangedDamageTakenReductionPercent { get; init; }

    public ResourceSet Income { get; init; }

    public int TerrainPenaltyReductionPercent { get; init; }

    public int AllSkills { get; init; }

    public IReadOnlySet<TerrainType> NativeTerrains { get; init; } = new HashSet<TerrainType>();

    public IReadOnlyDictionary<string, int> Skills { get; init; } = new Dictionary<string, int>();

    public int Sight { get; init; }

    public int ManaADay { get; init; }

    public int ManaMax { get; init; }

    public int CreatureHealthPercent { get; init; }

    public bool NoRetreat { get; init; }

    public int NecromancyPercent { get; init; }

    public int GrowthPercent { get; init; }

    /// <summary>Spell tags the army is immune to.</summary>
    public IReadOnlySet<string> Immunities { get; init; } = new HashSet<string>();

    /// <summary>Percent more effect of the hero's spells by element (lower-case: <c>fire</c>).</summary>
    public IReadOnlyDictionary<string, int> ElementPercent { get; init; } = new Dictionary<string, int>();

    /// <summary>The spell levels known while worn (every school), none when empty.</summary>
    public IReadOnlySet<int> SpellLevels { get; init; } = new HashSet<int>();

    /// <summary>The highest spell level either side may cast in the hero's battles, or null.</summary>
    public int? SpellLevelCap { get; init; }

    /// <summary>Levels the artifacts add to one skill (not counting <see cref="AllSkills"/>).</summary>
    public int SkillBonus(string skill) => Skills.GetValueOrDefault(skill);

    /// <summary>The percent the artifacts add to spells of the element (none for Magic Arrow's "all" or a bound element).</summary>
    public int ElementBonus(Magic.SpellElement element) => ElementPercent.GetValueOrDefault(element.ToString().ToLowerInvariant());

    /// <summary>Adds up the effects of everything worn, and the bonus of every set whose pieces are all worn.</summary>
    public static ArtifactBonuses Compute(ArtifactCatalog catalog, HeroArtifacts artifacts)
    {
        var effects = new List<ArtifactEffect>();
        var wornIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, id) in artifacts.AllWorn)
        {
            if (catalog.TryGet(id, out var artifact))
            {
                effects.AddRange(artifact.Effects);
                wornIds.Add(id);
            }
        }

        foreach (var set in catalog.Sets)
        {
            if (catalog.PiecesOf(set.Id).All(p => wornIds.Contains(p.Id)))
            {
                effects.AddRange(set.Bonus);
            }
        }

        return Of(effects);
    }

    public static ArtifactBonuses Of(IEnumerable<ArtifactEffect> effects)
    {
        var list = effects.ToList();
        int Sum(ArtifactEffectKind kind) => list.Where(e => e.Kind == kind).Sum(e => e.Value);

        var income = ResourceSet.Empty;
        var terrains = new HashSet<TerrainType>();
        var skills = new Dictionary<string, int>(StringComparer.Ordinal);
        var immunities = new HashSet<string>(StringComparer.Ordinal);
        var elements = new Dictionary<string, int>(StringComparer.Ordinal);
        var spellLevels = new HashSet<int>();
        int? cap = null;
        foreach (var e in list)
        {
            switch (e.Kind)
            {
                case ArtifactEffectKind.Immune when e.Target is { } tag:
                    immunities.Add(tag);
                    break;
                case ArtifactEffectKind.ElementPower when e.Target is { } element:
                    elements[element] = elements.GetValueOrDefault(element) + e.Value;
                    break;
                case ArtifactEffectKind.Spells when int.TryParse(e.Target, NumberStyles.Integer, CultureInfo.InvariantCulture, out var from):
                    for (var level = from; level <= e.Value; level++)
                    {
                        spellLevels.Add(level);
                    }

                    break;
                case ArtifactEffectKind.NoSpellsAbove:
                    cap = Math.Min(cap ?? int.MaxValue, e.Value);
                    break;
                case ArtifactEffectKind.Income when Enum.TryParse<ResourceType>(e.Target, true, out var resource):
                    income += ResourceSet.Of(resource, e.Value);
                    break;
                case ArtifactEffectKind.NativeTerrain when Enum.TryParse<TerrainType>(e.Target, true, out var terrain):
                    terrains.Add(terrain);
                    break;
                case ArtifactEffectKind.Skill when e.Target is { } skill:
                    skills[skill] = skills.GetValueOrDefault(skill) + e.Value;
                    break;
            }
        }

        return new ArtifactBonuses
        {
            Attack = Sum(ArtifactEffectKind.Attack),
            Defense = Sum(ArtifactEffectKind.Defense),
            SpellPower = Sum(ArtifactEffectKind.SpellPower),
            Knowledge = Sum(ArtifactEffectKind.Knowledge),
            Morale = Sum(ArtifactEffectKind.Morale),
            Luck = Sum(ArtifactEffectKind.Luck),
            MovementPercent = Sum(ArtifactEffectKind.Movement),
            MovementFlat = Sum(ArtifactEffectKind.MovementFlat),
            BattleSpeed = Sum(ArtifactEffectKind.BattleSpeed),
            MeleePercent = Sum(ArtifactEffectKind.MeleeDamage),
            RangedPercent = Sum(ArtifactEffectKind.RangedDamage),
            DamageTakenReductionPercent = Math.Min(90, Sum(ArtifactEffectKind.DamageTaken)),
            RangedDamageTakenReductionPercent = Math.Min(90, Sum(ArtifactEffectKind.RangedDamageTaken)),
            Income = income,
            TerrainPenaltyReductionPercent = Math.Min(100, Sum(ArtifactEffectKind.TerrainPenalty)),
            AllSkills = Sum(ArtifactEffectKind.AllSkills),
            NativeTerrains = terrains,
            Skills = skills,
            Sight = Sum(ArtifactEffectKind.Sight),
            ManaADay = Sum(ArtifactEffectKind.ManaADay),
            ManaMax = Sum(ArtifactEffectKind.ManaMax),
            CreatureHealthPercent = Sum(ArtifactEffectKind.CreatureHealth),
            NoRetreat = list.Any(e => e.Kind == ArtifactEffectKind.NoRetreat),
            NecromancyPercent = Sum(ArtifactEffectKind.Necromancy),
            GrowthPercent = Sum(ArtifactEffectKind.Growth),
            Immunities = immunities,
            ElementPercent = elements,
            SpellLevels = spellLevels,
            SpellLevelCap = cap,
        };
    }

    /// <summary>A short line for the hero screen: <c>+3 Attack, +1 morale</c>.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        void Add(int value, string what)
        {
            if (value != 0)
            {
                parts.Add($"{(value > 0 ? "+" : string.Empty)}{value.ToString(CultureInfo.InvariantCulture)} {what}");
            }
        }

        Add(Attack, "Attack");
        Add(Defense, "Defense");
        Add(SpellPower, "Spell Power");
        Add(Knowledge, "Knowledge");
        Add(Morale, "morale");
        Add(Luck, "luck");
        Add(MovementPercent, "% movement");
        Add(MovementFlat, "movement points");
        Add(BattleSpeed, "battle speed");
        Add(MeleePercent, "% melee damage");
        Add(RangedPercent, "% ranged damage");
        Add(DamageTakenReductionPercent, "% less damage taken");
        Add(RangedDamageTakenReductionPercent, "% less ranged damage taken");
        Add(Income.Gold, "gold a day");
        foreach (var resource in Enum.GetValues<ResourceType>().Where(r => r != ResourceType.Gold))
        {
            Add(Income[resource], $"{resource.ToString().ToLowerInvariant()} a day");
        }

        Add(AllSkills, "to every skill");
        Add(Sight, "sight radius");
        Add(ManaADay, "mana a day");
        Add(ManaMax, "maximum mana");
        Add(CreatureHealthPercent, "% creature health");
        Add(NecromancyPercent, "% Necromancy");
        Add(GrowthPercent, "% creature growth");
        foreach (var (element, percent) in ElementPercent.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            Add(percent, $"% {element} spells");
        }

        parts.AddRange(Immunities.Order(StringComparer.Ordinal).Select(tag => $"immune to {tag} spells"));
        if (SpellLevels.Count > 0)
        {
            parts.Add($"knows every level {SpellLevels.Min()}–{SpellLevels.Max()} spell");
        }

        if (SpellLevelCap is { } cap)
        {
            parts.Add($"no spells above level {cap} in its battles");
        }

        if (NoRetreat)
        {
            parts.Add("no retreat or surrender in its battles");
        }

        return parts.Count == 0 ? "no bonuses" : string.Join(", ", parts);
    }
}
