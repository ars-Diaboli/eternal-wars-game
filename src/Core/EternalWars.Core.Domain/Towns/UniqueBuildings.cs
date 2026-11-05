using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Towns;

/// <summary>What a unique building gives a visiting hero (ADR-126, ADR-138).</summary>
public enum UniqueBonus
{
    /// <summary>As the map's Stables: +400 movement now and on every day until the week ends; once a week per hero.</summary>
    Stables,

    /// <summary>+1 Attack, once per hero.</summary>
    Attack,

    /// <summary>+1 Defense, once per hero.</summary>
    Defense,

    /// <summary>+1 Knowledge, once per hero.</summary>
    Knowledge,

    /// <summary>+1 Spell Power, once per hero.</summary>
    SpellPower,

    /// <summary>+1000 experience, once per hero.</summary>
    Experience,

    /// <summary>Mana + 75 % of the hero's maximum, above it until spent (to at most twice the maximum, as the Mana Vortex); once a week per hero.</summary>
    ManaSpring,
}

/// <summary>
/// What a town's buildings add to its defending hero in a siege of that town (ADR-126): a hero on its entrance or in its garrison. Knowledge lends
/// <see cref="ManaRules.ManaPerKnowledge"/> mana a point for the battle, spent first and gone afterwards. <see cref="Initiative"/> (the Grail
/// building, ADR-130) goes to every defending stack, with or without a hero.
/// </summary>
public sealed record SiegeBonus(int Attack = 0, int Defense = 0, int Knowledge = 0, int SpellPower = 0, int Morale = 0, int Luck = 0, int Initiative = 0)
{
    public static SiegeBonus None { get; } = new();

    public bool IsNone => this == None;

    public static SiegeBonus operator +(SiegeBonus a, SiegeBonus b) =>
        new(a.Attack + b.Attack, a.Defense + b.Defense, a.Knowledge + b.Knowledge, a.SpellPower + b.SpellPower, a.Morale + b.Morale, a.Luck + b.Luck,
            a.Initiative + b.Initiative);
}

/// <summary>One town's unique building: its id in <c>buildings.csv</c>, its town, the visit bonus and the siege bonus.</summary>
public sealed record UniqueBuilding(string Id, string Town, UniqueBonus Bonus, SiegeBonus Siege)
{
    /// <summary>How often a hero takes the bonus here: once a week (Stables, Mana Spring) or once ever.</summary>
    public VisitScope Scope => Bonus is UniqueBonus.Stables or UniqueBonus.ManaSpring ? VisitScope.WeeklyPerHero : VisitScope.OncePerHero;
}

/// <summary>
/// The first set of unique buildings (ADR-126, ADR-138), one per town beside the Necromancy Amplifier. Names, costs and requirements are rows of
/// <c>buildings.csv</c>; the effects are here. An own hero visiting the town (entering it, hired there, standing in it at the start of the turn,
/// or there when the building is finished) takes the bonus once per town, or once a week per town; the record is the town's
/// (<see cref="MapObject.Visits"/>) and stays with it when the town is captured.
/// </summary>
public static class UniqueBuildings
{
    public const string Stables = "stables";
    public const string ProvingRing = "proving-ring";
    public const string ShellWall = "shell-wall";
    public const string HallOfRecords = "hall-of-records";
    public const string AltarOfBrimstone = "altar-of-brimstone";
    public const string ElderGrove = "elder-grove";
    public const string ManaSpring = "mana-spring";

    /// <summary>The Elder Grove's experience.</summary>
    public const int ExperienceBonus = 1000;

    /// <summary>The Mana Spring's share of the hero's maximum mana.</summary>
    public const int ManaSpringPercent = 75;

