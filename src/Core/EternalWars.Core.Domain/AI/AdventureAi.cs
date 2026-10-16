using EternalWars.Core.Domain.Armies;
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
        foreach (var hero in _state.HeroesOf(player).OrderBy(h => h.Id.Value).Select(h => h.Id).ToList())
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
                case TreasureChestDecision chest:
                    var hero = _state.GetHero(chest.Hero);
                    var levels = ExperienceTable.LevelFor(hero.Experience + chest.Experience) > hero.Level;
                    command = new ResolveTreasureChestCommand(player, levels ? TreasureChestReward.Experience : TreasureChestReward.Gold);
                    break;
                case HeroExchangeDecision:
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
        foreach (var town in _state.ObjectsOf<Town>().Where(t => t.Owner == player).OrderBy(t => t.Id.Value).ToList())
        {
            if (!Active(player) || _state.PendingDecision is not null)
            {
                return;
            }

            var reserve = Build(player, town);
            UpgradeStacks(player, town, reserve);
            Recruit(player, town, reserve);
            Consolidate(player, town);
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
        if (!builtToday && resources.CanAfford(wanted.Next.Cost))
        {
            var command = wanted.Dwelling
                ? (GameCommand)new BuildDwellingCommand(player, town.Id, wanted.Id)
                : new BuildCommand(player, town.Id, wanted.Id);
            Execute(command);
            return 0;
        }

        // Save for it rather than spending everything on creatures, as long as it is within reach.
        return wanted.Next.Cost.Gold;
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
                || !Reachable(next.Cost, have, income))
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

    /// <summary>What a town wants built, best first (the order is the strategy): units first, then income, then the rest.</summary>
    private IEnumerable<(bool Dwelling, string Id)> BuildOrder(Town town)
    {
        var lines = TownRules.Lines(_state, town);
        IEnumerable<CreatureDefinition> Best(int tier) =>
            lines.Where(l => l.Tier == tier).OrderByDescending(l => (long)l.Value * l.WeeklyGrowth).ThenBy(l => l.Id, StringComparer.Ordinal);
        bool TierBuilt(int tier) => lines.Any(l => l.Tier == tier && town.DwellingLevel(l.Id) > 0);

        foreach (var tier in new[] { 1, 2, 3, 4, 5 }.Where(t => !TierBuilt(t)))
        {
            if (Best(tier).FirstOrDefault() is { } line)
            {
                yield return (true, line.Id);
            }
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

                var cost = TownRules.UpgradeCost(_state, line, stack.Count);
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
        var count = TownRules.MaxAffordable(spare, _state.Creatures.Get(creature).Cost, Math.Min(limit, town.AvailableOf(line.Id)));
        if (count > 0)
        {
            Execute(new RecruitCommand(player, town.Id, creature, count, hero));
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

    private bool PlayHero(PlayerId player, HeroId id)
    {
        var failed = new HashSet<MapPosition>();
        for (var move = 0; move < MaxMovesPerHero; move++)
        {
            if (!ResolvePending(player))
            {
                return false;
            }

            if (!Active(player) || _state.Heroes.FirstOrDefault(h => h.Id == id) is not { } hero || hero.MovementPoints <= 0)
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

    private long Power(CreatureStack stack) => (long)stack.Count * _state.Creatures.Get(stack.Creature).Value;

    private long ArmyPower(Army army) => army.Stacks.Sum(s => Power(s.Stack));

    private long GuardPower(MapPosition position) =>
        _state.GuardsOf(position).Sum(m => (long)(m.Count ?? 1) * _state.Creatures.Get(m.Creature).Value);

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
        var candidates = Targets(hero)
            .Where(t => !failed.Contains(t.Position) && !_claimed.Contains(t.Position) && t.Position != hero.Position)
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
    private Army? GuardArmy(MapPosition position)
    {
        var stacks = _state.GuardsOf(position).Take(Army.SlotCount).Select(m => new CreatureStack(m.Creature, Math.Max(1, m.Count ?? 1))).ToArray();
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
                case ResourcePile pile when Plausible(GuardPower(visit)):
                    yield return new(visit, GoldValue(pile.Resource, pile.Amount), GuardArmy(visit), MaxLoss: MonsterLoss);
                    break;
                case TreasureChest when Plausible(GuardPower(visit)):
                    yield return new(visit, 1500, GuardArmy(visit), MaxLoss: MonsterLoss);
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
                case Town town when !OwnTeam(owner, town.Owner):
                    var garrison = ArmyPower(town.Garrison);
                    if (Plausible(garrison))
                    {
                        yield return new(visit, 4000 + (0.3 * garrison) + (town.Owner is null ? 0 : 2000) + (town.Dwellings.Count * 500), town.Garrison.IsEmpty ? null : town.Garrison);
                    }

                    break;
                case Town own when own.Owner == owner && own.VisitPosition != hero.Position:
                    var waiting = ArmyPower(own.Garrison) + own.Available.Sum(a => (long)a.Value * _state.Creatures.Get(a.Key).Value);
                    if (waiting * 5 > power * 3)
                    {
                        yield return new(visit, 0.3 * Math.Min(waiting, Math.Max(power, 1000)));
                    }

                    break;
            }
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
                || _state.HeroAt(position) is not null || _state.ObjectCellAt(position) is not null || _state.IsGuarded(position))
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
