using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
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
/// carries on where the turn stands. The parts: <see cref="DecisionResponder"/> answers the decisions, <see cref="TownManager"/> runs the
/// towns, <see cref="TargetValuer"/> values and picks the heroes' targets, <see cref="ThreatMap"/> weighs the enemy heroes' danger; the
/// numbers they weigh with are <see cref="AiWeights"/>.
/// </remarks>
public sealed partial class AdventureAi
{
    /// <summary>Most moves one hero makes in a turn; a safety net, a hero normally runs out of movement first.</summary>
    public const int MaxMovesPerHero = 24;

    private readonly GameSession _session;
    private readonly GameState _state;
    private readonly AiWeights _weights;
    private readonly AiContext _ai;
    private readonly ThreatMap _threats;
    private readonly TargetValuer _values;
    private readonly TownManager _towns;
    private readonly DecisionResponder _decisions;

    /// <summary>An AI for the session's players.</summary>
    /// <param name="session">The game it plays in.</param>
    /// <param name="controlHumanSlots">
    /// Also play turns of human slots and command battle sides of human players (the simulator, where nobody sits at the
    /// keyboard). Without it only computer players are played, and battles with a human side wait for that human.
    /// </param>
    /// <param name="weights">The numbers it weighs its choices with; <see cref="AiWeights.Default"/> when null.</param>
    public AdventureAi(GameSession session, bool controlHumanSlots = false, AiWeights? weights = null)
    {
        _session = session;
        _state = session.State;
        _weights = weights ?? AiWeights.Default;
        _ai = new AiContext(session, _weights, controlHumanSlots);
        _threats = new ThreatMap(_ai);
        _values = new TargetValuer(_ai, _threats);
        _towns = new TownManager(_ai, _threats);
        _decisions = new DecisionResponder(_ai, _values);
    }

    /// <summary>The numbers the AI weighs its choices with.</summary>
    public AiWeights Weights => _weights;

    /// <summary>Commands the AI issued, and how many of them the game refused (should stay 0).</summary>
    public int Commands => _ai.Commands;

    public int Refused => _ai.Refused;

    /// <summary>The refused commands with the game's reasons, for tuning (<c>sim game</c> prints them).</summary>
    public IReadOnlyList<string> Refusals => _ai.Refusals;

    /// <summary>Whether the current turn is the AI's to play.</summary>
    public bool IsMyTurn => _state.IsStarted && !_state.IsOver && (_ai.ControlHumanSlots || !_state.CurrentPlayer.IsHuman);

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
    /// Ends the current computer player's turn without playing it (a play-test's others off, ADR-267), once what it is asked is answered as in
    /// <see cref="PlayTurn"/> (a map event's question, a level-up). True when the turn is over; false when it has to wait for a human.
    /// </summary>
    public bool PassTurn()
    {
        if (!IsMyTurn)
        {
            return false;
        }

        var player = _state.CurrentPlayer.Id;
        return _decisions.ResolvePending(player)
            && (_state.CurrentPlayer.Id != player || _ai.Session.Execute(new Commands.EndTurnCommand(player)).IsAccepted);
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
        _ai.Claimed.Clear();
        _doorChecks.Clear();
        _threats.BeginTurn();
        _towns.Gathering = false;
        if (!_decisions.ResolvePending(player))
        {
            return false;
        }

        if (!_ai.Active(player))
        {
            return true;
        }

        _towns.LeaveGarrisons(player);
        _towns.ManageTowns(player);
        _ai.Main = _state.HeroesOf(player).OrderByDescending(h => _ai.ArmyPower(h.Army)).ThenBy(h => h.Id.Value).FirstOrDefault()?.Id;

        // The main hero moves first, so the carriers head for where it ends up.
        foreach (var hero in _state.HeroesOf(player).OrderBy(h => h.Id != _ai.Main).ThenBy(h => h.Id.Value).Select(h => h.Id).ToList())
        {
            if (!PlayHero(player, hero))
            {
                return false;
            }

            if (!_ai.Active(player))
            {
                return true;
            }
        }

        // The arena's preparation day ends with every army in the hero that fights at the start of day 2 (ADR-160).
        if (_ai.Preparing && !GatherForBattle(player))
        {
            return false;
        }

        _towns.ManageTowns(player);
        if (!_decisions.ResolvePending(player))
        {
            return false;
        }

        return !_ai.Active(player) || _ai.Execute(new EndTurnCommand(player)).IsAccepted;
    }

    // Heroes --------------------------------------------------------------------------------------------

    /// <summary>What an artifact is worth to the AI (<see cref="ArtifactValue.Of(ArtifactDefinition)"/>).</summary>
    public static double Score(ArtifactDefinition artifact) => ArtifactValue.Of(artifact);

    /// <summary>What wearing it is worth: an assembled relic with its pieces' effects.</summary>
    private double Worth(ArtifactDefinition artifact) => ArtifactValue.Of(_state.Artifacts, artifact);

