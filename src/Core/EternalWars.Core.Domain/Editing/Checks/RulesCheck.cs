using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Game.Conditions;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>Victory and defeat conditions (ADR-057): what they point at must exist, and a game should be able to end.</summary>
internal sealed class RulesCheck : IMapCheck
{
    public IEnumerable<MapIssue> Check(GameState scenario)
    {
        var rules = scenario.Rules;
        if (rules.WeekEvents.Problem() is { } weekEvents)
        {
            yield return new(IssueSeverity.Error, weekEvents);
        }
        else if (rules.WeekEvents.Enabled)
        {
            // Without creatures to pick, a week or month of a creature quietly becomes an ordinary week.
            if (Turns.WeekEventRules.Candidates(scenario.Creatures, rules.WeekEvents.Weekly).Count == 0)
            {
                yield return new(IssueSeverity.Warning, "No faction is chosen for the weekly events: there will be no week of a creature.");
            }

            if (Turns.WeekEventRules.Candidates(scenario.Creatures, rules.WeekEvents.Monthly).Count == 0)
            {
                yield return new(IssueSeverity.Warning, "No faction is chosen for the monthly events: there will be no month of a creature.");
            }
        }

        foreach (var condition in rules.Victory.Concat(rules.Defeat))
        {
            var name = ScenarioRules.Describe(scenario, condition);
            if (condition.Player is { } player && !scenario.HasPlayer(player))
            {
                yield return new(IssueSeverity.Error, $"Condition '{condition.Kind}' belongs to a missing player.");
                continue;
            }

            if (condition.Deadline is { } deadline)
            {
                if (!condition.CanHaveDeadline)
                {
                    yield return new(IssueSeverity.Error, $"'{name}': only a victory condition other than surviving may have a deadline.");
                }
                else if (deadline < 2)
                {
                    yield return new(IssueSeverity.Error, $"'{name}': a deadline needs a day after the first.");
                }
            }

            if (ScenarioConditionHandlers.Find(condition.Kind)?.Problem(scenario, condition, name) is { } issue)
            {
                yield return issue;
            }
        }

        if (!rules.CanBeWon)
        {
            yield return new(IssueSeverity.Warning, "Nobody can win this scenario: the standard victory is off and there is no victory condition.");
        }
        else if (!rules.Arena && rules.StandardVictory && rules.Victory.Count == 0
                 && scenario.Players.Where(p => scenario.HeroesOf(p.Id).Any() || scenario.ObjectsOf<Town>().Any(t => t.Owner == p.Id)).Select(p => p.Team).Distinct().Count() < 2)
        {
            yield return new(IssueSeverity.Warning, "Fewer than two teams start with a hero or a town, so the game cannot end by defeating enemies.");
        }

        // Team counts (ADR-117): the default teams must fit them, and with locked teams nothing at setup can fix them.
        var teams = scenario.Players.Select(p => p.Team).Distinct().Count();
        if (scenario.TeamCountProblem(teams) is { } problem)
        {
            yield return new(scenario.TeamsLocked ? IssueSeverity.Error : IssueSeverity.Warning, $"The default teams do not fit the team counts: {problem}");
        }

        if (scenario.MinTeams > scenario.Players.Count)
        {
            yield return new(IssueSeverity.Error, $"The map asks for at least {scenario.MinTeams} teams but has {scenario.Players.Count} players.");
        }
    }
}
