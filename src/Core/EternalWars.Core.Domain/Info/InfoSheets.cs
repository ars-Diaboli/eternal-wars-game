using System.Globalization;
using System.Text.RegularExpressions;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Info;

/// <summary>
/// What a right-click information window shows (ADR-118, UI-3; ADR-154): a title, a line under it (what kind of thing it is) and the lines of
/// the window. Built by <see cref="InfoSheets"/>; the front-end only lays it out.
/// </summary>
public sealed record InfoSheet(string Title, string Kind, IReadOnlyList<string> Lines);

/// <summary>
/// The information windows (ADR-118, UI-3; ADR-154): one sheet per creature, battle stack, hero, skill, spell, artifact, building and map object,
/// as the viewing player may know it (other players' heroes and armies through Scouting, ADR-125; the map under the fog as remembered).
/// <c>detailed</c> is the "Detailed tooltips" setting: it adds the formulas and the raw numbers. Pure functions of the state.
/// </summary>
public static partial class InfoSheets
{
    // Creatures ---------------------------------------------------------------------------------------------------------------

    /// <summary>A creature type, with a known count or the amount Scouting shows (<paramref name="shown"/>) when it is one of an army's stacks.</summary>
    public static InfoSheet Creature(GameState state, string creature, bool detailed, ShownStack? shown = null)
    {
        var c = state.Creatures.Get(creature);
        var lines = new List<string>();
        if (shown is not null)
        {
            lines.Add(shown.Count is { } count ? $"Count: {N(count)}" : $"Count: {shown.Amount}");
        }

        lines.AddRange(CreatureStats(c, state.Spells));
        if (c.WeeklyGrowth > 0 || !c.Cost.IsEmpty)
        {
            lines.Add($"Cost {Cost(c.Cost)}   Growth {N(c.WeeklyGrowth)} a week");
        }

        if (c.UpgradesTo is { } up && state.Creatures.TryGet(up, out var upgraded))
        {
            lines.Add($"Upgrades to {upgraded.Name}.");
        }

        if (detailed)
        {
            lines.Add($"Map speed {N(c.MapSpeed)}   Size {c.Size} ({N(c.Size.Cells())}×{N(c.Size.Cells())} cells)"
                      + (c.Category == CreatureCategory.None ? "" : $"   Class {c.Category}") + (c.Tags.Count > 0 ? $"   Tags: {string.Join(", ", c.Tags)}" : ""));
            lines.Add($"Strength {N(c.Value)} a creature = health × average damage × (20 + Attack + Defense) / 40"
                      + (c.IsRanged ? ", ×1.5 for a shooter" : "") + (c.MovesFreely ? ", ×1.2 for a flyer" : ""));
        }

        return new InfoSheet(c.Name, $"Creature, tier {N(c.Tier)}{(c.Town is { } town ? $", {Title(town)}" : ", neutral")}", lines);
    }

    /// <summary>A stack whose count the viewer knows exactly (its own), for <see cref="Creature"/>.</summary>
    public static ShownStack Known(CreatureStack stack) => new(stack.Creature, stack.Count, stack.Count, stack.Count);

