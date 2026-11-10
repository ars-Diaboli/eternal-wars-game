using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// Begins battles from encounters on the adventure map: the sides (a hero with its stats, skills, spells and war machines, a town's or a map
/// garrison's troops, a neutral guard split into stacks), the battlefield and a siege's fortifications, then the width choice or the deployment.
/// A battle with nobody to fight is settled at once (<see cref="BattleAftermath"/>).
/// </summary>
internal sealed class BattleStarter(GameState state, IEventPublisher events, ArtifactService artifacts, BattleFlow flow, BattleAftermath aftermath)
{
    /// <summary>The AI's throwaway estimate fights play a hero battle as a siege of these fortifications (<see cref="AI.BattleEstimator"/>).</summary>
    public SiegeSpec? EstimateSiege { get; set; }

    public void BeginHeroBattle(Hero attacker, Hero defender, bool isAmbush)
    {
        if (defender.Army.IsEmpty)
        {
            // Nobody to fight: the defender is overrun without a battlefield.
            aftermath.Award(attacker, BattleAftermath.HeroDefeatExperience);
            aftermath.RemoveHero(defender, BattleEndReason.Defeated);
            return;
        }

        var battle = Create(
            Participant(BattleSide.Attacker, attacker),
            Participant(BattleSide.Defender, defender),
            defender.Position,
            isAmbush);

        // A hero standing in its own town with a Fort defends it behind the walls (ADR-106).
        var siege = EstimateSiege ?? (state.ObjectsOf<Town>().FirstOrDefault(t => t.Owner == defender.Owner && t.VisitPosition == defender.Position) is { } home
            ? SiegeRules.SpecOf(home)
            : null);
        Begin(attacker, battle, Stacks(attacker, siege is not null), Stacks(defender), siege: siege);
    }

    /// <summary>
    /// The arena map's battle (ADR-097, ADR-100): the two champions, already on the combat place, fight on its terrain; never a siege or an ambush.
    /// Both sides have an army (<see cref="ArenaService"/> settles the others). It gives no experience and the AI never leaves it; its result is
    /// recorded in <see cref="GameState.Arena"/> (<see cref="BattleAftermath.Finish"/>).
    /// </summary>
    public void BeginArenaBattle(Hero attacker, Hero defender, MapPosition location)
    {
        var battle = Create(Participant(BattleSide.Attacker, attacker), Participant(BattleSide.Defender, defender), location, isAmbush: false);
        Begin(attacker, battle, Stacks(attacker), Stacks(defender));
    }

    public void BeginGuardBattle(Hero attacker, MapObject guard, string creature, int count)
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
    public void BeginBankBattle(Hero attacker, CreatureBank bank)
    {
        var neutral = new BattleParticipant(BattleSide.Defender, null, null, bank.Id, 0, 0, SkillLevel.None, IsComputer: true);
        var battle = Create(Participant(BattleSide.Attacker, attacker), neutral, bank.VisitPosition, isAmbush: false, bank.Definition.BattleTerrain);
        battle.CliffsAllowed = false;
        Begin(attacker, battle, Stacks(attacker), GuardStacks(bank.Guard), widthChoice: false, deployment: false);
    }

    /// <summary>
    /// A hero opens a guarded Pandora's Box (ADR-100): the guard fights like a creature bank's (split the same way, no Tactics choice); winning opens
    /// the box, a lost or fled battle leaves the guard whole.
    /// </summary>
    public void BeginBoxBattle(Hero attacker, PandorasBox box)
    {
        var neutral = new BattleParticipant(BattleSide.Defender, null, null, box.Id, 0, 0, SkillLevel.None, IsComputer: true);
        var battle = Create(Participant(BattleSide.Attacker, attacker), neutral, box.VisitPosition, isAmbush: false, box.Definition.BattleTerrain);
        Begin(attacker, battle, Stacks(attacker), GuardStacks(box.Guard), widthChoice: false, deployment: false);
    }

    /// <summary>
    /// A map event's battle (ADR-115, EVT-4 d): the hero fights the author's army where it stands, split like a bank's guard, with no Tactics choice.
    /// There is no map object behind the army: the winner keeps its survivors and its experience, a beaten hero leaves the map.
    /// </summary>
    public void BeginEventBattle(Hero attacker, IReadOnlyList<CreatureStack> army)
    {
        var neutral = new BattleParticipant(BattleSide.Defender, null, null, null, 0, 0, SkillLevel.None, IsComputer: true);
        var battle = Create(Participant(BattleSide.Attacker, attacker), neutral, attacker.Position, isAmbush: false);
        Begin(attacker, battle, Stacks(attacker), GuardStacks(army), widthChoice: false, deployment: false);
    }

