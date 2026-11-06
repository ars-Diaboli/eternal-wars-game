using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Visibility;

namespace EternalWars.Core.Domain.Magic;

/// <summary>
/// What Scouting tells of a hero besides its army (ADR-125): from Basic its level and primary stats (with what its artifacts add), from Advanced
/// its war machines, from Expert its skills and known spells; null lists are not shown.
/// </summary>
public sealed record ScoutedHero(
    int Level, int Attack, int Defense, int SpellPower, int Knowledge, IReadOnlyList<WarMachineSlot>? WarMachines,
    IReadOnlyDictionary<string, SkillLevel>? Skills, IReadOnlyList<string>? Spells);

/// <summary>
/// How a player is shown the armies it does not own on the adventure map (ADR-125): every count as an amount word with its range unless the
/// Scouting of one of the player's heroes that sees the army shows more. Basic: neutral stacks' and guards' counts, a hero's level and primary
/// stats; Advanced: enemy heroes' armies and town garrisons, a hero's artifacts and war machines; Expert: a hero's skills and spells, a town's
/// available creatures. Veil I hides counts from Scouting below Expert, Veil II the artifacts from everyone. The screens and the AI read these;
/// the information spells keep their own exact queries.
/// </summary>
public static partial class AdventureIntel
{
    /// <summary>
    /// The best Scouting among <paramref name="viewer"/>'s heroes whose sight reaches one of the cells; <see cref="SkillLevel.None"/> when none
    /// does (an army seen only by a town or a mine, or remembered under the fog).
    /// </summary>
    public static SkillLevel ScoutingAt(GameState state, PlayerId viewer, IReadOnlyCollection<MapPosition> cells) =>
        state.HeroesOf(viewer)
            .Where(h => cells.Any(c => FogOfWar.IsWithinRadius(h.Position, c, h.ScoutingRadius)))
            .Select(h => h.SkillLevelOf(BuiltInSkills.Scouting))
            .DefaultIfEmpty(SkillLevel.None).Max();

    public static SkillLevel ScoutingAt(GameState state, PlayerId viewer, MapPosition cell) => ScoutingAt(state, viewer, [cell]);

    /// <summary>
    /// A neutral or another player's army that is not a hero's (a guard, a map garrison's troops): exact for its owner, a neutral one from
    /// Basic Scouting, another player's from Advanced.
    /// </summary>
    public static IReadOnlyList<ShownStack> ScoutStacks(GameState state, IEnumerable<CreatureStack> stacks, MapPosition at, PlayerId? owner, PlayerId viewer) =>
        Stacks(stacks, hidden: owner != viewer && ScoutingAt(state, viewer, at) < (owner is null ? SkillLevel.Basic : SkillLevel.Advanced));

    /// <summary>A neutral map stack as Scouting shows it: its count from Basic, else its amount.</summary>
    public static ShownArmy ScoutMonster(GameState state, MonsterGuard monster, PlayerId viewer)
    {
        var name = state.Creatures.TryGet(monster.Creature, out var creature) ? creature.Name : monster.Creature;
        var stacks = monster.Count is { } count ? [Shown(monster.Creature, count, ScoutingAt(state, viewer, monster.Position) < SkillLevel.Basic)] : Array.Empty<ShownStack>();
        return new ShownArmy(monster.Position, name, null, null, monster.Id, stacks, [], false, false);
    }

    /// <summary>
    /// Another player's hero as <paramref name="viewer"/> is shown it (its own heroes in full): seen from <paramref name="at"/>, the hero's cell
    /// unless it sits in a town's garrison.
    /// </summary>
    public static ShownArmy ScoutHero(GameState state, Hero hero, PlayerId viewer, MapPosition? at = null)
    {
        var own = hero.Owner == viewer;
        var scouting = own ? SkillLevel.Expert : ScoutingAt(state, viewer, at ?? hero.Position);
        var veil = own ? 0 : AdventureMagic.VeilRank(hero);
        var veiled = veil >= 1 && scouting is >= SkillLevel.Advanced and < SkillLevel.Expert;
        var countsShown = scouting >= SkillLevel.Advanced && !veiled;
        var artifactsShown = scouting >= SkillLevel.Advanced && veil < 2;
        var details = scouting < SkillLevel.Basic
            ? null
            : new ScoutedHero(
                hero.Level, hero.TotalAttack, hero.TotalDefense, hero.TotalSpellPower, hero.TotalKnowledge,
                scouting >= SkillLevel.Advanced ? [.. hero.WarMachines.Order()] : null,
                scouting >= SkillLevel.Expert ? hero.Skills.Levels.ToDictionary(StringComparer.Ordinal) : null,
                scouting >= SkillLevel.Expert ? [.. hero.Spells.Keys] : null);
        return new ShownArmy(
            hero.Position, hero.Name, hero.Id, hero.Owner, null, Stacks(hero.Army.Stacks.Select(s => s.Stack), !countsShown),
            artifactsShown ? [.. hero.Artifacts.AllWorn.Select(w => w.Id)] : [], veiled, !artifactsShown, details);
    }

    /// <summary>
    /// A town as Scouting shows it to <paramref name="viewer"/> (its own in full): the garrison's counts from Advanced (Expert when the hero inside
    /// is veiled), the creatures waiting in its dwellings from Expert; its buildings are not shown. The heroes inside and at the gate as
    /// <see cref="ScoutHero"/> shows them.
    /// </summary>
    public static TownIntel ScoutTown(GameState state, Town town, PlayerId viewer)
    {
        var own = town.Owner == viewer;
        var cells = town.OccupiedCells().Select(c => c.Position).Append(town.VisitPosition).ToList();
        var scouting = own ? SkillLevel.Expert : ScoutingAt(state, viewer, cells);
        var veiledInside = !own && town.GarrisonHero is { } inside && inside.Owner != viewer && AdventureMagic.VeilRank(inside) >= 1;
        var garrisonShown = scouting >= (veiledInside ? SkillLevel.Expert : SkillLevel.Advanced);
        var available = scouting >= SkillLevel.Expert
            ? town.Available.Where(a => a.Value > 0).OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => Shown(a.Key, a.Value, hidden: false)).ToList()
            : null;
        return new TownIntel(
            town.Id, town.Name, town.TownType, town.Owner, null, available, Stacks(town.Garrison.Stacks.Select(s => s.Stack), !garrisonShown),
            town.GarrisonHero is { } garrisonHero ? ScoutHero(state, garrisonHero, viewer, town.VisitPosition) : null,
            state.HeroAt(town.VisitPosition) is { } visitor && visitor.Owner == town.Owner ? ScoutHero(state, visitor, viewer) : null);
    }

    /// <summary>
    /// An army as a player reads it for planning (the AI, ADR-125): the shown counts, and for each hidden one the middle of its
    /// word's range (a legion as 1000). At most <see cref="Army.SlotCount"/> stacks.
    /// </summary>
    public static Army Reckoned(IEnumerable<ShownStack> shown) =>
        Army.Of([.. shown.Take(Army.SlotCount).Select(s => new CreatureStack(s.Creature, Math.Max(1, s.Estimate)))]);
}