    /// <summary>A stack in the battle as it stands: its count, the top creature's health, what its hero and its spells add, its shots.</summary>
    public static InfoSheet BattleStack(BattleState battle, BattleUnit unit, bool detailed)
    {
        var c = unit.Creature;
        var lines = new List<string>();
        var attack = DamageCalculator.AttackOf(battle, unit);
        var defense = DamageCalculator.DefenseOf(battle, unit);
        lines.Add(unit.IsMachine ? "War machine" : $"Count: {N(unit.Count)}   Health of the top creature {N(unit.TopHealth)}/{N(c.Health)}");
        lines.Add($"Attack {WithBase(c.Attack, attack)}   Defense {WithBase(c.Defense, defense)}{(unit.IsDefending ? " (defending: more)" : "")}   Damage {N(c.MinDamage)}–{N(c.MaxDamage)}");
        lines.Add($"Speed {WithBase(c.Speed, battle.SpeedOf(unit))}   Initiative {WithBase(c.Initiative, battle.InitiativeOf(unit))}"
                  + (unit.IsMachine ? "" : $"   Morale {Signed(battle.MoraleOf(unit))}   Luck {Signed(battle.LuckOf(unit))}"));
        if (c.IsRanged)
        {
            lines.Add(battle.HasUnlimitedShots(unit) ? "Shots: unlimited" : $"Shots: {N(unit.ShotsLeft)} of {N(c.Shots)}");
        }

        if (battle.IsNative(unit))
        {
            lines.Add("On its native terrain: +1 Attack, Defense and Speed.");
        }

        lines.Add(unit.HasRetaliated && !c.Has(CapabilityKind.UnlimitedRetaliation) ? "Has retaliated this round." : "Can retaliate.");
        lines.AddRange(Capabilities(c, battle.Spells));
        foreach (var spell in unit.Spells)
        {
            var name = battle.Spells.TryGet(spell.Spell, out var known) ? known.Name : spell.Spell;
            var left = spell.TurnsLeft is { } turns ? $"{N(turns)} more turn{(turns == 1 ? "" : "s")}" : $"until round {N(spell.UntilRound)}";
            lines.Add($"Under {name} ({left})");
        }

        if (detailed)
        {
            lines.Add("Damage: the roll × the count; +5 % for each point of Attack over the target's Defense (at most +300 %), −2.5 % for each "
                      + "point of Defense over the Attack (at most −70 %).");
            lines.Add($"Total health {N(unit.TotalHealth)}");
        }

        return new InfoSheet(c.Name, unit.IsMachine ? "War machine" : $"Creature, tier {N(c.Tier)}", lines);
    }

    /// <summary>The creature's base numbers, as the creature screen and the recruit window show them.</summary>
    private static IEnumerable<string> CreatureStats(CreatureDefinition c, SpellCatalog spells)
    {
        yield return $"Attack {N(c.Attack)}   Defense {N(c.Defense)}   Damage {N(c.MinDamage)}–{N(c.MaxDamage)}   Health {N(c.Health)}";
        yield return $"Speed {N(c.Speed)}   Initiative {N(c.Initiative)}" + (c.IsRanged ? $"   Shots {N(c.Shots)}" : "")
                     + (c.Morale != 0 ? $"   Morale {Signed(c.Morale)}" : "") + (c.Luck != 0 ? $"   Luck {Signed(c.Luck)}" : "");
        foreach (var line in Capabilities(c, spells))
        {
            yield return line;
        }
    }

    private static IEnumerable<string> Capabilities(CreatureDefinition c, SpellCatalog spells) => c.Capabilities.Select(k => Capability(k, spells));

    /// <summary>A creature's special rule in words; a caster's spell is named from the game's spell table.</summary>
    public static string Capability(CreatureCapability capability, SpellCatalog spells) => capability.Kind switch
    {
        CapabilityKind.Flying => "Flies over everything in its way.",
        CapabilityKind.Teleporting => "Teleports to any free spot within its speed.",
        CapabilityKind.ManaThief => $"Mana thief: each of its attacks takes {N(capability.Value)} mana from the enemy hero, for its own hero.",
        CapabilityKind.DoubleStrike => "Strikes twice.",
        CapabilityKind.ShootsTwice => "Shoots twice.",
        CapabilityKind.NoEnemyRetaliation => "No retaliation against its attacks.",
        CapabilityKind.UnlimitedRetaliation => "Retaliates against every attack.",
        CapabilityKind.FirstStrike => "Retaliates before the attacker strikes.",
        CapabilityKind.RangedRetaliation => "Shoots back when shot at.",
        CapabilityKind.MeleePenalty => "Half damage in melee.",
        CapabilityKind.Breath => "Breath: also hits what stands behind the target.",
        CapabilityKind.Cleave => "Cleave: also hits up to two enemies beside it and the target.",
        CapabilityKind.MultiHead => "Attacks every enemy around it.",
        CapabilityKind.PackHunter => $"Pack hunter: +{N(capability.Value)} % damage for every other own stack fighting the target.",
        CapabilityKind.Relentless => $"Relentless: +{N(capability.Value)} % damage for every hit the target took this round.",
        CapabilityKind.SpellImmunity => capability.Parameter == SpellTags.All ? "Immune to all spells." : $"Immune to {capability.Parameter} spells.",
        CapabilityKind.SpellResistance => $"Unaffected by spells of level {N(capability.Value)} and below.",
        CapabilityKind.Caster => $"Casts {(spells.TryGet(capability.Parameter!, out var spell) ? spell.Name : capability.Parameter)} "
                                 + $"{N(capability.Value)} time{(capability.Value == 1 ? "" : "s")} a battle.",
        _ => capability.Kind.ToString(),
    };