    /// <summary>Puts on what is in the backpack when a slot is free or the new one is clearly better than the worst worn there.</summary>
    private void EquipBest(PlayerId player, Hero hero)
    {
        for (var guard = 0; guard < ArtifactSlots.BackpackSize * 2 && _ai.Active(player); guard++)
        {
            var candidate = hero.Artifacts.Backpack
                .Select(id => _state.Artifacts.TryGet(id, out var def) ? def : null)
                .OfType<ArtifactDefinition>()
                .Where(def => Worth(def) > 0 && Fits(hero, def) && (hero.Artifacts.HasRoomToWear(def.Slot) || Worth(def) > 1.2 * WorstWornScore(hero, def.Slot)))
                .OrderByDescending(Worth).ThenBy(def => def.Id)
                .FirstOrDefault();
            if (candidate is null || !_ai.Execute(new EquipArtifactCommand(player, hero.Id, candidate.Id)).IsAccepted)
            {
                break;
            }
        }

        // The AI always joins an assembled relic it can (ADR-121).
        foreach (var relic in ArtifactService.Assemblable(_state.Artifacts, hero).ToList())
        {
            _ai.Execute(new AssembleRelicCommand(player, hero.Id, relic.Id));
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
        for (var move = 0; move < (_ai.Preparing ? ArenaMovesPerHero : MaxMovesPerHero); move++)
        {
            if (!_decisions.ResolvePending(player))
            {
                return false;
            }

            if (!_ai.Active(player) || _state.Heroes.FirstOrDefault(h => h.Id == id) is not { } hero)
            {
                return true;
            }

            EquipBest(player, hero);
            RecruitHere(player, hero);
            UseServicesHere(player, hero);

            // The Grail's spot, known from the whole puzzle (ADR-140): dig with the day's full movement, or wait there for the next day.
            if (_values.GrailSpot(player) == hero.Position && !hero.Artifacts.BackpackFull)
            {
                if (GrailRules.CheckDig(_state, hero) is null)
                {
                    _ai.Execute(new DigCommand(player, hero.Id));
                }

                return true;
            }

            var town = _state.ObjectCellAt(hero.Position) is { Object: Town own, Cell: FootprintCell.Visit } && own.Owner == player ? own : null;
            if (town is not null && _threats.ShouldHoldGarrison(hero, town))
            {
                // Inside, the hero leads the whole garrison behind the walls; at the entrance it would fight alone and the garrison after it (ADR-112).
                _ai.Execute(new EnterGarrisonCommand(player, town.Id));
                return true;
            }

            // Arrived at an own town on the way: take the garrison along now, or the troops would call the hero back every day (ADR-114).
            if (town is not null)
            {
                _towns.Consolidate(player, town);
            }

            if (_threats.Defending(hero))
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

            CastBeforeMoving(player, hero);
            if (!_ai.Active(player))
            {
                return true;
            }

            var target = _values.ChooseTarget(hero, failed);
            if (target is null)
            {
                return true;
            }

            _ai.Claimed.Add(target.Value);
            CastForRoute(player, hero, target.Value);
            if (!_ai.Active(player) || hero.MovementPoints <= 0 || hero.Position == target.Value)
            {
                continue;
            }

            var result = _ai.Execute(new MoveHeroCommand(player, id, target.Value));
            if (!result.IsAccepted || result.Outcome is not MovementOutcome { StepsTaken: > 0 })
            {
                failed.Add(target.Value);
            }
        }

        return true;
    }

    /// <summary>
    /// For tuning (<c>EW_SIM_EXPLAIN</c> with <c>sim game</c>): the hero's best candidate targets as <see cref="TargetValuer.ChooseTarget"/> sees them, with their value, the
    /// opponent's power, whether the simulated fight is safe and the route's cost; then the exploring tile. Changes nothing.
    /// </summary>
    public IReadOnlyList<string> Explain(HeroId id, int count = 12) => _values.Explain(id, count);

    /// <summary>A hero standing on a map dwelling buys what it can afford (keeping a third of the gold) into its army (ADR-054).</summary>
    private void RecruitHere(PlayerId player, Hero hero)
    {
        if (_state.ObjectCellAt(hero.Position) is not { Object: CreatureDwelling { Available: > 0 } dwelling, Cell: FootprintCell.Visit }
            || !_state.Creatures.TryGet(dwelling.Creature, out var creature) || !hero.Army.CanAccept(creature.Id))
        {
            return;
        }

        var each = StewardshipRules.DiscountCreatures(creature.Cost, hero.SkillLevelOf(BuiltInSkills.Stewardship));
        var spare = _ai.Preparing ? _state.GetPlayer(player).Resources : _state.GetPlayer(player).Resources - new ResourceSet(Gold: _state.GetPlayer(player).Resources.Gold / 3);
        var count = TownRules.MaxAffordable(spare, each, dwelling.Available!.Value);
        if (count > 0)
        {
            _ai.Execute(new RecruitAtDwellingCommand(player, dwelling.Id, count));
        }
    }

    /// <summary>A hero on a hill fort or war machine factory entrance uses it (ADR-068).</summary>
    private void UseServicesHere(PlayerId player, Hero hero)
    {
        switch (_state.ObjectCellAt(hero.Position))
        {
            case { Object: HillFort fort, Cell: FootprintCell.Visit }:
                foreach (var (slot, _, _, _) in _values.HillFortUpgrades(hero))
                {
                    if (_ai.Active(player) && _state.PendingDecision is null)
                    {
                        _ai.Execute(new UpgradeAtHillFortCommand(player, fort.Id, slot));
                    }
                }

                break;
            case { Object: WarMachineFactory factory, Cell: FootprintCell.Visit }:
                foreach (var slot in _values.FactoryWants(hero))
                {
                    if (_ai.Active(player) && _state.PendingDecision is null)
                    {
                        _ai.Execute(new BuyWarMachineAtFactoryCommand(player, factory.Id, slot));
                    }
                }

                break;
            case { Object: SkillSchool school, Cell: FootprintCell.Visit }:
                foreach (var lesson in _values.LessonsToTake(hero, school))
                {
                    if (_ai.Active(player) && _state.PendingDecision is null)
                    {
                        _ai.Execute(new TakeLessonCommand(player, school.Id, lesson.Skill));
                    }
                }

                break;
        }
    }
}
