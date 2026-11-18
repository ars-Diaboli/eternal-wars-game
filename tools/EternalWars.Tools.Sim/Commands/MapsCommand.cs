using EternalWars.Core.Data.Records;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Generation;

namespace EternalWars.Tools.Sim.Commands;

/// <summary>
/// <c>sim maps [--template T] [--maps N] [--days D] [--seed S]</c>, the CI check of the shipped templates (or one): N maps each, cycling through
/// the template's sizes and player counts (from the most players down, so empty slots come up too). Each map must generate without problems and
/// the same way twice, let every start town reach every other one, and survive D days of AI-vs-AI play with no refused command and a replay of
/// the record through JSON to the same state.
/// </summary>
internal sealed class MapsCommand : SimCommand
{
    public override string Name => "maps";

    public override IReadOnlyList<OptionSpec> Options =>
        [OptionSpec.Text("--template"), OptionSpec.Number("--maps", min: 1), OptionSpec.Number("--days", min: 0), OptionSpec.Seed("--seed")];

    public override bool TakesWeights => true;

    public override int Run(CommandLine line, SimContext sim)
    {
        var (only, count, days, first) = (line.Text("--template"), line.Number("--maps", 3), line.Number("--days", 14), line.Seed("--seed", 1));
        var templates = only is null ? BuiltInTemplates.All : [SimContext.LoadTemplate(only)];
        var failed = 0;
        foreach (var template in templates)
        {
            if (TemplateValidator.Validate(template).Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Message).ToList() is { Count: > 0 } issues)
            {
                failed += count;
                Console.WriteLine($"{template.Name}: invalid template: {string.Join("; ", issues)}");
                continue;
            }

            for (var i = 0; i < count; i++)
            {
                var seed = first + (ulong)i;
                var size = template.Sizes[i % template.Sizes.Count];
                var players = template.MaxPlayers - (i % (template.MaxPlayers - template.MinPlayers + 1));
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var label = $"{template.Name} {size}×{size} seed {seed}, {players} players";
                try
                {
                    var errors = CheckMap(sim, template, size, players, seed, days, out var summary);
                    Console.WriteLine($"{label}: {(errors.Count == 0 ? "ok" : "FAILED")}, {summary}, {clock.ElapsedMilliseconds} ms");
                    errors.ForEach(e => Console.WriteLine($"  {e}"));
                    failed += errors.Count == 0 ? 0 : 1;
                }
                catch (Exception e) when (e is InvalidOperationException or ArgumentException or FormatException)
                {
                    failed++;
                    Console.WriteLine($"{label}: FAILED: {e.Message}");
                }
            }
        }

        Console.WriteLine($"{templates.Count * count} maps of {templates.Count} templates: {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    private static List<string> CheckMap(SimContext sim, MapTemplate template, int size, int players, ulong seed, int days, out string summary)
    {
        var errors = new List<string>();
        var game = SimGames.GeneratedGame(sim, template, size, players, seed);
        errors.AddRange(game.Problems.Select(p => $"generator: {p}"));
        if (GameStateDigest.Compute(game.State) != GameStateDigest.Compute(SimGames.GeneratedGame(sim, template, size, players, seed).State))
        {
            errors.Add("the same seed generated two different games");
        }

        errors.AddRange(MapConnectivity.UnreachableRivalTowns(game.State)
            .Select(u => $"{u.Player.Name} cannot reach the town of {game.State.GetPlayer(u.Town.Owner!.Value).Name} at {u.Town.VisitPosition}"));
        errors.AddRange(MapValidator.Validate(game.State).Where(i => i.Severity == IssueSeverity.Error).Select(i => $"map validator: {i.Message}"));

        var (session, ai) = SimGames.PlayAiGame(sim, game, days);
        if (ai.Refused > 0)
        {
            errors.Add($"the AI issued {ai.Refused} refused commands");
        }

        var record = GameRecordSerializer.Serialize(GameReplay.Record(game.Map, session, generated: game.Generated));
        GameReplay.Replay(GameRecordSerializer.Deserialize(record), sim.Content);

        var state = session.State;
        var outcome = state.Outcome is { HasWinner: true } o ? string.Join("+", o.Winners.Select(w => state.GetPlayer(w).Name)) + " won" : state.IsOver ? "nobody won" : "unfinished";
        summary = $"attempt {game.Generated!.Attempt}, day {state.Date.AbsoluteDay} {outcome}, {session.CommandLog.Count} commands";
        return errors;
    }
}
