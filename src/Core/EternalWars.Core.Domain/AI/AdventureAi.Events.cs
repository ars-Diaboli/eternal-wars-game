using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.AI;

/// <summary>
/// The AI and the map events (EVT-6): it answers a question by what each answer is worth, does a quest stage when what it gives is worth more than
/// what it takes, and treats Quest Guards and Gates as obstacles until its hero meets them. It does not look ahead at events: event cells, player
/// and town events and a Pandora's Box's event are worth nothing to it until they run. Random numbers count as their average, so the AI still
/// draws none.
/// </summary>
public sealed partial class AdventureAi
{
    /// <summary>How deep the worth of a "run another event" is followed.</summary>
    private const int EventWorthDepth = 4;

    /// <summary>What getting a Quest Guard out of the way is worth on its own (more when unexplored ground is behind it).</summary>
    private const double GuardOpened = 500;

    /// <summary>The answer worth the most to the player (the first of equals), counting a quest's completion; 0 when the AI cannot see the question.</summary>
    private int BestAnswer()
    {
        if (session.MapEvents.PendingQuestion is not { } question)
        {
            return 0;
        }

        var scope = new EventScope(question.Player, question.Hero is { } id ? _state.Heroes.FirstOrDefault(h => h.Id == id) : null, question.Town);
        var best = 0;
        var bestWorth = double.MinValue;
        for (var i = 0; i < question.Answers.Count; i++)
        {
            var answer = question.Answers[i];
            var worth = Worth(answer.Actions, scope, 0).Value;
            if (MapEventService.CompletedSite(answer) is { } site && _state.FindObject(site) is QuestSite { Kind: MapObjectKind.QuestGuard } guard && scope.Player is { } player)
            {
                worth += GuardOpened + OpensTheWay(player, guard.VisitPosition);
            }

            if (worth > bestWorth)
            {
                (best, bestWorth) = (i, worth);
            }
        }

        return best;
    }

    /// <summary>
    /// What doing the quest object's current stage is worth to the hero, or 0 when it does not meet it (or it is a gate, which it simply walks
    /// through once it does): the stage's actions (what it takes, then its rewards), and for a guard the way it opens.
    /// </summary>
    private double QuestWorth(Hero hero, QuestSite site)
    {
        if (site.Kind == MapObjectKind.QuestGate || QuestRules.Current(_state, site) is not { } current || !QuestRules.Meets(_state, hero, site))
        {
            return 0;
        }

        var worth = Worth(current.Stage.Actions, new EventScope(hero.Owner, hero, null), 0).Value;
        return site.Kind == MapObjectKind.QuestGuard ? worth + GuardOpened + OpensTheWay(hero.Owner, site.VisitPosition) : worth;
    }

    /// <summary>The actions' worth in gold, and whether they end the event (a stop, or a battle the hero would lose).</summary>
    private (double Value, bool Stops) Worth(IReadOnlyList<EventAction> actions, EventScope scope, int depth)
    {
        var total = 0.0;
        foreach (var action in actions)
        {
            var (value, stops) = Worth(action, scope, depth);
            total += value;
            if (stops)
            {
                return (total, true);
            }
        }

        return (total, false);
    }

    /// <summary>One action's worth by the AI's usual values (those of <see cref="BoxValue"/>); a battle is played out by the estimator.</summary>
    private (double Value, bool Stops) Worth(EventAction action, EventScope scope, int depth)
    {
        var hero = scope.Hero;
        var town = scope.Town is { } townId ? _state.FindObject(townId) as Town : null;
        switch (action)
        {
            case StopAction:
                return (0, true);
            case IfAction branch:
                return Worth(EventHolds(branch.Condition, scope) ? branch.Then : branch.Else, scope, depth);
            case RepeatAction repeat:
                var times = (int)Math.Clamp(Math.Round(Average(repeat.Times, scope)), 0, MapEventService.MaxLoopRounds);
                if (times == 0)
                {
                    return (0, false);
                }

                var round = Worth(repeat.Actions, scope, depth);
                return round.Stops ? round : (times * round.Value, false);
            case WhileAction loop:
                return EventHolds(loop.Condition, scope) ? Worth(loop.Actions, scope, depth) : (0, false); // one round: what changes is not followed
            case RunEventAction run:
                // A stop in the run event ends only it.
                return depth < EventWorthDepth && _state.MapEvents.Set.Find(run.Event) is { Kind: not MapEventKind.Quest } other && _state.MapEvents.IsEnabled(other.Name)
                    ? (Worth(other.Actions, scope, depth + 1).Value, false)
                    : (0, false);
            case MessageAction message:
                return Worth(message.Rewards, scope, depth);
            case QuestionAction question when question.Answers.Count > 0:
                return question.Answers.Select(a => Worth(a.Actions, scope, depth)).MaxBy(w => w.Value);
            case BattleAction fight when hero is not null:
                if (hero.Army.IsEmpty || fight.Army.Count == 0)
                {
                    return (0, true);
                }

                var enemy = Army.Of([.. fight.Army.Take(Army.SlotCount)]);
                return _estimator.Fight(hero, enemy).IsSafe(MonsterLoss)
                    ? (0.35 * ArmyPower(enemy), false)
                    : (-(ArmyPower(hero.Army) + 5000.0), true); // the hero would be lost
        }

        return (Gain(action, scope, hero, town), false);
    }

