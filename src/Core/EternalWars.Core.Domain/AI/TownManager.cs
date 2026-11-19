using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.AI;

/// <summary>
/// The AI's towns (ADR-059): it builds by a fixed order of wants (saving for the next building, trading at the marketplace for it, ADR-189),
/// hires heroes, transforms, upgrades and recruits creatures, buys war machines and spell books, hires caravans, appoints governors, and moves
/// troops between the garrisons and the heroes. A garrison hero comes out once the town is safe without it. On an arena's preparation day
/// it spends everything on the army that fights (ADR-160).
/// </summary>
internal sealed partial class TownManager(AiContext ai, ThreatMap threats)
{
    private readonly AiContext _ai = ai;
    private readonly ThreatMap _threats = threats;
    private readonly GameSession _session = ai.Session;
    private readonly GameState _state = ai.State;
    private readonly AiWeights _weights = ai.Weights;

    /// <summary>Runs every own town once, by id: governors first, then each town's building, hiring and buying; then spare troops to empty towns.</summary>
    public void ManageTowns(PlayerId player)
    {
        AppointGovernors(player);
        foreach (var town in _state.ObjectsOf<Town>().Where(t => t.Owner == player).OrderBy(t => t.Id.Value).ToList())
        {
            if (!_ai.Active(player) || _state.PendingDecision is not null)
            {
                return;
            }

            // A player without heroes cannot take anything (ADR-114): it hires before it builds, and saves for a hero rather than build.
            var heroless = !_state.HeroesOf(player).Any() && town.Level(BuiltInBuildings.Tavern) > 0;
            if (heroless)
            {
                Hire(player, town, reserve: 0);
            }

            // The arena's preparation day (ADR-160): no tomorrow to save for.
            if (_ai.Preparing)
            {
                PrepareTown(player, town);
                continue;
            }

            var reserve = heroless && !_state.HeroesOf(player).Any() ? TavernRules.HeroCost.Gold : Build(player, town);
            Hire(player, town, reserve);
            TransformStacks(player, town);
            UpgradeStacks(player, town, reserve);
            Recruit(player, town, reserve);
            BuyWarMachines(player, town, reserve);
            BuySpellBook(player, town, reserve);
            HireCaravans(player, town, reserve);
            FetchResources(player, town);
            Consolidate(player, town);
        }

        if (_ai.Active(player) && _state.PendingDecision is null && !_ai.Preparing)
        {
            SendSpareTroops(player);
        }
    }

    /// <summary>
    /// Builds the most wanted thing it can afford today, trading at the marketplace for it when that is worth it (ADR-189); returns the gold
    /// to keep for the one it is saving for (with the gold buying its missing resources would take).
    /// </summary>
    private int Build(PlayerId player, Town town)
    {
        var resources = _state.GetPlayer(player).Resources;
        var income = _session.Economy.DailyIncome(player);
        if (Wanted(player, town, resources, income) is not { } wanted)
        {
            return 0;
        }

        var builtToday = TownRules.HasBuiltToday(_state, town);
        var cost = TownRules.BuildCost(_state, town, wanted.Next);
        if (!builtToday && TradeFor(player, cost, income))
        {
            var command = wanted.Dwelling
                ? (GameCommand)new BuildDwellingCommand(player, town.Id, wanted.Id)
                : new BuildCommand(player, town.Id, wanted.Id);
            _ai.Execute(command);
            return 0;
        }

        // Save for it rather than spending everything on creatures, as long as it is within reach.
        var buying = Reachable(cost, resources, income) ? 0 : BuyingGold(player, cost, resources) ?? 0;
        return (int)Math.Min(int.MaxValue, cost.Gold + buying);
    }

