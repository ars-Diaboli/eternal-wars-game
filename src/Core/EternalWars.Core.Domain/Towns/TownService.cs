using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Towns;

/// <summary>Which army of a town a stack command means: the garrison or the own hero standing on the entrance.</summary>
public enum TownArmy
{
    Garrison,
    VisitingHero,
}

/// <summary>
/// Towns on the owner's turn (ADR-046, HoMM3-style): one building a day, dwellings by the 7-of-9 rule (ADR-030),
/// recruiting into the garrison or the visiting hero, stack upgrades for the price difference, moving stacks between
/// garrison and visiting hero, and the weekly growth. Capturing happens on visits (<c>MapObjectInteractionService</c>)
/// and after garrison battles (<c>BattleService</c>).
/// </summary>
public sealed class TownService(GameState state, IEventPublisher events, EconomyService economy)
{
    internal void Build(PlayerId player, MapObjectId townId, string building)
    {
        var town = OwnTown(player, townId);
        Refuse(TownRules.CheckBuild(state, town, building));

        var level = TownRules.NextLevel(state, town, building)!;
        Pay(player, TownRules.BuildCost(state, town, level));
        town.SetLevel(building, level.Level);
        town.LastBuilt = state.Date;
        events.Publish(new TownBuilt(town.Id, building, level.Level));
        if (building == BuiltInBuildings.MageGuild)
        {
            Magic.MagicRules.VisitBy(state, events, town);
        }
    }

    internal void BuildDwelling(PlayerId player, MapObjectId townId, string line)
    {
        var town = OwnTown(player, townId);
        Refuse(TownRules.CheckDwelling(state, town, line));

        var definition = state.Creatures.Get(line);
        var level = TownRules.NextDwellingLevel(state, town, definition)!;
        Pay(player, TownRules.BuildCost(state, town, level));
        town.SetDwelling(line, level.Level);
        town.LastBuilt = state.Date;
        if (level.Level == 1)
        {
            // HoMM3: a new dwelling opens with its first week of creatures, fort bonus included (ADR-066).
            town.SetAvailable(line, town.AvailableOf(line) + TownRules.WeeklyGrowth(town, definition));
        }

        events.Publish(new DwellingBuilt(town.Id, line, level.Level, town.AvailableOf(line)));
    }

    internal void Recruit(PlayerId player, MapObjectId townId, string creature, int count, HeroId? heroId)
    {
        var town = OwnTown(player, townId);
        var line = TownRules.LineOf(state, town, creature) ?? throw new InvalidOperationException($"{town.Name} does not recruit '{creature}'.");
        var upgraded = creature != line.Id;
        if (town.DwellingLevel(line.Id) < (upgraded ? 2 : 1))
        {
            throw new InvalidOperationException(upgraded ? $"The {line.Name} dwelling is not upgraded." : $"{town.Name} has no {line.Name} dwelling.");
        }

        if (count < 1 || count > town.AvailableOf(line.Id))
        {
            throw new InvalidOperationException($"Only {town.AvailableOf(line.Id)} {line.Name} can be recruited.");
        }

        var (army, hero) = ArmyOf(town, heroId is null ? TownArmy.Garrison : TownArmy.VisitingHero, heroId);
        if (!army.CanAccept(creature))
        {
            throw new InvalidOperationException("There is no free slot for the recruits.");
        }

        Pay(player, TownRules.RecruitCost(state, town, state.Creatures.Get(creature), count));
        town.SetAvailable(line.Id, town.AvailableOf(line.Id) - count);
        army.TryAdd(new CreatureStack(creature, count));
        events.Publish(new CreaturesRecruited(town.Id, creature, count, hero?.Id, town.AvailableOf(line.Id)));
        PublishArmy(town, hero);
    }