    /// <summary>Each kind of creature split into a few stacks, so that the guard has at most <see cref="BattleSetup.MaxGuardStacks"/> + 2 of them.</summary>
    private List<BattleStackSpec> GuardStacks(IReadOnlyList<CreatureStack> guard)
    {
        var perKind = Math.Max(1, (BattleSetup.MaxGuardStacks + 2) / guard.Count);
        return guard
            .SelectMany(s => BattleSetup.SplitGuard(s.Count).Chunk((int)Math.Ceiling(Math.Min(s.Count, BattleSetup.MaxGuardStacks) / (double)perKind))
                .Select(part => new BattleStackSpec(state.Creatures.Get(s.Creature), part.Sum(), -1)))
            .ToList();
    }

    /// <summary>
    /// A hero attacks a town's garrison: the town's owner commands it (the AI for computer players and neutral towns),
    /// without a hero's stats or skills. Beating it captures the town. A town with a Fort is besieged (ADR-106).
    /// </summary>
    public void BeginTownBattle(Hero attacker, Town town)
    {
        if (town.GarrisonHero is { } inside)
        {
            BeginGarrisonHeroBattle(attacker, town, inside);
            return;
        }

        var owner = town.Owner;
        var siege = SiegeRules.SpecOf(town);
        // The Grail building's Initiative goes to the garrison's stacks also without a hero (ADR-130).
        var initiative = siege is not null ? UniqueBuildings.SiegeOf(town).Initiative : 0;
        var garrison = new BattleParticipant(
            BattleSide.Defender, owner, null, town.Id, 0, 0, SkillLevel.None, IsComputer: owner is not { } o || !state.GetPlayer(o).IsHuman,
            initiative == 0 ? null : HeroBattleBonuses.None with { Initiative = initiative },
            ArmyMorale: MoraleAndLuck.MixedFactions(town.Garrison.Stacks.Select(s => state.Creatures.Get(s.Stack.Creature))) + DefenderMorale(town));
        var battle = Create(Participant(BattleSide.Attacker, attacker), garrison, town.VisitPosition, isAmbush: false);
        var stacks = town.Garrison.Stacks.Select(s => new BattleStackSpec(state.Creatures.Get(s.Stack.Creature), s.Stack.Count, s.Slot)).ToList();
        Begin(attacker, battle, Stacks(attacker, siege is not null), stacks, siege: siege);
    }

    /// <summary>
    /// A hero attacks a town with a garrison hero (ADR-112): the garrison hero commands the garrison with its stats, skills, spells and war machines,
    /// behind the walls when the town has a Fort. A garrison hero without troops is overrun, and the town falls.
    /// </summary>
    private void BeginGarrisonHeroBattle(Hero attacker, Town town, Hero inside)
    {
        if (town.Garrison.IsEmpty)
        {
            aftermath.Award(attacker, BattleAftermath.HeroDefeatExperience);
            artifacts.Loot(inside, attacker);
            aftermath.DefeatGarrisonHero(town, inside, attacker);
            return;
        }

        var battle = Create(Participant(BattleSide.Attacker, attacker), Participant(BattleSide.Defender, inside, town), town.VisitPosition, isAmbush: false);
        var siege = SiegeRules.SpecOf(town);
        Begin(attacker, battle, Stacks(attacker, siege is not null), Stacks(inside, army: town.Garrison), siege: siege);
    }

    /// <summary>
    /// A hero attacks the troops in a garrison (ADR-070): like a town's garrison, its owner commands them (the AI for computer players and neutral
    /// garrisons) without a hero. Beating them flags the garrison for the winner.
    /// </summary>
    public void BeginGarrisonBattle(Hero attacker, MapGarrison garrison)
    {
        var owner = garrison.Owner;
        var defender = new BattleParticipant(
            BattleSide.Defender, owner, null, garrison.Id, 0, 0, SkillLevel.None, IsComputer: owner is not { } o || !state.GetPlayer(o).IsHuman,
            ArmyMorale: MoraleAndLuck.MixedFactions(garrison.Troops.Stacks.Select(s => state.Creatures.Get(s.Stack.Creature))));
        var battle = Create(Participant(BattleSide.Attacker, attacker), defender, garrison.VisitPosition, isAmbush: false);
        var stacks = garrison.Troops.Stacks.Select(s => new BattleStackSpec(state.Creatures.Get(s.Stack.Creature), s.Stack.Count, s.Slot)).ToList();
        Begin(attacker, battle, Stacks(attacker), stacks);
    }