    private (bool Dwelling, string Id, BuildingLevel Next)? Wanted(PlayerId player, Town town, ResourceSet have, ResourceSet income)
    {
        foreach (var (dwelling, id) in BuildOrder(town))
        {
            var next = dwelling
                ? TownRules.NextDwellingLevel(_state, town, TownRules.Lines(_state, town).First(l => l.Id == id))
                : TownRules.NextLevel(_state, town, id);
            if (next is null
                || (dwelling && next.Level == 1 && TownRules.CheckDwellingChoice(_state, town, id, 1) is not null)
                || next.Requires.Any(r => TownRules.Level(_state, town, r.Building) < r.Level)
                || TownRules.CheckCapitol(_state, town, next) is not null
                || !(Reachable(TownRules.BuildCost(_state, town, next), have, income)
                    || Buyable(player, TownRules.BuildCost(_state, town, next), have, income)))
            {
                continue;
            }

            return (dwelling, id, next);
        }

        return null;
    }

    /// <summary>
    /// Whether the cost can be met within a few days of the income, so saving for it is not hopeless (<see cref="AiWeights.BuildGoldDays"/>,
    /// <see cref="AiWeights.BuildResourceDays"/>).
    /// </summary>
    private bool Reachable(ResourceSet cost, ResourceSet have, ResourceSet income) =>
        Enum.GetValues<ResourceType>().All(r => cost[r] <= have[r] + (income[r] * (r == ResourceType.Gold ? _weights.BuildGoldDays : _weights.BuildResourceDays)));

    /// <summary>What a town wants built, best first (the order is the strategy): the fort the dwellings need, units, then income, then the rest.</summary>
    private IEnumerable<(bool Dwelling, string Id)> BuildOrder(Town town)
    {
        var lines = TownRules.Lines(_state, town);
        IEnumerable<CreatureDefinition> Best(int tier) =>
            lines.Where(l => l.Tier == tier).OrderByDescending(l => (long)l.FightValue * TownRules.WeeklyGrowth(_state, town, l)).ThenBy(l => l.Id);
        bool TierBuilt(int tier) => lines.Any(l => l.Tier == tier && town.DwellingLevel(l.Id) > 0);

        if (town.Level(BuiltInBuildings.Fort) < 1)
        {
            if (town.Level(BuiltInBuildings.Tavern) < 1)
            {
                yield return (false, BuiltInBuildings.Tavern); // the fort needs it
            }

            yield return (false, BuiltInBuildings.Fort); // every dwelling needs it (ADR-066)
        }

        foreach (var tier in new[] { 1, 2, 3, 4, 5 }.Where(t => !TierBuilt(t)))
        {
            if (Best(tier).FirstOrDefault() is { } line)
            {
                yield return (true, line.Id.Value);
            }
        }

        if (town.Level(BuiltInBuildings.Caravan) < 1 && CaravanRules.Dwellings(_state, town).Count > 0)
        {
            yield return (false, BuiltInBuildings.Caravan); // there is something to hire from within reach
        }

        yield return (false, BuiltInBuildings.Marketplace);
        yield return (false, BuiltInBuildings.Blacksmith);
        yield return (false, BuiltInBuildings.MageGuild);
        yield return (false, BuiltInBuildings.WarLodge); // Stronghold and Swamp Bestiary instead of the guild (ADR-071)
        yield return (false, BuiltInBuildings.TownHall);
        foreach (var line in lines.Where(l => town.DwellingLevel(l.Id) == 1 && l.UpgradesTo is not null).OrderBy(l => l.Tier))
        {
            yield return (true, line.Id.Value);
        }

        yield return (false, BuiltInBuildings.Growth(1).Value); // the growth buildings (ADR-071), once their tier has a dwelling
        yield return (false, BuiltInBuildings.Growth(2).Value);
        foreach (var unique in UniqueBuildings.All.Where(b => b.Town == town.TownType))
        {
            yield return (false, unique.Id); // the town's unique building (ADR-138): its heroes take the bonus when they pass through
        }

        yield return (false, UndeadTransformer.Building); // Necropolis (ADR-137): its living recruits and prisoners' troops become undead
        yield return (false, Town.Observatory); // Academy (ADR-143): a wider watch

        yield return (false, BuiltInBuildings.Fort);
        yield return (false, BuiltInBuildings.TownHall);
        yield return (false, BuiltInBuildings.Marketplace);
        foreach (var line in Enumerable.Range(1, 4).SelectMany(Best).Where(l => town.DwellingLevel(l.Id) == 0))
        {
            yield return (true, line.Id.Value);
        }

        yield return (false, BuiltInBuildings.Fort);
        yield return (false, BuiltInBuildings.MageGuild);
        yield return (false, BuiltInBuildings.WarLodge);
        yield return (false, BuiltInBuildings.Tavern);
        yield return (false, BuiltInBuildings.TownHall); // the Capitol, once the Castle stands
    }