    internal void UpgradeStack(PlayerId player, MapObjectId townId, TownArmy which, int slot)
    {
        var town = OwnTown(player, townId);
        var (army, hero) = ArmyOf(town, which, null);
        var stack = (Army.IsValidSlot(slot) ? army[slot] : null) ?? throw new InvalidOperationException("The slot is empty.");
        var line = TownRules.Lines(state, town).FirstOrDefault(l => l.Id == stack.Creature && l.UpgradesTo is not null)
                   ?? throw new InvalidOperationException($"{town.Name} cannot upgrade {stack.Creature}.");
        if (town.DwellingLevel(line.Id) < 2)
        {
            throw new InvalidOperationException($"The {line.Name} dwelling is not upgraded.");
        }

        Pay(player, TownRules.UpgradeCost(state, town, line, stack.Count));
        army.Set(slot, new CreatureStack(line.UpgradesTo!, stack.Count));
        events.Publish(new StackUpgraded(town.Id, hero?.Id, slot, line.Id, line.UpgradesTo!, stack.Count));
        PublishArmy(town, hero);
    }

    internal void MoveStack(PlayerId player, MapObjectId townId, TownArmy from, int fromSlot, TownArmy to, int toSlot, int? count)
    {
        var town = OwnTown(player, townId);
        var (source, sourceHero) = ArmyOf(town, from, null);
        var (target, targetHero) = ArmyOf(town, to, null);
        ArmyService.Transfer(source, fromSlot, target, toSlot, count, keepOneStack: from == TownArmy.VisitingHero && to != from);

        PublishArmy(town, sourceHero);
        if (to != from)
        {
            PublishArmy(town, targetHero);
        }
    }

    /// <summary>The visiting hero buys a war machine at the town's blacksmith (ADR-056).</summary>
    internal void BuyWarMachine(PlayerId player, MapObjectId townId, Battles.WarMachineSlot slot)
    {
        var town = OwnTown(player, townId);
        Refuse(TownRules.CheckBuyWarMachine(state, town, slot));

        var hero = TownRules.VisitingHero(state, town)!;
        Pay(player, Battles.WarMachines.Cost(slot));
        hero.AddWarMachine(slot);
        events.Publish(new WarMachineBought(town.Id, hero.Id, slot));
    }

    /// <summary>The visiting hero buys a spell book in the town's mage guild and learns what the guild teaches it (ADR-071, ADR-110).</summary>
    internal void BuySpellBook(PlayerId player, MapObjectId townId)
    {
        var town = OwnTown(player, townId);
        Refuse(Magic.MagicRules.CheckBuySpellBook(state, town));

        var hero = TownRules.VisitingHero(state, town)!;
        Pay(player, Magic.MagicRules.SpellBookCost);
        hero.HasSpellBook = true;
        events.Publish(new SpellBookBought(town.Id, hero.Id));
        Magic.MagicRules.Visit(state, events, hero, town);
    }

    /// <summary>The owner appoints one of its heroes governor of the town; the hero leaves any other town it governed.</summary>
    internal void AppointGovernor(PlayerId player, MapObjectId townId, HeroId heroId)
    {
        var town = OwnTown(player, townId);
        var hero = state.Heroes.FirstOrDefault(h => h.Id == heroId && h.Owner == player) ?? throw new InvalidOperationException($"{heroId} is not an own hero.");
        Refuse(StewardshipRules.CheckAppoint(state, town, hero));
        SetGovernor(state, events, town, hero);
    }

    /// <summary>The owner dismisses the town's governor.</summary>
    internal void DismissGovernor(PlayerId player, MapObjectId townId)
    {
        var town = OwnTown(player, townId);
        if (StewardshipRules.GovernorOf(state, town) is null)
        {
            throw new InvalidOperationException($"{town.Name} has no governor.");
        }

        SetGovernor(state, events, town, null);
    }

    /// <summary>A visiting hero with Stewardship takes over a town without a governor, unless it governs another town.</summary>
    internal static void AutoAppoint(GameState state, IEventPublisher events, Town town, Hero hero)
    {
        if (town.Owner == hero.Owner && StewardshipRules.GovernorOf(state, town) is null
            && StewardshipRules.CheckAppoint(state, town, hero) is null && StewardshipRules.TownGovernedBy(state, hero) is null)
        {
            SetGovernor(state, events, town, hero);
        }
    }

