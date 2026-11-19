using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Events;
using EternalWars.Core.Generation;

namespace EternalWars.Tools.Sim;

/// <summary>
/// A game ready to start: its state, the map document it starts from (for records) and, on a generated map, how it was generated and the
/// problems of the map kept (<see cref="MapGenerator.GenerateBest"/>).
/// </summary>
internal sealed record SimGame(MapDocument Map, GameState State, GenerationInfo? Generated = null, IReadOnlyList<string>? GenerationProblems = null)
{
    public IReadOnlyList<string> Problems => GenerationProblems ?? [];
}

/// <summary>Games the adventure AI plays on every slot: on a built-in or file map, or on a map generated from a template.</summary>
internal static class SimGames
{
    /// <summary>A built-in map (<c>towns</c>, <c>demo</c>, <c>ember</c>) or a map file, as a game with <paramref name="seed"/>.</summary>
    public static SimGame HandMadeGame(SimContext sim, string map, ulong seed, Difficulty? difficulty = null, FogMode fog = FogMode.PermanentReveal)
    {
        var document = map switch
        {
            "towns" => MapConverter.ToDocument(TownsScenario.Create(sim.Content)),
            "demo" => MapConverter.ToDocument(DemoScenario.Create(content: sim.Content)),
            "ember" => MapConverter.ToDocument(EmberMarchScenario.Create(sim.Content)),
            _ => MapSerializer.Load(map),
        };
        return new SimGame(document, MapConverter.ToGameState(document, sim.Content, new GameOptions(FogMode: fog, Seed: seed, Difficulty: difficulty)));
    }

    /// <summary>
    /// A map generated from the template for computer players in the first <paramref name="players"/> start slots, each with a random town,
    /// assembled like the new-game screen does (<see cref="MapGenerator.Build"/>); the map seed is the game seed.
    /// </summary>
    public static SimGame GeneratedGame(SimContext sim, MapTemplate template, int size, int players, ulong seed, Difficulty? difficulty = null, FogMode fog = FogMode.PermanentReveal)
    {
        if (players < 1 || players > template.MaxPlayers)
        {
            throw new FormatException($"{template.Name} has {template.MaxPlayers} start slots, not room for {players} players.");
        }

        var setups = Enumerable.Range(0, players).Select(slot => new PlayerSetup(slot, $"Player {slot + 1}", IsHuman: false, PlayerSetup.RandomTown)).ToList();
        var (state, generation) = MapGenerator.Build(template, size, seed, setups, new GameOptions(FogMode: fog, Seed: seed, Difficulty: difficulty), sim.Content);
        return new SimGame(MapConverter.ToDocument(state), state, GenerationInfo.Of(template.Name, size, seed, generation.Attempt), generation.Problems);
    }

    /// <summary>
    /// The adventure AI (with <see cref="SimContext.Weights"/>) plays every slot until the game is over or <paramref name="days"/> have passed.
    /// Honours the debug variables <c>EW_SIM_TRACE</c>, <c>EW_SIM_EXPLAIN</c> and <c>EW_SIM_GUARDS</c>.
    /// </summary>
    public static (GameSession Session, AdventureAi Ai) PlayAiGame(SimContext sim, SimGame game, int days, FightLog? fights = null, TurnProfile? turns = null)
    {
        var session = new GameSession(game.State);
        var ai = new AdventureAi(session, controlHumanSlots: true, sim.Weights);
        fights?.Attach(session);
        turns?.Attach(session);
        if (Environment.GetEnvironmentVariable("EW_SIM_TRACE") is not null)
        {
            session.Events.Subscribe<HeroDefeated>(e => Console.WriteLine($"    day {session.State.Date.AbsoluteDay}: {e.Hero} of {e.Owner} defeated"));
            session.Events.Subscribe<BattleStarted>(e => Console.WriteLine($"    day {session.State.Date.AbsoluteDay}: battle {e.AttackerHero} vs {e.DefenderHero?.ToString() ?? e.Guard?.ToString()} units {string.Join(",", e.Units.Select(u => $"{u.Side}:{u.Count}x{u.Creature}"))}"));
            session.Events.Subscribe<AdventureSpellCast>(e => Console.WriteLine($"    day {session.State.Date.AbsoluteDay}: {e.Hero} casts {e.Spell}{(e.Target is { } at ? $" at {at}" : string.Empty)}"));
        }

        session.Start();
        var explain = Environment.GetEnvironmentVariable("EW_SIM_EXPLAIN")?.Split(',').Select(int.Parse).ToHashSet() ?? [];
        while (!session.State.IsOver && session.State.Date.AbsoluteDay <= days)
        {
            if (explain.Contains(session.State.Date.AbsoluteDay))
            {
                // EW_SIM_EXPLAIN=10,20: what each hero of the player about to move weighs at the start of those days' turns.
                Console.WriteLine($"  day {session.State.Date.AbsoluteDay}, {session.State.CurrentPlayer.Name}:");
                foreach (var hero in session.State.HeroesOf(session.State.CurrentPlayer.Id).ToList())
                {
                    ai.Explain(hero.Id).ToList().ForEach(line => Console.WriteLine($"    {line}"));
                }

                if (Environment.GetEnvironmentVariable("EW_SIM_GUARDS") is not null)
                {
                    foreach (var guard in session.State.ObjectsOf<MonsterGuard>())
                    {
                        Console.WriteLine($"    guard {guard.Creature} x{guard.Count} at {guard.Position}, explored {session.State.Fog.IsExplored(session.State.CurrentPlayer.Id, guard.VisitPosition)}");
                    }
                }
            }

            var turn = turns?.Begin(session.State);
            var played = ai.PlayTurn();
            turns?.End(turn!);
            if (!played)
            {
                throw new InvalidOperationException($"The AI got stuck on {session.State.Date} ({session.State.CurrentPlayer.Name}, decision {session.State.PendingDecision}).");
            }

            if (Environment.GetEnvironmentVariable("EW_SIM_TRACE") is not null && session.State.Date.AbsoluteDay <= days)
            {
                // Where every hero stands after each turn, with its army power and mana.
                Console.WriteLine($"    day {session.State.Date.AbsoluteDay} {session.State.CurrentPlayer.Name} to move; heroes: {string.Join(", ", session.State.Heroes.Select(h =>
                    $"{h.Name} ({h.Position.X},{h.Position.Y}) power {h.Army.Stacks.Sum(s => (long)s.Stack.Count * session.State.Creatures.Get(s.Stack.Creature).FightValue)} mana {h.Mana}"))}");
            }
        }

        return (session, ai);
    }
}