    /// <summary>
    /// Hires a hero while it has fewer than one more than its towns (at most four): the best offered one, its own returning heroes first
    /// (ADR-081), then warriors, when the gold is there beyond what it saves for a building.
    /// </summary>
    private void Hire(PlayerId player, Town town, int reserve)
    {
        var heroes = _state.HeroesOf(player).Count();
        var towns = _state.ObjectsOf<Town>().Count(t => t.Owner == player);
        if (heroes >= Math.Min(1 + towns, _weights.MostHeroes) || !(_state.GetPlayer(player).Resources - new ResourceSet(Gold: reserve)).CanAfford(TavernRules.HeroCost))
        {
            return;
        }

        var offer = TavernRules.Offers(_state, player, town)
            .OrderByDescending(h => TavernRules.Returning(_state, player, h.Id) is not null)
            .ThenByDescending(h => h.Class == HeroClass.Warrior).ThenByDescending(h => h.Attack + h.Defense).ThenBy(h => h.Id, StringComparer.Ordinal)
            .FirstOrDefault(h => TavernRules.CheckHire(_state, town, h.Id) is null);
        if (offer is not null)
        {
            _ai.Execute(new HireHeroCommand(player, town.Id, offer.Id));
        }
    }

    /// <summary>
    /// At its Undead Transformer (ADR-137) the AI turns the living stacks of the garrison and the visiting hero into undead: all of them in an army
    /// that is mostly undead already (by worth, creature value × count: the living ones cost it morale), elsewhere those whose undead are worth at
    /// least as much. A creature without a class becomes the more valuable line. It never swaps undead lines.
    /// </summary>
    private void TransformStacks(PlayerId player, Town town)
    {
        if (UndeadTransformer.CheckTown(town) is not null)
        {
            return;
        }

        var orders = new List<TransformOrder>();
        foreach (var (army, slots) in ArmiesOf(town))
        {
            var undead = slots.Stacks.Select(s => (Creature: _state.Creatures.Get(s.Stack.Creature), s.Stack.Count))
                .GroupBy(s => s.Creature.HasTag(CreatureTags.Undead)).ToDictionary(g => g.Key, g => g.Sum(s => (long)s.Creature.FightValue * s.Count));
            var mostlyUndead = undead.GetValueOrDefault(true) > 0 && undead.GetValueOrDefault(true) >= undead.GetValueOrDefault(false);
            foreach (var (slot, stack) in slots.Stacks)
            {
                var creature = _state.Creatures.Get(stack.Creature);
                if (!UndeadTransformer.IsLiving(creature))
                {
                    continue;
                }

                var worth = (long)creature.FightValue * stack.Count;
                var best = new[] { CreatureCategory.A, CreatureCategory.B }
                    .Select(line => (Line: line, UndeadTransformer.Of(_state, town, stack.Creature, stack.Count, line).Transform))
                    .Where(t => t.Transform is not null)
                    .OrderByDescending(t => (long)_state.Creatures.Get(t.Transform!.To).FightValue * t.Transform.Result)
                    .FirstOrDefault();
                if (best.Transform is { } transform && (mostlyUndead || (long)_state.Creatures.Get(transform.To).FightValue * transform.Result >= worth))
                {
                    orders.Add(new TransformOrder(army, slot, best.Line));
                }
            }
        }

        if (orders.Count > 0)
        {
            _ai.Execute(new TransformCreaturesCommand(player, town.Id, orders));
        }
    }

