using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Battles;

/// <summary>An action of the active battle unit.</summary>
public abstract record BattleAction;

public sealed record MoveAction(CellPosition To) : BattleAction;

/// <summary>Melee; <paramref name="From"/> is where to stand (null = the closest placement next to the target).</summary>
public sealed record AttackAction(int Target, CellPosition? From = null) : BattleAction;

public sealed record ShootAction(int Target) : BattleAction;

public sealed record WaitAction : BattleAction;

public sealed record DefendAction : BattleAction;

/// <summary>
/// Battles on the adventure map: starts them from encounters, lets the players (or the AI) command their
/// sides, and applies the result to the map (survivors, experience, defeated heroes and guards).
/// While a battle runs, a <see cref="BattleDecision"/> is pending, so the adventure map is frozen.
/// </summary>
public sealed class BattleService(
    GameState state, IEventPublisher events, ArmyService armies, ProgressionService progression, ArtifactService artifacts, Economy.EconomyService economy)
{
    /// <summary>Experience for defeating (not merely routing) an enemy hero.</summary>
    public const int HeroDefeatExperience = 500;

    /// <summary>Who may command <paramref name="side"/> of the current battle right now (null: the AI does).</summary>
    public PlayerId? Commander(BattleSide side) =>
        state.Battle is { } battle && !battle.IsComputerControlled(side) ? battle.Participant(side).Owner : null;

    internal void BeginHeroBattle(Hero attacker, Hero defender, bool isAmbush)
    {
        if (defender.Army.IsEmpty)
        {
            // Nobody to fight: the defender is overrun without a battlefield.
            Award(attacker, HeroDefeatExperience);
            RemoveHero(defender, BattleEndReason.Defeated);
            return;
        }

        var battle = Create(
            Participant(BattleSide.Attacker, attacker),
            Participant(BattleSide.Defender, defender),
            defender.Position,
            isAmbush);
        Begin(attacker, battle, Stacks(attacker), Stacks(defender));
    }

    internal void BeginGuardBattle(Hero attacker, MapObject guard, string creature, int count)
    {
        var neutral = new BattleParticipant(BattleSide.Defender, null, null, guard.Id, 0, 0, SkillLevel.None, IsComputer: true);
        var battle = Create(Participant(BattleSide.Attacker, attacker), neutral, guard.VisitPosition, isAmbush: false, guard.Definition.BattleTerrain);
        var definition = state.Creatures.Get(creature);
        var stacks = BattleSetup.SplitGuard(count).Select(c => new BattleStackSpec(definition, c, -1)).ToList();
        Begin(attacker, battle, Stacks(attacker), stacks);
    }

    /// <summary>
    /// A hero attacks a creature bank (ADR-068): its guard always fights at the default width (no Tactics choice); each kind of creature is split into
    /// a few stacks so that the guard has at most <see cref="BattleSetup.MaxGuardStacks"/> + 2 of them.
    /// </summary>
    internal void BeginBankBattle(Hero attacker, CreatureBank bank)
    {
        var neutral = new BattleParticipant(BattleSide.Defender, null, null, bank.Id, 0, 0, SkillLevel.None, IsComputer: true);
        var battle = Create(Participant(BattleSide.Attacker, attacker), neutral, bank.VisitPosition, isAmbush: false, bank.Definition.BattleTerrain);
        var perKind = Math.Max(1, (BattleSetup.MaxGuardStacks + 2) / bank.Guard.Count);
        var stacks = bank.Guard
            .SelectMany(s => BattleSetup.SplitGuard(s.Count).Chunk((int)Math.Ceiling(Math.Min(s.Count, BattleSetup.MaxGuardStacks) / (double)perKind))
                .Select(part => new BattleStackSpec(state.Creatures.Get(s.Creature), part.Sum(), -1)))
            .ToList();
        Begin(attacker, battle, Stacks(attacker), stacks, widthChoice: false, deployment: false);
    }

    /// <summary>
    /// A hero attacks a town's garrison: the town's owner commands it (the AI for computer players and neutral towns),
    /// without a hero's stats or skills. Beating it captures the town.
    /// </summary>
    internal void BeginTownBattle(Hero attacker, Town town)
    {
        var owner = town.Owner;
        var garrison = new BattleParticipant(
            BattleSide.Defender, owner, null, town.Id, 0, 0, SkillLevel.None, IsComputer: owner is not { } o || !state.GetPlayer(o).IsHuman,
            ArmyMorale: MoraleAndLuck.MixedFactions(town.Garrison.Stacks.Select(s => state.Creatures.Get(s.Stack.Creature))) + DefenderMorale(town));
        var battle = Create(Participant(BattleSide.Attacker, attacker), garrison, town.VisitPosition, isAmbush: false);
        var stacks = town.Garrison.Stacks.Select(s => new BattleStackSpec(state.Creatures.Get(s.Stack.Creature), s.Stack.Count, s.Slot)).ToList();
        Begin(attacker, battle, Stacks(attacker), stacks);
    }

    /// <summary>
    /// A hero attacks the troops in a garrison (ADR-070): like a town's garrison, its owner commands them (the AI for computer players and neutral
    /// garrisons) without a hero. Beating them flags the garrison for the winner.
    /// </summary>
    internal void BeginGarrisonBattle(Hero attacker, MapGarrison garrison)
    {
        var owner = garrison.Owner;
        var defender = new BattleParticipant(
            BattleSide.Defender, owner, null, garrison.Id, 0, 0, SkillLevel.None, IsComputer: owner is not { } o || !state.GetPlayer(o).IsHuman,
            ArmyMorale: MoraleAndLuck.MixedFactions(garrison.Troops.Stacks.Select(s => state.Creatures.Get(s.Stack.Creature))));
        var battle = Create(Participant(BattleSide.Attacker, attacker), defender, garrison.VisitPosition, isAmbush: false);
        var stacks = garrison.Troops.Stacks.Select(s => new BattleStackSpec(state.Creatures.Get(s.Stack.Creature), s.Stack.Count, s.Slot)).ToList();
        Begin(attacker, battle, Stacks(attacker), stacks);
    }

    internal void ChooseWidth(PlayerId player, int width)
    {
        var battle = Current(BattlePhase.ChoosingWidth);
        if (battle.WidthChooser is not { } chooser || Commander(chooser) != player)
        {
            throw new InvalidOperationException("Only the side with the better Tactics chooses the width.");
        }

        if (!battle.WidthOptions.Contains(width))
        {
            throw new InvalidOperationException($"Width {width} is not one of {string.Join(", ", battle.WidthOptions)}.");
        }

        Deploy(battle, width);
    }

    internal void Act(PlayerId player, BattleAction action)
    {
        var battle = Current(BattlePhase.Fighting);
        var unit = battle.ActiveUnit ?? throw new InvalidOperationException("No unit is active.");
        if (Commander(unit.Side) != player)
        {
            throw new InvalidOperationException($"{unit.Creature.Name} is not under {player}'s command.");
        }

        var refusal = action switch
        {
            MoveAction move => Engine(battle).Move(move.To),
            AttackAction attack => Engine(battle).Attack(attack.Target, attack.From),
            ShootAction shoot => Engine(battle).Shoot(shoot.Target),
            WaitAction => Engine(battle).Wait(),
            DefendAction => Engine(battle).Defend(),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };
        if (refusal is not null)
        {
            throw new InvalidOperationException(refusal);
        }

        Continue(battle);
    }

    /// <summary>
    /// The player's hero uses a warcry (ADR-067) while one of the player's units is up; it does not use up the unit's action.
    /// </summary>
    internal void UseWarcry(PlayerId player, Warcry warcry)
    {
        var battle = Current(BattlePhase.Fighting);
        var unit = battle.ActiveUnit ?? throw new InvalidOperationException("No unit is active.");
        if (Commander(unit.Side) != player)
        {
            throw new InvalidOperationException($"{unit.Creature.Name} is not under {player}'s command.");
        }

        if (battle.Participant(unit.Side).Hero is null)
        {
            throw new InvalidOperationException("Only a hero uses warcries.");
        }

        if (Engine(battle).UseWarcry(unit.Side, warcry) is { } refusal)
        {
            throw new InvalidOperationException(refusal);
        }
    }

    /// <summary>Whether computer players' heroes retreat from lost battles (off in the AI's throwaway estimate fights).</summary>
    internal bool AiMayRetreat { get; set; } = true;

    /// <summary>The player's hero leaves the battle; the army is lost and the hero leaves the map for the tavern (ADR-081).</summary>
    internal void Retreat(PlayerId player)
    {
        var battle = Current(BattlePhase.Fighting);
        var side = SideOf(battle, player) ?? throw new InvalidOperationException($"{player} does not fight in this battle.");
        if (battle.Participant(side).Hero is null)
        {
            throw new InvalidOperationException("Only a hero can retreat.");
        }

        Engine(battle).Retreat(side);
        Continue(battle);
    }

    /// <summary>
    /// The player's hero surrenders to the other player (ADR-070, CMB-1): it pays half the gold cost of its surviving creatures to that player
    /// and leaves the map with its army for the tavern (ADR-081).
    /// </summary>
    internal void Surrender(PlayerId player)
    {
        var battle = Current(BattlePhase.Fighting);
        var side = SideOf(battle, player) ?? throw new InvalidOperationException($"{player} does not fight in this battle.");
        if (SurrenderRules.Check(state, battle, side) is { } refusal)
        {
            throw new InvalidOperationException(refusal);
        }

        var price = SurrenderRules.Price(battle, side);
        var winner = battle.Participant(side.Opponent()).Owner!.Value;
        if (!economy.TrySpend(player, new Economy.ResourceSet(Gold: price)))
        {
            throw new InvalidOperationException($"Surrendering costs {price} gold.");
        }

        economy.Grant(winner, new Economy.ResourceSet(Gold: price));
        events.Publish(new HeroSurrendered(battle.Participant(side).Hero!.Value, player, winner, price));
        Engine(battle).Surrender(side);
        Continue(battle);
    }

    /// <summary>Quick combat: the AI fights both sides to the end (either participant may ask, ADR-038).</summary>
    internal void AutoResolve(PlayerId player)
    {
        var battle = state.Battle ?? throw new InvalidOperationException("No battle is being fought.");
        if (SideOf(battle, player) is null)
        {
            throw new InvalidOperationException($"{player} does not fight in this battle.");
        }

        battle.AutoResolve = true;
        if (battle.Phase == BattlePhase.ChoosingWidth)
        {
            Deploy(battle, BattleState.DefaultWidth);
            return;
        }

        if (battle.Phase == BattlePhase.Deploying)
        {
            StartFighting(battle);
            return;
        }

        Continue(battle);
    }

    /// <summary>
    /// Auto combat (ADR-090, CMB-10): the human player whose auto combat setting applies to the current battle, or null. That is a battle between
    /// one human player and a computer player or neutrals, not yet handed to quick combat; two humans fight by hand.
    /// </summary>
    public PlayerId? AutoCombatPlayer()
    {
        if (state.Battle is not { AutoResolve: false } battle || battle.Phase == BattlePhase.Finished || battle.Attacker.IsComputer == battle.Defender.IsComputer)
        {
            return null;
        }

        return battle.Participant(battle.Attacker.IsComputer ? BattleSide.Defender : BattleSide.Attacker).Owner;
    }

    /// <summary>
    /// Auto combat's preview (ADR-090, CMB-10): plays a copy of the current battle to its end as quick combat would, with the same random numbers,
    /// and tells how it ends. The game is not touched, so the player may still fight the battle by hand; quick combat
    /// (<see cref="Commands.AutoResolveBattleCommand"/>) right after this ends the battle exactly so.
    /// </summary>
    public AutoCombatPreview Preview()
    {
        var battle = (state.Battle ?? throw new InvalidOperationException("No battle is being fought.")).Copy();
        var unheard = new EventBus();
        battle.AutoResolve = true;
        switch (battle.Phase)
        {
            case BattlePhase.ChoosingWidth:
                Deploy(battle, BattleState.DefaultWidth, unheard);
                break;
            case BattlePhase.Deploying:
                StartFighting(battle, unheard);
                break;
            default:
                Continue(battle, unheard);
                break;
        }

        var winner = battle.Winner!.Value;
        var reason = battle.EndReason!.Value;
        var winning = battle.Participant(winner);
        var losing = battle.Participant(winner.Opponent());
        var raised = new List<RaisedUndead>();
        var artifacts = new List<string>();
        if (winning.Hero is { } heroId && reason == BattleEndReason.Defeated)
        {
            var hero = state.GetHero(heroId);
            if (hero.SkillLevelOf(NecromancyRules.Skill) is var level and not SkillLevel.None)
            {
                // The survivors' army and a copy of the game's random generator: the end of the real battle draws the same numbers.
                var army = new Army(hero.Army.Slots);
                foreach (var (slot, stack) in hero.Army.Stacks.ToList())
                {
                    var survivors = battle.Units.Where(u => u.Side == winner && u.SourceSlot == slot).Sum(u => u.Count);
                    army.Set(slot, survivors > 0 ? new CreatureStack(stack.Creature, survivors) : null);
                }

                raised = JoinUndead(battle, winner.Opponent(), hero, level, army, new GameRandom(state.Random.State));
            }

            if (losing.Hero is { } loserId)
            {
                var loser = state.GetHero(loserId);
                artifacts = ArtifactValue.BestFirst(state.Artifacts, loser.Artifacts.AllWorn.Select(w => w.Id).Concat(loser.Artifacts.Backpack)).ToList();
            }
        }

        return new AutoCombatPreview(winner, reason, Casualties(battle), winning.Hero is null ? 0 : Experience(battle), raised, artifacts);
    }

    /// <summary>A human player's auto combat setting (ADR-090, CMB-10): a player setting that changes no rule.</summary>
    internal void SetAutoCombat(PlayerId player, AutoCombatMode mode)
    {
        var who = state.GetPlayer(player);
        if (!who.IsHuman)
        {
            throw new InvalidOperationException("Only a human player sets auto combat.");
        }

        if (who.AutoCombat == mode)
        {
            return;
        }

        who.AutoCombat = mode;
        events.Publish(new AutoCombatChanged(player, mode));
    }

    private static BattleSide? SideOf(BattleState battle, PlayerId player) =>
        battle.Attacker.Owner == player ? BattleSide.Attacker
        : battle.Defender.Owner == player ? BattleSide.Defender
        : null;

    private BattleParticipant Participant(BattleSide side, Hero hero) =>
        new(side, hero.Owner, hero.Id, null, hero.TotalAttack, hero.TotalDefense, hero.SkillLevelOf(BuiltInSkills.Tactics), !state.GetPlayer(hero.Owner).IsHuman,
            HeroBattleBonuses.For(hero, state.Creatures),
            MoraleAndLuck.MixedFactions(hero.Army.Stacks.Select(s => state.Creatures.Get(s.Stack.Creature)))
            + (side == BattleSide.Defender && state.ObjectsOf<Town>().FirstOrDefault(t => t.Owner == hero.Owner && t.VisitPosition == hero.Position) is { } home
                ? DefenderMorale(home)
                : 0));

    /// <summary>A town's defenders get the Brotherhood's morale (ADR-075, CMB-2): its garrison, or an own hero standing at its entrance.</summary>
    private static int DefenderMorale(Town town) =>
        town.Level(BuiltInBuildings.Tavern) >= MoraleAndLuck.BrotherhoodLevel ? MoraleAndLuck.BrotherhoodDefenderMorale : 0;

    /// <summary>
    /// The hero's army and war machines (ADR-056): tent and cart when owned, the offensive machine only from Advanced
    /// Ballistics (outside sieges, which do not exist yet).
    /// </summary>
    private List<BattleStackSpec> Stacks(Hero hero)
    {
        var stacks = hero.Army.Stacks.Select(s => new BattleStackSpec(state.Creatures.Get(s.Stack.Creature), s.Stack.Count, s.Slot)).ToList();
        var town = ProgressionService.TownOf(state, hero);
        foreach (var slot in hero.WarMachines.Order())
        {
            if (slot != WarMachineSlot.Offensive || WarMachines.FieldsOffensive(hero.SkillLevelOf(BuiltInSkills.Ballistics)))
            {
                stacks.Add(BattleStackSpec.Of(WarMachines.KindOf(slot, town)));
            }
        }

        return stacks;
    }

    /// <summary>The battlefield at <paramref name="location"/>; an object may fix the terrain (abandoned mines underground, ADR-068).</summary>
    private BattleState Create(BattleParticipant attacker, BattleParticipant defender, MapPosition location, bool isAmbush, Map.TerrainType? terrain = null)
    {
        var ground = terrain ?? state.Map.GetTerrain(location);
        return new(attacker, defender, ground, location, isAmbush, state.Random.Fork(), state.Terrains.Get(ground).NativeTowns);
    }

    private void Begin(
        Hero attacker, BattleState battle, List<BattleStackSpec> attackers, List<BattleStackSpec> defenders, bool widthChoice = true, bool deployment = true)
    {
        if (state.Battle is not null)
        {
            throw new InvalidOperationException("A battle is already being fought.");
        }

        state.Battle = battle;
        battle.PendingStacks = (attackers, defenders);
        battle.DeploymentAllowed = deployment;
        var decision = new BattleDecision(attacker.Id);
        state.PendingDecision = decision;

        // The phase is settled before anyone hears of the battle, so views see the width choice (publish after mutating).
        var (chooser, level) = BattleSetup.EffectiveTactics(battle.Attacker.Tactics, battle.Defender.Tactics);
        var choosing = widthChoice && chooser is { } side && !battle.IsComputerControlled(side) && BattleSetup.WidthOptions(level).Count > 1;
        if (choosing)
        {
            battle.Phase = BattlePhase.ChoosingWidth;
            battle.WidthChooser = chooser;
            battle.WidthOptions = BattleSetup.WidthOptions(level);
        }

        events.Publish(new DecisionRequested(decision));
        if (!choosing)
        {
            // The AI keeps the default width.
            Deploy(battle, BattleState.DefaultWidth);
        }
    }

    /// <summary>
    /// Deployment step (ADR-072): the deployer moves one of its stacks within its zone. Machines keep their column; another
    /// stack's cells, obstacles and cells outside the zone are refused.
    /// </summary>
    internal void Redeploy(PlayerId player, int unitId, CellPosition to)
    {
        var battle = Deploying(player);
        var unit = battle.FindUnit(unitId) ?? throw new InvalidOperationException($"No unit {unitId}.");
        if (BattleSetup.DeploymentRefusal(battle, unit, to) is { } refusal)
        {
            throw new InvalidOperationException(refusal);
        }

        var from = unit.Position;
        unit.Position = to;
        events.Publish(new BattleUnitDeployed(unit.Id, from, to));
    }

    /// <summary>"Start battle": the deployment step ends and the first round begins.</summary>
    internal void EndDeployment(PlayerId player) => StartFighting(Deploying(player));

    private BattleState Deploying(PlayerId player)
    {
        var battle = Current(BattlePhase.Deploying);
        if (Commander(battle.Deployer!.Value) != player)
        {
            throw new InvalidOperationException("Only the side with the higher Tactics deploys.");
        }

        return battle;
    }

    // The methods below take the events' bus so that the auto combat preview plays a copy unheard (null: the game's bus).
    private void StartFighting(BattleState battle, IEventPublisher? bus = null)
    {
        if (battle.Phase == BattlePhase.Deploying)
        {
            (bus ?? events).Publish(new BattleDeploymentEnded(battle.Deployer!.Value));
        }

        Engine(battle, bus).StartFighting();
        Continue(battle, bus);
    }

    private void Deploy(BattleState battle, int width, IEventPublisher? bus = null)
    {
        battle.Width = width;

        // Only the side with the higher Tactics deploys, and only when a person commands it (ADR-072); a tie lets nobody.
        var (chooser, _) = BattleSetup.EffectiveTactics(battle.Attacker.Tactics, battle.Defender.Tactics);
        if (battle.DeploymentAllowed && chooser is { } side && !battle.IsComputerControlled(side))
        {
            battle.Deployer = side;
            battle.DeploymentColumns = BattleSetup.DeploymentColumns(battle.Participant(side).Tactics);
        }

        BattleSetup.Deploy(battle, battle.PendingStacks.Attackers, battle.PendingStacks.Defenders);
        (bus ?? events).Publish(new BattleStarted(
            battle.Attacker.Hero,
            battle.Defender.Hero,
            battle.Defender.Guard,
            battle.Width,
            BattleState.Height,
            battle.IsAmbush,
            battle.Units.Select(u => new BattleUnitInfo(u.Id, u.Side, u.Creature.Id, u.Count, u.Position, u.Width, u.Height, u.Machine)).ToList(),
            battle.Obstacles.ToList()));
        if (battle.Deployer is { } deployer)
        {
            battle.Phase = BattlePhase.Deploying;
            (bus ?? events).Publish(new BattleDeploymentStarted(deployer, battle.DeploymentColumns));
            return;
        }

        StartFighting(battle, bus);
    }

    /// <summary>Lets the AI play its units until a human has to act or the battle is over; only the game's own battle is then finished.</summary>
    private void Continue(BattleState battle, IEventPublisher? bus = null)
    {
        while (battle.Phase == BattlePhase.Fighting && battle.ActiveUnit is { } unit && battle.IsComputerControlled(unit.Side))
        {
            // A computer player's hero gives up a lost battle (ADR-070); quick combat never retreats for a person.
            if (AiMayRetreat && battle.Participant(unit.Side).IsComputer && SurrenderRules.ShouldRetreat(battle, unit.Side))
            {
                Engine(battle, bus).Retreat(unit.Side);
                break;
            }

            BattleAi.Act(battle, Engine(battle, bus));
        }

        if (battle.Phase == BattlePhase.Finished && ReferenceEquals(battle, state.Battle))
        {
            Finish(battle);
        }
    }

    private BattleEngine Engine(BattleState battle, IEventPublisher? bus = null) => new(battle, bus ?? events);

    private BattleState Current(BattlePhase phase) =>
        state.Battle is { } battle && battle.Phase == phase
            ? battle
            : throw new InvalidOperationException(phase switch
            {
                BattlePhase.Fighting => "No battle is being fought.",
                BattlePhase.Deploying => "No stacks to deploy.",
                _ => "No battlefield width to choose.",
            });

    private void Finish(BattleState battle)
    {
        var winner = battle.Winner!.Value;
        var loser = winner.Opponent();
        var reason = battle.EndReason!.Value;
        var winning = battle.Participant(winner);
        var losing = battle.Participant(loser);

        var experience = Experience(battle);
        var casualties = Casualties(battle);

        state.Battle = null;
        events.Publish(new BattleEnded(winner, reason, casualties, winning.Hero is null ? 0 : experience));

        // "For the next battle" statuses are used up by this one, whatever its outcome (ADR-068).
        progression.ExpireStatuses(new[] { winning.Hero, losing.Hero }.OfType<HeroId>().Select(state.GetHero), StatusExpiry.NextBattle);

        // Winner: survivors back into their slots, then Necromancy, then the experience (its level-ups come after the raised undead).
        if (winning.Hero is { } winnerId)
        {
            var hero = state.GetHero(winnerId);
            ApplySurvivors(battle, winner, hero);
            if (reason == BattleEndReason.Defeated)
            {
                RaiseUndead(battle, loser, hero);
            }

            Award(hero, experience);
        }
        else if (winning.Guard is { } guardId && state.GetObject(guardId) is Town town)
        {
            ApplySurvivors(battle, winner, town);
        }
        else if (winning.Guard is { } held && state.GetObject(held) is MapGarrison kept)
        {
            ApplySurvivors(battle, winner, kept.Id, kept.Troops);
        }
        else if (winning.Guard is { } guard)
        {
            SetGuardCount(state.GetObject(guard), battle.LivingUnitsOf(winner).Sum(u => u.Count));
        }

        // Loser: a hero leaves the map, a guard is gone.
        if (losing.Hero is { } loserId)
        {
            // A defeated hero's artifacts go to the winning hero (ADR-061); a hero that flees or surrenders takes them away.
            if (reason == BattleEndReason.Defeated && winning.Hero is { } looterId)
            {
                artifacts.Loot(state.GetHero(loserId), state.GetHero(looterId));
            }

            var loserHero = state.GetHero(loserId);
            if (reason != BattleEndReason.Defeated)
            {
                // It keeps what lived (a surrender) and loses the machines destroyed in the battle.
                ApplySurvivors(battle, loser, loserHero);
            }

            RemoveHero(loserHero, reason);
        }
        else if (losing.Guard is { } guardId)
        {
            DefeatGuard(state.GetObject(guardId), winning.Hero is { } heroId ? state.GetHero(heroId) : null);
        }

        var decision = state.PendingDecision;
        state.PendingDecision = null;
        if (decision is not null)
        {
            events.Publish(new DecisionResolved(decision));
        }

        progression.OfferNext();
    }

    /// <summary>What a finished battle is worth to the winner: the health of the creatures it killed, and a defeated hero.</summary>
    private static int Experience(BattleState battle)
    {
        var loser = battle.Winner!.Value.Opponent();
        var killedHealth = battle.Units.Where(u => u.Side == loser).Sum(u => (long)(u.InitialCount - u.Count) * u.Creature.Health);
        var heroDefeated = battle.Participant(loser).Hero is not null && battle.EndReason == BattleEndReason.Defeated;
        return (int)Math.Min(int.MaxValue, killedHealth + (heroDefeated ? HeroDefeatExperience : 0));
    }

    private static List<BattleCasualty> Casualties(BattleState battle) => battle.Units
        .GroupBy(u => (u.Side, u.Creature.Id))
        .Select(g => new BattleCasualty(g.Key.Side, g.Key.Id, g.Sum(u => u.InitialCount - u.Count)))
        .Where(c => c.Lost > 0)
        .ToList();

    private void ApplySurvivors(BattleState battle, BattleSide side, Hero hero)
    {
        // Destroyed war machines are lost; the blacksmith sells new ones (ADR-056).
        foreach (var machine in battle.Units.Where(u => u.Side == side && u.Machine is not null && !u.IsAlive))
        {
            var slot = WarMachines.SlotOf(machine.Machine!.Value);
            if (hero.RemoveWarMachine(slot))
            {
                events.Publish(new WarMachineLost(hero.Id, slot));
            }
        }

        foreach (var (slot, stack) in hero.Army.Stacks.ToList())
        {
            var survivors = battle.Units.Where(u => u.Side == side && u.SourceSlot == slot).Sum(u => u.Count);
            hero.Army.Set(slot, survivors > 0 ? new CreatureStack(stack.Creature, survivors) : null);
        }

        armies.Publish(hero);
    }

    /// <summary>
    /// Necromancy (ADR-072, MAG-7): after a victory over a defeated enemy (not one that retreated), the base undead raised from the enemy's
    /// slain creatures join a stack of their own or a free slot; with neither, an army stack of the upgrade takes 3/4 of them; otherwise they
    /// are lost.
    /// </summary>
    private void RaiseUndead(BattleState battle, BattleSide enemy, Hero hero)
    {
        var level = hero.SkillLevelOf(NecromancyRules.Skill);
        if (level == SkillLevel.None)
        {
            return;
        }

        var joined = JoinUndead(battle, enemy, hero, level, hero.Army, state.Random);
        if (joined.Count == 0)
        {
            return;
        }

        armies.Publish(hero);
        events.Publish(new UndeadRaised(hero.Id, joined));
    }

    /// <summary>Raises the undead from the enemy's slain creatures into <paramref name="army"/> and returns what joined it.</summary>
    private List<RaisedUndead> JoinUndead(BattleState battle, BattleSide enemy, Hero hero, SkillLevel level, Army army, GameRandom random)
    {
        var slain = battle.Units.Where(u => u.Side == enemy && !u.IsMachine).Select(u => (u.Creature, u.InitialCount - u.Count));
        var joined = new List<RaisedUndead>();
        foreach (var raised in NecromancyRules.Raise(slain, level, NecromancyRules.IsAmplified(state, hero.Owner), state.Creatures, random))
        {
            if (army.TryAdd(new CreatureStack(raised.Creature, raised.Count)))
            {
                joined.Add(raised);
            }
            else if (state.Creatures.Get(raised.Creature).UpgradesTo is { } upgrade && army.Stacks.Any(s => s.Stack.Creature == upgrade)
                     && raised.Count * 3 / 4 is > 0 and var count)
            {
                army.TryAdd(new CreatureStack(upgrade, count));
                joined.Add(new RaisedUndead(upgrade, count));
            }
        }

        return joined;
    }

    private void ApplySurvivors(BattleState battle, BattleSide side, Town town) => ApplySurvivors(battle, side, town.Id, town.Garrison);

    /// <summary>The troops of a town or a map garrison keep what lived.</summary>
    private void ApplySurvivors(BattleState battle, BattleSide side, MapObjectId holder, Army troops)
    {
        foreach (var (slot, stack) in troops.Stacks.ToList())
        {
            var survivors = battle.Units.Where(u => u.Side == side && u.SourceSlot == slot).Sum(u => u.Count);
            troops.Set(slot, survivors > 0 ? new CreatureStack(stack.Creature, survivors) : null);
        }

        events.Publish(new GarrisonChanged(holder, troops.Slots.ToList()));
    }

    private void Award(Hero hero, int experience)
    {
        if (experience <= 0)
        {
            return;
        }

        progression.GrantExperience(hero, experience);
    }

    /// <summary>
    /// The losing hero leaves the map. One that retreated or surrendered and comes from the roster waits in its player's taverns as it is
    /// (ADR-081): a retreat costs it its army, a surrender does not. It stops governing.
    /// </summary>
    private void RemoveHero(Hero hero, BattleEndReason reason)
    {
        var governed = StewardshipRules.TownGovernedBy(state, hero);
        state.RemoveHero(hero);
        var left = reason != BattleEndReason.Defeated;
        if (left && hero.Definition is not null)
        {
            if (reason == BattleEndReason.Retreated)
            {
                for (var slot = 0; slot < Army.SlotCount; slot++)
                {
                    hero.Army.Set(slot, null);
                }
            }

            state.Retire(hero);
        }

        if (governed is not null)
        {
            governed.Governor = null;
            events.Publish(new GovernorChanged(governed.Id, hero.Id, null));
        }

        events.Publish(new HeroDefeated(hero.Id, hero.Owner, left));
    }

    private void SetGuardCount(MapObject guard, int count)
    {
        switch (guard)
        {
            case MonsterGuard monster:
                monster.Count = count;
                break;
            case Mine { Guard: { } stack } mine:
                mine.Guard = stack with { Count = count };
                break;
            case CreatureDwelling { Guard: { } stack } dwelling:
                dwelling.Guard = stack with { Count = count };
                break;
        }

        events.Publish(new MapObjectChanged(guard.Id));
    }

    /// <summary>A beaten monster leaves the map; a beaten mine guard leaves the mine, a beaten garrison the town, to the winner.</summary>
    private void DefeatGuard(MapObject guard, Hero? winner)
    {
        switch (guard)
        {
            case MonsterGuard:
                state.RemoveObject(guard);
                events.Publish(new MapObjectRemoved(guard.Id));
                break;
            case Mine mine:
                mine.Guard = null;
                mine = ClearAbandoned(mine);
                events.Publish(new MineGuardDefeated(mine.Id));
                if (winner is not null && mine.Owner != winner.Owner)
                {
                    var previous = mine.Owner;
                    mine.Owner = winner.Owner;
                    events.Publish(new MineOwnerChanged(mine.Id, previous, winner.Owner));
                }

                break;
            case CreatureBank bank when winner is not null:
                Plunder(bank, winner);
                break;
            case CreatureDwelling dwelling:
                dwelling.Guard = null;
                events.Publish(new MapObjectChanged(dwelling.Id));
                if (winner is not null && dwelling.Owner != winner.Owner)
                {
                    var before = dwelling.Owner;
                    dwelling.Owner = winner.Owner;
                    events.Publish(new DwellingOwnerChanged(dwelling.Id, before, winner.Owner));
                }

                break;
            case Town town when winner is not null:
                TownService.Capture(state, events, town, winner.Owner);
                break;
            case MapGarrison garrison:
                for (var slot = 0; slot < Army.SlotCount; slot++)
                {
                    garrison.Troops.Set(slot, null);
                }

                events.Publish(new GarrisonChanged(garrison.Id, garrison.Troops.Slots.ToList()));
                if (winner is not null)
                {
                    var previous = garrison.Owner;
                    garrison.Owner = winner.Owner;
                    events.Publish(new GarrisonOwnerChanged(garrison.Id, previous, winner.Owner));
                }

                break;
        }
    }

    /// <summary>The bank's reward goes to the winner and the bank is empty until it refills (ADR-068).</summary>
    private void Plunder(CreatureBank bank, Hero winner)
    {
        var (reward, tier, more) = (bank.Reward, bank.RewardArtifact, bank.MoreArtifacts);
        bank.PlunderedOn = state.Date;
        bank.Guard = [];
        bank.Reward = Economy.ResourceSet.Empty;
        bank.RewardArtifact = null;
        bank.MoreArtifacts = [];
        economy.Grant(winner.Owner, reward);
        var found = tier is { } t && artifacts.Draw(t) is { } artifact && artifacts.Grant(winner, artifact, ArtifactSource.Found) ? artifact : null;
        foreach (var extra in more)
        {
            // Further artifacts (Dragon City) go the same way; a full backpack loses them.
            if (artifacts.Draw(extra) is { } drawn)
            {
                artifacts.Grant(winner, drawn, ArtifactSource.Found);
            }
        }

        events.Publish(new CreatureBankPlundered(bank.Id, winner.Id, reward, found));
        events.Publish(new MapObjectChanged(bank.Id));
    }

    /// <summary>
    /// A beaten abandoned mine becomes the normal mine of its resource (ADR-068): same id, place and owner, the definition it
    /// <see cref="MapObjectDefinition.Becomes"/>. Views redraw it on <see cref="MapObjectChanged"/>.
    /// </summary>
    private Mine ClearAbandoned(Mine mine)
    {
        if (mine.Definition.Becomes is not { } becomes || !state.ObjectCatalog.TryGet(becomes, out var normal))
        {
            return mine;
        }

        var cleared = new Mine(mine.Id, normal, mine.Position, mine.Owner);
        state.RemoveObject(mine);
        state.AddObject(cleared);
        events.Publish(new MapObjectChanged(cleared.Id));
        return cleared;
    }
}
