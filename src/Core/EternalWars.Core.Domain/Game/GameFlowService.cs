using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// Decides when players are eliminated and when the game is won (ADR-057). <see cref="Evaluate"/> runs after the game
/// starts and after every accepted command, so rules react to anything that happened, in any order of causes. Special
/// victory conditions are checked first, then defeats (special ones and the default elimination), then the default
/// victory: one team left standing, in a game that began with several.
/// </summary>
public sealed class GameFlowService(GameState state, IEventPublisher events)
{
    private readonly Dictionary<HeroId, (PlayerId Owner, string Name)> _heroes = [];
    private readonly Dictionary<MapObjectId, string> _prisoners = [];
    private int _teamsAtStart;

    /// <summary>Who is in the game: players with a hero or a town at the start; what heroes there are (for hero conditions).</summary>
    /// <summary>
    /// Before the game starts: a player with more heroes than it may have (a map made that way, ADR-068) loses the surplus, the newest heroes (highest
    /// ids) first. Each one is reported with <see cref="HeroRemoved"/>.
    /// </summary>
    internal void RemoveSurplusHeroes()
    {
        foreach (var player in state.Players)
        {
            foreach (var surplus in state.HeroesOf(player.Id).OrderBy(h => h.Id.Value).Skip(state.HeroLimit(player.Id)).ToList())
            {
                state.RemoveHero(surplus);
                events.Publish(new HeroRemoved(surplus.Id));
            }
        }
    }

    internal void Begin()
    {
        foreach (var player in state.Players)
        {
            player.TookPart = state.HeroesOf(player.Id).Any() || OwnsTown(player);
            player.HadTown = OwnsTown(player);
            player.StartTown = state.ObjectsOf<Town>().Where(t => t.Owner == player.Id).OrderBy(t => t.Id.Value).FirstOrDefault()?.TownType;
            player.TownlessSince = null;
        }

        _teamsAtStart = state.Players.Where(p => p.TookPart).Select(p => p.Team).Distinct().Count();
        _heroes.Clear();
        _prisoners.Clear();
        foreach (var hero in state.Heroes)
        {
            _heroes[hero.Id] = (hero.Owner, hero.Name);
        }
    }

    internal void Evaluate()
    {
        // A battle in progress is settled first; a scenario without any participant has nothing to decide.
        if (!state.IsStarted || state.IsOver || state.Battle is not null || _teamsAtStart == 0)
        {
            return;
        }

        var rules = state.Rules;
        WatchPrisons();
        foreach (var player in InGame())
        {
            if (rules.Victory.Where(c => c.Player is null || c.Player == player.Id).FirstOrDefault(c => HoldsFor(c, player)) is { } won)
            {
                End(player.Team, ScenarioRules.Achieved(state, player, won, won.Kind == ConditionKind.ReleaseHero ? PrisonerOf(won) : HeroNameOf(won)));
                return;
            }
        }

        foreach (var player in InGame().ToList())
        {
            TrackGrace(player);
            if (DefeatReason(player) is { } reason)
            {
                Defeat(player, reason);
            }
        }

        var left = InGame().ToList();
        if (left.Count == 0)
        {
            End(null, "Every player was defeated.");
        }
        else if (rules.StandardVictory && _teamsAtStart > 1 && left.Select(p => p.Team).Distinct().Count() == 1)
        {
            End(left[0].Team, "Every enemy was defeated.");
        }
    }

    private IEnumerable<Player> InGame() => state.Players.Where(p => p.TookPart && !p.IsDefeated);

    private bool HoldsFor(ScenarioCondition c, Player player) => c.Kind switch
    {
        ConditionKind.CaptureTown => c.Town is { } id && state.FindObject(id) is Town { Owner: { } owner }
                                     && (owner == player.Id || state.AreAllies(owner, player.Id)),
        ConditionKind.DefeatHero => c.Hero is { } hero && _heroes.TryGetValue(hero, out var known)
                                    && state.Heroes.All(h => h.Id != hero) && state.AreEnemies(known.Owner, player.Id),
        ConditionKind.GatherResources => c.Amount > 0 && player.Resources[c.Resource] >= c.Amount,
        ConditionKind.SurviveUntilDay => state.Date.AbsoluteDay >= c.Day,
        ConditionKind.CaptureObject => c.Target is { } id && state.FindObject(id) is IFlaggable { Owner: { } owner }
                                       && (owner == player.Id || state.AreAllies(owner, player.Id)),
        ConditionKind.ObtainArtifact => c.Artifact is { } artifact
                                        && state.Heroes.Any(h => h.Artifacts.Has(artifact) && (h.Owner == player.Id || state.AreAllies(h.Owner, player.Id))),
        ConditionKind.ReleaseHero => c.Target is { } prison && state.FindObject(prison) is null && _prisoners.TryGetValue(prison, out var prisoner)
                                     && state.Heroes.Any(h => h.Definition == prisoner && (h.Owner == player.Id || state.AreAllies(h.Owner, player.Id))),
        _ => false,
    };

