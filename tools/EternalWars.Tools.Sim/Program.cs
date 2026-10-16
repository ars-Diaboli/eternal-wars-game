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
/// sim towns [--runs 32] [--upgraded]  (round robin of one-week town armies, for roster balance)
/// sim creatures [--write table.csv]  (per-line weekly power against the tier median; optionally rewrite the table)
/// sim lines [--runs 4] [--upgraded]  (every creature line against the other lines of its tier, one week each)
/// sim selftest                      (CI: determinism and replay round trip)
/// </code>
/// Any command takes <c>--creatures path/to/creatures.csv</c> to use an edited creature table without rebuilding.
/// </summary>
public static class Program
{
    private static readonly string[] PlayableTowns =
        [TownIds.Haven, TownIds.Sylvan, TownIds.Academy, TownIds.Necropolis, TownIds.Inferno, TownIds.Dungeon, TownIds.Stronghold, TownIds.SwampBestiary];

    /// <summary>The built-in creatures, or the table given with <c>--creatures</c>.</summary>
    private static CreatureCatalog Catalog { get; set; } = CreatureCatalog.Default;

    public static int Main(string[] args)
    {
        try
        {
            if (Array.IndexOf(args, "--creatures") is var index and >= 0 && index + 1 < args.Length)
            {
                Catalog = new CreatureCatalog(CreatureTable.Parse(File.ReadAllText(args[index + 1])));
                args = [.. args[..index], .. args[(index + 2)..]];
            }

            return args switch
            {
                ["battle", var attacker, var defender, .. var rest] => Battle(ParseArmy(attacker), ParseArmy(defender), Option(rest, "--runs", 100), (ulong)Option(rest, "--seed", 1)),
                ["record", var path, .. var rest] => Record(path, Option(rest, "--days", 7), (ulong)Option(rest, "--seed", 1)),
                ["replay", var path] => Replay(path),
                ["towns", .. var rest] => Towns(Option(rest, "--runs", 32), rest.Contains("--upgraded")),
                ["creatures", "--write", var path] => WriteTable(path),
                ["creatures"] => Creatures(),
                ["lines", .. var rest] => Lines(Option(rest, "--runs", 4), rest.Contains("--upgraded")),
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
        Console.Error.WriteLine("usage: sim battle <army> <army> [--runs N] [--seed S] | record <file> [--days D] [--seed S] | replay <file> | towns [--runs N] [--upgraded] | creatures [--write F] | lines [--runs N] [--upgraded] | selftest");
        Console.Error.WriteLine("       [--creatures table.csv] with any command uses that creature table");
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
        var session = new GameSession(new GameState(map, players, heroes, options: new GameOptions(Seed: seed), creatures: Catalog));
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
    /// Every playable town against every other with a one-week army of seven dwellings (ADR-030): both lines of tiers
    /// 1–2, one line each of tiers 3 and 4, and tier 5, one week of growth each. The four possible builds rotate between
    /// runs and each pairing fights from both sides. Prints army power and win rates.
    /// </summary>
    private static int Towns(int runs, bool upgraded)
    {
        var builds = PlayableTowns.ToDictionary(t => t, t => WeekArmies(t, upgraded));
        foreach (var (town, armies) in builds)
        {
            var powers = armies.Select(Power).ToList();
            Console.WriteLine($"{town,-15} power {powers.Min(),6}–{powers.Max(),-6} " +
                              string.Join("  ", Catalog.BaseCreaturesOf(town).Select(c => $"T{c.Tier} {c.Name} {c.WeeklyGrowth}×{Catalog.Get(upgraded ? c.UpgradesTo! : c.Id).Value}")));
        }

        Console.WriteLine($"Win rate of the row town over the column town ({runs} battles per side, {(upgraded ? "upgraded" : "base")} one-week armies)");
        Console.WriteLine("".PadRight(16) + string.Concat(PlayableTowns.Select(t => t[..Math.Min(7, t.Length)].PadLeft(8))) + "   total");
        foreach (var row in PlayableTowns)
        {
            var wins = 0;
            var line = row.PadRight(16);
            foreach (var column in PlayableTowns)
            {
                if (row == column)
                {
                    line += "-".PadLeft(8);
                    continue;
                }

                var won = 0;
                for (var run = 0; run < runs; run++)
                {
                    var (mine, theirs) = (builds[row][run % 4], builds[column][run / 4 % 4]);
                    won += FightOnce(mine, theirs, (ulong)run).Ended.Winner == BattleSide.Attacker ? 1 : 0;
                    won += FightOnce(theirs, mine, (ulong)(run + 1000)).Ended.Winner == BattleSide.Defender ? 1 : 0;
                }

                wins += won;
                line += $"{100 * won / (2 * runs)}%".PadLeft(8);
            }

            Console.WriteLine(line + $"{100 * wins / (2 * runs * (PlayableTowns.Length - 1))}%".PadLeft(8));
        }

        return 0;

        static long Power(Army army) => army.Stacks.Sum(s => (long)s.Stack.Count * Catalog.Get(s.Stack.Creature).Value);
    }

    /// <summary>
    /// Every creature line of the playable towns against every other line of its tier: one week of growth each,
    /// both sides, <paramref name="runs"/> seeds. Prints each line's win rate, weakest first, per tier.
    /// </summary>
    private static int Lines(int runs, bool upgraded)
    {
        var lines = PlayableTowns.SelectMany(Catalog.BaseCreaturesOf).ToList();
        foreach (var tier in lines.Select(c => c.Tier).Distinct().Order())
        {
            var group = lines.Where(c => c.Tier == tier).ToList();
            var wins = group.ToDictionary(c => c.Id, _ => 0);
            foreach (var (a, i) in group.Select((c, i) => (c, i)))
            {
                foreach (var b in group.Skip(i + 1))
                {
                    var (armyA, armyB) = (Week(a), Week(b));
                    for (var run = 0; run < runs; run++)
                    {
                        var first = FightOnce(armyA, armyB, (ulong)run).Ended.Winner == BattleSide.Attacker;
                        var second = FightOnce(armyB, armyA, (ulong)(run + 1000)).Ended.Winner == BattleSide.Defender;
                        wins[a.Id] += (first ? 1 : 0) + (second ? 1 : 0);
                        wins[b.Id] += (first ? 0 : 1) + (second ? 0 : 1);
                    }
                }
            }

            Console.WriteLine($"Tier {tier} ({(upgraded ? "upgraded" : "base")}, one week of growth, {runs} battles per side):");
            foreach (var (id, won) in wins.OrderBy(w => w.Value))
            {
                Console.WriteLine($"  {id,-30} {100.0 * won / (2 * runs * (group.Count - 1)),5:0}%");
            }
        }

        return 0;

        Army Week(CreatureDefinition line) => Army.Of(new CreatureStack(upgraded ? line.UpgradesTo! : line.Id, line.WeeklyGrowth));
    }

    /// <summary>The four seven-dwelling builds of a town: either line of tier 3 × either line of tier 4.</summary>
    private static List<Army> WeekArmies(string town, bool upgraded)
    {
        var lines = Catalog.BaseCreaturesOf(town).ToList();
        return
        [
            .. from third in lines.Where(c => c.Tier == 3)
               from fourth in lines.Where(c => c.Tier == 4)
               select Army.Of(lines.Where(c => c.Tier is 1 or 2 or 5).Append(third).Append(fourth).OrderBy(c => c.Tier)
                   .Select(c => new CreatureStack(upgraded ? c.UpgradesTo! : c.Id, c.WeeklyGrowth)).ToArray()),
        ];
    }

    /// <summary>
    /// Roster overview for balancing: per creature line its value (<see cref="CreatureDefinition.Value"/>), weekly
    /// growth, weekly power (growth × value) relative to the median line of that tier in the playable towns, the upgrade's
    /// value relative to the base, and gold per point of value.
    /// </summary>
    private static int Creatures()
    {
        var upgrades = Catalog.All.Where(c => c.UpgradesTo is not null).Select(c => c.UpgradesTo!).ToHashSet();
        var bases = Catalog.All.Where(c => !upgrades.Contains(c.Id)).ToList();
        var medians = Enumerable.Range(CreatureDefinition.MinTier, CreatureDefinition.MaxTier).ToDictionary(t => t, t =>
        {
            var powers = bases.Where(c => c.Tier == t && PlayableTowns.Contains(c.Town)).Select(Week).Order().ToList();
            return powers.Count == 0 ? 1 : powers[powers.Count / 2];
        });

        Console.WriteLine($"{"town",-15} T {"creature",-18} {"value",6} {"grow",4} {"week",7} {"×med",5}  {"upgrade",-18} {"value",6} {"×base",5} {"gold/val",8}");
        foreach (var creature in bases.OrderBy(c => c.Town is { } t && PlayableTowns.Contains(t) ? Array.IndexOf(PlayableTowns, t) : PlayableTowns.Length)
                     .ThenBy(c => c.Town).ThenBy(c => c.Tier).ThenBy(c => c.Name))
        {
            var upgrade = creature.UpgradesTo is { } id ? Catalog.Get(id) : null;
            Console.WriteLine($"{creature.Town ?? "neutral",-15} {creature.Tier} {creature.Name,-18} {creature.Value,6} {creature.WeeklyGrowth,4} {Week(creature),7:0} " +
                              $"{Week(creature) / medians[creature.Tier],5:0.00}  {upgrade?.Name ?? "",-18} {upgrade?.Value.ToString() ?? "",6} " +
                              $"{(upgrade is null ? "" : $"{(double)upgrade.Value / creature.Value:0.00}"),5} {(double)creature.Cost.Gold / creature.Value,8:0.00}");
        }

        Console.WriteLine();
        Console.WriteLine("One-week army power (mean of the four seven-dwelling builds):");
        foreach (var town in PlayableTowns)
        {
            Console.WriteLine($"  {town,-15} {WeekArmies(town, upgraded: false).Average(a => a.Stacks.Sum(s => (double)s.Stack.Count * Catalog.Get(s.Stack.Creature).Value)),8:0}");
        }

        return 0;

        static double Week(CreatureDefinition creature) => (double)creature.WeeklyGrowth * creature.Value;
    }

    /// <summary>Writes the catalog (built-in or <c>--creatures</c>) as a creature table; the notes column stays empty.</summary>
    private static int WriteTable(string path)
    {
        File.WriteAllText(path, CreatureTable.Format(Catalog.All));
        Console.WriteLine($"{Catalog.All.Count()} creatures → {path}");
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
        var session = GameReplay.Replay(record, creatures: Catalog);
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
        GameReplay.Replay(GameRecordSerializer.Deserialize(json), creatures: Catalog);

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
        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: seed), creatures: Catalog));
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
                LevelUpDecision levelUp => new ChooseSkillCommand(player, levelUp.Options[0]),
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
            Catalog.Get(creature);
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
