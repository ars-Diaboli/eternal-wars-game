using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
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
/// garrison and visiting hero, the weekly growth, and the guild spells drawn at game start. Hero arrivals and captures are
/// <see cref="TownVisitService"/>'s.
/// </summary>
public sealed class TownService(GameState state, IEventPublisher events, EconomyService economy, TownVisitService visits, HeroRecruitment recruits)
    : Turns.ITurnPhaseParticipant
{
    private readonly TownConstruction _construction = new(state, events, visits);

    void Turns.ITurnPhaseParticipant.StockFirstWeek() => FillNewDwellings();

    void Turns.ITurnPhaseParticipant.ApplyWeekEvent() => ApplyPlague();

    void Turns.ITurnPhaseParticipant.StockWeek() => GrowWeekly();

    void Turns.ITurnPhaseParticipant.PrepareTurn(PlayerId player) => AppointVisitors(player);

    internal void Build(PlayerId player, MapObjectId townId, Common.BuildingId building)
    {
        var town = OwnTown(player, townId);
        Refuse(TownRules.CheckBuild(state, town, building));

        var level = TownRules.NextLevel(state, town, building)!;
        Pay(player, TownRules.BuildCost(state, town, level));
        town.LastBuilt = state.Date;
        town.LastBuiltName = level.Name;
        town.LastBuiltPlot = building.Value;
        _construction.Raise(town, building, level.Level);
    }

    internal void BuildDwelling(PlayerId player, MapObjectId townId, Common.CreatureId line)
    {
        var town = OwnTown(player, townId);
        Refuse(TownRules.CheckDwelling(state, town, line));

        var definition = state.Creatures.Get(line);
        var level = TownRules.NextDwellingLevel(state, town, definition)!;
        Pay(player, TownRules.BuildCost(state, town, level));
        town.LastBuilt = state.Date;
        town.LastBuiltName = level.Level == 1 ? $"{definition.Name} dwelling" : $"{definition.Name} dwelling upgrade";
        town.LastBuiltPlot = TownOverview.DwellingPlot(line);
        _construction.RaiseDwelling(town, definition, level.Level);
    }

    internal void Recruit(PlayerId player, MapObjectId townId, Common.CreatureId creature, int count, HeroId? heroId)
    {
        var town = OwnTown(player, townId);
        var (army, hero) = ArmyOf(town, heroId is null ? TownArmy.Garrison : TownArmy.VisitingHero, heroId);
        var line = CheckRecruit(town, creature, count, town.Available, army);

        Pay(player, TownRules.RecruitCost(state, town, state.Creatures.Get(creature), count));
        town.SetAvailable(line.Id, town.AvailableOf(line.Id) - count);
        army.TryAdd(new CreatureStack(creature, count));
        events.Publish(new CreaturesRecruited(town.Id, creature, count, hero?.Id, town.AvailableOf(line.Id)));
        PublishArmy(town, hero);
    }

    /// <summary>
    /// Recruit all (ADR-296): every order as <see cref="Recruit"/> would, checked one after another on copies of the armies and of what waits, then
    /// carried out together and paid in one sum; one refused order refuses them all.
    /// </summary>
    internal void RecruitMany(PlayerId player, MapObjectId townId, IReadOnlyList<RecruitOrder> orders)
    {
        var town = OwnTown(player, townId);
        if (orders.Count == 0)
        {
            throw new CommandRefusedException("Nothing to recruit.");
        }

        var garrison = new Army(town.Garrison.Slots);
        Army? heroCopy = null;
        var waiting = new Dictionary<Common.CreatureId, int>(town.Available);
        var cost = ResourceSet.Empty;
        var planned = new List<(RecruitOrder Order, Common.CreatureId Line, Hero? Hero)>();
        foreach (var order in orders)
        {
            var (army, hero) = ArmyOf(town, order.Hero is null ? TownArmy.Garrison : TownArmy.VisitingHero, order.Hero);
            var copy = hero is null ? garrison : heroCopy ??= new Army(army.Slots);
            var line = CheckRecruit(town, order.Creature, order.Count, waiting, copy);
            copy.TryAdd(new CreatureStack(order.Creature, order.Count));
            waiting[line.Id] = waiting.GetValueOrDefault(line.Id) - order.Count;
            cost += TownRules.RecruitCost(state, town, state.Creatures.Get(order.Creature), order.Count);
            planned.Add((order, line.Id, hero));
        }

        Pay(player, cost);
        foreach (var (order, line, hero) in planned)
        {
            town.SetAvailable(line, town.AvailableOf(line) - order.Count);
            (hero?.Army ?? town.Garrison).TryAdd(new CreatureStack(order.Creature, order.Count));
        }

        events.Publish(new CreaturesRecruitedMany(town.Id, orders.ToList()));
        if (planned.Any(p => p.Hero is null))
        {
            PublishArmy(town, null);
        }

        if (planned.Select(p => p.Hero).OfType<Hero>().FirstOrDefault() is { } visitor)
        {
            PublishArmy(town, visitor);
        }
    }

    /// <summary>
    /// The line of a recruit that may go ahead, or a refusal: the town recruits the creature, its dwelling is built (upgraded for an upgraded
    /// creature), at least <paramref name="count"/> of the line wait in <paramref name="waiting"/>, and <paramref name="army"/> has room for it.
    /// </summary>
    private Creatures.CreatureDefinition CheckRecruit(Town town, Common.CreatureId creature, int count, IReadOnlyDictionary<Common.CreatureId, int> waiting, Army army)
    {
        var line = TownRules.LineOf(state, town, creature) ?? throw new CommandRefusedException($"{town.Name} does not recruit '{creature}'.");
        var upgraded = creature != line.Id;
        if (town.DwellingLevel(line.Id) < (upgraded ? 2 : 1))
        {
            throw new CommandRefusedException(upgraded ? $"The {line.Name} dwelling is not upgraded." : $"{town.Name} has no {line.Name} dwelling.");
        }

        var left = waiting.GetValueOrDefault(line.Id);
        if (count < 1 || count > left)
        {
            throw new CommandRefusedException($"Only {left} {line.Name} can be recruited.");
        }

        return army.CanAccept(creature) ? line : throw new CommandRefusedException("There is no free slot for the recruits.");
    }

    internal void UpgradeStack(PlayerId player, MapObjectId townId, TownArmy which, int slot)
    {
        var town = OwnTown(player, townId);
        var (army, hero) = ArmyOf(town, which, null);
        var stack = (Army.IsValidSlot(slot) ? army[slot] : null) ?? throw new CommandRefusedException("The slot is empty.");
        Refuse(TownRules.CheckUpgradeLine(state, town, stack.Creature, out var found));
        var line = found!;

        Pay(player, TownRules.UpgradeCost(state, town, line, stack.Count));
        army.Set(slot, new CreatureStack(line.UpgradesTo!.Value, stack.Count));
        events.Publish(new StackUpgraded(town.Id, hero?.Id, slot, line.Id, line.UpgradesTo!.Value, stack.Count));
        PublishArmy(town, hero);
    }

    /// <summary>Upgrade all (ADR-296): the stacks <see cref="TownPlans.Upgrade"/> lists, paid in one sum; refused when it lists none.</summary>
    internal void UpgradeAll(PlayerId player, MapObjectId townId)
    {
        var town = OwnTown(player, townId);
        var plan = TownPlans.Upgrade(state, town);
        if (plan.IsEmpty)
        {
            throw new CommandRefusedException($"Nothing in {town.Name} can be upgraded that you can pay for.");
        }

        Pay(player, plan.Cost);
        var hero = TownRules.VisitingHero(state, town);
        foreach (var order in plan.Orders)
        {
            (order.Army == TownArmy.Garrison ? town.Garrison : hero!.Army).Set(order.Slot, new CreatureStack(order.To, order.Count));
        }

        foreach (var order in plan.Orders)
        {
            events.Publish(new StackUpgraded(town.Id, order.Army == TownArmy.Garrison ? null : hero!.Id, order.Slot, order.From, order.To, order.Count));
        }

        if (plan.Orders.Any(o => o.Army == TownArmy.Garrison))
        {
            PublishArmy(town, null);
        }

        if (plan.Orders.Any(o => o.Army == TownArmy.VisitingHero))
        {
            PublishArmy(town, hero);
        }
    }

    /// <summary>Renames an own town (ADR-296, <see cref="TownRules.CheckName"/>).</summary>
    internal void Rename(PlayerId player, MapObjectId townId, string name)
    {
        var town = OwnTown(player, townId);
        Refuse(TownRules.CheckName(name));
        town.Rename(name);
        events.Publish(new TownRenamed(town.Id, town.Name));
    }

    /// <summary>
    /// The Undead Transformer (ADR-137) turns the ordered stacks into undead, all or none. A result joins a stack of the same creature already in
    /// its army (the first one), else takes the transformed stack's slot.
    /// </summary>
    internal void Transform(PlayerId player, MapObjectId townId, IReadOnlyList<TransformOrder> orders)
    {
        var town = OwnTown(player, townId);
        Refuse(UndeadTransformer.CheckTown(town));
        if (orders.Count == 0 || orders.DistinctBy(o => (o.Army, o.Slot)).Count() != orders.Count)
        {
            throw new CommandRefusedException("Pick each stack to transform once.");
        }

        var planned = orders.Select(order =>
        {
            var (army, hero) = ArmyOf(town, order.Army, null);
            var stack = (Army.IsValidSlot(order.Slot) ? army[order.Slot] : null) ?? throw new CommandRefusedException("The slot is empty.");
            var (transform, refusal) = UndeadTransformer.Of(state, town, stack.Creature, stack.Count, order.Line);
            Refuse(refusal);
            return (Army: army, Hero: hero, order.Slot, Transform: transform!);
        }).ToList();

        foreach (var (army, hero, slot, transform) in planned)
        {
            army.Set(slot, null);
            var same = army.Stacks.FirstOrDefault(s => s.Stack.Creature == transform.To);
            var into = same.Stack is null ? slot : same.Slot;
            army.Set(into, new CreatureStack(transform.To, transform.Result + (same.Stack?.Count ?? 0)));
            events.Publish(new CreaturesTransformed(town.Id, hero?.Id, slot, transform.From, transform.Count, transform.To, transform.Result));
        }

        foreach (var hero in planned.Select(p => p.Hero).Distinct())
        {
            PublishArmy(town, hero);
        }
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
        visits.Visit(hero, town);
    }

    /// <summary>The visiting hero moves into the garrison (ADR-112): its stacks join the garrison's, and it leaves the map.</summary>
    internal void EnterGarrison(PlayerId player, MapObjectId townId)
    {
        var town = OwnTown(player, townId);
        Refuse(TownRules.CheckEnterGarrison(state, town));

        var hero = TownRules.VisitingHero(state, town)!;
        var merged = TownRules.Merged(town.Garrison, hero.Army)!;
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            town.Garrison.Set(slot, merged[slot]);
            hero.Army.Set(slot, null);
        }

        state.RemoveHero(hero);
        town.GarrisonHero = hero;
        events.Publish(new HeroEnteredGarrison(town.Id, hero.Id));
        events.Publish(new GarrisonChanged(town.Id, town.Garrison.Slots.ToList()));
    }

    /// <summary>The garrison hero comes out onto the entrance and leads the whole garrison as its army (ADR-112, HoMM3).</summary>
    internal void LeaveGarrison(PlayerId player, MapObjectId townId)
    {
        var town = OwnTown(player, townId);
        Refuse(TownRules.CheckLeaveGarrison(state, town));

        var hero = town.GarrisonHero!;
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            hero.Army.Set(slot, town.Garrison[slot]);
            town.Garrison.Set(slot, null);
        }

        town.GarrisonHero = null;
        hero.Position = town.VisitPosition;
        hero.MaxMovementPoints = MovementAllowance.Daily(state, hero);
        hero.MovementPoints = Math.Min(hero.MovementPoints, hero.MaxMovementPoints);
        state.AddHero(hero);
        events.Publish(new HeroLeftGarrison(town.Id, hero.Id, hero.Position));
        events.Publish(new GarrisonChanged(town.Id, town.Garrison.Slots.ToList()));
        events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
    }

    /// <summary>
    /// The garrison hero and the visiting hero change places (ADR-294): each keeps its own troops, so the garrison hero comes out leading the
    /// garrison and the visiting hero's army becomes the garrison.
    /// </summary>
    internal void SwapHeroes(PlayerId player, MapObjectId townId)
    {
        var town = OwnTown(player, townId);
        Refuse(TownRules.CheckSwapHeroes(state, town));

        var entering = TownRules.VisitingHero(state, town)!;
        var leaving = town.GarrisonHero!;
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            leaving.Army.Set(slot, town.Garrison[slot]);
            town.Garrison.Set(slot, entering.Army[slot]);
            entering.Army.Set(slot, null);
        }

        state.RemoveHero(entering);
        town.GarrisonHero = entering;
        leaving.Position = town.VisitPosition;
        leaving.MaxMovementPoints = MovementAllowance.Daily(state, leaving);
        leaving.MovementPoints = Math.Min(leaving.MovementPoints, leaving.MaxMovementPoints);
        state.AddHero(leaving);
        events.Publish(new HeroEnteredGarrison(town.Id, entering.Id));
        events.Publish(new HeroLeftGarrison(town.Id, leaving.Id, leaving.Position));
        events.Publish(new GarrisonChanged(town.Id, town.Garrison.Slots.ToList()));
        events.Publish(new ArmyChanged(entering.Id, entering.Army.Slots.ToList()));
        events.Publish(new ArmyChanged(leaving.Id, leaving.Army.Slots.ToList()));
    }

    /// <summary>The owner appoints one of its heroes governor of the town; the hero leaves any other town it governed.</summary>
    internal void AppointGovernor(PlayerId player, MapObjectId townId, HeroId heroId)
    {
        var town = OwnTown(player, townId);
        var hero = state.Heroes.FirstOrDefault(h => h.Id == heroId && h.Owner == player) ?? throw new CommandRefusedException($"{heroId} is not an own hero.");
        Refuse(StewardshipRules.CheckAppoint(state, town, hero));
        visits.SetGovernor(town, hero);
    }

    /// <summary>The owner dismisses the town's governor.</summary>
    internal void DismissGovernor(PlayerId player, MapObjectId townId)
    {
        var town = OwnTown(player, townId);
        if (StewardshipRules.GovernorOf(state, town) is null)
        {
            throw new CommandRefusedException($"{town.Name} has no governor.");
        }

        visits.SetGovernor(town, null);
    }

    /// <summary>
    /// Start of the owner's turn: heroes already standing in a town (or in its garrison, ADR-112) without a governor are appointed, and a night in a
    /// town with a mage guild refills their mana.
    /// </summary>
    internal void AppointVisitors(PlayerId player)
    {
        foreach (var town in state.ObjectsOf<Town>().Where(t => t.Owner == player).OrderBy(t => t.Id.Value))
        {
            foreach (var hero in new[] { TownRules.VisitingHero(state, town), town.GarrisonHero }.OfType<Hero>())
            {
                visits.AutoAppoint(town, hero);
                visits.Visit(hero, town, night: true);
            }
        }
    }

    internal void HireHero(PlayerId player, MapObjectId townId, string heroId)
    {
        var town = OwnTown(player, townId);
        Refuse(TavernRules.CheckHire(state, town, heroId));

        Pay(player, TavernRules.HeroCost);
        var definition = state.HeroRoster.Get(heroId);

        // A retired hero comes back as it left (ADR-081): level, skills, artifacts, machines, and the army a surrender kept.
        var hero = TavernRules.Returning(state, player, heroId) is { } returning
            ? recruits.Return(returning, town.VisitPosition, definition)
            : recruits.Join(player, town.VisitPosition, definition, hired: true, movesToday: true);
        events.Publish(new HeroHired(town.Id, hero.Id, heroId));
        events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
        visits.Visit(hero, town);
    }

    /// <summary>
    /// Monday: every built dwelling, in towns and on the map, adds its creature's weekly growth to what is waiting (owned
    /// or not); town dwellings add the fort bonus too (<see cref="TownRules.WeeklyGrowth(GameState, Town, Creatures.CreatureDefinition, int)"/>, ADR-066) and the owner's heroes' growth artifacts
    /// (ADR-111), map dwellings do not.
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

            var artifacts = TownRules.ArtifactGrowthPercent(state, town);
            foreach (var line in lines)
            {
                town.SetAvailable(line.Id, town.AvailableOf(line.Id) + week.Growth(state.Creatures, line.Id, TownRules.WeeklyGrowth(state, town, line, artifacts)));
            }

            events.Publish(new TownCreaturesGrew(town.Id, new Dictionary<Common.CreatureId, int>(town.Available)));
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

            events.Publish(new TownCreaturesGrew(town.Id, new Dictionary<Common.CreatureId, int>(town.Available)));
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
    /// Game start (ADR-106, MAG-13): every town, by id, draws 3 / 3 / 2 / 2 of its school's spells per level without repeats from the game's
    /// random source; the spells the map fixes (<see cref="Town.FixedGuildSpells"/>) take their places first and the draw fills the rest from the
    /// others. Towns without a school (Stronghold, Swamp Bestiary) offer none. Then every town, by id, draws its spell buildings' spells
    /// (<see cref="Town.SchoolSpells"/>) from the same source, school by school in id order, one per level.
    /// </summary>
    internal void DrawGuilds()
    {
        var perLevel = Magic.MagicRules.GuildSpellsPerLevel;
        var towns = state.ObjectsOf<Town>().OrderBy(t => t.Id.Value).ToList();
        foreach (var town in towns)
        {
            var guild = new Dictionary<int, IReadOnlyList<Common.SpellId>>();
            for (var level = 1; level <= perLevel.Count; level++)
            {
                // The map's fixed spells stay even when banned; the draw skips banned ones unless the level has no other (ADR-126).
                var all = state.Spells.Of(town.TownType, level).Select(s => s.Id).ToList();
                var drawn = town.FixedGuildSpells.Where(all.Contains).Distinct().Take(perLevel[level - 1]).ToList();
                var pool = MapBans.Allowed(all, state.Rules.Bans.BansSpell).Where(s => !drawn.Contains(s)).ToList();
                while (drawn.Count < perLevel[level - 1] && pool.Count > 0)
                {
                    var pick = state.Random.Next(pool.Count);
                    drawn.Add(pool[pick]);
                    pool.RemoveAt(pick);
                }

                if (drawn.Count > 0)
                {
                    drawn.Sort();
                    guild[level] = drawn;
                }
            }

            town.SetGuildSpells(guild);
        }

        foreach (var town in towns)
        {
            var schools = new Dictionary<string, IReadOnlyList<Common.SpellId>>(StringComparer.Ordinal);
            foreach (var school in TownRules.Buildings(state, town).TownBuildings.Select(BuiltInBuildings.SchoolOf).OfType<string>().Distinct().Order(StringComparer.Ordinal))
            {
                var spells = new List<Common.SpellId>();
                for (var level = 1; level <= perLevel.Count; level++)
                {
                    var pool = MapBans.Allowed(state.Spells.Of(school, level), s => state.Rules.Bans.BansSpell(s.Id));
                    if (pool.Count > 0)
                    {
                        spells.Add(pool[state.Random.Next(pool.Count)].Id);
                    }
                }

                schools[school] = spells;
            }

            town.SetSchoolSpells(schools);
        }
    }

    private Town OwnTown(PlayerId player, MapObjectId townId)
    {
        if (state.FindObject(townId) is not Town town)
        {
            throw new CommandRefusedException($"{townId} is not a town.");
        }

        if (town.Owner != player)
        {
            throw new CommandRefusedException($"{town.Name} does not belong to {player}.");
        }

        if (state.PendingDecision is not null)
        {
            throw new CommandRefusedException("Resolve the pending decision first.");
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
            throw new CommandRefusedException($"No own hero is visiting {town.Name}.");
        }

        return (hero.Army, hero);
    }

    private void Pay(PlayerId player, ResourceSet cost)
    {
        if (!economy.TrySpend(player, cost))
        {
            throw new CommandRefusedException("Not enough resources.");
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
            throw new CommandRefusedException(refusal);
        }
    }
}