    // Heroes ------------------------------------------------------------------------------------------------------------------

    /// <summary>A hero: one of the viewer's in full, another player's as the viewer's Scouting shows it (ADR-125).</summary>
    public static InfoSheet Hero(GameState state, Hero hero, PlayerId viewer, bool detailed)
    {
        var owner = state.GetPlayer(hero.Owner).Name;
        if (hero.Owner != viewer)
        {
            return ScoutedHero(state, AdventureIntel.ScoutHero(state, hero, viewer), owner);
        }

        var lines = new List<string>
        {
            $"Attack {N(hero.TotalAttack)}   Defense {N(hero.TotalDefense)}   Spell Power {N(hero.TotalSpellPower)}   Knowledge {N(hero.TotalKnowledge)}",
            $"Morale {Signed(hero.TotalMorale)}   Luck {Signed(hero.TotalLuck)}   Mana {N(hero.Mana)}/{N(hero.MaxMana)}   Movement {N(hero.MovementPoints)}/{N(hero.MaxMovementPoints)}",
        };
        if (hero.Specialty is { } specialty)
        {
            lines.Add($"Specialty: {specialty.Describe(state.Spells)}");
        }

        var skills = hero.Skills.Levels.Where(s => s.Value > SkillLevel.None).Select(s => $"{BuiltInSkills.Get(s.Key).Name} {s.Value}").ToList();
        lines.Add(skills.Count == 0 ? "No skills yet." : $"Skills: {string.Join(", ", skills)}");
        lines.Add($"Army: {ArmyText(state, hero.Army.Stacks.Select(s => s.Stack))}");
        if (!hero.Artifacts.IsEmpty)
        {
            lines.Add($"Artifacts: {string.Join(", ", hero.Artifacts.AllWorn.Select(w => state.Artifacts.Get(w.Id).Name))}"
                      + (hero.Artifacts.Backpack.Count > 0 ? $" (and {N(hero.Artifacts.Backpack.Count)} in the backpack)" : ""));
        }

        if (detailed)
        {
            var bonus = hero.Bonus;
            lines.Add($"Base Attack {N(hero.Attack)} {Signed(bonus.Attack)}, Defense {N(hero.Defense)} {Signed(bonus.Defense)}, Spell Power "
                      + $"{N(hero.SpellPower)} {Signed(bonus.SpellPower)}, Knowledge {N(hero.Knowledge)} {Signed(bonus.Knowledge)} (from artifacts)");
            lines.Add($"Experience {N(hero.Experience)}/{N(ExperienceTable.For(hero.Level + 1))}   Scouting radius {N(hero.ScoutingRadius)}");
        }

        return new InfoSheet(hero.Name, $"Level {N(hero.Level)} {hero.Class}, {owner}", lines);
    }

