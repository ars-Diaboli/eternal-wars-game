using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
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
        Pay(player, level.Cost);
        town.SetLevel(building, level.Level);
        town.LastBuilt = state.Date;
        events.Publish(new TownBuilt(town.Id, building, level.Level));
    }

    internal void BuildDwelling(PlayerId player, MapObjectId townId, string line)
    {
        var town = OwnTown(player, townId);
        Refuse(TownRules.CheckDwelling(state, town, line));

        var definition = state.Creatures.Get(line);
        var level = TownRules.NextDwellingLevel(state, town, definition)!;
        Pay(player, level.Cost);
        town.SetDwelling(line, level.Level);
        town.LastBuilt = state.Date;
        if (level.Level == 1)
        {
            // HoMM3: a new dwelling opens with its first week of creatures.
            town.SetAvailable(line, town.AvailableOf(line) + definition.WeeklyGrowth);
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

        Pay(player, TownRules.RecruitCost(state.Creatures.Get(creature), count));
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

        Pay(player, TownRules.UpgradeCost(state, line, stack.Count));
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

    /// <summary>Monday: every built dwelling adds its creature's weekly growth to what is waiting (owned or not).</summary>
    internal void GrowWeekly()
    {
        foreach (var town in state.ObjectsOf<Town>().OrderBy(t => t.Id.Value))
        {
            var lines = TownRules.Lines(state, town).Where(l => town.DwellingLevel(l.Id) > 0).ToList();
            if (lines.Count == 0)
            {
                continue;
            }

            foreach (var line in lines)
            {
                town.SetAvailable(line.Id, town.AvailableOf(line.Id) + line.WeeklyGrowth);
            }

            events.Publish(new TownCreaturesGrew(town.Id, new Dictionary<string, int>(town.Available, StringComparer.Ordinal)));
        }
    }

    /// <summary>A town falls to <paramref name="owner"/>: an empty garrison was walked into, or the garrison was beaten.</summary>
    internal static void Capture(IEventPublisher events, Town town, PlayerId owner)
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
        events.Publish(new TownOwnerChanged(town.Id, previous, owner));
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