    /// <summary>Upgrades the stacks of the garrison and the visiting hero whose dwelling is upgraded, strongest tier first.</summary>
    private void UpgradeStacks(PlayerId player, Town town, int reserve)
    {
        foreach (var (army, slots) in ArmiesOf(town))
        {
            foreach (var (slot, stack) in slots.Stacks.ToList().OrderByDescending(s => _state.Creatures.Get(s.Stack.Creature).Tier))
            {
                if (TownRules.LineOf(_state, town, stack.Creature) is not { UpgradesTo: not null } line
                    || line.Id != stack.Creature
                    || town.DwellingLevel(line.Id) < 2)
                {
                    continue;
                }

                var cost = TownRules.UpgradeCost(_state, town, line, stack.Count);
                var spare = _state.GetPlayer(player).Resources - new ResourceSet(Gold: reserve);
                if (spare.CanAfford(cost))
                {
                    _ai.Execute(new UpgradeStackCommand(player, town.Id, army, slot));
                }
            }
        }
    }

    private IEnumerable<(TownArmy Army, Army Slots)> ArmiesOf(Town town)
    {
        yield return (TownArmy.Garrison, town.Garrison);
        if (TownRules.VisitingHero(_state, town) is { } hero)
        {
            yield return (TownArmy.VisitingHero, hero.Army);
        }
    }

    /// <summary>Buys what is waiting, strongest creatures first: a first garrison of the cheapest line, the rest for the hero.</summary>
    private void Recruit(PlayerId player, Town town, int reserve)
    {
        var hero = TownRules.VisitingHero(_state, town);
        var lines = TownRules.Lines(_state, town).Where(l => town.DwellingLevel(l.Id) > 0 && town.AvailableOf(l.Id) > 0).ToList();

        if (town.Garrison.IsEmpty && lines.OrderBy(l => l.Tier).FirstOrDefault() is { } cheap)
        {
            Buy(player, town, cheap, null, reserve, (town.AvailableOf(cheap.Id) + 1) / 2);
        }

        // The week's creature first (ADR-073): its doubled stock is the cheapest strength on offer this week.
        foreach (var line in lines.OrderByDescending(l => _state.WeekEvent.Growth(_state.Creatures, l.Id, 1) > 1).ThenByDescending(l => l.Tier).ThenByDescending(l => l.FightValue))
        {
            if (!_ai.Active(player) || town.AvailableOf(line.Id) <= 0)
            {
                continue;
            }

            var creature = town.DwellingLevel(line.Id) >= 2 ? line.UpgradesTo!.Value : line.Id;
            var target = hero is not null && hero.Army.CanAccept(creature) ? hero.Id : (HeroId?)null;
            if (target is null && !town.Garrison.CanAccept(creature))
            {
                continue; // no room anywhere (a garrison hero's stacks can fill all seven slots)
            }

            Buy(player, town, line, target, reserve, town.AvailableOf(line.Id));
        }
    }

    private void Buy(PlayerId player, Town town, CreatureDefinition line, HeroId? hero, int reserve, int limit)
    {
        var creature = town.DwellingLevel(line.Id) >= 2 ? line.UpgradesTo!.Value : line.Id;
        var spare = _state.GetPlayer(player).Resources - new ResourceSet(Gold: reserve);
        var count = TownRules.MaxAffordable(spare, TownRules.CreatureCost(_state, town, _state.Creatures.Get(creature)), Math.Min(limit, town.AvailableOf(line.Id)));
        if (count > 0)
        {
            _ai.Execute(new RecruitCommand(player, town.Id, creature, count, hero));
        }
    }

    /// <summary>
    /// Heroes with Stewardship that govern nothing are appointed to the ungoverned towns that spend the most (most dwellings, then the highest
    /// town hall), the best steward to the best town (ADR-063). A governor keeps its town while it roams.
    /// </summary>
    private void AppointGovernors(PlayerId player)
    {
        var free = _state.HeroesOf(player)
            .Where(h => h.SkillLevelOf(BuiltInSkills.Stewardship) > SkillLevel.None && StewardshipRules.TownGovernedBy(_state, h) is null)
            .OrderByDescending(h => h.SkillLevelOf(BuiltInSkills.Stewardship)).ThenBy(h => h.Id.Value).ToList();
        var towns = _state.ObjectsOf<Town>().Where(t => t.Owner == player && StewardshipRules.GovernorOf(_state, t) is null)
            .OrderByDescending(t => t.Dwellings.Count).ThenByDescending(t => t.Level(BuiltInBuildings.TownHall)).ThenBy(t => t.Id.Value).ToList();
        foreach (var (hero, town) in free.Zip(towns))
        {
            if (_ai.Active(player) && _state.PendingDecision is null && StewardshipRules.CheckAppoint(_state, town, hero) is null)
            {
                _ai.Execute(new AppointGovernorCommand(player, town.Id, hero.Id));
            }
        }
    }