    /// <summary>A hero as an information spell or Scouting shows it (<see cref="AdventureIntel.ScoutHero"/>).</summary>
    public static InfoSheet ScoutedHero(GameState state, ShownArmy shown, string owner)
    {
        var lines = new List<string>();
        if (shown.Details is { } d)
        {
            lines.Add($"Attack {N(d.Attack)}   Defense {N(d.Defense)}   Spell Power {N(d.SpellPower)}   Knowledge {N(d.Knowledge)}");
            if (d.Skills is { } skills)
            {
                var learned = skills.Where(s => s.Value > SkillLevel.None).Select(s => $"{BuiltInSkills.Get(s.Key).Name} {s.Value}").ToList();
                lines.Add(learned.Count == 0 ? "No skills." : $"Skills: {string.Join(", ", learned)}");
            }

            if (d.Spells is { } spells)
            {
                lines.Add(spells.Count == 0 ? "No spells." : $"Spells: {string.Join(", ", spells.Select(s => state.Spells.TryGet(s, out var spell) ? spell.Name : s))}");
            }

            if (d.WarMachines is { } machines)
            {
                lines.Add($"War machines: {(machines.Count == 0 ? "none" : string.Join(", ", machines))}");
            }
        }
        else
        {
            lines.Add("Scouting shows nothing more of this hero.");
        }

        lines.Add($"Army: {ShownArmyText(state, shown.Stacks)}" + (shown.Veiled ? " (veiled)" : ""));
        lines.Add(shown.ArtifactsHidden ? "Artifacts: not seen." : shown.Artifacts.Count == 0 ? "No artifacts." : $"Artifacts: {string.Join(", ", shown.Artifacts.Select(a => state.Artifacts.Get(a).Name))}");
        return new InfoSheet(shown.Name, shown.Details is { } level ? $"Level {N(level.Level)} hero, {owner}" : $"Hero, {owner}", lines);
    }

    // Skills, spells, artifacts -----------------------------------------------------------------------------------------------

    /// <summary>A skill, with the level a hero has (<see cref="SkillLevel.None"/> for one shown on its own) and, for that hero, its specialty.</summary>
    public static InfoSheet Skill(string skill, SkillLevel level, bool detailed, Hero? hero = null, SkillLevel? learned = null)
    {
        var definition = BuiltInSkills.Get(skill);
        var group = BuiltInSkills.GroupOf(skill);
        var lines = new List<string>();
        if (level > SkillLevel.None)
        {
            lines.Add($"Level: {level} of {definition.MaxLevel}" + (learned is { } l && l != level ? $" (learned {l}, the rest from artifacts)" : ""));
        }
        else
        {
            lines.Add($"Up to {definition.MaxLevel}.");
        }

        lines.Add(definition.Description);
        if (hero is not null && SkillEffects.IsSpecialty(hero, skill))
        {
            lines.Add(hero.Specialty!.Amount == 0
                ? $"{hero.Name}'s specialty: {N(SkillEffects.SpecialtyPercentPerLevel)} % stronger for each hero level."
                : $"{hero.Name}'s specialty: +{N(hero.Specialty.Amount)}.");
        }

        if (detailed)
        {
            lines.Add($"Group {group.Name}: at most {N(SkillGroup.Points)} points in its skills.   Id {definition.Id}");
        }

        return new InfoSheet(definition.Name, $"Skill, {group.Name}", lines);
    }

    /// <summary>A spell at a rank (1–3), with the hero who would cast it: the cost and, detailed, its numbers at the hero's Spell Power.</summary>
    public static InfoSheet Spell(GameState state, string spell, int rank, bool detailed, Hero? hero = null)
    {
        var definition = state.Spells.Get(spell);
        var lines = new List<string>
        {
            $"Level {N(definition.Level)}   {(definition.IsAdventure ? "Adventure spell" : "Battle spell")}   Element {definition.Element}",
            $"Mana {N(hero is not null && definition.IsAdventure ? AdventureMagic.Cost(state, hero, spell) : definition.Mana)}",
        };
        if (definition.Notes.Length > 0)
        {
            lines.Add(definition.Notes);
        }

        if (hero is not null)
        {
            lines.Add($"{hero.Name} casts it at rank {Roman(rank)} of {Roman(MagicRules.MaxRank(hero, definition))} ({definition.School} Mastery raises it).");
        }

        if (detailed)
        {
            var power = hero?.TotalSpellPower;
            for (var r = 1; r <= SpellLevels.MaxRank; r++)
            {
                if (definition.EffectAt(r) is { } effect && (r == 1 || !ReferenceEquals(effect, definition.EffectAt(r - 1))))
                {
                    lines.Add($"Rank {Roman(r)}: {EffectText(effect, power)}");
                }
            }

            lines.Add("Amounts and lengths grow with Spell Power: × (10 + Spell Power) / 10.");
        }

        return new InfoSheet(MagicRules.NameAtRank(definition, Math.Max(1, rank)), $"Spell, {definition.School}", lines);
    }

