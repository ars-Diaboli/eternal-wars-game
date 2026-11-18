using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.AI;

/// <summary>
/// The AI's answers to the player's pending decisions (<see cref="GameState.PendingDecision"/>), each through its command like a front-end:
/// town gates, training, cartographers, deposits, sphinxes, map event questions (EVT-6, by what each answer is worth), witch huts, Pandora's
/// Boxes, treasure chests, hero meetings (a secondary hero hands its troops to the main hero, ADR-059), guards, level-ups, and battles it
/// commands (auto-resolved).
/// </summary>
internal sealed class DecisionResponder(AiContext ai, TargetValuer values)
{
    private readonly AiContext _ai = ai;
    private readonly TargetValuer _values = values;
    private readonly GameSession _session = ai.Session;
    private readonly GameState _state = ai.State;
    private readonly AiWeights _weights = ai.Weights;

    /// <summary>Answers whatever is pending. False when a human has to answer (battle) or the AI cannot.</summary>
    public bool ResolvePending(PlayerId player)
    {
        for (var i = 0; i < 32; i++)
        {
            if (!_ai.Active(player))
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
                    var gateTown = _values.GateTown(_state.GetHero(gate.Hero))?.Town.Id;
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
                    command = new AnswerDepositCommand(player, _ai.Spare(player).CanAfford(deposit.Price));
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
                    command = new AnswerPandorasBoxCommand(player, _values.BoxValue(opener, pandora.Contents) > 0
                        && (!pandora.IsGuarded || _ai.Estimator.Fight(opener, _ai.Seen(pandora.Guard, pandora.VisitPosition, null)).IsSafe(_weights.MonsterLoss)));
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
                    var commander = _session.Battles.Commander(BattleSide.Attacker) ?? _session.Battles.Commander(BattleSide.Defender);
                    if (commander is not { } who || (!_ai.ControlHumanSlots && _state.GetPlayer(who).IsHuman))
                    {
                        return false; // a human fights this one
                    }

                    command = new AutoResolveBattleCommand(who);
                    break;
                default:
                    return false;
            }

            if (!_ai.Execute(command).IsAccepted)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>The answer worth the most to the player (the first of equals), counting a quest's completion; 0 when the AI cannot see the question.</summary>
    private int BestAnswer()
    {
        if (_session.MapEvents.PendingQuestion is not { } question)
        {
            return 0;
        }

        var scope = new EventScope(question.Player, question.Hero is { } id ? _state.Heroes.FirstOrDefault(h => h.Id == id) : null, question.Town);
        var best = 0;
        var bestWorth = double.MinValue;
        for (var i = 0; i < question.Answers.Count; i++)
        {
            var answer = question.Answers[i];
            var worth = _values.Worth(answer.Actions, scope, 0).Value;
            if (MapEventService.CompletedSite(answer) is { } site && _state.FindObject(site) is QuestSite { Kind: MapObjectKind.QuestGuard } guard && scope.Player is { } player)
            {
                worth += _weights.GuardOpened + _values.OpensTheWay(player, guard.VisitPosition);
            }

            if (worth > bestWorth)
            {
                (best, bestWorth) = (i, worth);
            }
        }

        return best;
    }

    /// <summary>
    /// Two own heroes met: the secondary one hands its troops to the main hero (ADR-059), strongest stacks first, into a slot of the same creature or a
    /// free one, else in exchange for the main army's weakest stack when that is weaker. It keeps one creature, as a hero must keep a stack.
    /// </summary>
    private void Exchange(PlayerId player, HeroId first, HeroId second)
    {
        var a = _state.GetHero(first);
        var b = _state.GetHero(second);
        var (main, feeder) = a.Id == _ai.Main || (b.Id != _ai.Main && _ai.ArmyPower(a.Army) >= _ai.ArmyPower(b.Army)) ? (a, b) : (b, a);

        // The main hero takes the war machines it lacks (ADR-070).
        foreach (var machine in feeder.WarMachines.Except(main.WarMachines).Order().ToList())
        {
            _ai.Execute(new GiveWarMachineCommand(player, feeder.Id, machine, main.Id));
        }

        foreach (var (slot, stack) in feeder.Army.Stacks.OrderByDescending(s => _ai.Power(s.Stack)).ThenBy(s => s.Slot).ToList())
        {
            if (!_ai.Active(player) || feeder.Army[slot] is not { } current)
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
                var (weakSlot, weak) = main.Army.Stacks.OrderBy(s => _ai.Power(s.Stack)).ThenBy(s => s.Slot).First();
                to = _ai.Power(weak) < _ai.Power(current) ? weakSlot : -1;
            }

            if (to >= 0 && count > 0)
            {
                _ai.Execute(new MoveStackCommand(player, feeder.Id, slot, main.Id, to, count == current.Count ? null : count));
            }
        }
    }
}