    /// <summary>The worth of an action that gives or takes something (0 for one the event cannot do, or that gives nothing the AI values).</summary>
    private double Gain(EventAction action, EventScope scope, Hero? hero, Town? town)
    {
        double Amount(EventExpr expr) => Average(expr, scope);
        double CreatureValue(string id) => _state.Creatures.TryGet(id, out var creature) ? creature.Value : 0;
        double ArtifactWorth(string id) => hero is not null && _state.Artifacts.TryGet(id, out var artifact) ? 800 + (80 * FindScore(hero, artifact)) : 0;
        return action switch
        {
            ResourceAction resource when scope.Player is { } player => Amount(resource.Amount) is var amount && amount >= 0
                ? amount * GoldValue(resource.Resource, 1)
                : -Math.Min(-amount, _state.GetPlayer(player).Resources[resource.Resource]) * GoldValue(resource.Resource, 1),
            ExperienceAction experience when hero is not null => Math.Max(0, Amount(experience.Amount)),
            PrimaryStatAction stat when hero is not null => 750 * Math.Max(0, Amount(stat.Amount)),
            SkillAction skill when hero is not null && BuiltInSkills.Contains(skill.Skill) && BuiltInSkills.Resolve(skill.Skill) is var id && hero.Skills.CanRaise(id) =>
                1000 * Math.Max(0, (int)skill.Level - (int)hero.Skills.Level(id)),
            ArtifactAction { Take: false } artifact => hero is { Artifacts.BackpackFull: false } ? ArtifactWorth(artifact.Artifact) : 0,
            ArtifactAction artifact when hero is not null && (hero.Artifacts.IsWorn(artifact.Artifact) || hero.Artifacts.Backpack.Contains(artifact.Artifact)) =>
                -ArtifactWorth(artifact.Artifact),
            WarMachineAction machine when hero is not null => machine.Take
                ? hero.WarMachines.Contains(machine.Slot) ? -300 : 0
                : hero.WarMachines.Contains(machine.Slot) ? 0 : 300,
            SpellBookAction => hero is { HasSpellBook: false } ? 500 : 0,
            SpellAction spell when hero is { HasSpellBook: true } && !hero.Spells.ContainsKey(spell.Spell) && _state.Spells.TryGet(spell.Spell, out var taught) =>
                400 * taught.Level,
            ManaAction mana when hero is not null => 2 * Math.Max(-hero.Mana, Amount(mana.Amount)),
            MovementAction movement when hero is not null => Math.Max(-hero.MovementPoints, Amount(movement.Amount)),
            MoraleAction morale when hero is not null => 150 * (Math.Clamp(morale.Morale, -3, 3) + Math.Clamp(morale.Luck, -3, 3)),
            CreaturesAction creatures when hero is not null => Amount(creatures.Count) is var count && count >= 0
                ? hero.Army.CanAccept(creatures.Creature) ? 0.85 * count * CreatureValue(creatures.Creature) : 0
                : -Math.Min(-count, hero.Army.Stacks.Where(s => s.Stack.Creature == creatures.Creature).Sum(s => s.Stack.Count)) * CreatureValue(creatures.Creature),
            CreaturesAction creatures when town is not null => 0.6 * Amount(creatures.Count) * CreatureValue(creatures.Creature),
            AvailableCreaturesAction available when town is not null => 0.4 * Amount(available.Count) * CreatureValue(available.Line),
            BuildAction build when town is not null => town.Level(build.Building) >= build.Level ? 0 : 1000,
            RevealAction when scope.Player is not null => 100,
            GiveHeroAction give when scope.Player is { } owner && _state.HeroRoster.Contains(give.Hero) && !_state.HeroLimitReached(owner) => 2500,
            _ => 0,
        };
    }

    /// <summary>An expression's value with every random number at its average (the AI draws none).</summary>
    private double Average(EventExpr expr, EventScope scope) => expr switch
    {
        RandomExpr random => (random.Min + random.Max) / 2.0,
        ArithmeticExpr arithmetic when Average(arithmetic.Left, scope) is var left && Average(arithmetic.Right, scope) is var right => arithmetic.Op switch
        {
            ArithmeticOp.Add => left + right,
            ArithmeticOp.Subtract => left - right,
            ArithmeticOp.Multiply => left * right,
            _ => right == 0 ? 0 : left / right,
        },
        _ => MapEventEvaluator.Evaluate(_state, scope, expr), // no random number in it
    };

    /// <summary>A condition, its comparisons made with averages.</summary>
    private bool EventHolds(EventCondition condition, EventScope scope) => condition switch
    {
        AllCondition all => all.Conditions.All(c => EventHolds(c, scope)),
        AnyCondition any => any.Conditions.Any(c => EventHolds(c, scope)),
        NotCondition not => !EventHolds(not.Condition, scope),
        CompareCondition compare => Average(compare.Left, scope) is var left && Average(compare.Right, scope) is var right && compare.Op switch
        {
            CompareOp.Equal => left == right,
            CompareOp.NotEqual => left != right,
            CompareOp.Less => left < right,
            CompareOp.LessOrEqual => left <= right,
            CompareOp.Greater => left > right,
            _ => left >= right,
        },
        _ => MapEventEvaluator.Test(_state, scope, condition),
    };
}