    private string? DefeatReason(Player player)
    {
        foreach (var c in state.Rules.Defeat)
        {
            var applies = c.Player == player.Id || (c.Player is null && c.Kind == ConditionKind.TimeLimit);
            var holds = applies && c.Kind switch
            {
                ConditionKind.LoseTown => c.Town is { } id && state.FindObject(id) is Town town && town.Owner != player.Id,
                ConditionKind.LoseHero => c.Hero is { } hero && _heroes.ContainsKey(hero) && state.Heroes.All(h => h.Id != hero),
                ConditionKind.TimeLimit => state.Date.AbsoluteDay > c.Day,
                ConditionKind.LoseArtifact => c.Artifact is { } artifact && !state.HeroesOf(player.Id).Any(h => h.Artifacts.Has(artifact)),
                _ => false,
            };
            if (holds)
            {
                return ScenarioRules.Lost(state, c, HeroNameOf(c));
            }
        }

        // A victory condition with a deadline not met in time (ADR-073); met in time, the game would have ended above.
        if (state.Rules.Victory.FirstOrDefault(c => c.Deadline is { } day && state.Date.AbsoluteDay > day && (c.Player is null || c.Player == player.Id)) is { } missed)
        {
            return ScenarioRules.Missed(state, missed);
        }

        if (!state.Rules.Elimination)
        {
            return null;
        }

        if (!state.HeroesOf(player.Id).Any() && !OwnsTown(player))
        {
            return "Lost every town and hero.";
        }

        return player.TownlessSince is { } since && state.Rules.GraceDays > 0 && state.Date.AbsoluteDay >= since.AbsoluteDay + state.Rules.GraceDays
            ? $"Held no town for {state.Rules.GraceDays} days."
            : null;
    }

    private bool OwnsTown(Player player) => state.ObjectsOf<Town>().Any(t => t.Owner == player.Id);

    /// <summary>Starts the countdown when a player that had a town has none, and stops it when it has one again.</summary>
    private void TrackGrace(Player player)
    {
        if (!state.Rules.Elimination || state.Rules.GraceDays <= 0)
        {
            return;
        }

        if (OwnsTown(player))
        {
            player.HadTown = true;
            if (player.TownlessSince is not null)
            {
                player.TownlessSince = null;
                events.Publish(new GracePeriodEnded(player.Id));
            }
        }
        else if (player.HadTown && player.TownlessSince is null)
        {
            player.TownlessSince = state.Date;
            events.Publish(new GracePeriodStarted(player.Id, new GameDate(state.Date.AbsoluteDay + state.Rules.GraceDays)));
        }
    }

    private string? HeroNameOf(ScenarioCondition c) => c.Hero is { } hero && _heroes.TryGetValue(hero, out var known) ? known.Name : null;

    /// <summary>
    /// Remembers who sits in each prison a condition names: freeing the hero removes the prison, and a prisoner left open by the map is drawn
    /// after <see cref="Begin"/> (<see cref="PrisonRules.Prepare"/>).
    /// </summary>
    private void WatchPrisons()
    {
        foreach (var c in state.Rules.Victory)
        {
            if (c is { Kind: ConditionKind.ReleaseHero, Target: { } id } && state.FindObject(id) is Prison { Hero: { } hero })
            {
                _prisoners[id] = hero;
            }
        }
    }

    private string? PrisonerOf(ScenarioCondition c) =>
        c.Target is { } id && _prisoners.TryGetValue(id, out var hero) && state.HeroRoster.Contains(hero) ? state.HeroRoster.Get(hero).Name : null;

    /// <summary>A defeated player leaves: its heroes are removed, its towns and mines turn neutral.</summary>
    private void Defeat(Player player, string reason)
    {
        player.IsDefeated = true;
        foreach (var hero in state.HeroesOf(player.Id).ToList())
        {
            state.RemoveHero(hero);
            if (state.PendingDecision?.Hero == hero.Id)
            {
                state.PendingDecision = null;
            }

            events.Publish(new HeroDefeated(hero.Id, hero.Owner, Retreated: false));
        }

        // Heroes waiting in its taverns go back to the roster (ADR-081).
        state.RemoveRetiredHeroesOf(player.Id);

        foreach (var town in state.ObjectsOf<Town>().Where(t => t.Owner == player.Id).ToList())
        {
            town.Owner = null;
            events.Publish(new TownOwnerChanged(town.Id, player.Id, null));
        }

        foreach (var mine in state.ObjectsOf<Mine>().Where(m => m.Owner == player.Id).ToList())
        {
            mine.Owner = null;
            events.Publish(new MineOwnerChanged(mine.Id, player.Id, null));
        }

        foreach (var dwelling in state.ObjectsOf<CreatureDwelling>().Where(d => d.Owner == player.Id).ToList())
        {
            dwelling.Owner = null;
            events.Publish(new DwellingOwnerChanged(dwelling.Id, player.Id, null));
        }

        foreach (var provider in state.ObjectsOf<ResourceProvider>().Where(p => p.Owner == player.Id).ToList())
        {
            provider.Owner = null;
            events.Publish(new ProviderOwnerChanged(provider.Id, player.Id, null));
        }

        // A defeated player's garrisons turn neutral and keep their troops (ADR-082).
        foreach (var garrison in state.ObjectsOf<MapGarrison>().Where(g => g.Owner == player.Id).ToList())
        {
            garrison.Owner = null;
            events.Publish(new GarrisonOwnerChanged(garrison.Id, player.Id, null));
        }

        state.RemoveCaravansOf(player.Id);
        events.Publish(new PlayerDefeated(player.Id, reason));
    }

    /// <summary>
    /// Ends the game for the team's players still in it. While one of its human players survives, the team's eliminated human players share the
    /// win (ADR-073); eliminated computer players never do, and a computer ally's survival alone wins nothing for eliminated humans.
    /// </summary>
    private void End(TeamId? winningTeam, string reason)
    {
        IReadOnlyList<PlayerId> winners = winningTeam is { } team
            ? state.Players.Where(p => p.TookPart && p.Team == team)
                .Where(p => !p.IsDefeated || (p.IsHuman && InGame().Any(o => o.Team == team && o.IsHuman)))
                .Select(p => p.Id).ToList()
            : [];
        state.Outcome = new GameOutcome(winners, reason, state.Date);
        events.Publish(new GameEnded(winners, reason, state.Date));
    }
}