    private static string EffectText(SpellEffect effect, int? power)
    {
        var parts = new List<string> { effect.Kind.ToString() };
        if (effect.Amount != 0)
        {
            parts.Add(power is { } p ? $"amount {N(effect.Amount)} (×(10 + {N(p)})/10 = {N((int)((long)effect.Amount * (10 + p) / 10))})" : $"amount {N(effect.Amount)}");
        }

        parts.Add($"reach {effect.Reach}");
        if (effect.AreaSize > 0)
        {
            parts.Add($"area {N(effect.AreaSize)}");
        }

        if (effect.Rounds is { } rounds)
        {
            parts.Add($"{N(rounds)} rounds");
        }

        if (effect.Targets > 1)
        {
            parts.Add($"{N(effect.Targets)} targets");
        }

        return string.Join(", ", parts);
    }

    /// <summary>An artifact: its tier and slot, what it gives, its relic and set.</summary>
    public static InfoSheet Artifact(GameState state, string artifact, bool detailed)
    {
        var a = state.Artifacts.Get(artifact);
        var effects = state.Artifacts.EffectsOf(a);
        var lines = new List<string> { ArtifactBonuses.Of(effects).Describe(state.Spells) };
        if (a.IsAssembled)
        {
            lines.Add($"Pieces: {string.Join(", ", a.Pieces.Select(p => state.Artifacts.Get(p).Name))}");
        }

        if (state.Artifacts.RelicOf(a.Id) is { } relic)
        {
            lines.Add($"A piece of {state.Artifacts.Get(relic).Name}.");
        }

        if (a.Set is { } set)
        {
            var definition = state.Artifacts.GetSet(set);
            lines.Add($"Set: {definition.Name}" + (definition.Bonus.Count > 0 ? $" (all worn: {ArtifactBonuses.Of(definition.Bonus).Describe(state.Spells)})" : ""));
        }

        if (a.Notes.Length > 0)
        {
            lines.Add(a.Notes);
        }

        if (detailed)
        {
            lines.Add($"Effects: {string.Join("; ", effects.Select(e => $"{e.Kind} {N(e.Value)}{(e.Target is { } t ? $" {t}" : "")}"))}");
        }

        return new InfoSheet(a.Name, $"Artifact, {(a.IsAssembled ? "assembled relic" : a.Tier.ToString())}, {a.Slot}", lines);
    }

    // Buildings ---------------------------------------------------------------------------------------------------------------

    /// <summary>A town building: what is built of it, and each level with its cost, what it needs and what it gives.</summary>
    public static InfoSheet Building(GameState state, Town town, string building, bool detailed)
    {
        var catalog = TownRules.Buildings(state, town);
        var built = town.Level(building);
        var max = catalog.MaxLevel(building);
        var lines = new List<string> { built == 0 ? "Not built." : built == max ? "Fully built." : $"Built: {catalog.Get(building, built).Name} ({N(built)} of {N(max)})." };
        for (var level = 1; level <= max; level++)
        {
            var row = catalog.Get(building, level);
            var parts = new List<string> { $"{(level <= built ? "✓" : "·")} {row.Name}: {Cost(level <= built ? row.Cost : TownRules.BuildCost(state, town, row))}" };
            if (row.Requires.Count > 0)
            {
                parts.Add($"needs {string.Join(", ", row.Requires.Select(r => RequirementName(catalog, r)))}");
            }

            if (!row.Income.IsEmpty)
            {
                parts.Add($"pays {Cost(row.Income)} a day");
            }

            if (row.Growth is { } growth)
            {
                parts.Add($"every tier {N(growth.Tier)} dwelling grows {N(growth.Amount)} more a week");
            }

            lines.Add(string.Join("; ", parts));
        }

        if (BuiltInBuildings.SchoolOf(building) is { } school)
        {
            lines.Add($"Adds one {school} spell for each mage guild level.");
        }

        if (detailed)
        {
            lines.Add($"Id {building}" + (catalog.Get(building, 1).Town is { } own ? $" ({own}'s own)" : ""));
        }

        var name = catalog.Get(building, Math.Max(1, built)).Name;
        return new InfoSheet(name, $"Building of {town.Name}", lines);
    }

