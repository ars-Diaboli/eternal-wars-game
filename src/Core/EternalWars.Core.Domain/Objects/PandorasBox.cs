using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// What a Pandora's Box holds (ADR-100, MAP-35): HoMM3's contents, gains only. Morale and luck last until the hero's next battle; a skill is
/// raised to at least its level. Spells come with Phase 5.
/// </summary>
public sealed record BoxContents
{
    public ResourceSet Resources { get; init; }

    public int Experience { get; init; }

    public int Mana { get; init; }

    public int Morale { get; init; }

    public int Luck { get; init; }

    public int Attack { get; init; }

    public int Defense { get; init; }

    public int SpellPower { get; init; }

    public int Knowledge { get; init; }

    /// <summary>Skill id → the level the hero gets it at, at least.</summary>
    public IReadOnlyDictionary<string, SkillLevel> Skills { get; init; } = new Dictionary<string, SkillLevel>();

    public IReadOnlyList<string> Artifacts { get; init; } = [];

    public IReadOnlyList<CreatureStack> Creatures { get; init; } = [];

    public static BoxContents Empty { get; } = new();

    public bool IsEmpty =>
        Resources.IsEmpty && Experience == 0 && Mana == 0 && Morale == 0 && Luck == 0 && Attack == 0 && Defense == 0 && SpellPower == 0 && Knowledge == 0
        && Skills.Count == 0 && Artifacts.Count == 0 && Creatures.Count == 0;

    public int Stat(PrimaryStat stat) => stat switch
    {
        PrimaryStat.Attack => Attack,
        PrimaryStat.Defense => Defense,
        PrimaryStat.SpellPower => SpellPower,
        _ => Knowledge,
    };

    /// <summary>Why the contents are not valid map data (a loss, an unknown skill, a skill above its maximum, an empty stack), or null.</summary>
    public string? Problem()
    {
        if (Enum.GetValues<ResourceType>().Any(r => Resources[r] < 0)
            || new[] { Experience, Mana, Morale, Luck, Attack, Defense, SpellPower, Knowledge }.Any(v => v < 0))
        {
            return "holds a loss; a box only gives.";
        }

        if (Morale > PandorasBoxRules.MaxMoraleOrLuck || Luck > PandorasBoxRules.MaxMoraleOrLuck)
        {
            return $"gives more than +{PandorasBoxRules.MaxMoraleOrLuck} morale or luck.";
        }

        foreach (var (skill, level) in Skills)
        {
            if (!BuiltInSkills.Contains(skill))
            {
                return $"names the unknown skill '{skill}'.";
            }

            if (level < SkillLevel.Basic || level > BuiltInSkills.Get(skill).MaxLevel)
            {
                return $"gives {BuiltInSkills.Get(skill).Name} at a level it does not have.";
            }
        }

        if (Creatures.Count > Army.SlotCount || Creatures.Any(s => s.Count < 1))
        {
            return $"holds an empty stack or more than {Army.SlotCount} stacks.";
        }

        return null;
    }

    public bool Equals(BoxContents? other) =>
        other is not null && ToString() == other.ToString();

    public override int GetHashCode() => ToString().GetHashCode(StringComparison.Ordinal);

    /// <summary>Every field, in a fixed order (the state digest and equality).</summary>
    public override string ToString() =>
        $"{Resources} xp {Experience} mana {Mana} morale {Morale} luck {Luck} stats {Attack}/{Defense}/{SpellPower}/{Knowledge} "
        + $"skills {string.Join(',', Skills.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => $"{s.Key}={s.Value}"))} "
        + $"artifacts {string.Join(',', Artifacts)} creatures {string.Join(',', Creatures)}";
}

/// <summary>
/// A Pandora's Box (ADR-100, MAP-35): a visiting hero is asked whether to open it. An optional <see cref="Guard"/> is fought first, and the box
/// opens only when the hero wins (a lost or fled battle leaves the guard whole). Opening gives the <see cref="Contents"/> and an optional
/// <see cref="Message"/>, and the box vanishes; then its hero <see cref="Event"/>, if it names one, runs for the hero (map events, step 3). The
/// contents are hidden from the players until then.
/// </summary>
public sealed class PandorasBox(
    MapObjectId id, MapObjectDefinition definition, MapPosition position,
    BoxContents? contents = null, IReadOnlyList<CreatureStack>? guard = null, string? message = null, string? @event = null)
    : MapObject(id, definition, position, MapObjectKind.PandorasBox), IBattleGuard
{
    /// <inheritdoc/>
    /// <remarks>A lost or fled battle leaves the guard whole.</remarks>
    void IBattleGuard.KeepSurvivors(BattleGuardOutcome outcome) => outcome.Events.Publish(new MapObjectChanged(Id));

    /// <inheritdoc/>
    /// <remarks>Winning opens the box for the hero.</remarks>
    void IBattleGuard.Defeated(BattleGuardOutcome outcome, Hero? winner)
    {
        if (winner is not null)
        {
            outcome.Boxes.Open(winner, this);
        }
    }

    /// <inheritdoc/>
    internal override string DigestState() => $"contents {Contents} guard {string.Join(',', Guard)} message {Message}{(Event is null ? "" : $" event {Event}")}";

    /// <summary>The hero event run for the opener once the contents are given (null: none); a box with an event may hold nothing else.</summary>
    public string? Event { get; internal set; } = @event;

    public BoxContents Contents { get; internal set; } = contents ?? BoxContents.Empty;

    /// <summary>The guard fought after "open?"; at most <see cref="Army.SlotCount"/> stacks, empty for none.</summary>
    public IReadOnlyList<CreatureStack> Guard { get; internal set; } = guard ?? [];

    /// <summary>Shown when the box opens; null for none.</summary>
    public string? Message { get; internal set; } = message;

    public bool IsGuarded => Guard.Count > 0;
}

public static class PandorasBoxRules
{
    /// <summary>The most morale or luck a box gives (HoMM3's +3).</summary>
    public const int MaxMoraleOrLuck = 3;

}