    /// <summary>
    /// The visiting hero buys war machines with what is left after building and recruiting, keeping a cushion of gold (ADR-056): a healing tent,
    /// an ammo cart when its army shoots, and its offensive machine back when it was lost and the hero can field it (Advanced Ballistics).
    /// </summary>
    private void BuyWarMachines(PlayerId player, Town town, int reserve)
    {
        if (TownRules.VisitingHero(_state, town) is not { } hero || town.Level(BuiltInBuildings.Blacksmith) < 1)
        {
            return;
        }

        var wanted = new List<WarMachineSlot> { WarMachineSlot.Tent };
        if (hero.Army.Stacks.Any(s => _state.Creatures.Get(s.Stack.Creature).Shots > 0))
        {
            wanted.Add(WarMachineSlot.Cart);
        }

        if (WarMachines.FieldsOffensive(hero.SkillLevelOf(BuiltInSkills.Ballistics)))
        {
            wanted.Insert(0, WarMachineSlot.Offensive);
        }

        foreach (var slot in wanted)
        {
            var spare = _state.GetPlayer(player).Resources.Gold - reserve - WarMachines.Cost(slot).Gold;
            if (_ai.Active(player) && spare >= _weights.WarMachineCushion && TownRules.CheckBuyWarMachine(_state, town, slot) is null)
            {
                _ai.Execute(new BuyWarMachineCommand(player, town.Id, slot));
            }
        }
    }

    /// <summary>A visiting hero with a school's Magic skill but no spell book buys one, with the same cushion as a war machine (ADR-110).</summary>
    private void BuySpellBook(PlayerId player, Town town, int reserve)
    {
        if (TownRules.VisitingHero(_state, town) is not { } hero || hero.HasSpellBook
            || !BuiltInSkills.Schools.Keys.Any(school => hero.SkillLevelOf(Magic.MagicRules.MagicSkill(school)) > SkillLevel.None))
        {
            return;
        }

        var spare = _state.GetPlayer(player).Resources.Gold - reserve - Magic.MagicRules.SpellBookGold;
        if (_ai.Active(player) && spare >= _weights.WarMachineCushion && Magic.MagicRules.CheckBuySpellBook(_state, town) is null)
        {
            _ai.Execute(new BuySpellBookCommand(player, town.Id));
        }
    }

    /// <summary>
    /// A town with a caravan hires what waits in the owned map dwellings, most valuable creatures first, with the gold beyond
    /// what is saved for a building. At most two caravans travel to a town at a time (ADR-054).
    /// </summary>
    private void HireCaravans(PlayerId player, Town town, int reserve)
    {
        if (town.Level(BuiltInBuildings.Caravan) < 1)
        {
            return;
        }

        var dwellings = CaravanRules.Dwellings(_state, town)
            .Where(d => d.Available > 0)
            .OrderByDescending(d => _state.Creatures.Get(d.Creature).FightValue).ThenBy(d => d.Id.Value);
        foreach (var dwelling in dwellings)
        {
            if (!_ai.Active(player) || _state.Caravans.Count(c => c.Destination == town.Id) >= 2)
            {
                return;
            }

            var creature = _state.Creatures.Get(dwelling.Creature);
            var spare = _state.GetPlayer(player).Resources - new ResourceSet(Gold: reserve);
            var count = TownRules.MaxAffordable(spare, TownRules.CreatureCost(_state, town, creature), dwelling.Available!.Value);
            if (count > 0 && CaravanRules.CheckOrder(_state, town, dwelling, count) is null)
            {
                _ai.Execute(new OrderCaravanCommand(player, town.Id, dwelling.Id, count));
            }
        }
    }

