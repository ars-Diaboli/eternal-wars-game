using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Events;

namespace EternalWars.Tools.Sim;

/// <summary>
/// <c>sim game --profile</c>: how long each AI turn took, the path searches and cost maps it ran (<see cref="PathFinder.Counters"/>), the battle estimates it
/// asked for (<see cref="BattleEstimateCounters"/>) and the battles in it,
/// for a baseline before and after performance work. Times are wall-clock on this machine; the counts are the same on every run of a seed.
/// </summary>
internal sealed class TurnProfile
{
    private readonly List<Turn> _turns = [];
    private readonly System.Diagnostics.Stopwatch _battleClock = new();
    private Turn? _current;

    public sealed class Turn(int day, PlayerId player)
    {
        public int Day { get; } = day;
        public PlayerId Player { get; } = player;
        public long Started { get; set; }
        public TimeSpan Time { get; set; }
        public PathSearchCounters Paths { get; set; }
        public BattleEstimateCounters Estimates { get; set; }
        public int Battles { get; set; }
        public TimeSpan BattleTime { get; set; }
    }

    public void Attach(GameSession session)
    {
        session.Events.Subscribe<BattleStarted>(_ => _battleClock.Restart());
        session.Events.Subscribe<BattleEnded>(_ =>
        {
            if (_current is { } turn)
            {
                turn.Battles++;
                turn.BattleTime += _battleClock.Elapsed;
            }
        });
    }

    public Turn Begin(GameState state)
    {
        _current = new Turn(state.Date.AbsoluteDay, state.CurrentPlayer.Id) { Paths = PathFinder.Counters, Estimates = BattleEstimateCounters.Current, Started = System.Diagnostics.Stopwatch.GetTimestamp() };
        return _current;
    }

    public void End(Turn turn)
    {
        turn.Time = System.Diagnostics.Stopwatch.GetElapsedTime(turn.Started);
        turn.Paths = PathFinder.Counters - turn.Paths;
        turn.Estimates = BattleEstimateCounters.Current - turn.Estimates;
        _turns.Add(turn);
        _current = null;
    }

    public List<string> Describe(GameState state)
    {
        if (_turns.Count == 0)
        {
            return [];
        }

        var total = Sum(_turns);
        var lines = new List<string>
        {
            $"profile: {state.Map.Width}×{state.Map.Height}, {state.Map.Levels} level(s), {state.Objects.Count(o => o.TeleportTarget is not null)} teleporters " +
            $"(path searches {(state.Objects.Any(o => o.TeleportTarget is not null) ? "without" : "with")} the distance estimate)",
            $"  {_turns.Count} turns in {total.Time.TotalMilliseconds:0} ms ({total.Time.TotalMilliseconds / _turns.Count:0.0} ms a turn, " +
            $"median {Median(_turns):0.0}); battles {total.Battles} in {total.BattleTime.TotalMilliseconds:0} ms",
            $"  path searches {total.Paths.Searches} ({(double)total.Paths.Searches / _turns.Count:0} a turn), {total.Paths.Found} found; " +
            $"cost maps {total.Paths.Floods} ({(double)total.Paths.Floods / _turns.Count:0} a turn); " +
            $"{total.Paths.Expanded} tiles expanded ({(total.Paths.Searches + total.Paths.Floods == 0 ? 0 : (double)total.Paths.Expanded / (total.Paths.Searches + total.Paths.Floods)):0} each), " +
            $"{total.Paths.Time.TotalMilliseconds:0} ms ({100 * total.Paths.Time / total.Time:0} % of the turns)",
            $"  battle estimates {total.Estimates.Asked} asked, {total.Estimates.Played} played in " +
            $"{total.Estimates.Time.TotalMilliseconds:0} ms ({100 * total.Estimates.Time / total.Time:0} % of the turns)",
        };
        foreach (var group in _turns.GroupBy(t => t.Player).OrderBy(g => g.Key.Value))
        {
            var sum = Sum(group);
            lines.Add($"  {state.GetPlayer(group.Key).Name,-8} {sum.Time.TotalMilliseconds,7:0} ms, {sum.Paths.Searches,7} searches, {sum.Paths.Floods,5} maps, {sum.Paths.Expanded,10} tiles in {sum.Paths.Time.TotalMilliseconds,6:0} ms, {sum.Battles,3} battles");
        }

        foreach (var week in _turns.GroupBy(t => (t.Day - 1) / 7))
        {
            var sum = Sum(week);
            lines.Add($"  week {week.Key + 1,-2} {sum.Time.TotalMilliseconds,7:0} ms, {sum.Paths.Searches,7} searches, {sum.Paths.Floods,5} maps, {sum.Paths.Expanded,10} tiles in {sum.Paths.Time.TotalMilliseconds,6:0} ms, {sum.Battles,3} battles");
        }

        lines.Add("  slowest turns:");
        lines.AddRange(_turns.OrderByDescending(t => t.Time).Take(5).Select(t =>
            $"    day {t.Day,-3} {state.GetPlayer(t.Player).Name,-8} {t.Time.TotalMilliseconds,6:0} ms, {t.Paths.Searches,5} searches, {t.Paths.Floods,3} maps, {t.Paths.Expanded,8} tiles in {t.Paths.Time.TotalMilliseconds,4:0} ms, {t.Battles} battles ({t.BattleTime.TotalMilliseconds:0} ms)"));
        return lines;

        static Turn Sum(IEnumerable<Turn> turns) => turns.Aggregate(new Turn(0, default), (sum, t) =>
        {
            sum.Time += t.Time;
            sum.Paths += t.Paths;
            sum.Estimates += t.Estimates;
            sum.Battles += t.Battles;
            sum.BattleTime += t.BattleTime;
            return sum;
        });

        static double Median(List<Turn> turns) => turns.Select(t => t.Time.TotalMilliseconds).Order().ElementAt(turns.Count / 2);
    }
}