    public static IReadOnlyList<UniqueBuilding> All { get; } =
    [
        new(Stables, TownIds.Haven, UniqueBonus.Stables, SiegeBonus.None),
        new(ProvingRing, TownIds.Stronghold, UniqueBonus.Attack, new(Attack: 1)),
        new(ShellWall, TownIds.SwampBestiary, UniqueBonus.Defense, new(Defense: 1)),
        new(HallOfRecords, TownIds.Academy, UniqueBonus.Knowledge, new(Knowledge: 1)),
        new(AltarOfBrimstone, TownIds.Inferno, UniqueBonus.SpellPower, new(SpellPower: 1)),
        new(ElderGrove, TownIds.Sylvan, UniqueBonus.Experience, new(Luck: 1)),
        new(ManaSpring, TownIds.Dungeon, UniqueBonus.ManaSpring, new(Morale: 1)),
    ];

    /// <summary>The unique building with this id, or null.</summary>
    public static UniqueBuilding? Get(string building) => All.FirstOrDefault(b => b.Id == building);

    /// <summary>The town's built unique buildings, in table order.</summary>
    public static IEnumerable<UniqueBuilding> Built(Town town) => All.Where(b => town.Level(b.Id) > 0);

    /// <summary>The Grail building's siege bonus (ADR-130): +2 Initiative for every defending stack, +5 Attack, Defense, Spell Power and Knowledge for the hero.</summary>
    public static SiegeBonus GrailSiege { get; } = new(Attack: 5, Defense: 5, Knowledge: 5, SpellPower: 5, Initiative: 2);

    /// <summary>What the town's buildings (its unique buildings and the Grail building) add to its defenders in a siege; <see cref="SiegeBonus.None"/> without any.</summary>
    public static SiegeBonus SiegeOf(Town town) =>
        Built(town).Aggregate(town.Level(BuiltInBuildings.Grail) > 0 ? GrailSiege : SiegeBonus.None, (sum, b) => sum + b.Siege);

    /// <summary>Whether <paramref name="hero"/> may take the building's bonus in <paramref name="town"/> today.</summary>
    public static bool CanTake(GameState state, Town town, UniqueBuilding building, Hero hero) =>
        town.Owner == hero.Owner && town.Level(building.Id) > 0 && town.Visits.CanCollect(building.Scope, hero.Id, state.Date);

    /// <summary>An own hero in the town takes the bonuses of its built unique buildings that it may take today.</summary>
    internal static void Visit(GameState state, IEventPublisher events, Hero hero, Town town)
    {
        foreach (var building in Built(town).Where(b => CanTake(state, town, b, hero)).ToList())
        {
            town.Visits.Record(hero.Id, state.Date);
            events.Publish(new UniqueBuildingVisited(hero.Id, town.Id, building.Id));
            Give(state, events, hero, building.Bonus);
        }
    }

    private static void Give(GameState state, IEventPublisher events, Hero hero, UniqueBonus bonus)
    {
        var progression = new ProgressionService(state, events);
        switch (bonus)
        {
            case UniqueBonus.Stables:
                // The same status as the map's Stables, so the two do not add up (ADR-126: without the free Lancer upgrade).
                progression.GrantStatus(hero, new HeroStatus(BuiltInMapObjects.Stables, HeroStatusKind.Movement, BuffRules.StablesMovement, StatusExpiry.EndOfWeek));
                hero.MovementPoints += BuffRules.StablesMovement;
                events.Publish(new HeroMovementGained(hero.Id, BuffRules.StablesMovement, hero.MovementPoints));
                break;
            case UniqueBonus.Attack:
                progression.RaiseStat(hero, PrimaryStat.Attack, 1);
                break;
            case UniqueBonus.Defense:
                progression.RaiseStat(hero, PrimaryStat.Defense, 1);
                break;
            case UniqueBonus.Knowledge:
                progression.RaiseStat(hero, PrimaryStat.Knowledge, 1);
                break;
            case UniqueBonus.SpellPower:
                progression.RaiseStat(hero, PrimaryStat.SpellPower, 1);
                break;
            case UniqueBonus.Experience:
                progression.GrantExperience(hero, ExperienceBonus);
                break;
            case UniqueBonus.ManaSpring:
                var before = hero.Mana;
                hero.Mana = before + (hero.MaxMana * ManaSpringPercent / 100);
                if (hero.Mana > before)
                {
                    events.Publish(new HeroManaRestored(hero.Id, hero.Mana, hero.MaxMana));
                }

                break;
        }
    }
}
