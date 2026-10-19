using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.AI;

/// <summary>
/// The computer player on the adventure map (ADR-059). It plays a turn exactly like a front-end: with
/// <see cref="GameSession.Execute"/> only, so everything it does is in the command log and replays. It draws no random
/// numbers: the same game state always gives the same turn.
/// </summary>
/// <remarks>
/// A turn: answer pending decisions, run the towns (build, upgrade, recruit, hand the garrison to the visiting hero),
/// move every hero (take the best-scoring target it knows about and can beat: pickups, mines, towns, monsters, enemy
/// heroes, or its own towns to reinforce; explore when nothing is known), run the towns again, end the turn. Strength is
/// army power, Σ count × creature value, the measure the neutral guards use too. The AI only uses what its player has
/// explored. <see cref="PlayTurn"/> can be called again after it returned false (waiting for a human in a battle): it
/// carries on where the turn stands.
/// </remarks>
/// <param name="session">The game it plays in.</param>
/// <param name="controlHumanSlots">
/// Also play turns of human slots and command battle sides of human players (the simulator, where nobody sits at the
/// keyboard). Without it only computer players are played, and battles with a human side wait for that human.
/// </param>
public sealed class AdventureAi(GameSession session, bool controlHumanSlots = false)
{
    /// <summary>Most moves one hero makes in a turn; a safety net, a hero normally runs out of movement first.</summary>
    public const int MaxMovesPerHero = 24;

    private const int CandidatesChecked = 10;
    private const int FrontierChecked = 8;

    /// <summary>The share of its army power a hero accepts to lose clearing a monster stack.</summary>
    private const double MonsterLoss = 0.4;

    private readonly GameState _state = session.State;
    private readonly BattleEstimator _estimator = new(session.State);
    private readonly HashSet<MapPosition> _claimed = [];

    /// <summary>The player's main hero this turn: the strongest army; the others carry troops to it.</summary>
    private HeroId? _main;

    /// <summary>A secondary hero hands its troops to the main hero when it carries at least this much army power (or a quarter of the main army).</summary>
    private const long CarryThreshold = 1000;

    /// <summary>A place is safe from an enemy hero when the army there has at least this share of the enemy's power.</summary>
    private const double SafeShare = 0.8;

    /// <summary>Commands the AI issued, and how many of them the game refused (should stay 0).</summary>
    public int Commands { get; private set; }

    public int Refused { get; private set; }

    /// <summary>Whether the current turn is the AI's to play.</summary>
    public bool IsMyTurn => _state.IsStarted && !_state.IsOver && (controlHumanSlots || !_state.CurrentPlayer.IsHuman);

    /// <summary>Whether a game app should hand the current turn to an AI that does not control human slots.</summary>
    public static bool IsComputerTurn(GameState state) => state.IsStarted && !state.IsOver && !state.CurrentPlayer.IsHuman;

    /// <summary>Plays turns until it is a human's, the game is over or a human has to act in a battle; returns the turns ended.</summary>
    public int PlayTurns(int limit = int.MaxValue)
    {
        var turns = 0;
        while (turns < limit && IsMyTurn && PlayTurn())
        {
            turns++;
        }

        return turns;
    }

    /// <summary>
    /// Plays the rest of the current player's turn. True when the turn is over (it ended, or the player was defeated or
    /// the game ended meanwhile); false when it has to wait for a human or got stuck.
    /// </summary>
    public bool PlayTurn()
    {
        if (!IsMyTurn)
        {
            return false;
        }

        var player = _state.CurrentPlayer.Id;
        _claimed.Clear();
        _estimator.Clear();
        if (!ResolvePending(player))
        {
            return false;
        }

        if (!Active(player))
        {
            return true;
        }

        ManageTowns(player);
        _main = _state.HeroesOf(player).OrderByDescending(h => ArmyPower(h.Army)).ThenBy(h => h.Id.Value).FirstOrDefault()?.Id;

        // The main hero moves first, so the carriers head for where it ends up.
        foreach (var hero in _state.HeroesOf(player).OrderBy(h => h.Id != _main).ThenBy(h => h.Id.Value).Select(h => h.Id).ToList())
        {
            if (!PlayHero(player, hero))
            {
                return false;
            }

            if (!Active(player))
            {
                return true;
            }
        }

        ManageTowns(player);
        if (!ResolvePending(player))
        {
            return false;
        }

        return !Active(player) || Execute(new EndTurnCommand(player)).IsAccepted;
    }

    private bool Active(PlayerId player) => !_state.IsOver && _state.CurrentPlayer.Id == player;

