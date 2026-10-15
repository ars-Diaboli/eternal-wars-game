using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Tools.Sim;

/// <summary>
/// Headless simulator (ADR-034): drives the core through commands only, exactly like a front-end or the AI.
/// <code>
/// sim battle haven.pikeman:20,haven.archer:10 inferno.hell-hound:12 [--runs 200] [--seed 1]
/// sim record demo.ewreplay.json [--days 7] [--seed 1]
/// sim replay demo.ewreplay.json
/// sim towns [--runs 20] [--upgraded]  (round robin of one-week town armies, for roster balance)
/// sim selftest                      (CI: determinism and replay round trip)
/// </code>
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return args switch
            {
                ["battle", var attacker, var defender, .. var rest] => Battle(ParseArmy(attacker), ParseArmy(defender), Option(rest, "--runs", 100), (ulong)Option(rest, "--seed", 1)),
                ["record", var path, .. var rest] => Record(path, Option(rest, "--days", 7), (ulong)Option(rest, "--seed", 1)),
                ["replay", var path] => Replay(path),
                ["towns", .. var rest] => Towns(Option(rest, "--runs", 20), rest.Contains("--upgraded")),
                ["selftest"] => SelfTest(),
                _ => Usage(),
            };
        }
        catch (Exception e) when (e is FormatException or InvalidOperationException or KeyNotFoundException or IOException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("usage: sim battle <army> <army> [--runs N] [--seed S] | record <file> [--days D] [--seed S] | replay <file> | towns [--runs N] [--upgraded] | selftest");
        Console.Error.WriteLine("       army = creature:count[,creature:count...], e.g. haven.pikeman:20,haven.archer:10");
        return 2;
    }

    /// <summary>Two computer heroes meet N times with different seeds; prints win rate and average losses.</summary>
    private static int Battle(Army attacker, Army defender, int runs, ulong seed)
    {
        var wins = 0;
        var rounds = 0L;
        var losses = new Dictionary<(BattleSide, string), long>();
        for (var run = 0; run < runs; run++)
        {
            var result = FightOnce(attacker, defender, seed + (ulong)run);
            wins += result.Ended.Winner == BattleSide.Attacker ? 1 : 0;
            rounds += result.Rounds;
            foreach (var casualty in result.Ended.Casualties)
            {
                losses[(casualty.Side, casualty.Creature)] = losses.GetValueOrDefault((casualty.Side, casualty.Creature)) + casualty.Lost;
            }
        }

        Console.WriteLine($"{runs} battles: attacker won {wins} ({100.0 * wins / runs:0.#} %), {(double)rounds / runs:0.#} rounds on average");
        foreach (var ((side, creature), lost) in losses.OrderBy(l => l.Key.Item1).ThenBy(l => l.Key.Item2))
        {
            Console.WriteLine($"  {side,-8} {creature,-28} lost {(double)lost / runs:0.##} per battle");
        }

        return 0;
    }

    private static (BattleEnded Ended, int Rounds) FightOnce(Army attacker, Army defender, ulong seed)
    {
        var map = new AdventureMap(3, 1);
        var players = new[] { new Player(new PlayerId(0), "Attacker", isHuman: false), new Player(new PlayerId(1), "Defender", isHuman: false) };
        var heroes = new[]
        {
            new Hero(new HeroId(1), "Attacker", players[0].Id, new MapPosition(0, 0), Copy(attacker)),
            new Hero(new HeroId(2), "Defender", players[1].Id, new MapPosition(1, 0), Copy(defender)),
        };
        var session = new GameSession(new GameState(map, players, heroes, options: new GameOptions(Seed: seed)));
        BattleEnded? ended = null;
        var rounds = 0;
        session.Events.Subscribe<BattleEnded>(e => ended = e);
        session.Events.Subscribe<BattleRoundStarted>(e => rounds = e.Round);
        session.Start();
        session.Execute(new StepHeroCommand(players[0].Id, heroes[0].Id, Direction.East)).ThrowIfRejected();
        return (ended ?? throw new InvalidOperationException("The battle did not finish."), rounds);

        static Army Copy(Army army) => new(army.Slots);
    }

    /// <summary>
    /// Every playable town against every other with a one-week army: both lines of tiers 1–2, the first line of tiers
    /// 3–4 and the tier-5 creature, one week of growth each. Each pairing fights from both sides. Prints win rates.
    /// </summary>
    private static int Towns(int runs, bool upgraded)
    {
        string[] towns = [TownIds.Haven, TownIds.Sylvan, TownIds.Academy, TownIds.Necropolis, TownIds.Inferno, TownIds.Dungeon, TownIds.Stronghold, TownIds.SwampBestiary];
        var catalog = CreatureCatalog.Default;
        var armies = towns.ToDictionary(t => t, t =>
        {
            var lines = catalog.BaseCreaturesOf(t).ToList();
            var picked = lines.Where(c => c.Tier <= 2)
                .Concat(lines.Where(c => c.Tier is 3 or 4).GroupBy(c => c.Tier).Select(g => g.First()))
                .Concat(lines.Where(c => c.Tier == 5));
            return Army.Of(picked.Select(c => new CreatureStack(upgraded ? c.UpgradesTo! : c.Id, c.WeeklyGrowth)).ToArray());
        });

        foreach (var (town, army) in armies)
        {
            var stacks = army.Stacks.Select(s => (Creature: catalog.Get(s.Stack.Creature), s.Stack.Count)).ToList();
            Console.WriteLine($"{town,-16} power {stacks.Sum(s => (long)s.Count * s.Creature.Value),7}  " +
                              string.Join("  ", stacks.Select(s => $"T{s.Creature.Tier} {s.Creature.Name} {s.Count}×{s.Creature.Value}")));
        }

        Console.WriteLine($"Win rate of the row town over the column town ({runs} battles per side, {(upgraded ? "upgraded" : "base")} one-week armies)");
        Console.WriteLine("".PadRight(16) + string.Concat(towns.Select(t => t[..Math.Min(7, t.Length)].PadLeft(8))) + "   total");
        foreach (var row in towns)
        {
            var wins = 0;
            var line = row.PadRight(16);
            foreach (var column in towns)
            {
                if (row == column)
                {
                    line += "-".PadLeft(8);
                    continue;
                }

                var won = 0;
                for (var run = 0; run < runs; run++)
                {
                    won += FightOnce(armies[row], armies[column], (ulong)run).Ended.Winner == BattleSide.Attacker ? 1 : 0;
                    won += FightOnce(armies[column], armies[row], (ulong)(run + 1000)).Ended.Winner == BattleSide.Defender ? 1 : 0;
                }

                wins += won;
                line += $"{100 * won / (2 * runs)}%".PadLeft(8);
            }

            Console.WriteLine(line + $"{100 * wins / (2 * runs * (towns.Length - 1))}%".PadLeft(8));
        }

        return 0;
    }

    /// <summary>Plays the demo scenario with scripted heroes and writes a replay file.</summary>
    private static int Record(string path, int days, ulong seed)
    {
        var (map, session) = PlayDemo(days, seed);
        var record = GameReplay.Record(map, session);
        GameRecordSerializer.Save(record, path);
        var battles = session.CommandLog.Count(c => c is AutoResolveBattleCommand);
        Console.WriteLine($"{session.CommandLog.Count} commands ({battles} battles) over {days} days → {path} (digest {record.FinalDigest})");
        return 0;
    }

    private static int Replay(string path)
    {
        var record = GameRecordSerializer.Load(path);
        var session = GameReplay.Replay(record);
        Console.WriteLine($"replayed {record.Commands.Count} commands: {session.State.Date}, digest {GameStateDigest.Compute(session.State)} (matches the record)");
        return 0;
    }

    /// <summary>CI check: the same seed gives the same game, and a recorded game replays through JSON to the same state.</summary>
    private static int SelfTest()
    {
        var (map, first) = PlayDemo(days: 10, seed: 99);
        var (_, second) = PlayDemo(days: 10, seed: 99);
        if (GameStateDigest.Compute(first.State) != GameStateDigest.Compute(second.State))
        {
            Console.Error.WriteLine("selftest: two runs with the same seed diverged");
            return 1;
        }

        var json = GameRecordSerializer.Serialize(GameReplay.Record(map, first));
        GameReplay.Replay(GameRecordSerializer.Deserialize(json));

        var army = ParseArmy($"{BuiltInCreatures.Pikeman}:20,{BuiltInCreatures.Archer}:10");
        var defender = ParseArmy($"{BuiltInCreatures.HellHound}:10");
        if (Describe(FightOnce(army, defender, 5)) != Describe(FightOnce(army, defender, 5)))
        {
            Console.Error.WriteLine("selftest: the same battle had two outcomes");
            return 1;
        }

        Console.WriteLine($"selftest ok: {first.CommandLog.Count} commands replayed, digest {GameStateDigest.Compute(first.State)}");
        return 0;

        static string Describe((BattleEnded Ended, int Rounds) result) =>
            $"{result.Ended.Winner} {result.Rounds} {string.Join(",", result.Ended.Casualties)}";
    }

    /// <summary>Every day each hero heads for the enemy's start; chests give gold, guards are fought, battles quick-resolved.</summary>
    private static (MapDocument Map, GameSession Session) PlayDemo(int days, ulong seed)
    {
        var map = MapConverter.ToDocument(DemoScenario.Create());
        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: seed)));
        var state = session.State;
        session.Start();
        for (var turn = 0; turn < days * state.Players.Count; turn++)
        {
            var player = state.CurrentPlayer.Id;
            var goal = player.Value == 0 ? new MapPosition(state.Map.Width - 3, state.Map.Height - 3) : new MapPosition(2, 2);
            foreach (var hero in state.HeroesOf(player).ToList())
            {
                if (state.Heroes.Contains(hero) && Waypoint(session, hero, goal) is { } waypoint)
                {
                    session.Execute(new MoveHeroCommand(player, hero.Id, waypoint));
                    Settle(session);
                }
            }

            session.Execute(new EndTurnCommand(state.CurrentPlayer.Id)).ThrowIfRejected();
        }

        return (map, session);
    }

    /// <summary>The explored tile closest to <paramref name="goal"/> that the hero has a route to (routes avoid unexplored tiles).</summary>
    private static MapPosition? Waypoint(GameSession session, Hero hero, MapPosition goal)
    {
        var state = session.State;
        return state.Map.Positions(hero.Position.Level)
            .Where(p => p != hero.Position && state.Fog.IsExplored(hero.Owner, p))
            .OrderBy(p => Math.Max(Math.Abs(p.X - goal.X), Math.Abs(p.Y - goal.Y)))
            .ThenBy(p => p.Y).ThenBy(p => p.X)
            .Take(40)
            .FirstOrDefault(p => session.Movement.FindPath(hero.Id, p) is not null) is { } found && found != default
            ? found
            : null;
    }

    private static void Settle(GameSession session)
    {
        var state = session.State;
        while (state.PendingDecision is { } decision)
        {
            var player = state.CurrentPlayer.Id;
            GameCommand command = decision switch
            {
                TreasureChestDecision => new ResolveTreasureChestCommand(player, TreasureChestReward.Gold),
                HeroExchangeDecision => new CompleteExchangeCommand(player),
                GuardEncounterDecision => new RespondToGuardCommand(player, Accept: false), // always fight: exercises battles
                _ => new AutoResolveBattleCommand(state.Battle!.Attacker.Owner ?? player),
            };
            session.Execute(command).ThrowIfRejected();
        }
    }

    private static Army ParseArmy(string spec)
    {
        var stacks = spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(part =>
        {
            var (creature, count) = part.Split(':') is [var c, var n] && int.TryParse(n, out var parsed)
                ? (c, parsed)
                : throw new FormatException($"'{part}' is not creature:count.");
            CreatureCatalog.Default.Get(creature);
            return new CreatureStack(creature, count);
        }).ToArray();
        return stacks.Length is > 0 and <= Army.SlotCount ? Army.Of(stacks) : throw new FormatException("An army has 1 to 7 stacks.");
    }

    private static int Option(string[] args, string name, int fallback)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var value) ? value : fallback;
    }
}