    /// <summary>A dwelling of the town: the creature line, its growth and what waits there.</summary>
    public static InfoSheet Dwelling(GameState state, Town town, CreatureDefinition line, bool detailed)
    {
        var sheet = Creature(state, line.Id, detailed);
        var level = town.DwellingLevel(line.Id);
        var lines = new List<string>
        {
            level switch { 0 => "Dwelling not built.", 1 => "Dwelling built.", _ => "Dwelling upgraded." },
            $"{N(town.AvailableOf(line.Id))} waiting, +{N(TownRules.WeeklyGrowth(state, town, line))} a week here",
        };
        lines.AddRange(sheet.Lines);
        return sheet with { Lines = lines };
    }

    private static string RequirementName(BuildingCatalog catalog, BuildingRequirement requirement) =>
        BuiltInBuildings.DwellingTier(requirement.Building) is { } tier
            ? requirement.Level == 1 ? $"a tier {N(tier)} dwelling" : $"an upgraded tier {N(tier)} dwelling"
            : catalog.Contains(requirement.Building) ? catalog.Get(requirement.Building, requirement.Level).Name : requirement.ToString();

    // Shared ------------------------------------------------------------------------------------------------------------------

    private static string ArmyText(GameState state, IEnumerable<CreatureStack> stacks)
    {
        var list = stacks.Select(s => $"{N(s.Count)} {state.Creatures.Get(s.Creature).Name}").ToList();
        return list.Count == 0 ? "none" : string.Join(", ", list);
    }

    private static string ShownArmyText(GameState state, IEnumerable<ShownStack> stacks)
    {
        var list = stacks.Select(s => $"{(s.Count is { } count ? N(count) : s.Amount)} {state.Creatures.Get(s.Creature).Name}").ToList();
        return list.Count == 0 ? "none" : string.Join(", ", list);
    }

    internal static string Cost(ResourceSet cost)
    {
        var parts = Enum.GetValues<ResourceType>().Where(t => cost[t] != 0).Select(t => $"{N(cost[t])} {t.ToString().ToLowerInvariant()}").ToList();
        return parts.Count == 0 ? "free" : string.Join(", ", parts);
    }

    private static string WithBase(int baseValue, int value) => value == baseValue ? N(value) : $"{N(value)} ({N(baseValue)} {(value > baseValue ? "+" : "−")} {N(Math.Abs(value - baseValue))})";

    internal static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

    internal static string Signed(int value) => value > 0 ? $"+{N(value)}" : N(value);

    private static string Roman(int rank) => rank switch { 1 => "I", 2 => "II", 3 => "III", _ => N(rank) };

    /// <summary>"haven" → "Haven", "ResourceProvider" → "Resource provider".</summary>
    internal static string Title(string id)
    {
        var words = SplitWords().Replace(id.Replace('-', ' ').Replace('_', ' '), " $1").Trim().ToLowerInvariant();
        return words.Length == 0 ? id : char.ToUpperInvariant(words[0]) + words[1..];
    }

    [GeneratedRegex("(?<=[a-z])([A-Z])")]
    private static partial Regex SplitWords();
}