    /// <summary>Start of the owner's turn: heroes already standing in a town without a governor are appointed, and a night in a town with a mage guild refills their mana.</summary>
    internal void AppointVisitors(PlayerId player)
    {
        foreach (var town in state.ObjectsOf<Town>().Where(t => t.Owner == player).OrderBy(t => t.Id.Value))
        {
            if (TownRules.VisitingHero(state, town) is { } hero)
            {
                AutoAppoint(state, events, town, hero);
                Magic.MagicRules.Visit(state, events, hero, town, night: true);
            }
        }
    }

    private static void SetGovernor(GameState state, IEventPublisher events, Town town, Hero? hero)
    {
        if (hero is not null && StewardshipRules.TownGovernedBy(state, hero) is { } other && other != town)
        {
            other.Governor = null;
            events.Publish(new GovernorChanged(other.Id, hero.Id, null));
        }

        var previous = StewardshipRules.GovernorOf(state, town)?.Id;
        town.Governor = hero?.Id;
        events.Publish(new GovernorChanged(town.Id, previous, hero?.Id));
    }

    internal void HireHero(PlayerId player, MapObjectId townId, string heroId)
    {
        var town = OwnTown(player, townId);
        Refuse(TavernRules.CheckHire(state, town, heroId));

        Pay(player, TavernRules.HeroCost);
        var definition = state.HeroRoster.Get(heroId);
        Hero hero;
        if (TavernRules.Returning(state, player, heroId) is { } returning)
        {
            // Back as it left (ADR-081): level, skills, artifacts, machines, and the army a surrender kept.
            state.Unretire(returning);
            hero = returning;
            hero.Position = town.VisitPosition;
        }
        else
        {
            hero = Hero.Create(state.NextUnusedHeroId(), player, town.VisitPosition, null, definition);
        }

        if (hero.Army.IsEmpty && TavernRules.StarterStack(state, definition) is { } starter)
        {
            hero.Army.TryAdd(starter);
        }

        hero.MaxMovementPoints = MovementAllowance.Daily(hero, state.Creatures);
        hero.MovementPoints = hero.MaxMovementPoints;
        state.AddHero(hero);
        events.Publish(new HeroHired(town.Id, hero.Id, heroId));
        events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
        Magic.MagicRules.Visit(state, events, hero, town);
    }

    /// <summary>
    /// Monday: every built dwelling, in towns and on the map, adds its creature's weekly growth to what is waiting (owned
    /// or not); town dwellings add the fort bonus too (<see cref="TownRules.WeeklyGrowth"/>, ADR-066), map dwellings do not.
    /// The week's event doubles a line's growth or stops all growth (<see cref="Turns.WeekEvent.Growth"/>, ADR-073).
    /// </summary>
    internal void GrowWeekly()
    {
        var week = state.WeekEvent;
        if (week.StopsGrowth)
        {
            return;
        }

        foreach (var dwelling in state.ObjectsOf<CreatureDwelling>().OrderBy(d => d.Id.Value))
        {
            dwelling.Available = (dwelling.Available ?? 0) + week.Growth(state.Creatures, dwelling.Creature, state.Creatures.Get(dwelling.Creature).WeeklyGrowth);
            events.Publish(new DwellingCreaturesGrew(dwelling.Id, dwelling.Available.Value));
        }

        foreach (var town in state.ObjectsOf<Town>().OrderBy(t => t.Id.Value))
        {
            var lines = TownRules.Lines(state, town).Where(l => town.DwellingLevel(l.Id) > 0).ToList();
            if (lines.Count == 0)
            {
                continue;
            }

            foreach (var line in lines)
            {
                town.SetAvailable(line.Id, town.AvailableOf(line.Id) + week.Growth(state.Creatures, line.Id, TownRules.WeeklyGrowth(town, line)));
            }

            events.Publish(new TownCreaturesGrew(town.Id, new Dictionary<string, int>(town.Available, StringComparer.Ordinal)));
        }
    }

