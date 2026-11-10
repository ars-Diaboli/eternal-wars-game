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
using EternalWars.Core.Domain.Magic;
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
public sealed partial class AdventureAi(GameSession session, bool controlHumanSlots = false)
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

    /// <summary>The refused commands with the game's reasons, for tuning (<c>sim game</c> prints them).</summary>
    public IReadOnlyList<string> Refusals => _refusals;

    private readonly List<string> _refusals = [];

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
        _gathering = false;
        _estimator.Clear();
        if (!ResolvePending(player))
        {
            return false;
        }

        if (!Active(player))
        {
            return true;
        }

        LeaveGarrisons(player);
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

        // The arena's preparation day ends with every army in the hero that fights at the start of day 2 (ADR-160).
        if (Preparing && !GatherForBattle(player))
        {
            return false;
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
            _refusals.Add($"day {_state.Date.AbsoluteDay}: {command}: {result.Error}");
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
                case TownGateDecision gate:
                    // The town the hero needs most, if it is on the list and free; else stay.
                    var gateTown = GateTown(_state.GetHero(gate.Hero))?.Town.Id;
                    command = new ChooseTownGateCommand(player, gate.Towns.Any(t => t.Town == gateTown && t.Available) ? gateTown : null);
                    break;
                case TrainingDecision training:
                    // A stat: the first (Attack, Spell Power); a price: gold when plenty is left, else the magic resources, else leave.
                    var gold = _state.GetPlayer(player).Resources.Gold;
                    var site = (TrainingSite)_state.GetObject(training.Site);
                    TrainingOption? pick = training.Options.FirstOrDefault(o => TrainingRules.Stat(o) is not null) is var stat && TrainingRules.Stat(stat) is not null
                        ? stat
                        : training.Options.Contains(TrainingOption.PayGold) && gold >= TrainingRules.Price(site.Rule, TrainingOption.PayGold).Gold + 3000
                            ? TrainingOption.PayGold
                            : training.Options.Contains(TrainingOption.PayMagicResources) ? TrainingOption.PayMagicResources : null;
                    command = new ChooseTrainingCommand(player, pick ?? (training.CanDecline ? null : training.Options[0]));
                    break;
                case CartographerDecision cartographer:
                    command = new AnswerCartographerCommand(player, _state.GetPlayer(player).Resources.Gold >= 3 * cartographer.Price);
                    break;
                case DepositDecision deposit:
                    // A mine is worth it while the price leaves the usual cushion.
                    command = new AnswerDepositCommand(player, Spare(player).CanAfford(deposit.Price));
                    break;
                case SphinxDecision:
                    command = new AnswerSphinxCommand(player, 0); // no knowledge of riddles: the first answer (its place is rolled)
                    break;
                case MapEventDecision:
                    // A computer player is shown no messages; a question takes the answer worth the most (EVT-6 a).
                    command = new AnswerMapEventCommand(player, BestAnswer());
                    break;
                case WitchHutDecision hut:
                    command = new ChooseWitchHutSkillCommand(player, SkillOffers.ComputerChoice(hut.Options));
                    break;
                case PandorasBoxDecision box:
                    // The AI knows what is inside (ADR-100): open when the box is worth something to the hero and any guard can be beaten safely.
                    var opener = _state.GetHero(box.Hero);
                    var pandora = (PandorasBox)_state.GetObject(box.Box);
                    command = new AnswerPandorasBoxCommand(player, BoxValue(opener, pandora.Contents) > 0
                        && (!pandora.IsGuarded || _estimator.Fight(opener, Seen(pandora.Guard, pandora.VisitPosition, null)).IsSafe(MonsterLoss)));
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
                    command = new ChooseSkillCommand(player, SkillOffers.ComputerChoice(levelUp.Options)!);
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

            // A player without heroes cannot take anything (ADR-114): it hires before it builds, and saves for a hero rather than build.
            var heroless = !_state.HeroesOf(player).Any() && town.Level(BuiltInBuildings.Tavern) > 0;
            if (heroless)
            {
                Hire(player, town, reserve: 0);
            }

            // The arena's preparation day (ADR-160): no tomorrow to save for.
            if (Preparing)
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

        if (Active(player) && _state.PendingDecision is null && !Preparing)
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

        var builtToday = TownRules.HasBuiltToday(_state, town);
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
                || TownRules.CheckCapitol(_state, town, next) is not null
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
            lines.Where(l => l.Tier == tier).OrderByDescending(l => (long)l.Value * TownRules.WeeklyGrowth(_state, town, l)).ThenBy(l => l.Id, StringComparer.Ordinal);
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
            yield return (true, line.Id);
        }

        yield return (false, BuiltInBuildings.Growth(1)); // the growth buildings (ADR-071), once their tier has a dwelling
        yield return (false, BuiltInBuildings.Growth(2));
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
            yield return (true, line.Id);
        }

        yield return (false, BuiltInBuildings.Fort);
        yield return (false, BuiltInBuildings.MageGuild);
        yield return (false, BuiltInBuildings.WarLodge);
        yield return (false, BuiltInBuildings.Tavern);
        yield return (false, BuiltInBuildings.TownHall); // the Capitol, once the Castle stands
    }

    /// <summary>
    /// Hires a hero while it has fewer than one more than its towns (at most four): the best offered one, its own returning heroes first
    /// (ADR-081), then warriors (mages have nothing to cast yet), when the gold is there beyond what it saves for a building.
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
            .OrderByDescending(h => TavernRules.Returning(_state, player, h.Id) is not null)
            .ThenByDescending(h => h.Class == HeroClass.Warrior).ThenByDescending(h => h.Attack + h.Defense).ThenBy(h => h.Id, StringComparer.Ordinal)
            .FirstOrDefault(h => TavernRules.CheckHire(_state, town, h.Id) is null);
        if (offer is not null)
        {
            Execute(new HireHeroCommand(player, town.Id, offer.Id));
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
                .GroupBy(s => s.Creature.HasTag(CreatureTags.Undead)).ToDictionary(g => g.Key, g => g.Sum(s => (long)s.Creature.Value * s.Count));
            var mostlyUndead = undead.GetValueOrDefault(true) > 0 && undead.GetValueOrDefault(true) >= undead.GetValueOrDefault(false);
            foreach (var (slot, stack) in slots.Stacks)
            {
                var creature = _state.Creatures.Get(stack.Creature);
                if (!UndeadTransformer.IsLiving(creature))
                {
                    continue;
                }

                var worth = (long)creature.Value * stack.Count;
                var best = new[] { CreatureCategory.A, CreatureCategory.B }
                    .Select(line => (Line: line, UndeadTransformer.Of(_state, town, stack.Creature, stack.Count, line).Transform))
                    .Where(t => t.Transform is not null)
                    .OrderByDescending(t => (long)_state.Creatures.Get(t.Transform!.To).Value * t.Transform.Result)
                    .FirstOrDefault();
                if (best.Transform is { } transform && (mostlyUndead || (long)_state.Creatures.Get(transform.To).Value * transform.Result >= worth))
                {
                    orders.Add(new TransformOrder(army, slot, best.Line));
                }
            }
        }

        if (orders.Count > 0)
        {
            Execute(new TransformCreaturesCommand(player, town.Id, orders));
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

        // The week's creature first (ADR-073): its doubled stock is the cheapest strength on offer this week.
        foreach (var line in lines.OrderByDescending(l => _state.WeekEvent.Growth(_state.Creatures, l.Id, 1) > 1).ThenByDescending(l => l.Tier).ThenByDescending(l => l.Value))
        {
            if (!Active(player) || town.AvailableOf(line.Id) <= 0)
            {
                continue;
            }

            var creature = town.DwellingLevel(line.Id) >= 2 ? line.UpgradesTo! : line.Id;
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

    /// <summary>A visiting hero with a school's Magic skill but no spell book buys one, with the same cushion as a war machine (ADR-110).</summary>
    private void BuySpellBook(PlayerId player, Town town, int reserve)
    {
        if (TownRules.VisitingHero(_state, town) is not { } hero || hero.HasSpellBook
            || !BuiltInSkills.Schools.Keys.Any(school => hero.SkillLevelOf(Magic.MagicRules.MagicSkill(school)) > SkillLevel.None))
        {
            return;
        }

        var spare = _state.GetPlayer(player).Resources.Gold - reserve - Magic.MagicRules.SpellBookGold;
        if (Active(player) && spare >= WarMachineCushion && Magic.MagicRules.CheckBuySpellBook(_state, town) is null)
        {
            Execute(new BuySpellBookCommand(player, town.Id));
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

        var dwellings = CaravanRules.Dwellings(_state, town)
            .Where(d => d.Available > 0)
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
    /// Sends the town's caravan for what waits at own resource providers (ADR-079, TOWN-9) when it is worth well over the caravan's price,
    /// most valuable first, as far as the caravan limit allows.
    /// </summary>
    private void FetchResources(PlayerId player, Town town)
    {
        var worth = 5 * Value(CaravanRules.ResourceCaravanCost);
        foreach (var provider in CaravanRules.Providers(_state, town).Where(p => Value(p.Stored) >= worth).OrderByDescending(p => Value(p.Stored)).ThenBy(p => p.Id.Value).ToList())
        {
            if (!Active(player))
            {
                return;
            }

            if (CaravanRules.CheckFetch(_state, town, provider) is null)
            {
                Execute(new FetchResourcesCommand(player, town.Id, provider.Id));
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

    /// <summary>
    /// A garrison hero comes out once no enemy that the garrison alone could not hold can reach the town, and leaves the weakest stack behind
    /// (as <see cref="Consolidate"/> does); it stays while it would leave the town open to a threat.
    /// </summary>
    private void LeaveGarrisons(PlayerId player)
    {
        foreach (var town in _state.ObjectsOf<Town>().Where(t => t.Owner == player && t.GarrisonHero is not null).OrderBy(t => t.Id.Value).ToList())
        {
            if (!Active(player) || _state.PendingDecision is not null || Danger(player, town.VisitPosition) > 0
                || TownRules.CheckLeaveGarrison(_state, town) is not null || !Execute(new LeaveGarrisonCommand(player, town.Id)).IsAccepted)
            {
                continue;
            }

            var hero = TownRules.VisitingHero(_state, town)!;
            if (hero.Army.StackCount >= 2)
            {
                var (slot, _) = hero.Army.Stacks.OrderBy(s => Power(s.Stack)).ThenBy(s => s.Slot).First();
                Execute(new MoveTownStackCommand(player, town.Id, TownArmy.VisitingHero, slot, TownArmy.Garrison, 0));
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

    /// <summary>What an artifact is worth to the AI (<see cref="ArtifactValue.Of(ArtifactDefinition)"/>).</summary>
    public static double Score(ArtifactDefinition artifact) => ArtifactValue.Of(artifact);

    /// <summary>What wearing it is worth: an assembled relic with its pieces' effects.</summary>
    private double Worth(ArtifactDefinition artifact) => ArtifactValue.Of(_state.Artifacts, artifact);

    /// <summary>
    /// What finding it is worth to the hero (ADR-121): a piece of an assembled relic counts half again for each other piece the hero already has
    /// (worn or carried), so the AI goes for the last pieces.
    /// </summary>
    private double FindScore(Hero hero, ArtifactDefinition artifact) =>
        Score(artifact) * (1 + (0.5 * (_state.Artifacts.RelicOf(artifact.Id) is { } relic
            ? _state.Artifacts.Get(relic).Pieces.Count(p => p != artifact.Id && hero.Artifacts.Has(p))
            : 0)));

    /// <summary>Puts on what is in the backpack when a slot is free or the new one is clearly better than the worst worn there.</summary>
    private void EquipBest(PlayerId player, Hero hero)
    {
        for (var guard = 0; guard < ArtifactSlots.BackpackSize * 2 && Active(player); guard++)
        {
            var candidate = hero.Artifacts.Backpack
                .Select(id => _state.Artifacts.TryGet(id, out var def) ? def : null)
                .OfType<ArtifactDefinition>()
                .Where(def => Worth(def) > 0 && Fits(hero, def) && (hero.Artifacts.HasRoomToWear(def.Slot) || Worth(def) > 1.2 * WorstWornScore(hero, def.Slot)))
                .OrderByDescending(Worth).ThenBy(def => def.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (candidate is null || !Execute(new EquipArtifactCommand(player, hero.Id, candidate.Id)).IsAccepted)
            {
                break;
            }
        }

        // The AI always joins an assembled relic it can (ADR-121).
        foreach (var relic in ArtifactService.Assemblable(_state.Artifacts, hero).ToList())
        {
            Execute(new AssembleRelicCommand(player, hero.Id, relic.Id));
        }
    }

    // An assembled relic needs the slots it blocks free; a worn piece of a relic the hero could finish is not swapped out.
    private bool Fits(Hero hero, ArtifactDefinition artifact) =>
        _state.Artifacts.BlockedSlots(artifact).GroupBy(s => s).All(g => hero.Artifacts.Free(g.Key) >= g.Count())
        && (hero.Artifacts.HasRoomToWear(artifact.Slot) || hero.Artifacts.Worn(artifact.Slot).Any(w => _state.Artifacts.RelicOf(w) is null));

    private double WorstWornScore(Hero hero, ArtifactSlot slot) =>
        hero.Artifacts.Worn(slot).Select(id => _state.Artifacts.TryGet(id, out var def) ? Worth(def) : 0).DefaultIfEmpty(0).Min();

    private bool PlayHero(PlayerId player, HeroId id)
    {
        var failed = new HashSet<MapPosition>();
        for (var move = 0; move < (Preparing ? ArenaMovesPerHero : MaxMovesPerHero); move++)
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
            UseServicesHere(player, hero);

            // The Grail's spot, known from the whole puzzle (ADR-140): dig with the day's full movement, or wait there for the next day.
            if (GrailSpot(player) == hero.Position && !hero.Artifacts.BackpackFull)
            {
                if (GrailRules.CheckDig(_state, hero) is null)
                {
                    Execute(new DigCommand(player, hero.Id));
                }

                return true;
            }

            var town = _state.ObjectCellAt(hero.Position) is { Object: Town own, Cell: FootprintCell.Visit } && own.Owner == player ? own : null;
            if (town is not null && ShouldHoldGarrison(hero, town))
            {
                // Inside, the hero leads the whole garrison behind the walls; at the entrance it would fight alone and the garrison after it (ADR-112).
                Execute(new EnterGarrisonCommand(player, town.Id));
                return true;
            }

            // Arrived at an own town on the way: take the garrison along now, or the troops would call the hero back every day (ADR-114).
            if (town is not null)
            {
                Consolidate(player, town);
            }

            if (Defending(hero))
            {
                return true;
            }

            if (hero.MovementPoints <= 0)
            {
                return true;
            }

            if (PortalHome(player, hero))
            {
                continue;
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

    /// <summary>
    /// Town Portal home (ADR-120): when the town it would take the hero to is threatened by an enemy the garrison alone cannot hold but the two
    /// together can, and the hero cannot walk there today.
    /// </summary>
    private bool PortalHome(PlayerId player, Hero hero)
    {
        if (Magic.AdventureMagic.Refusal(_state, hero, Magic.AdventureMagic.TownPortal, null) is not null
            || Magic.AdventureMagic.PortalTown(_state, hero) is not { } town)
        {
            return false;
        }

        var danger = Danger(player, town.VisitPosition);
        if (danger == 0 || Holds(ArmyPower(town.Garrison), danger) || !Holds(ArmyPower(hero.Army) + ArmyPower(town.Garrison), danger)
            || session.Movement.FindPath(hero.Id, town.VisitPosition) is { } path && path.TotalCost <= hero.MovementPoints)
        {
            return false;
        }

        return Execute(new CastAdventureSpellCommand(player, hero.Id, Magic.AdventureMagic.TownPortal)).IsAccepted;
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
        var spare = Preparing ? _state.GetPlayer(player).Resources : _state.GetPlayer(player).Resources - new ResourceSet(Gold: _state.GetPlayer(player).Resources.Gold / 3);
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

        // The main hero takes the war machines it lacks (ADR-070).
        foreach (var machine in feeder.WarMachines.Except(main.WarMachines).Order().ToList())
        {
            Execute(new GiveWarMachineCommand(player, feeder.Id, machine, main.Id));
        }

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

    /// <summary>
    /// Visible enemy heroes and how far (in tiles) each can reach in a day. Kept until the next command runs (only commands change the game), as
    /// every candidate target and flooded tile asks for it.
    /// </summary>
    private IReadOnlyList<(Hero Enemy, long Power, int Reach)> Threats(PlayerId owner)
    {
        if (_threatsAt != session.CommandLog.Count)
        {
            _threats.Clear();
            _threatsAt = session.CommandLog.Count;
        }

        if (!_threats.TryGetValue(owner, out var threats))
        {
            threats = [.. _state.Heroes
                .Where(h => _state.AreEnemies(owner, h.Owner) && _state.CanSee(owner, h) && _state.Fog.IsExplored(owner, h.Position))
                .Select(h => (h, ArmyPower(Seen(h)), (h.MaxMovementPoints / MovementCostPerTile) + 1))];
            _threats[owner] = threats;
        }

        return threats;
    }

    private readonly Dictionary<PlayerId, List<(Hero Enemy, long Power, int Reach)>> _threats = [];
    private int _threatsAt = -1;

    /// <summary>A rough tile cost for how far an enemy gets in a day: between the native 100 and the dearer terrains, diagonals included.</summary>
    private const int MovementCostPerTile = 150;

    /// <summary>The strongest enemy army that can reach the place within a day, leaving out the hero at <paramref name="except"/> (the one being attacked); 0 when none can.</summary>
    private long Danger(PlayerId owner, MapPosition position, MapPosition? except = null) =>
        Preparing ? 0 : Threats(owner).Where(t => t.Enemy.Position != except && t.Enemy.Position.Level == position.Level && Distance(t.Enemy.Position, position) <= t.Reach)
            .Select(t => t.Power).DefaultIfEmpty(0).Max();

    /// <summary>What defends a place for the player: the hero's army, plus the garrison when it is the entrance of an own town.</summary>
    private long DefenceAt(Hero hero, MapPosition position) =>
        ArmyPower(hero.Army) + (_state.ObjectCellAt(position) is { Object: Town town, Cell: FootprintCell.Visit } && town.Owner == hero.Owner ? ArmyPower(town.Garrison) : 0);

    private static bool Holds(long defence, long danger) => danger == 0 || defence >= danger * SafeShare;

    /// <summary>
    /// Whether a hero defending its town should move into the garrison (ADR-112): only a secondary hero while the main hero is out (a garrison hero
    /// never retreats, so the player's last hero does not go in), and only when the hero and the garrison together at least match the danger.
    /// </summary>
    private bool ShouldHoldGarrison(Hero hero, Town town) =>
        hero.Id != _main && _main is { } main && _state.Heroes.Any(h => h.Id == main) && Defending(hero)
        && DefenceAt(hero, hero.Position) >= Danger(hero.Owner, hero.Position) && TownRules.CheckEnterGarrison(_state, town) is null;

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
        GuardsToBeat(position, pickup).Sum(m => Power(SeenMonster(m)));

    /// <summary>
    /// What the player knows of armies it does not own (ADR-125): the counts its heroes' Scouting shows, the middle of each amount's range for
    /// the rest. <paramref name="owner"/>: the army's owner, null for neutral.
    /// </summary>
    private Army Seen(IEnumerable<CreatureStack> stacks, MapPosition at, PlayerId? owner) =>
        AdventureIntel.Reckoned(AdventureIntel.ScoutStacks(_state, stacks, at, owner, _state.CurrentPlayer.Id));

    private Army Seen(Hero enemy) => AdventureIntel.Reckoned(AdventureIntel.ScoutHero(_state, enemy, _state.CurrentPlayer.Id).Stacks);

    private CreatureStack SeenMonster(MonsterGuard monster) =>
        Seen([new CreatureStack(monster.Creature, Math.Max(1, monster.Count ?? 1))], monster.Position, null)[0]!;

    /// <summary>The stacks a hero entering <paramref name="position"/> must beat: a pickup only by its Area-mode guards (ADR-065).</summary>
    private IEnumerable<MonsterGuard> GuardsToBeat(MapPosition position, bool pickup) =>
        _state.GuardsOf(position).Where(m => !pickup || m.Mode == GuardMode.Area);

    private static double Value(ResourceSet resources) => Enum.GetValues<ResourceType>().Sum(r => GoldValue(r, resources[r]));

    /// <summary>
    /// What a visit to a resource provider is worth: what waits at an own one; for another's, a week's output on average (what flagging a
    /// neutral one brings at once), doubled for the weeks to come (ADR-079).
    /// </summary>
    private double ProviderValue(Hero hero, ResourceProvider provider) =>
        provider.Owner == hero.Owner ? Value(provider.Stored) : 2 * Math.Max(Value(provider.Stored), WeeklyValue(provider));

    /// <summary>A provider's average week, without rolling.</summary>
    private double WeeklyValue(ResourceProvider provider) => provider.Rule switch
    {
        ProviderRule.WaterWheel => _state.Date.AbsoluteWeek == 1 ? ResourceProviderRules.WaterWheelFirstWeek : ResourceProviderRules.WaterWheelGold,
        ProviderRule.Warehouse when provider.Definition.Resource == ResourceType.Gold => provider.Definition.Amount,
        ProviderRule.Warehouse => (GoldValue(provider.Definition.Resource!.Value, provider.Definition.Amount) + ResourceProviderRules.WarehouseGold) / 2,
        ProviderRule.Windmill => 4.5 * ((2 * GoldValue(ResourceType.Ore, 1)) + (3 * GoldValue(ResourceType.Crystal, 1))) / 5,
        _ => (ResourceProviderRules.GardenGold + GoldValue(ResourceType.Crystal, ResourceProviderRules.GardenMagic)) / 2,
    };

    /// <summary>What training is worth to the hero now (0: not worth the trip, or the player cannot pay with gold to spare).</summary>
    private double TrainingValue(Hero hero, TrainingRule rule)
    {
        var gold = _state.GetPlayer(hero.Owner).Resources.Gold;
        return rule switch
        {
            TrainingRule.LearningStone => 1200,
            TrainingRule.Arena or TrainingRule.ColosseumOfTheMagi => 1500,
            TrainingRule.LibraryOfEnlightenment => hero.Level >= TrainingRules.LibraryLevel ? 3000 : 0,
            TrainingRule.Gazebo => gold >= TrainingRules.GazeboGold + 3000 ? 1000 : 0,
            TrainingRule.DreamTeacher => 600,
            TrainingRule.TrainingGrounds or TrainingRule.MarlettoTower or TrainingRule.StarAxis or TrainingRule.GardenOfRevelation => 750,
            _ => gold >= TrainingRules.TreeGold + 3000 ? 1500 : 0,
        };
    }

    /// <summary>
    /// What going to an own town is worth to the hero (0: nothing to do there): defending it when an enemy can reach it, the garrison alone cannot
    /// hold it, nobody of ours is in it and the hero can; else picking up the troops waiting there when they are worth the trip.
    /// </summary>
    private double TownNeed(Hero hero, Town own)
    {
        var power = ArmyPower(hero.Army);
        var danger = Danger(hero.Owner, own.VisitPosition);
        if (danger > 0 && !Holds(ArmyPower(own.Garrison), danger) && TownRules.VisitingHero(_state, own) is null
            && Holds(power + ArmyPower(own.Garrison), danger))
        {
            return 6000 + (own.Dwellings.Count * 500);
        }

        // Creatures in the dwellings count only as far as the gold buys them, or they would call the hero home every day (ADR-114).
        var resources = _state.GetPlayer(hero.Owner).Resources;
        var waiting = ArmyPower(own.Garrison) + own.Available.Sum(a => _state.Creatures.Get(a.Key) is var line
            ? (long)TownRules.MaxAffordable(resources, TownRules.CreatureCost(_state, own, line), a.Value) * line.Value
            : 0);
        return waiting * 5 > power * 3 ? 0.3 * Math.Min(waiting, Math.Max(power, 1000)) : 0;
    }

    /// <summary>The own town a town gate should take the hero to: the one it needs most whose entrance is free; null when none is worth it.</summary>
    private (Town Town, double Need)? GateTown(Hero hero) =>
        _state.ObjectsOf<Town>()
            .Where(t => t.Owner == hero.Owner && t.VisitPosition != hero.Position && _state.HeroAt(t.VisitPosition) is null)
            .Select(t => (Town: t, Need: TownNeed(hero, t)))
            .Where(t => t.Need > 0)
            .OrderByDescending(t => t.Need).ThenBy(t => t.Town.Id.Value)
            .Select(t => ((Town, double)?)t)
            .FirstOrDefault();

    /// <summary>The gold kept back when spending at a map object: a third of the gold.</summary>
    private ResourceSet Spare(PlayerId player)
    {
        var resources = _state.GetPlayer(player).Resources;
        return resources - new ResourceSet(Gold: resources.Gold / 3);
    }

    /// <summary>The stacks a hill fort would upgrade for the hero, strongest first, while the spare resources last (ADR-068).</summary>
    private List<(int Slot, CreatureStack Stack, ResourceSet Cost, long Gain)> HillFortUpgrades(Hero hero)
    {
        var spare = Spare(hero.Owner);
        var upgrades = new List<(int, CreatureStack, ResourceSet, long)>();
        foreach (var (slot, stack) in hero.Army.Stacks.OrderByDescending(s => Power(s.Stack)).ThenBy(s => s.Slot))
        {
            if (HillFortRules.UpgradeOf(_state.Creatures, stack.Creature) is not { } upgraded)
            {
                continue;
            }

            var cost = HillFortRules.Cost(_state.Creatures, stack);
            if (spare.CanAfford(cost))
            {
                spare -= cost;
                // Worth what the upgrade costs in a town: the army power sum barely sees what an upgrade adds.
                upgrades.Add((slot, stack, cost, (long)Value((upgraded.Cost - _state.Creatures.Get(stack.Creature).Cost) * stack.Count)));
            }
        }

        return upgrades;
    }

    /// <summary>What a visit to a hill fort is worth now: the town price of the upgrades the hero would buy.</summary>
    private long HillFortGain(Hero hero) => HillFortUpgrades(hero).Sum(u => u.Gain);

    /// <summary>The war machines the hero lacks and would buy at a factory, as in a town (ADR-056), with the gold cushion kept.</summary>
    private List<WarMachineSlot> FactoryWants(Hero hero)
    {
        var wanted = new List<WarMachineSlot>();
        if (WarMachines.FieldsOffensive(hero.SkillLevelOf(BuiltInSkills.Ballistics)))
        {
            wanted.Add(WarMachineSlot.Offensive);
        }

        wanted.Add(WarMachineSlot.Tent);
        if (hero.Army.Stacks.Any(s => _state.Creatures.Get(s.Stack.Creature).Shots > 0))
        {
            wanted.Add(WarMachineSlot.Cart);
        }

        var gold = _state.GetPlayer(hero.Owner).Resources.Gold;
        var bought = new List<WarMachineSlot>();
        foreach (var slot in wanted.Where(s => !hero.WarMachines.Contains(s)))
        {
            if (gold - WarMachines.Cost(slot).Gold >= WarMachineCushion)
            {
                gold -= WarMachines.Cost(slot).Gold;
                bought.Add(slot);
            }
        }

        return bought;
    }

    /// <summary>A hero on a hill fort or war machine factory entrance uses it (ADR-068).</summary>
    private void UseServicesHere(PlayerId player, Hero hero)
    {
        switch (_state.ObjectCellAt(hero.Position))
        {
            case { Object: HillFort fort, Cell: FootprintCell.Visit }:
                foreach (var (slot, _, _, _) in HillFortUpgrades(hero))
                {
                    if (Active(player) && _state.PendingDecision is null)
                    {
                        Execute(new UpgradeAtHillFortCommand(player, fort.Id, slot));
                    }
                }

                break;
            case { Object: WarMachineFactory factory, Cell: FootprintCell.Visit }:
                foreach (var slot in FactoryWants(hero))
                {
                    if (Active(player) && _state.PendingDecision is null)
                    {
                        Execute(new BuyWarMachineAtFactoryCommand(player, factory.Id, slot));
                    }
                }

                break;
            case { Object: SkillSchool school, Cell: FootprintCell.Visit }:
                foreach (var lesson in LessonsToTake(hero, school))
                {
                    if (Active(player) && _state.PendingDecision is null)
                    {
                        Execute(new TakeLessonCommand(player, school.Id, lesson.Skill));
                    }
                }

                break;
        }
    }

    /// <summary>
    /// The lessons the hero would take at a University or War Academy (ADR-139): its open lessons while the spare resources last, skills it already
    /// learns first, a new group only while it has a free group slot.
    /// </summary>
    private List<Lesson> LessonsToTake(Hero hero, SkillSchool school)
    {
        var spare = Spare(hero.Owner);
        var groups = hero.Skills.Groups.Count();
        var taken = new List<Lesson>();
        foreach (var lesson in SkillSchoolRules.LessonsOf(school, hero.Id).Where(l => SkillSchoolRules.StateOf(hero, l) == LessonState.Open).OrderBy(l => l.NewGroup))
        {
            if (!spare.CanAfford(SkillSchoolRules.Price) || (lesson.NewGroup && groups >= HeroSkills.MaxGroups))
            {
                continue;
            }

            groups += lesson.NewGroup ? 1 : 0;
            spare -= SkillSchoolRules.Price;
            taken.Add(lesson);
        }

        return taken;
    }

    /// <summary>
    /// What a visit to a University or War Academy is worth: the lessons it would take; before the first visit (the lessons are drawn then) a
    /// guess of one lesson when the player can pay for one.
    /// </summary>
    private double SchoolValue(Hero hero, SkillSchool school) =>
        school.Lessons.ContainsKey(hero.Id) ? LessonValue * LessonsToTake(hero, school).Count
        : Spare(hero.Owner).CanAfford(SkillSchoolRules.Price) ? LessonValue : 0;

    /// <summary>A skill level bought at a school, in gold (a draft beside a level-up's worth).</summary>
    private const double LessonValue = 1200;

    /// <summary>An obelisk the player has not visited yet, in gold (a draft: a step toward the Grail, ADR-140).</summary>
    private const double ObeliskValue = 1000;

    /// <summary>Digging up the Grail, and bringing it into a town without a Grail building, in gold (a draft: a building worth +5000 gold a day).</summary>
    private const double GrailValue = 12000;

    /// <summary>Where the Grail lies when the player's puzzle is whole and nobody has dug it up yet (ADR-140), else null.</summary>
    private MapPosition? GrailSpot(PlayerId player) =>
        _state.Grail.IsFound ? null : GrailRules.Puzzle(_state, player)?.Grail;

    /// <summary>
    /// What a Pandora's Box is worth to the hero, by the AI's values (ADR-100): resources at their gold value, experience 1:1, a primary stat as a
    /// training site's (750), morale and luck a buff's (150 a point), a skill 1000 a level gained, artifacts as pickups (only while the backpack has
    /// room) and creatures at 85 % of their value where they fit. Mana counts for nothing until there are spells.
    /// </summary>
    private double BoxValue(Hero hero, BoxContents contents)
    {
        var stats = Enum.GetValues<PrimaryStat>().Sum(contents.Stat);
        var skills = contents.Skills.Where(s => hero.Skills.CanRaise(s.Key)).Sum(s => Math.Max(0, s.Value - hero.Skills.Level(s.Key)));
        var artifacts = hero.Artifacts.BackpackFull ? 0 : contents.Artifacts.Sum(a => _state.Artifacts.TryGet(a, out var found) ? 800 + (80 * FindScore(hero, found)) : 0);
        var creatures = contents.Creatures.Where(s => hero.Army.CanAccept(s.Creature)).Sum(s => 0.85 * s.Count * _state.Creatures.Get(s.Creature).Value);
        return Value(contents.Resources) + contents.Experience + (750 * stats) + (150 * (contents.Morale + contents.Luck)) + (1000 * skills) + artifacts + creatures;
    }

    /// <summary>A buff is worth a short detour at most; the Magic Well nothing until there are spells.</summary>
    private static double BuffValue(BuffRule rule) => rule switch
    {
        BuffRule.Stables => 400,
        BuffRule.FountainOfYouth or BuffRule.MineralSpring or BuffRule.RallyFlag => 300,
        BuffRule.FaerieRing or BuffRule.Temple or BuffRule.CloverField or BuffRule.Rainbow => 150,
        _ => 0,
    };

    /// <summary>A loot site's average worth by the AI's values (a Minor artifact counted as 1000, the tomb's morale as a cost).</summary>
    private static double LootValue(LootRule rule)
    {
        var resource = ResourceProviderRules.NonGold.Average(r => GoldValue(r, 1));
        return rule switch
        {
            LootRule.Campfire => 500 + (5 * resource),
            LootRule.SupplyCrates => 10 * resource,
            LootRule.TravelersWagon => (0.3 * 1000) + (0.6 * 3.5 * resource),
            LootRule.Corpse => 0.2 * 1000,
            LootRule.LeanTo => 3.5 * resource,
            LootRule.MagesChest => (0.9 * 1000) + (0.05 * 2500) + (0.3 * 750),
            _ => 1200 - 400,
        };
    }

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
            .Where(t => !(Preparing && t.Later) && !failed.Contains(t.Position) && !_claimed.Contains(t.Position) && t.Position != hero.Position)
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
                && !_estimator.Fight(hero, opponent, candidate.OpponentAttack, candidate.OpponentDefense, candidate.Siege).IsSafe(Preparing ? Math.Min(candidate.MaxLoss, ArenaLoss) : candidate.MaxLoss))
            {
                continue;
            }

            checkedCount++;
            if (session.Movement.FindPath(hero.Id, candidate.Position) is not { } path)
            {
                continue;
            }

            var score = candidate.Value / (0.5 + Days(hero, path.TotalCost));
            if (score > bestScore)
            {
                (best, bestScore) = (candidate.Position, score);
            }
        }

        // Unexplored ground is worth a trip of its own (it hides what the targets are made of), not only when nothing else is left.
        if (Explore(hero, failed) is { } explore && ExploreWorth(hero) / (0.5 + Days(hero, explore.Cost)) > bestScore)
        {
            return explore.Position;
        }

        return best;
    }

    /// <summary>A guard or garrison next to unexplored ground is also worth the exploring it opens (passage guards stand between zones).</summary>
    private double OpensTheWay(PlayerId owner, MapPosition position)
    {
        for (var dy = -WayRadius; dy <= WayRadius; dy++)
        {
            for (var dx = -WayRadius; dx <= WayRadius; dx++)
            {
                var near = new MapPosition(position.X + dx, position.Y + dy, position.Level);
                if (_state.Map.Contains(near) && !_state.Fog.IsExplored(owner, near))
                {
                    return ExploreValue;
                }
            }
        }

        return 0;
    }

    private const int WayRadius = 3;

    private static double Days(Hero hero, int cost) => cost / (double)Math.Max(1, hero.MaxMovementPoints);

    /// <summary>What reaching unexplored ground is worth, against the targets' values (a small treasure).</summary>
    private const double ExploreValue = 900;

    /// <summary>The share of its army power a main hero puts on exploring while it knows no enemy town: a strong army goes looking for one.</summary>
    private const double HuntShare = 0.1;

    /// <summary>What exploring is worth to the hero now: <see cref="ExploreValue"/>, more for the main hero while no enemy town is known.</summary>
    private double ExploreWorth(Hero hero) =>
        ExploreValue + (hero.Id == _main && !Preparing && !_state.ObjectsOf<Town>().Any(t => t.Owner is { } o && _state.AreEnemies(hero.Owner, o) && _state.Fog.IsExplored(hero.Owner, t.VisitPosition))
            ? HuntShare * ArmyPower(hero.Army) : 0);

    /// <summary>
    /// For tuning (<c>sim game --explain</c>): the hero's best candidate targets as <see cref="ChooseTarget"/> sees them, with their value, the
    /// opponent's power, whether the simulated fight is safe and the route's cost; then the exploring tile. Changes nothing.
    /// </summary>
    public IReadOnlyList<string> Explain(HeroId id, int count = 12)
    {
        var hero = _state.GetHero(id);
        var lines = new List<string> { $"{hero.Name} at {hero.Position}, power {ArmyPower(hero.Army)}, movement {hero.MovementPoints}/{hero.MaxMovementPoints}, main {hero.Id == _main}, defending {Defending(hero)}" };
        foreach (var t in Targets(hero)
                     .OrderByDescending(t => t.Value / (1 + (0.08 * Distance(hero.Position, t.Position))))
                     .Take(count))
        {
            var what = _state.ObjectCellAt(t.Position)?.Object.Definition.Id ?? (_state.HeroAt(t.Position) is { } h ? $"hero {h.Name}" : "guarded tile");
            var danger = Danger(hero.Owner, t.Position, except: t.Position);
            var safe = t.Opponent is not { } opponent ? "no fight"
                : _estimator.Fight(hero, opponent, t.OpponentAttack, t.OpponentDefense, t.Siege).IsSafe(t.MaxLoss) ? $"safe vs {ArmyPower(opponent)}" : $"UNSAFE vs {ArmyPower(opponent)}";
            var path = session.Movement.FindPath(hero.Id, t.Position) is { } p ? $"path {p.TotalCost}" : "NO PATH";
            lines.Add($"  {t.Position} {what}: value {t.Value:0}, {safe}, {path}{(Holds(DefenceAt(hero, t.Position), danger) ? string.Empty : $", danger {danger}")}");
        }

        lines.Add($"  explore: {(Explore(hero, []) is { } e ? $"{e.Position} cost {e.Cost}" : "nothing")}");
        return lines;
    }

    private static int Distance(MapPosition a, MapPosition b) =>
        a.Level != b.Level ? 40 + Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) : Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>A place worth going to. <see cref="Opponent"/> is who has to be beaten there, if anyone; <see cref="Siege"/> the walls it holds (ADR-106).</summary>
    private readonly record struct Target(
        MapPosition Position, double Value, Army? Opponent = null, int OpponentAttack = 0, int OpponentDefense = 0, double MaxLoss = 0.5,
        SiegeSpec? Siege = null, bool Later = false);

    /// <summary>The neutral guards of a tile as one army, or null.</summary>
    private Army? GuardArmy(MapPosition position, bool pickup = false)
    {
        var stacks = GuardsToBeat(position, pickup).Take(Army.SlotCount).Select(SeenMonster).ToArray();
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
                    yield return new(visit, GoldValue(pile.Resource, pile.Amount ?? ResourcePile.AmountRange(pile.Resource).Min), GuardArmy(visit, pickup: true), MaxLoss: MonsterLoss);
                    break;
                case ArtifactPickup { Artifact: { } artifact } when !hero.Artifacts.BackpackFull && _state.Artifacts.TryGet(artifact, out var found) && Plausible(GuardPower(visit, pickup: true)):
                    yield return new(visit, 800 + (80 * FindScore(hero, found)), GuardArmy(visit, pickup: true), MaxLoss: MonsterLoss);
                    break;
                case ResourceProvider provider when (provider.Owner != hero.Owner || !provider.Stored.IsEmpty) && Plausible(GuardPower(visit)) && visit != hero.Position:
                    yield return new(visit, ProviderValue(hero, provider), GuardArmy(visit), MaxLoss: MonsterLoss, Later: provider.Stored.IsEmpty);
                    break;
                case CreatureBank { IsGuarded: true } bank when visit != hero.Position && !hero.Army.IsEmpty:
                    // Worth its reward; the simulated fight later decides whether the hero can take it (the guard stays whole if it fails).
                    var bankGuard = Seen(bank.Guard, visit, null);
                    if (Plausible(ArmyPower(bankGuard)))
                    {
                        yield return new(visit, Value(bank.Reward) + (bank.RewardArtifact is null ? 0 : 1500) + (1500 * bank.MoreArtifacts.Count), bankGuard, MaxLoss: MonsterLoss);
                    }

                    break;
                case PandorasBox box when visit != hero.Position && (!box.IsGuarded || !hero.Army.IsEmpty) && BoxValue(hero, box.Contents) is > 0 and var boxWorth:
                    // Weighed by its contents against its own guard, else the monsters next to it; the simulated fight later decides.
                    var boxGuard = box.IsGuarded ? Seen(box.Guard, visit, null) : GuardArmy(visit);
                    if (Plausible(boxGuard is null ? 0 : ArmyPower(boxGuard)))
                    {
                        yield return new(visit, boxWorth, boxGuard, MaxLoss: MonsterLoss);
                    }

                    break;
                case QuestSite site when visit != hero.Position && QuestWorth(hero, site) is > 0 and var questWorth && Plausible(GuardPower(visit)):
                    // A Seer's Hut or Quest Guard whose stage the hero meets and which gives more than it takes (EVT-6); the others are obstacles.
                    yield return new(visit, questWorth, GuardArmy(visit), MaxLoss: MonsterLoss);
                    break;
                case Prison { Hero: not null } when !_state.HeroLimitReached(owner) && Plausible(GuardPower(visit)) && visit != hero.Position:
                    yield return new(visit, 2500, GuardArmy(visit), MaxLoss: MonsterLoss, Later: true); // a hero without an army
                    break;
                case Observatory observatory when !observatory.Visits.Heroes.Any(h => _state.Heroes.Any(x => x.Id == h && x.Owner == owner))
                                                  && Plausible(GuardPower(visit)) && visit != hero.Position:
                    yield return new(visit, 600, GuardArmy(visit), MaxLoss: MonsterLoss, Later: true);
                    break;
                case TrainingSite training when training.CanCollect(hero.Id, _state.Date) && TrainingValue(hero, training.Rule) is > 0 and var trainingWorth
                                                 && Plausible(GuardPower(visit)) && visit != hero.Position:
                    yield return new(visit, trainingWorth, GuardArmy(visit), MaxLoss: MonsterLoss);
                    break;
                case BuffSite buff when buff.CanCollect(hero.Id, _state.Date) && BuffValue(buff.Rule) is > 0 and var buffWorth
                                        && Plausible(GuardPower(visit)) && visit != hero.Position:
                    yield return new(visit, buffWorth, GuardArmy(visit), MaxLoss: MonsterLoss);
                    break;
                case LootSite site when site.CanCollect(hero.Id, _state.Date) && Plausible(GuardPower(visit, pickup: site.Vanishes)) && visit != hero.Position:
                    yield return new(visit, LootValue(site.Rule), GuardArmy(visit, pickup: site.Vanishes), MaxLoss: MonsterLoss);
                    break;
                case TreasureChest when Plausible(GuardPower(visit, pickup: true)):
                    yield return new(visit, 1500, GuardArmy(visit, pickup: true), MaxLoss: MonsterLoss);
                    break;
                case Deposit deposit when Spare(owner).CanAfford(deposit.Price) && Plausible(GuardPower(visit)) && visit != hero.Position:
                    var mineYield = _state.ObjectCatalog.Get(deposit.Definition.Becomes!);
                    yield return new(visit, (8 * mineYield.DailyYield * GoldValue(mineYield.Resource!.Value, 1)) - (0.5 * Value(deposit.Price)), GuardArmy(visit), MaxLoss: MonsterLoss, Later: true);
                    break;
                case Shrine { Spell: { } taught } shrine when !hero.Spells.ContainsKey(taught) && _state.Spells.TryGet(taught, out var shrineSpell)
                                                          && Magic.MagicRules.CanLearn(hero, shrineSpell) && Plausible(GuardPower(visit)) && visit != hero.Position:
                    // A spell it can learn (ADR-120): worth more the higher its level.
                    yield return new(visit, 400 * shrine.SpellLevel, GuardArmy(visit), MaxLoss: MonsterLoss);
                    break;
                case KeymasterTent tent when !_state.GetPlayer(owner).Keys.Contains(tent.Colour) && Plausible(GuardPower(visit)) && visit != hero.Position:
                    // The key opens the border posts of its colour (ADR-071): worth most once one is known to bar the way.
                    var barred = _state.ObjectsOf<BorderPost>().Any(b => b.Colour == tent.Colour && _state.Fog.IsExplored(owner, b.VisitPosition));
                    yield return new(visit, barred ? 2500 : 600, GuardArmy(visit), MaxLoss: MonsterLoss, Later: !barred);
                    break;
                case MapGarrison mapGarrison when !OwnTeam(owner, mapGarrison.Owner) && visit != hero.Position
                                                  && Seen(mapGarrison.Troops.Stacks.Select(s => s.Stack), visit, mapGarrison.Owner) is var troops && Plausible(ArmyPower(troops)):
                    yield return new(visit, 1000 + (0.35 * ArmyPower(troops)) + OpensTheWay(owner, visit), troops.IsEmpty ? null : troops, MaxLoss: MonsterLoss, Later: true);
                    break;
                case Mine mine when !OwnTeam(owner, mine.Owner):
                    var mineGuard = mine.Guard is { } g ? Seen([new CreatureStack(g.Creature, Math.Max(1, g.Count ?? 1))], visit, null)[0] : null;
                    if (mineGuard is null || Plausible(Power(mineGuard)))
                    {
                        yield return new(visit, 8 * mine.DailyYield * GoldValue(mine.Resource, 1), mineGuard is null ? null : Army.Of(mineGuard), Later: true);
                    }

                    break;
                case MonsterGuard monster when !hero.Army.IsEmpty:
                    var stack = SeenMonster(monster);
                    var monsterPower = Power(stack);
                    var joins = GuardService.JoinRatio(monster.Disposition) is { } ratio && power >= ratio * monsterPower && hero.Army.CanAccept(monster.Creature);
                    if (joins)
                    {
                        yield return new(visit, 150 + (monsterPower * 0.85));
                    }
                    else if (Plausible(monsterPower))
                    {
                        yield return new(visit, 150 + (monsterPower * 0.35) + OpensTheWay(owner, visit), Army.Of(stack), MaxLoss: MonsterLoss);
                    }

                    break;
                case CreatureDwelling { Guard: { } dwellingGuard } guardedDwelling when visit != hero.Position:
                    // Beat the guard to flag it (ADR-068); worth the dwelling plus the experience.
                    var guardStack = Seen([new CreatureStack(dwellingGuard.Creature, Math.Max(1, dwellingGuard.Count ?? 1))], visit, null)[0]!;
                    if (Plausible(Power(guardStack) + GuardPower(visit)))
                    {
                        yield return new(visit, 1200 + (0.35 * Power(guardStack)), Army.Of(guardStack), MaxLoss: MonsterLoss);
                    }

                    break;
                case CreatureDwelling dwelling when Plausible(GuardPower(visit)) && visit != hero.Position:
                    // Flag it (so caravans can hire there) and buy what waits, if the hero can pay and has room.
                    var dwelt = _state.Creatures.Get(dwelling.Creature);
                    var buyable = hero.Army.CanAccept(dwelt.Id)
                        ? TownRules.MaxAffordable(_state.GetPlayer(owner).Resources, dwelt.Cost, dwelling.Available ?? 0) : 0;
                    var worth = (OwnTeam(owner, dwelling.Owner) || Preparing ? 0 : 1200) + (0.4 * buyable * dwelt.Value);
                    if (worth > 0)
                    {
                        yield return new(visit, worth, GuardArmy(visit), MaxLoss: MonsterLoss);
                    }

                    break;
                // On an arena map's preparation day only neutral towns: an enemy's brings its heroes onto ours before the battle (ADR-160).
                case Town town when !OwnTeam(owner, town.Owner) && !(Preparing && town.Owner is not null):
                    var defenders = AdventureIntel.Reckoned(AdventureIntel.ScoutTown(_state, town, owner).Garrison);
                    var garrison = ArmyPower(defenders);
                    if (Plausible(garrison))
                    {
                        yield return new(visit, 4000 + (0.3 * garrison) + (town.Owner is null ? 0 : 2000) + (town.Dwellings.Count * 500), defenders.IsEmpty ? null : defenders,
                            Siege: SiegeRules.SpecOf(town));
                    }

                    break;
                case Town own when own.Owner == owner && own.VisitPosition != hero.Position && GrailRules.Carries(_state, hero) && own.Level(BuiltInBuildings.Grail) == 0
                                   && TownRules.Buildings(_state, own).Contains(BuiltInBuildings.Grail):
                    // The Grail raises the town's Grail building (ADR-140).
                    yield return new(visit, GrailValue, Later: true);
                    break;
                case Town own when own.Owner == owner && own.VisitPosition != hero.Position && TownNeed(hero, own) is > 0 and var need:
                    yield return new(visit, need);
                    break;
                case TownGate when visit != hero.Position && GateTown(hero) is { Need: var gateNeed } && Plausible(GuardPower(visit)):
                    // The gate is worth (a little less than) the town it leads to; going there through the gate costs no movement (ADR-068).
                    yield return new(visit, 0.9 * gateNeed, GuardArmy(visit), MaxLoss: MonsterLoss, Later: true);
                    break;
                case HillFort when visit != hero.Position && HillFortGain(hero) is > 0 and var gain && Plausible(GuardPower(visit)):
                    yield return new(visit, gain, GuardArmy(visit), MaxLoss: MonsterLoss);
                    break;
                case WarMachineFactory when visit != hero.Position && FactoryWants(hero).Count is > 0 and var machines && Plausible(GuardPower(visit)):
                    yield return new(visit, 300 * machines, GuardArmy(visit), MaxLoss: MonsterLoss);
                    break;
                case SkillSchool school when visit != hero.Position && SchoolValue(hero, school) is > 0 and var schoolWorth && Plausible(GuardPower(visit)):
                    yield return new(visit, schoolWorth, GuardArmy(visit), MaxLoss: MonsterLoss);
                    break;
                case Obelisk obelisk when _state.Grail.Position is not null && !_state.Grail.IsFound && visit != hero.Position
                                          && !obelisk.WasVisitedBy(owner) && Plausible(GuardPower(visit)):
                    yield return new(visit, ObeliskValue, GuardArmy(visit), MaxLoss: MonsterLoss, Later: true);
                    break;
            }
        }

        if (GrailSpot(owner) is { } grail && grail != hero.Position && !hero.Artifacts.BackpackFull && Plausible(GuardPower(grail)))
        {
            yield return new(grail, GrailValue, GuardArmy(grail), MaxLoss: MonsterLoss, Later: true);
        }

        // A secondary hero carrying a worthwhile army brings it to the main hero (the meeting opens the exchange).
        if (hero.Id != _main && _main is { } mainId && _state.Heroes.FirstOrDefault(h => h.Id == mainId) is { } main
            && main.Position.Level == hero.Position.Level && (power >= CarryThreshold || power * 4 >= ArmyPower(main.Army)) && !main.Army.IsEmpty)
        {
            yield return new(main.Position, 0.5 * power);
        }

        // Not on an arena map's preparation day: beating the enemy's heroes before the battle leaves nobody to fight it, a loss for both (ADR-160).
        foreach (var enemy in _state.Heroes.Where(_ => !Preparing).OrderBy(h => h.Id.Value))
        {
            if (!_state.AreEnemies(owner, enemy.Owner) || !_state.CanSee(owner, enemy) || !_state.Fog.IsExplored(owner, enemy.Position) || Sanctuary.Protects(_state, enemy.Position))
            {
                continue;
            }

            var seen = Seen(enemy);
            var theirs = ArmyPower(seen);
            if (!hero.Army.IsEmpty && power * 10 >= theirs * 7)
            {
                // One standing in its own town defends it behind the walls (ADR-106).
                var home = _state.ObjectCellAt(enemy.Position) is { Object: Town town, Cell: FootprintCell.Visit } && town.Owner == enemy.Owner ? SiegeRules.SpecOf(town) : null;
                yield return new(enemy.Position, 2000 + (0.8 * theirs), seen, enemy.Attack, enemy.Defense, Siege: home);
            }
        }
    }

    /// <summary>
    /// The cheapest explored tile next to unexplored ground that the hero can walk to, and the route's cost. A flood over open ground (no
    /// objects, heroes or guarded tiles: those are targets with a fight check) finds the tiles really reachable, nearest first, so ground
    /// seen over a wall does not hide the hero's own frontier; the route finder prices the first few.
    /// </summary>
    private (MapPosition Position, int Cost)? Explore(Hero hero, HashSet<MapPosition> failed)
    {
        var map = _state.Map;
        var owner = hero.Owner;
        var power = ArmyPower(hero.Army);
        var seen = new HashSet<MapPosition> { hero.Position };
        var queue = new Queue<MapPosition>();
        queue.Enqueue(hero.Position);
        var frontier = new List<MapPosition>();
        while (queue.Count > 0 && frontier.Count < FrontierChecked)
        {
            var position = queue.Dequeue();
            foreach (var direction in DirectionExtensions.All)
            {
                var next = position.Step(direction);
                if (!map.Contains(next) || !seen.Add(next))
                {
                    continue;
                }

                if (Teleport(next) is { } entrance)
                {
                    // A gate or portal: an unexplored exit is a place to explore (walking in takes the hero there), an explored one goes on.
                    if (_state.TeleportDestination(entrance) is not { } exit || !_state.Fog.IsExplored(owner, exit))
                    {
                        if (!failed.Contains(next) && !_claimed.Contains(next) && Holds(power, Danger(owner, next)))
                        {
                            frontier.Add(next);
                        }
                    }
                    else if (seen.Add(exit))
                    {
                        queue.Enqueue(exit);
                    }

                    continue;
                }

                if (!Open(next))
                {
                    continue;
                }

                queue.Enqueue(next);
                if (!failed.Contains(next) && !_claimed.Contains(next) && Holds(power, Danger(owner, next))
                    && DirectionExtensions.All.Any(d => map.Contains(next.Step(d)) && !_state.Fog.IsExplored(owner, next.Step(d))))
                {
                    frontier.Add(next);
                }
            }
        }

        (MapPosition Position, int Cost)? best = null;
        foreach (var position in frontier)
        {
            if (session.Movement.FindPath(hero.Id, position) is { } path && (best is null || path.TotalCost < best.Value.Cost))
            {
                best = (position, path.TotalCost);
            }
        }

        return best;

        // Exploring never walks into anything: no heroes, objects or guarded ground; a border post it has the key for, an own or allied
        // garrison and a Quest Gate whose quest the hero meets are passes (ADR-070, ADR-071, EVT-6).
        bool Open(MapPosition position) =>
            _state.Fog.IsExplored(owner, position) && MovementRules.CheckTile(_state, position) == MoveResult.Moved
            && BorderRules.Barring(_state, owner, position) is null && _state.HeroAt(position) is null && !_state.IsGuarded(position)
            && _state.ObjectCellAt(position) switch
            {
                null => true,
                { Object: BorderPost, Cell: FootprintCell.Visit } => true,
                { Object: MapGarrison garrison, Cell: FootprintCell.Visit } => garrison.LetsThrough(_state, owner),
                { Object: QuestSite { Kind: MapObjectKind.QuestGate }, Cell: FootprintCell.Visit } => QuestRules.Barring(_state, hero, position) is null,
                _ => false,
            };

        MapObject? Teleport(MapPosition position) =>
            _state.Fog.IsExplored(owner, position) && !_state.IsGuarded(position) && _state.HeroAt(position) is null
            && _state.ObjectCellAt(position) is { Cell: FootprintCell.Visit, Object: { TeleportTarget: not null } gate } ? gate : null;
    }
}