    /// <summary>
    /// Sends the town's caravan for what waits at own resource providers (ADR-079, TOWN-9) when it is worth well over the caravan's price,
    /// most valuable first, as far as the caravan limit allows.
    /// </summary>
    private void FetchResources(PlayerId player, Town town)
    {
        var worth = 5 * AiContext.Value(CaravanRules.ResourceCaravanCost);
        foreach (var provider in CaravanRules.Providers(_state, town).Where(p => AiContext.Value(p.Stored) >= worth).OrderByDescending(p => AiContext.Value(p.Stored)).ThenBy(p => p.Id.Value).ToList())
        {
            if (!_ai.Active(player))
            {
                return;
            }

            if (CaravanRules.CheckFetch(_state, town, provider) is null)
            {
                _ai.Execute(new FetchResourcesCommand(player, town.Id, provider.Id));
            }
        }
    }

    /// <summary>
    /// An own town with no garrison and no hero gets the weakest stack of another own town with a caravan, as long as that
    /// town keeps at least one stack of its own: nobody can walk in for free (ADR-054).
    /// </summary>
    private void SendSpareTroops(PlayerId player)
    {
        var towns = _state.ObjectsOf<Town>().Where(t => t.Owner == player).OrderBy(t => t.Id.Value).ToList();
        foreach (var empty in towns.Where(t => t.Garrison.IsEmpty && TownRules.VisitingHero(_state, t) is null
                                               && !_state.Caravans.Any(c => c.Destination == t.Id)))
        {
            var source = towns.FirstOrDefault(t => t != empty && t.Level(BuiltInBuildings.Caravan) >= 1 && t.Garrison.StackCount >= 2);
            if (source is null || !_ai.Active(player))
            {
                continue;
            }

            var (slot, _) = source.Garrison.Stacks.OrderBy(s => _ai.Power(s.Stack)).ThenBy(s => s.Slot).First();
            if (CaravanRules.CheckSend(_state, source, slot, empty, null) is null)
            {
                _ai.Execute(new SendTroopsCommand(player, source.Id, slot, empty.Id));
            }
        }
    }

    /// <summary>
    /// A garrison hero comes out once no enemy that the garrison alone could not hold can reach the town, and leaves the weakest stack behind
    /// (as <see cref="Consolidate"/> does); it stays while it would leave the town open to a threat.
    /// </summary>
    public void LeaveGarrisons(PlayerId player)
    {
        foreach (var town in _state.ObjectsOf<Town>().Where(t => t.Owner == player && t.GarrisonHero is not null).OrderBy(t => t.Id.Value).ToList())
        {
            if (!_ai.Active(player) || _state.PendingDecision is not null || _threats.Danger(player, town.VisitPosition) > 0
                || TownRules.CheckLeaveGarrison(_state, town) is not null || !_ai.Execute(new LeaveGarrisonCommand(player, town.Id)).IsAccepted)
            {
                continue;
            }

            var hero = TownRules.VisitingHero(_state, town)!;
            if (hero.Army.StackCount >= 2)
            {
                var (slot, _) = hero.Army.Stacks.OrderBy(s => _ai.Power(s.Stack)).ThenBy(s => s.Slot).First();
                _ai.Execute(new MoveTownStackCommand(player, town.Id, TownArmy.VisitingHero, slot, TownArmy.Garrison, 0));
            }
        }
    }

    /// <summary>A hero in the town takes the garrison's creatures with it, leaving the weakest stack to hold the town.</summary>
    public void Consolidate(PlayerId player, Town town)
    {
        if (TownRules.VisitingHero(_state, town) is not { } hero)
        {
            return;
        }

        var stacks = town.Garrison.Stacks.OrderByDescending(s => _ai.Power(s.Stack)).ToList();
        for (var i = 0; i < stacks.Count - 1; i++)
        {
            var (slot, stack) = stacks[i];
            var to = Enumerable.Range(0, Army.SlotCount).FirstOrDefault(s => hero.Army[s]?.Creature == stack.Creature, -1);
            to = to >= 0 ? to : Enumerable.Range(0, Army.SlotCount).FirstOrDefault(s => hero.Army[s] is null, -1);
            if (to >= 0)
            {
                _ai.Execute(new MoveTownStackCommand(player, town.Id, TownArmy.Garrison, slot, TownArmy.VisitingHero, to));
            }
        }
    }
}