    private CommandResult Execute(GameCommand command)
    {
        var result = session.Execute(command);
        Commands++;
        if (!result.IsAccepted)
        {
            Refused++;
        }

        return result;
    }

    // Decisions -----------------------------------------------------------------------------------------

    /// <summary>Answers whatever is pending. False when a human has to answer (battle) or the AI cannot.</summary>
    private bool ResolvePending(PlayerId player)
    {
        for (var i = 0; i < 32; i++)
        {
            if (!Active(player))
            {
                return true;
            }

            GameCommand? command;
            switch (_state.PendingDecision)
            {
                case null:
                    return true;
                case ResourceChoiceDecision choice:
                    // The option worth the most by the AI's resource values (a warehouse: its resource or the gold).
                    var best = Enumerable.Range(0, choice.Options.Count).MaxBy(i => Value(choice.Options[i]));
                    command = new ChooseResourceRewardCommand(player, best);
                    break;
                case TreasureChestDecision chest:
                    var hero = _state.GetHero(chest.Hero);
                    var levels = ExperienceTable.LevelFor(hero.Experience + chest.Experience) > hero.Level;
                    command = new ResolveTreasureChestCommand(player, levels ? TreasureChestReward.Experience : TreasureChestReward.Gold);
                    break;
                case HeroExchangeDecision exchange:
                    Exchange(player, exchange.Hero, exchange.Other);
                    command = new CompleteExchangeCommand(player);
                    break;
                case GuardEncounterDecision guard:
                    // Take free troops; a stack that would flee is beaten for the experience, it is far weaker.
                    command = new RespondToGuardCommand(player, guard.Offer == GuardOffer.Join);
                    break;
                case LevelUpDecision levelUp:
                    command = new ChooseSkillCommand(player, levelUp.Options[0]);
                    break;
                case BattleDecision when _state.Battle is not null:
                    var commander = session.Battles.Commander(BattleSide.Attacker) ?? session.Battles.Commander(BattleSide.Defender);
                    if (commander is not { } who || (!controlHumanSlots && _state.GetPlayer(who).IsHuman))
                    {
                        return false; // a human fights this one
                    }

                    command = new AutoResolveBattleCommand(who);
                    break;
                default:
                    return false;
            }

            if (!Execute(command).IsAccepted)
            {
                return false;
            }
        }

        return false;
    }

    // Towns ---------------------------------------------------------------------------------------------

    private void ManageTowns(PlayerId player)
    {
        AppointGovernors(player);
        foreach (var town in _state.ObjectsOf<Town>().Where(t => t.Owner == player).OrderBy(t => t.Id.Value).ToList())
        {
            if (!Active(player) || _state.PendingDecision is not null)
            {
                return;
            }

            var reserve = Build(player, town);
            Hire(player, town, reserve);
            UpgradeStacks(player, town, reserve);
            Recruit(player, town, reserve);
            BuyWarMachines(player, town, reserve);
            HireCaravans(player, town, reserve);
            Consolidate(player, town);
        }

        if (Active(player) && _state.PendingDecision is null)
        {
            SendSpareTroops(player);
        }
    }

    /// <summary>Builds the most wanted thing it can afford today; returns the gold to keep for the one it is saving for.</summary>
    private int Build(PlayerId player, Town town)
    {
        var resources = _state.GetPlayer(player).Resources;
        var income = session.Economy.DailyIncome(player);
        if (Wanted(town, resources, income) is not { } wanted)
        {
            return 0;
        }

        var builtToday = town.LastBuilt == _state.Date;
        if (!builtToday && resources.CanAfford(TownRules.BuildCost(_state, town, wanted.Next)))
        {
            var command = wanted.Dwelling
                ? (GameCommand)new BuildDwellingCommand(player, town.Id, wanted.Id)
                : new BuildCommand(player, town.Id, wanted.Id);
            Execute(command);
            return 0;
        }

        // Save for it rather than spending everything on creatures, as long as it is within reach.
        return TownRules.BuildCost(_state, town, wanted.Next).Gold;
    }

    private (bool Dwelling, string Id, BuildingLevel Next)? Wanted(Town town, ResourceSet have, ResourceSet income)
    {
        foreach (var (dwelling, id) in BuildOrder(town))
        {
            var next = dwelling
                ? TownRules.NextDwellingLevel(_state, town, TownRules.Lines(_state, town).First(l => l.Id == id))
                : TownRules.NextLevel(_state, town, id);
            if (next is null
                || (dwelling && next.Level == 1 && TownRules.CheckDwellingChoice(_state, town, id, 1) is not null)
                || next.Requires.Any(r => TownRules.Level(_state, town, r.Building) < r.Level)
                || !Reachable(TownRules.BuildCost(_state, town, next), have, income))
            {
                continue;
            }

            return (dwelling, id, next);
        }

        return null;
    }