    /// <summary>A plague week (ADR-073): the creatures waiting in every town's dwellings are halved, rounded down; map dwellings keep theirs.</summary>
    internal void ApplyPlague()
    {
        if (state.WeekEvent.Kind != Turns.WeekEventKind.Plague)
        {
            return;
        }

        foreach (var town in state.ObjectsOf<Town>().Where(t => t.Available.Values.Any(v => v > 0)).OrderBy(t => t.Id.Value))
        {
            foreach (var (line, count) in town.Available.ToList())
            {
                town.SetAvailable(line, count / 2);
            }

            events.Publish(new TownCreaturesGrew(town.Id, new Dictionary<string, int>(town.Available, StringComparer.Ordinal)));
        }
    }

    /// <summary>Game start: a map dwelling whose waiting creatures the map did not set opens with one week of them.</summary>
    internal void FillNewDwellings()
    {
        foreach (var dwelling in state.ObjectsOf<CreatureDwelling>().Where(d => d.Available is null).OrderBy(d => d.Id.Value))
        {
            dwelling.Available = state.Creatures.Get(dwelling.Creature).WeeklyGrowth;
        }
    }

    /// <summary>
    /// A town falls to <paramref name="owner"/>: an empty garrison was walked into, or the garrison was beaten. A Capitol drops to
    /// town hall 3 when the new owner already has one (ADR-070, TOWN-3).
    /// </summary>
    internal static void Capture(GameState state, IEventPublisher events, Town town, PlayerId owner)
    {
        if (!town.Garrison.IsEmpty)
        {
            for (var slot = 0; slot < Army.SlotCount; slot++)
            {
                town.Garrison.Set(slot, null);
            }

            events.Publish(new GarrisonChanged(town.Id, town.Garrison.Slots.ToList()));
        }

        var previous = town.Owner;
        town.Owner = owner;
        town.Governor = null;
        events.Publish(new TownOwnerChanged(town.Id, previous, owner));

        if (town.Level(BuiltInBuildings.TownHall) >= BuiltInBuildings.CapitolLevel && TownRules.CapitolOf(state, owner, except: town) is not null)
        {
            town.SetLevel(BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel - 1);
            events.Publish(new TownBuildingDowngraded(town.Id, BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel - 1));
        }
    }

    private Town OwnTown(PlayerId player, MapObjectId townId)
    {
        if (state.FindObject(townId) is not Town town)
        {
            throw new InvalidOperationException($"{townId} is not a town.");
        }

        if (town.Owner != player)
        {
            throw new InvalidOperationException($"{town.Name} does not belong to {player}.");
        }

        if (state.PendingDecision is not null)
        {
            throw new InvalidOperationException("Resolve the pending decision first.");
        }

        return town;
    }

    private (Army Army, Hero? Hero) ArmyOf(Town town, TownArmy which, HeroId? expected)
    {
        if (which == TownArmy.Garrison)
        {
            return (town.Garrison, null);
        }

        var hero = TownRules.VisitingHero(state, town);
        if (hero is null || (expected is { } id && hero.Id != id))
        {
            throw new InvalidOperationException($"No own hero is visiting {town.Name}.");
        }

        return (hero.Army, hero);
    }

    private void Pay(PlayerId player, ResourceSet cost)
    {
        if (!economy.TrySpend(player, cost))
        {
            throw new InvalidOperationException("Not enough resources.");
        }
    }

    private void PublishArmy(Town town, Hero? hero)
    {
        if (hero is null)
        {
            events.Publish(new GarrisonChanged(town.Id, town.Garrison.Slots.ToList()));
        }
        else
        {
            events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
        }
    }

    private static void Refuse(string? refusal)
    {
        if (refusal is not null)
        {
            throw new InvalidOperationException(refusal);
        }
    }
}
