using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Tools.Sim.Commands;

/// <summary>
/// <c>sim game [--map M | --template T [--size N] [--players N]] [--days D] [--seed S] [--runs N] [--record F] [--difficulty C] [--fog reveal|refog]
/// [--profile]</c>: the adventure AI plays every slot of a map to the end or the day limit; prints how each game went.
/// </summary>
internal sealed class PlayGameCommand : SimCommand
{
    public override string Name => "game";

    public override IReadOnlyList<OptionSpec> Options =>
    [
        OptionSpec.Text("--map"), OptionSpec.Text("--template"), OptionSpec.Number("--size", min: 1), OptionSpec.Number("--players", min: 1),
        OptionSpec.Number("--days", min: 0), OptionSpec.Seed("--seed"), OptionSpec.Number("--runs", min: 1), OptionSpec.Text("--record"),
        OptionSpec.Choice("--difficulty", [.. Enum.GetNames<Difficulty>().Select(n => n.ToLowerInvariant())]), OptionSpec.Choice("--fog", "reveal", "refog"),
        OptionSpec.Flag("--profile"),
    ];

    public override bool TakesWeights => true;

    public override int Run(CommandLine line, SimContext sim) =>
        Game(sim, GameMaker(sim, line), line.Number("--days", 40), line.Seed("--seed", 1), line.Number("--runs", 1), line.Text("--record"), line.Has("--profile"));

    private static int Game(SimContext sim, Func<ulong, SimGame> make, int days, ulong seed, int runs, string? recordPath, bool profile)
    {
        var wins = new Dictionary<string, int>();
        var refusedTotal = 0;
        if (sim.WeightOptions.Count > 0)
        {
            Console.WriteLine($"AI weights: {string.Join(", ", sim.WeightOptions)}");
        }

        for (var run = 0; run < runs; run++)
        {
            var game = make(seed + (ulong)run);
            if (game.Generated is { } generated)
            {
                Console.WriteLine($"map {game.State.Map.Name}: attempt {generated.Attempt}{string.Concat(game.Problems.Select(p => $"; problem: {p}"))}");
            }

            var fights = new FightLog();
            var turns = profile ? new TurnProfile() : null;
            var (session, ai) = SimGames.PlayAiGame(sim, game, days, fights, turns);
            var state = session.State;
            var winner = state.Outcome is { HasWinner: true } outcome ? string.Join("+", outcome.Winners.Select(w => state.GetPlayer(w).Name)) : (state.IsOver ? "nobody" : "unfinished");
            wins[winner] = wins.GetValueOrDefault(winner) + 1;
            refusedTotal += ai.Refused;
            ai.Refusals.Take(10).ToList().ForEach(r => Console.WriteLine($"  refused: {r}"));
            Console.WriteLine($"run {run + 1}: {state.Date} (day {state.Date.AbsoluteDay}), {winner}{(state.Outcome is { } o ? ": " + o.Reason : string.Empty)}");
            Console.WriteLine($"  {session.CommandLog.Count} commands, AI refused {ai.Refused}, digest {GameStateDigest.Compute(state)}; garrison heroes in {session.CommandLog.Count(c => c is EnterGarrisonCommand)}, out {session.CommandLog.Count(c => c is LeaveGarrisonCommand)}");
            foreach (var player in state.Players)
            {
                var towns = state.ObjectsOf<Town>().Count(t => t.Owner == player.Id);
                var heroes = state.HeroesOf(player.Id).ToList();
                var power = heroes.Sum(h => h.Army.Stacks.Sum(s => (long)s.Stack.Count * state.Creatures.Get(s.Stack.Creature).FightValue));
                Console.WriteLine($"  {player.Name,-8} {(player.IsDefeated ? "defeated" : "playing "),-8} towns {towns}, heroes {heroes.Count} (levels {string.Join(",", heroes.Select(h => h.Level))}, at {string.Join(" ", heroes.Select(h => $"({h.Position.X},{h.Position.Y})"))}), army power {power}, {player.Resources}");
                Console.WriteLine($"           fights won / lost: {fights.Describe(player.Id)}");
            }

            var guards = state.ObjectsOf<MonsterGuard>().ToList();
            Console.WriteLine($"  left on the map: {guards.Count} monster stacks (power {guards.Sum(g => (long)Math.Max(1, g.Count ?? 1) * state.Creatures.Get(g.Creature).FightValue)}), {state.ObjectsOf<MapGarrison>().Count(g => g.Owner is null)} neutral garrisons, {state.ObjectsOf<Town>().Count(t => t.Owner is null)} neutral towns, {state.Objects.Count(o => o.Kind == MapObjectKind.BorderPost)} border posts; keys {string.Join(" ", state.Players.Select(p => $"{p.Name}:{p.Keys.Count}"))}");

            turns?.Describe(state).ForEach(line => Console.WriteLine($"  {line}"));

            if (recordPath is not null && run == 0)
            {
                GameRecordSerializer.Save(GameReplay.Record(game.Map, session, generated: game.Generated), recordPath);
                Console.WriteLine($"  recorded → {recordPath}");
            }
        }

        if (runs > 1)
        {
            Console.WriteLine($"{runs} games: {string.Join(", ", wins.OrderBy(w => w.Key).Select(w => $"{w.Key} {w.Value}"))}; AI refused {refusedTotal} commands");
        }

        return refusedTotal == 0 ? 0 : 1;
    }

    /// <summary>The games to play: a map generated from <c>--template</c> per seed, else the <c>--map</c> (default <c>towns</c>).</summary>
    private static Func<ulong, SimGame> GameMaker(SimContext sim, CommandLine line)
    {
        Difficulty? difficulty = line.Text("--difficulty") is { } level ? Enum.Parse<Difficulty>(level, ignoreCase: true) : null;
        var fog = line.Text("--fog") == "refog" ? FogMode.ShroudWithRefog : FogMode.PermanentReveal;
        if (line.Text("--template") is not { } name)
        {
            if (line.Has("--size") || line.Has("--players"))
            {
                throw new UsageException("game: --size and --players are for a map generated with --template.");
            }

            var map = line.Text("--map") ?? "towns";
            return seed => SimGames.HandMadeGame(sim, map, seed, difficulty, fog);
        }

        if (line.Has("--map"))
        {
            throw new UsageException("game: --map and --template exclude each other.");
        }

        var template = SimContext.LoadTemplate(name);
        var size = line.Number("--size", template.Sizes[0]);
        var players = line.Number("--players", template.MaxPlayers);
        return seed => SimGames.GeneratedGame(sim, template, size, players, seed, difficulty, fog);
    }
}