    /// <summary>
    /// A hero's side; a garrison hero (<paramref name="garrisonOf"/>, ADR-112) leads its town's garrison and holds the town as its guard. A hero defending
    /// its town (on the entrance or in the garrison) gets the Brotherhood's morale, and in a siege the town's unique buildings' bonus (ADR-138).
    /// </summary>
    private BattleParticipant Participant(BattleSide side, Hero hero, Town? garrisonOf = null)
    {
        var home = side == BattleSide.Defender ? garrisonOf ?? state.ObjectsOf<Town>().FirstOrDefault(t => t.Owner == hero.Owner && t.VisitPosition == hero.Position) : null;
        var siege = home is not null && SiegeRules.SpecOf(home) is not null ? UniqueBuildings.SiegeOf(home) : SiegeBonus.None;
        var bonuses = HeroBattleBonuses.For(hero, state.Creatures);
        var caster = BattleCaster.For(hero, state.Spells);
        if (!siege.IsNone)
        {
            bonuses = bonuses with { Morale = bonuses.Morale + siege.Morale, Luck = bonuses.Luck + siege.Luck, Initiative = bonuses.Initiative + siege.Initiative };
            var lent = siege.Knowledge * ManaRules.ManaPerKnowledge;
            caster = caster is null ? null : caster with { SpellPower = caster.SpellPower + siege.SpellPower, Mana = caster.Mana + lent, LentMana = lent };
        }

        return new(side, hero.Owner, hero.Id, garrisonOf?.Id, hero.TotalAttack + siege.Attack, hero.TotalDefense + siege.Defense, hero.SkillLevelOf(BuiltInSkills.Tactics),
            !state.GetPlayer(hero.Owner).IsHuman,
            bonuses,
            (hero.Bonus.NoFactionPenalty ? 0 : MoraleAndLuck.MixedFactions((garrisonOf?.Garrison ?? hero.Army).Stacks.Select(s => state.Creatures.Get(s.Stack.Creature))))
            + (home is not null ? DefenderMorale(home) : 0),
            caster);
    }

    /// <summary>A town's defenders get the Brotherhood's morale (ADR-075, CMB-2): its garrison, or an own hero standing at its entrance.</summary>
    private static int DefenderMorale(Town town) =>
        town.Level(BuiltInBuildings.Tavern) >= MoraleAndLuck.BrotherhoodLevel ? MoraleAndLuck.BrotherhoodDefenderMorale : 0;

    /// <summary>
    /// The hero's army and war machines (ADR-056): tent and cart when owned, the offensive machine only from Advanced
    /// Ballistics, except for the attacker of a siege, whose offensive machine is the siege engine (ADR-106). The hero's health artifacts give its
    /// creatures (not its machines) more health for the battle, rounded down (ADR-111). A garrison hero leads its town's garrison (<paramref name="army"/>,
    /// ADR-112): that is how a town keeps war machines.
    /// </summary>
    private List<BattleStackSpec> Stacks(Hero hero, bool besieging = false, Army? army = null)
    {
        var health = hero.Bonus.CreatureHealthPercent;
        var stacks = (army ?? hero.Army).Stacks
            .Select(s => state.Creatures.Get(s.Stack.Creature) is var creature && health != 0
                ? new BattleStackSpec(creature with { Health = Math.Max(1, creature.Health * (100 + health) / 100) }, s.Stack.Count, s.Slot)
                : new BattleStackSpec(creature, s.Stack.Count, s.Slot))
            .ToList();
        var town = ProgressionService.TownOf(state, hero);
        foreach (var slot in hero.WarMachines.Order())
        {
            if (slot != WarMachineSlot.Offensive || besieging || WarMachines.FieldsOffensive(hero.SkillLevelOf(BuiltInSkills.Ballistics)))
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
        return new(attacker, defender, ground, location, isAmbush, state.Random.Fork(), state.Terrains.Get(ground).NativeTowns) { Creatures = state.Creatures, Spells = state.Spells };
    }

    /// <summary>
    /// Sets the battle up; a <paramref name="siege"/> fixes the 36-wide field (no width choice) and raises the town's fortifications (ADR-106).
    /// </summary>
    private void Begin(
        Hero attacker, BattleState battle, List<BattleStackSpec> attackers, List<BattleStackSpec> defenders, bool widthChoice = true, bool deployment = true,
        SiegeSpec? siege = null)
    {
        if (state.Battle is not null)
        {
            throw new CommandRefusedException("A battle is already being fought.");
        }

        state.Battle = battle;
        battle.PendingStacks = (attackers, defenders);
        battle.DeploymentAllowed = deployment;
        battle.Siege = siege is null ? null : SiegeRules.Create(siege);
        var decision = new BattleDecision(attacker.Id);
        state.PendingDecision = decision;

        // The phase is settled before anyone hears of the battle, so views see the width choice (publish after mutating).
        var (chooser, level) = BattleSetup.EffectiveTactics(battle.Attacker.Tactics, battle.Defender.Tactics);
        var choosing = widthChoice && siege is null && chooser is { } side && !battle.IsComputerControlled(side) && BattleSetup.WidthOptions(level).Count > 1;
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
            flow.Deploy(battle, siege is null ? BattleState.DefaultWidth : BattleState.SiegeWidth);
        }
    }
}