    /// <summary>Whether the cost can be met within a few days of the income (so saving for it is not hopeless).</summary>
    private static bool Reachable(ResourceSet cost, ResourceSet have, ResourceSet income) =>
        Enum.GetValues<ResourceType>().All(r => cost[r] <= have[r] + (income[r] * (r == ResourceType.Gold ? 3 : 7)));

    /// <summary>What a town wants built, best first (the order is the strategy): the fort the dwellings need, units, then income, then the rest.</summary>
    private IEnumerable<(bool Dwelling, string Id)> BuildOrder(Town town)
    {
        var lines = TownRules.Lines(_state, town);
        IEnumerable<CreatureDefinition> Best(int tier) =>
            lines.Where(l => l.Tier == tier).OrderByDescending(l => (long)l.Value * TownRules.WeeklyGrowth(town, l)).ThenBy(l => l.Id, StringComparer.Ordinal);
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
                yield return (true, line.Id);
            }
        }

        if (_state.ObjectsOf<CreatureDwelling>().Any(d => d.Owner == town.Owner))
        {
            yield return (false, BuiltInBuildings.Caravan); // there is something to hire from
        }

        yield return (false, BuiltInBuildings.Marketplace);
        yield return (false, BuiltInBuildings.Blacksmith);
        yield return (false, BuiltInBuildings.MageGuild);
        yield return (false, BuiltInBuildings.TownHall);
        foreach (var line in lines.Where(l => town.DwellingLevel(l.Id) == 1 && l.UpgradesTo is not null).OrderBy(l => l.Tier))
        {
            yield return (true, line.Id);
        }

        yield return (false, BuiltInBuildings.Fort);
        yield return (false, BuiltInBuildings.TownHall);
        yield return (false, BuiltInBuildings.Marketplace);
        foreach (var line in Enumerable.Range(1, 4).SelectMany(Best).Where(l => town.DwellingLevel(l.Id) == 0))
        {
            yield return (true, line.Id);
        }

        yield return (false, BuiltInBuildings.Fort);
        yield return (false, BuiltInBuildings.MageGuild);
        yield return (false, BuiltInBuildings.Tavern);
    }

    /// <summary>
    /// Hires a hero while it has fewer than one more than its towns (at most four): the best offered one, warriors first
    /// (mages have nothing to cast yet), when the gold is there beyond what it saves for a building.
    /// </summary>
    private void Hire(PlayerId player, Town town, int reserve)
    {
        var heroes = _state.HeroesOf(player).Count();
        var towns = _state.ObjectsOf<Town>().Count(t => t.Owner == player);
        if (heroes >= Math.Min(1 + towns, 4) || !(_state.GetPlayer(player).Resources - new ResourceSet(Gold: reserve)).CanAfford(TavernRules.HeroCost))
        {
            return;
        }

        var offer = TavernRules.Offers(_state, player, town)
            .OrderByDescending(h => h.Class == HeroClass.Warrior).ThenByDescending(h => h.Attack + h.Defense).ThenBy(h => h.Id, StringComparer.Ordinal)
            .FirstOrDefault(h => TavernRules.CheckHire(_state, town, h.Id) is null);
        if (offer is not null)
        {
            Execute(new HireHeroCommand(player, town.Id, offer.Id));
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
                    Execute(new UpgradeStackCommand(player, town.Id, army, slot));
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

        foreach (var line in lines.OrderByDescending(l => l.Tier).ThenByDescending(l => l.Value))
        {
            if (!Active(player) || town.AvailableOf(line.Id) <= 0)
            {
                continue;
            }

            var creature = town.DwellingLevel(line.Id) >= 2 ? line.UpgradesTo! : line.Id;
            var target = hero is not null && hero.Army.CanAccept(creature) ? hero.Id : (HeroId?)null;
            Buy(player, town, line, target, reserve, town.AvailableOf(line.Id));
        }
    }

    private void Buy(PlayerId player, Town town, CreatureDefinition line, HeroId? hero, int reserve, int limit)
    {
        var creature = town.DwellingLevel(line.Id) >= 2 ? line.UpgradesTo! : line.Id;
        var spare = _state.GetPlayer(player).Resources - new ResourceSet(Gold: reserve);
        var count = TownRules.MaxAffordable(spare, TownRules.CreatureCost(_state, town, _state.Creatures.Get(creature)), Math.Min(limit, town.AvailableOf(line.Id)));
        if (count > 0)
        {
            Execute(new RecruitCommand(player, town.Id, creature, count, hero));
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
            if (Active(player) && _state.PendingDecision is null && StewardshipRules.CheckAppoint(_state, town, hero) is null)
            {
                Execute(new AppointGovernorCommand(player, town.Id, hero.Id));
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
            if (Active(player) && spare >= WarMachineCushion && TownRules.CheckBuyWarMachine(_state, town, slot) is null)
            {
                Execute(new BuyWarMachineCommand(player, town.Id, slot));
            }
        }
    }

    /// <summary>Gold kept after buying a war machine: creatures and buildings come first.</summary>
    private const int WarMachineCushion = 1500;

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

        var dwellings = _state.ObjectsOf<CreatureDwelling>()
            .Where(d => d.Owner == player && d.Available > 0)
            .OrderByDescending(d => _state.Creatures.Get(d.Creature).Value).ThenBy(d => d.Id.Value);
        foreach (var dwelling in dwellings)
        {
            if (!Active(player) || _state.Caravans.Count(c => c.Destination == town.Id) >= 2)
            {
                return;
            }

            var creature = _state.Creatures.Get(dwelling.Creature);
            var spare = _state.GetPlayer(player).Resources - new ResourceSet(Gold: reserve);
            var count = TownRules.MaxAffordable(spare, TownRules.CreatureCost(_state, town, creature), dwelling.Available!.Value);
            if (count > 0 && CaravanRules.CheckOrder(_state, town, dwelling, count) is null)
            {
                Execute(new OrderCaravanCommand(player, town.Id, dwelling.Id, count));
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
            if (source is null || !Active(player))
            {
                continue;
            }

            var (slot, _) = source.Garrison.Stacks.OrderBy(s => Power(s.Stack)).ThenBy(s => s.Slot).First();
            if (CaravanRules.CheckSend(_state, source, slot, empty, null) is null)
            {
                Execute(new SendTroopsCommand(player, source.Id, slot, empty.Id));
            }
        }
    }

    /// <summary>A hero in the town takes the garrison's creatures with it, leaving the weakest stack to hold the town.</summary>
    private void Consolidate(PlayerId player, Town town)
    {
        if (TownRules.VisitingHero(_state, town) is not { } hero)
        {
            return;
        }

        var stacks = town.Garrison.Stacks.OrderByDescending(s => Power(s.Stack)).ToList();
        for (var i = 0; i < stacks.Count - 1; i++)
        {
            var (slot, stack) = stacks[i];
            var to = Enumerable.Range(0, Army.SlotCount).FirstOrDefault(s => hero.Army[s]?.Creature == stack.Creature, -1);
            to = to >= 0 ? to : Enumerable.Range(0, Army.SlotCount).FirstOrDefault(s => hero.Army[s] is null, -1);
            if (to >= 0)
            {
                Execute(new MoveTownStackCommand(player, town.Id, TownArmy.Garrison, slot, TownArmy.VisitingHero, to));
            }
        }
    }

    // Heroes --------------------------------------------------------------------------------------------

    /// <summary>What an artifact is worth to the AI (<see cref="ArtifactValue.Of"/>).</summary>
    public static double Score(ArtifactDefinition artifact) => ArtifactValue.Of(artifact);

    /// <summary>Puts on what is in the backpack when a slot is free or the new one is clearly better than the worst worn there.</summary>
    private void EquipBest(PlayerId player, Hero hero)
    {
        for (var guard = 0; guard < ArtifactSlots.BackpackSize * 2 && Active(player); guard++)
        {
            var candidate = hero.Artifacts.Backpack
                .Select(id => _state.Artifacts.TryGet(id, out var def) ? def : null)
                .OfType<ArtifactDefinition>()
                .Where(def => Score(def) > 0 && (hero.Artifacts.HasRoomToWear(def.Slot) || Score(def) > 1.2 * WorstWornScore(hero, def.Slot)))
                .OrderByDescending(Score).ThenBy(def => def.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (candidate is null || !Execute(new EquipArtifactCommand(player, hero.Id, candidate.Id)).IsAccepted)
            {
                return;
            }
        }
    }

    private double WorstWornScore(Hero hero, ArtifactSlot slot) =>
        hero.Artifacts.Worn(slot).Select(id => _state.Artifacts.TryGet(id, out var def) ? Score(def) : 0).DefaultIfEmpty(0).Min();

    private bool PlayHero(PlayerId player, HeroId id)
    {
        var failed = new HashSet<MapPosition>();
        for (var move = 0; move < MaxMovesPerHero; move++)
        {
            if (!ResolvePending(player))
            {
                return false;
            }

            if (!Active(player) || _state.Heroes.FirstOrDefault(h => h.Id == id) is not { } hero)
            {
                return true;
            }

            EquipBest(player, hero);
            RecruitHere(player, hero);
            if (hero.MovementPoints <= 0 || Defending(hero))
            {
                return true;
            }

            var target = ChooseTarget(hero, failed);
            if (target is null)
            {
                return true;
            }

            _claimed.Add(target.Value);
            var result = Execute(new MoveHeroCommand(player, id, target.Value));
            if (!result.IsAccepted || result.Outcome is not MovementOutcome { StepsTaken: > 0 })
            {
                failed.Add(target.Value);
            }
        }

        return true;
    }

    /// <summary>A hero standing on a map dwelling buys what it can afford (keeping a third of the gold) into its army (ADR-054).</summary>
    private void RecruitHere(PlayerId player, Hero hero)
    {
        if (_state.ObjectCellAt(hero.Position) is not { Object: CreatureDwelling { Available: > 0 } dwelling, Cell: FootprintCell.Visit }
            || !_state.Creatures.TryGet(dwelling.Creature, out var creature) || !hero.Army.CanAccept(creature.Id))
        {
            return;
        }

        var each = StewardshipRules.DiscountCreatures(creature.Cost, hero.SkillLevelOf(BuiltInSkills.Stewardship));
        var spare = _state.GetPlayer(player).Resources - new ResourceSet(Gold: _state.GetPlayer(player).Resources.Gold / 3);
        var count = TownRules.MaxAffordable(spare, each, dwelling.Available!.Value);
        if (count > 0)
        {
            Execute(new RecruitAtDwellingCommand(player, dwelling.Id, count));
        }
    }

    // Several heroes and defence -----------------------------------------------------------------------

    /// <summary>
    /// Two own heroes met: the secondary one hands its troops to the main hero (ADR-059), strongest stacks first, into a slot of the same creature or a
    /// free one, else in exchange for the main army's weakest stack when that is weaker. It keeps one creature, as a hero must keep a stack.
    /// </summary>
    private void Exchange(PlayerId player, HeroId first, HeroId second)
    {
        var a = _state.GetHero(first);
        var b = _state.GetHero(second);
        var (main, feeder) = a.Id == _main || (b.Id != _main && ArmyPower(a.Army) >= ArmyPower(b.Army)) ? (a, b) : (b, a);
        foreach (var (slot, stack) in feeder.Army.Stacks.OrderByDescending(s => Power(s.Stack)).ThenBy(s => s.Slot).ToList())
        {
            if (!Active(player) || feeder.Army[slot] is not { } current)
            {
                return;
            }

            var last = feeder.Army.StackCount == 1;
            var count = last ? current.Count - 1 : current.Count;
            var to = Enumerable.Range(0, Army.SlotCount).FirstOrDefault(s => main.Army[s]?.Creature == current.Creature, -1);
            if (to < 0)
            {
                to = Enumerable.Range(0, Army.SlotCount).FirstOrDefault(s => main.Army[s] is null, -1);
            }

            if (to < 0 && !last)
            {
                // A full army: swap for its weakest stack if this one is stronger.
                var (weakSlot, weak) = main.Army.Stacks.OrderBy(s => Power(s.Stack)).ThenBy(s => s.Slot).First();
                to = Power(weak) < Power(current) ? weakSlot : -1;
            }

            if (to >= 0 && count > 0)
            {
                Execute(new MoveStackCommand(player, feeder.Id, slot, main.Id, to, count == current.Count ? null : count));
            }
        }
    }

    /// <summary>Visible enemy heroes and how far (in tiles) each can reach in a day.</summary>
    private IEnumerable<(Hero Enemy, long Power, int Reach)> Threats(PlayerId owner) =>
        _state.Heroes
            .Where(h => _state.AreEnemies(owner, h.Owner) && _state.CanSee(owner, h) && _state.Fog.IsExplored(owner, h.Position))
            .Select(h => (h, ArmyPower(h.Army), (h.MaxMovementPoints / MovementCostPerTile) + 1));

    /// <summary>A rough tile cost for how far an enemy gets in a day: between the native 100 and the dearer terrains, diagonals included.</summary>
    private const int MovementCostPerTile = 150;

    /// <summary>The strongest enemy army that can reach the place within a day, leaving out the hero at <paramref name="except"/> (the one being attacked); 0 when none can.</summary>
    private long Danger(PlayerId owner, MapPosition position, MapPosition? except = null) =>
        Threats(owner).Where(t => t.Enemy.Position != except && t.Enemy.Position.Level == position.Level && Distance(t.Enemy.Position, position) <= t.Reach)
            .Select(t => t.Power).DefaultIfEmpty(0).Max();

    /// <summary>What defends a place for the player: the hero's army, plus the garrison when it is the entrance of an own town.</summary>
    private long DefenceAt(Hero hero, MapPosition position) =>
        ArmyPower(hero.Army) + (_state.ObjectCellAt(position) is { Object: Town town, Cell: FootprintCell.Visit } && town.Owner == hero.Owner ? ArmyPower(town.Garrison) : 0);

    private static bool Holds(long defence, long danger) => danger == 0 || defence >= danger * SafeShare;

    /// <summary>A hero in an own town that an enemy can reach stays there when the garrison alone could not hold it but the two together can.</summary>
    private bool Defending(Hero hero)
    {
        if (_state.ObjectCellAt(hero.Position) is not { Object: Town town, Cell: FootprintCell.Visit } || town.Owner != hero.Owner)
        {
            return false;
        }

        var danger = Danger(hero.Owner, hero.Position);
        return danger > 0 && !Holds(ArmyPower(town.Garrison), danger) && Holds(DefenceAt(hero, hero.Position), danger);
    }

    private long Power(CreatureStack stack) => (long)stack.Count * _state.Creatures.Get(stack.Creature).Value;

    private long ArmyPower(Army army) => army.Stacks.Sum(s => Power(s.Stack));

    private long GuardPower(MapPosition position, bool pickup = false) =>
        GuardsToBeat(position, pickup).Sum(m => (long)(m.Count ?? 1) * _state.Creatures.Get(m.Creature).Value);

    /// <summary>The stacks a hero entering <paramref name="position"/> must beat: a pickup only by its Area-mode guards (ADR-065).</summary>
    private IEnumerable<MonsterGuard> GuardsToBeat(MapPosition position, bool pickup) =>
        _state.GuardsOf(position).Where(m => !pickup || m.Mode == GuardMode.Area);

    private static double Value(ResourceSet resources) => Enum.GetValues<ResourceType>().Sum(r => GoldValue(r, resources[r]));

    /// <summary>What a visit to a resource provider is worth on average, without rolling (the roll happens on the visit).</summary>
    private double ProviderValue(ResourceProvider provider) => provider.Rule switch
    {
        ProviderRule.WaterWheel => _state.Date.AbsoluteWeek == 1 ? ResourceProviderRules.WaterWheelFirstWeek : ResourceProviderRules.WaterWheelGold,
        ProviderRule.Warehouse => Math.Max(GoldValue(provider.Definition.Resource!.Value, provider.Definition.Amount),
            provider.Definition.Resource == ResourceType.Gold ? 0 : ResourceProviderRules.WarehouseGold),
        ProviderRule.Windmill => 4.5 * ((2 * GoldValue(ResourceType.Ore, 1)) + (3 * GoldValue(ResourceType.Crystal, 1))) / 5,
        _ => (ResourceProviderRules.GardenGold + GoldValue(ResourceType.Crystal, ResourceProviderRules.GardenMagic)) / 2,
    };

    private static double GoldValue(ResourceType resource, int amount) => amount * resource switch
    {
        ResourceType.Gold => 1,
        ResourceType.Lumber or ResourceType.Ore => 125,
        _ => 500,
    };

    private bool OwnTeam(PlayerId player, PlayerId? other) => other is { } o && (o == player || _state.AreAllies(o, player));

    /// <summary>The target tile of the best move, or null when there is nothing worth doing.</summary>
    private MapPosition? ChooseTarget(Hero hero, HashSet<MapPosition> failed)
    {
        // Stay out of reach of enemy heroes too strong for the hero (with the garrison, at an own town).
        var candidates = Targets(hero)
            .Where(t => !failed.Contains(t.Position) && !_claimed.Contains(t.Position) && t.Position != hero.Position)
            .Where(t => Holds(DefenceAt(hero, t.Position), Danger(hero.Owner, t.Position, except: t.Position)))
            .OrderByDescending(t => t.Value / (1 + (0.08 * Distance(hero.Position, t.Position))))
            .ThenBy(t => t.Position.Level).ThenBy(t => t.Position.Y).ThenBy(t => t.Position.X);

        MapPosition? best = null;
        var bestScore = 0.0;
        var checkedCount = 0;
        foreach (var candidate in candidates)
        {
            if (checkedCount >= CandidatesChecked)
            {
                break;
            }

            // Fights are played out before going: the army power sum does not see shooters, speed and defence.
            if (candidate.Opponent is { } opponent
                && !_estimator.Fight(hero, opponent, candidate.OpponentAttack, candidate.OpponentDefense).IsSafe(candidate.MaxLoss))
            {
                continue;
            }

            checkedCount++;
            if (session.Movement.FindPath(hero.Id, candidate.Position) is not { } path)
            {
                continue;
            }

            var days = path.TotalCost / (double)Math.Max(1, hero.MaxMovementPoints);
            var score = candidate.Value / (0.5 + days);
            if (score > bestScore)
            {
                (best, bestScore) = (candidate.Position, score);
            }
        }

        return best ?? Explore(hero, failed);
    }

    private static int Distance(MapPosition a, MapPosition b) =>
        a.Level != b.Level ? 40 + Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) : Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>A place worth going to. <see cref="Opponent"/> is who has to be beaten there, if anyone.</summary>
    private readonly record struct Target(
        MapPosition Position, double Value, Army? Opponent = null, int OpponentAttack = 0, int OpponentDefense = 0, double MaxLoss = 0.5);

    /// <summary>The neutral guards of a tile as one army, or null.</summary>
    private Army? GuardArmy(MapPosition position, bool pickup = false)
    {
        var stacks = GuardsToBeat(position, pickup).Take(Army.SlotCount).Select(m => new CreatureStack(m.Creature, Math.Max(1, m.Count ?? 1))).ToArray();
        return stacks.Length == 0 ? null : Army.Of(stacks);
    }

    /// <summary>Everything the hero knows about that is worth going to, with a value in gold. Fights are checked later.</summary>
    private IEnumerable<Target> Targets(Hero hero)
    {
        var owner = hero.Owner;
        var power = ArmyPower(hero.Army);

        // Loose pre-filter on the power sum; the real check is the simulated fight.
        bool Plausible(long enemy) => enemy == 0 || (!hero.Army.IsEmpty && power * 10 >= enemy * 7);

        foreach (var mapObject in _state.Objects.OrderBy(o => o.Id.Value))
        {
            var visit = mapObject.VisitPosition;
            if (!_state.Fog.IsExplored(owner, visit))
            {
                continue;
            }

            switch (mapObject)
            {
                case ResourcePile pile when Plausible(GuardPower(visit, pickup: true)):
                    yield return new(visit, GoldValue(pile.Resource, pile.Amount), GuardArmy(visit, pickup: true), MaxLoss: MonsterLoss);
                    break;
                case ArtifactPickup { Artifact: { } artifact } when !hero.Artifacts.BackpackFull && _state.Artifacts.TryGet(artifact, out var found) && Plausible(GuardPower(visit, pickup: true)):
                    yield return new(visit, 800 + (80 * Score(found)), GuardArmy(visit, pickup: true), MaxLoss: MonsterLoss);
                    break;
                case ResourceProvider provider when provider.CanCollect(hero.Id, _state.Date) && Plausible(GuardPower(visit)) && visit != hero.Position:
                    yield return new(visit, ProviderValue(provider), GuardArmy(visit), MaxLoss: MonsterLoss);
                    break;
                case TreasureChest when Plausible(GuardPower(visit, pickup: true)):
                    yield return new(visit, 1500, GuardArmy(visit, pickup: true), MaxLoss: MonsterLoss);
                    break;
                case Mine mine when !OwnTeam(owner, mine.Owner):
                    var mineGuard = mine.Guard is { } g ? new CreatureStack(g.Creature, Math.Max(1, g.Count ?? 1)) : null;
                    if (mineGuard is null || Plausible(Power(mineGuard)))
                    {
                        yield return new(visit, 8 * mine.DailyYield * GoldValue(mine.Resource, 1), mineGuard is null ? null : Army.Of(mineGuard));
                    }

                    break;
                case MonsterGuard monster when !hero.Army.IsEmpty:
                    var stack = new CreatureStack(monster.Creature, Math.Max(1, monster.Count ?? 1));
                    var monsterPower = Power(stack);
                    var joins = GuardService.JoinRatio(monster.Disposition) is { } ratio && power >= ratio * monsterPower && hero.Army.CanAccept(monster.Creature);
                    if (joins)
                    {
                        yield return new(visit, 150 + (monsterPower * 0.85));
                    }
                    else if (Plausible(monsterPower))
                    {
                        yield return new(visit, 150 + (monsterPower * 0.35), Army.Of(stack), MaxLoss: MonsterLoss);
                    }

                    break;
                case CreatureDwelling dwelling when Plausible(GuardPower(visit)) && visit != hero.Position:
                    // Flag it (so caravans can hire there) and buy what waits, if the hero can pay and has room.
                    var dwelt = _state.Creatures.Get(dwelling.Creature);
                    var buyable = hero.Army.CanAccept(dwelt.Id)
                        ? TownRules.MaxAffordable(_state.GetPlayer(owner).Resources, dwelt.Cost, dwelling.Available ?? 0) : 0;
                    var worth = (OwnTeam(owner, dwelling.Owner) ? 0 : 1200) + (0.4 * buyable * dwelt.Value);
                    if (worth > 0)
                    {
                        yield return new(visit, worth, GuardArmy(visit), MaxLoss: MonsterLoss);
                    }

                    break;
                case Town town when !OwnTeam(owner, town.Owner):
                    var garrison = ArmyPower(town.Garrison);
                    if (Plausible(garrison))
                    {
                        yield return new(visit, 4000 + (0.3 * garrison) + (town.Owner is null ? 0 : 2000) + (town.Dwellings.Count * 500), town.Garrison.IsEmpty ? null : town.Garrison);
                    }

                    break;
                case Town own when own.Owner == owner && own.VisitPosition != hero.Position:
                    // Defend it when an enemy can reach it, the garrison alone cannot hold it, nobody of ours is in it and the hero can.
                    var danger = Danger(owner, visit);
                    if (danger > 0 && !Holds(ArmyPower(own.Garrison), danger) && TownRules.VisitingHero(_state, own) is null
                        && Holds(power + ArmyPower(own.Garrison), danger))
                    {
                        yield return new(visit, 6000 + (own.Dwellings.Count * 500));
                        break;
                    }

                    var waiting = ArmyPower(own.Garrison) + own.Available.Sum(a => (long)a.Value * _state.Creatures.Get(a.Key).Value);
                    if (waiting * 5 > power * 3)
                    {
                        yield return new(visit, 0.3 * Math.Min(waiting, Math.Max(power, 1000)));
                    }

                    break;
            }
        }

        // A secondary hero carrying a worthwhile army brings it to the main hero (the meeting opens the exchange).
        if (hero.Id != _main && _main is { } mainId && _state.Heroes.FirstOrDefault(h => h.Id == mainId) is { } main
            && main.Position.Level == hero.Position.Level && (power >= CarryThreshold || power * 4 >= ArmyPower(main.Army)) && !main.Army.IsEmpty)
        {
            yield return new(main.Position, 0.5 * power);
        }

        foreach (var enemy in _state.Heroes.OrderBy(h => h.Id.Value))
        {
            if (!_state.AreEnemies(owner, enemy.Owner) || !_state.CanSee(owner, enemy) || !_state.Fog.IsExplored(owner, enemy.Position))
            {
                continue;
            }

            var theirs = ArmyPower(enemy.Army);
            if (!hero.Army.IsEmpty && power * 10 >= theirs * 7)
            {
                yield return new(enemy.Position, 2000 + (0.8 * theirs), enemy.Army, enemy.Attack, enemy.Defense);
            }
        }
    }

    /// <summary>With nothing to fight for: the nearest explored tile next to unexplored ground that the hero can reach.</summary>
    private MapPosition? Explore(Hero hero, HashSet<MapPosition> failed)
    {
        var map = _state.Map;
        var owner = hero.Owner;
        var frontier = new List<MapPosition>();
        foreach (var position in map.Positions(hero.Position.Level))
        {
            // Exploring never walks into anything: no heroes, objects or guarded ground (those are targets with a fight check).
            if (position == hero.Position || failed.Contains(position) || _claimed.Contains(position)
                || !_state.Fog.IsExplored(owner, position) || !_state.Terrains.IsPassable(map.GetTerrain(position))
                || _state.HeroAt(position) is not null || _state.ObjectCellAt(position) is not null || _state.IsGuarded(position)
                || !Holds(ArmyPower(hero.Army), Danger(owner, position)))
            {
                continue;
            }

            if (DirectionExtensions.All.Any(d => map.Contains(position.Step(d)) && !_state.Fog.IsExplored(owner, position.Step(d))))
            {
                frontier.Add(position);
            }
        }

        MapPosition? best = null;
        var bestCost = int.MaxValue;
        foreach (var position in frontier
                     .OrderBy(p => Distance(hero.Position, p)).ThenBy(p => p.Y).ThenBy(p => p.X)
                     .Take(FrontierChecked))
        {
            if (session.Movement.FindPath(hero.Id, position) is { } path && path.TotalCost < bestCost)
            {
                (best, bestCost) = (position, path.TotalCost);
            }
        }

        return best;
    }
}
