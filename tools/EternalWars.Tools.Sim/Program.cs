using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Generation;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Editing;
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
/// sim game [--map towns|demo|file.ewmap.json] [--days 40] [--seed 1] [--runs 1] [--record file]  (the adventure AI plays every slot)
/// sim game --template duel|file.ewtemplate.json [--size N] [--players N] [...]  (the same on a map generated per run, random towns)
/// sim maps [--template name] [--maps 3] [--days 14] [--seed 1]  (CI: generates maps of every shipped template and checks they are playable)
/// sim towns [--runs 32] [--upgraded]  (round robin of one-week town armies, for roster balance)
/// sim creatures [--write table.csv]  (per-line weekly power against the tier median; optionally rewrite the table)
/// sim lines [--runs 4] [--upgraded]  (every creature line against the other lines of its tier, one week each)
/// sim layout duel|file.ewtemplate.json [--size 96] [--seed 1] [--rects] [--terrain] [--seeds N]  (zone layout, or with --terrain the painted terrain with walls, of a random map template as text)
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
                ["game", .. var rest] => Game(GameMaker(rest), Option(rest, "--days", 40), (ulong)Option(rest, "--seed", 1), Option(rest, "--runs", 1), OptionText(rest, "--record")),
                ["maps", .. var rest] => Maps(OptionText(rest, "--template"), Option(rest, "--maps", 3), Option(rest, "--days", 14), (ulong)Option(rest, "--seed", 1)),
                ["towns", .. var rest] => Towns(Option(rest, "--runs", 32), rest.Contains("--upgraded")),
                ["creatures", "--write", var path] => WriteTable(path),
                ["creatures"] => Creatures(),
                ["lines", .. var rest] => Lines(Option(rest, "--runs", 4), rest.Contains("--upgraded")),
                ["layout", var template, .. var rest] => Option(rest, "--seeds", 0) is > 0 and var many
                    ? LayoutBatch(template, Option(rest, "--size", 96), (ulong)Option(rest, "--seed", 1), many, rest.Contains("--rects"))
                    : Layout(template, Option(rest, "--size", 96), (ulong)Option(rest, "--seed", 1), rest.Contains("--rects"), rest.Contains("--terrain")),
                ["selftest"] => SelfTest(),
                _ => Usage(),
            };
        }
        catch (Exception e) when (e is FormatException or InvalidOperationException or ArgumentException or KeyNotFoundException or IOException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }

    /// <summary>Prints the zone layout of a template (a built-in name or a file): one character per tile, one letter per zone.</summary>
    private static int Layout(string template, int size, ulong seed, bool rectangles, bool terrain)
    {
        var loaded = LoadTemplate(template);
        var strategy = rectangles ? LayoutStrategy.Rectangles : LayoutStrategy.Voronoi;
        var built = terrain ? MapGenerator.Generate(loaded, size, seed, strategy, Catalog) : null;
        var layout = built?.Layout ?? ZoneLayouter.Layout(loaded, size, seed, strategy);

        for (var level = 0; level < layout.Levels; level++)
        {
            Console.WriteLine($"level {level}:");
            if (built is null)
            {
                Console.WriteLine(layout.ToAscii(level));
                continue;
            }

            var marks = built.Objects.SelectMany(o => o.OccupiedCells()).Where(c => c.Position.Level == level).ToDictionary(c => c.Position, c => c);
            var kinds = built.Objects.ToDictionary(o => o.VisitPosition, o => o switch
            {
                MonsterGuard { Mode: GuardMode.Area } => 'A', MonsterGuard => 'M', Town => 'T', Mine => 'm', ResourcePile => 'p', TreasureChest => 'c', ArtifactPickup => 'a', ResourceProvider => 'r', LootSite => 'l', BuffSite => 'b', TrainingSite => 't', Scenery => '#', _ => 'G',
            });
            Console.WriteLine(string.Join(System.Environment.NewLine, Enumerable.Range(0, size).Select(y => string.Concat(Enumerable.Range(0, size).Select(x =>
            {
                var p = new MapPosition(x, y, level);
                return kinds.TryGetValue(p, out var mark) ? mark : marks.ContainsKey(p) ? 'g' : built.Map.GetRoad(p) != RoadType.None ? '+' : built.Map.GetRiver(p) != RiverType.None ? '%' : TerrainCodes.ToCode(built.Map.GetTerrain(p));
            })))));
        }

        if (built is not null)
        {
            foreach (var monster in built.Objects.OfType<MonsterGuard>())
            {
                Console.WriteLine($"  guard at {monster.Position}: {monster.Count} {monster.Creature} ({monster.Count * Catalog.Get(monster.Creature).Value} power)");
            }

            foreach (var mine in built.Objects.OfType<Mine>())
            {
                Console.WriteLine($"  {mine.Definition.Name} at {mine.Position}{(mine.Guard is { } g ? $" guarded by {g.Count} {g.Creature}" : "")}");
            }

            foreach (var item in built.Objects.Where(o => o is TreasureChest or ArtifactPickup or ResourcePile))
            {
                Console.WriteLine($"  {item switch { TreasureChest c => $"chest {c.FixedValues?.Gold} gold", ArtifactPickup a => $"artifact {a.Artifact}", ResourcePile p => $"{p.Amount} {p.Resource}", _ => "" }} at {item.Position}");
            }

            foreach (var town in built.Towns)
            {
                Console.WriteLine($"  {town.Town.TownType} town at {town.Town.Position}, {(town.Slot is { } slot ? $"player slot {slot}" : $"neutral, garrison {string.Join(", ", town.Town.Garrison.Stacks.Select(t => $"{t.Stack.Count} {t.Stack.Creature}"))}")}");
            }

            built.Problems.ToList().ForEach(p => Console.WriteLine($"  problem: {p}"));
        }

        for (var i = 0; i < layout.ZoneIds.Count; i++)
        {
            Console.WriteLine($"  {layout.ZoneIds[i],-12} area {layout.Area(i)}");
        }

        foreach (var (from, to) in layout.UnmetConnections)
        {
            Console.WriteLine($"  not touching: {from} - {to}");
        }

        return layout.UnmetConnections.Count == 0 ? 0 : 1;
    }

    /// <summary>Generates a template for N seeds in a row (layout, terrain, connections) and reports the ones with problems, and the time.</summary>
    private static int LayoutBatch(string template, int size, ulong first, int count, bool rectangles)
    {
        var loaded = LoadTemplate(template);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var bad = 0;
        for (var i = 0; i < count; i++)
        {
            var seed = first + (ulong)i;
            var built = MapGenerator.Generate(loaded, size, seed, rectangles ? LayoutStrategy.Rectangles : LayoutStrategy.Voronoi, Catalog);
            var problems = built.Problems.ToList();
            if (problems.Count > 0)
            {
                bad++;
                Console.WriteLine($"seed {seed}: {string.Join("; ", problems)}");
            }
        }

        Console.WriteLine($"{count} maps of {loaded.Name} at {size}: {bad} with problems, {clock.ElapsedMilliseconds / count} ms each");
        return bad == 0 ? 0 : 1;
    }

    private static MapTemplate LoadTemplate(string template) =>
        File.Exists(template) ? TemplateSerializer.Load(template) : BuiltInTemplates.Find(template)
            ?? throw new FormatException($"No template '{template}' (built-in: {string.Join(", ", BuiltInTemplates.Keys)}).");

    private static int Usage()
    {
        Console.Error.WriteLine("usage: sim battle <army> <army> [--runs N] [--seed S] | record <file> [--days D] [--seed S] | replay <file> | game [--map towns|demo|file | --template T [--size N] [--players N]] [--days D] [--seed S] [--runs N] [--record F] | maps [--template T] [--maps N] [--days D] [--seed S] | towns [--runs N] [--upgraded] | creatures [--write F] | lines [--runs N] [--upgraded] | layout <template> [--size N] [--seed S] [--rects] [--terrain] [--seeds N] | selftest");
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

    /// <summary>The adventure AI plays every slot of a map to the end or the day limit; prints how each game went.</summary>
    private static int Game(Func<ulong, SimGame> make, int days, ulong seed, int runs, string? recordPath)
    {
        var wins = new Dictionary<string, int>();
        var refusedTotal = 0;
        for (var run = 0; run < runs; run++)
        {
            var game = make(seed + (ulong)run);
            if (game.Generated is { } generated)
            {
                Console.WriteLine($"map {game.State.Map.Name}: attempt {generated.Attempt}{string.Concat(game.Problems.Select(p => $"; problem: {p}"))}");
            }

            var (session, ai) = PlayAiGame(game, days);
            var state = session.State;
            var winner = state.Outcome is { HasWinner: true } outcome ? string.Join("+", outcome.Winners.Select(w => state.GetPlayer(w).Name)) : (state.IsOver ? "nobody" : "unfinished");
            wins[winner] = wins.GetValueOrDefault(winner) + 1;
            refusedTotal += ai.Refused;
            Console.WriteLine($"run {run + 1}: {state.Date} (day {state.Date.AbsoluteDay}), {winner}{(state.Outcome is { } o ? ": " + o.Reason : string.Empty)}");
            Console.WriteLine($"  {session.CommandLog.Count} commands, AI refused {ai.Refused}, digest {GameStateDigest.Compute(state)}");
            foreach (var player in state.Players)
            {
                var towns = state.ObjectsOf<Town>().Count(t => t.Owner == player.Id);
                var heroes = state.HeroesOf(player.Id).ToList();
                var power = heroes.Sum(h => h.Army.Stacks.Sum(s => (long)s.Stack.Count * state.Creatures.Get(s.Stack.Creature).Value));
                Console.WriteLine($"  {player.Name,-8} {(player.IsDefeated ? "defeated" : "playing "),-8} towns {towns}, heroes {heroes.Count} (levels {string.Join(",", heroes.Select(h => h.Level))}, at {string.Join(" ", heroes.Select(h => $"({h.Position.X},{h.Position.Y})"))}), army power {power}, {player.Resources}");
            }

            if (Environment.GetEnvironmentVariable("EW_SIM_TRACE") is not null)
            {
                foreach (var command in session.CommandLog.Take(0))
                {
                    Console.WriteLine($"    {command}");
                }
            }

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

    /// <summary>
    /// A game ready to start: its state, the map document it starts from (for records) and, on a generated map, how it was generated and the
    /// problems of the map kept (<see cref="MapGenerator.GenerateBest"/>).
    /// </summary>
    private sealed record SimGame(MapDocument Map, GameState State, GenerationInfo? Generated = null, IReadOnlyList<string>? GenerationProblems = null)
    {
        public IReadOnlyList<string> Problems => GenerationProblems ?? [];
    }

    /// <summary>The games of <c>sim game</c>: a map generated from <c>--template</c> per seed, else the <c>--map</c> (default <c>towns</c>).</summary>
    private static Func<ulong, SimGame> GameMaker(string[] args)
    {
        if (OptionText(args, "--template") is not { } name)
        {
            var map = OptionText(args, "--map") ?? "towns";
            return seed => HandMadeGame(map, seed);
        }

        var template = LoadTemplate(name);
        var size = Option(args, "--size", template.Sizes[0]);
        var players = Option(args, "--players", template.MaxPlayers);
        return seed => GeneratedGame(template, size, players, seed);
    }

    private static SimGame HandMadeGame(string map, ulong seed)
    {
        var document = map switch
        {
            "towns" => MapConverter.ToDocument(TownsScenario.Create(Catalog)),
            "demo" => MapConverter.ToDocument(DemoScenario.Create()),
            _ => MapSerializer.Load(map),
        };
        return new SimGame(document, MapConverter.ToGameState(document, options: new GameOptions(Seed: seed), creatures: Catalog));
    }

    /// <summary>
    /// A map generated from the template for computer players in the first <paramref name="players"/> start slots, each with a random town,
    /// assembled like the new-game screen does (<see cref="MapGenerator.Build"/>); the map seed is the game seed.
    /// </summary>
    private static SimGame GeneratedGame(MapTemplate template, int size, int players, ulong seed)
    {
        if (players < 1 || players > template.MaxPlayers)
        {
            throw new FormatException($"{template.Name} has {template.MaxPlayers} start slots, not room for {players} players.");
        }

        var setups = Enumerable.Range(0, players).Select(slot => new PlayerSetup(slot, $"Player {slot + 1}", IsHuman: false, PlayerSetup.RandomTown)).ToList();
        var (state, generation) = MapGenerator.Build(template, size, seed, setups, new GameOptions(Seed: seed), Catalog);
        return new SimGame(MapConverter.ToDocument(state), state, GenerationInfo.Of(template.Name, size, seed, generation.Attempt), generation.Problems);
    }

    private static (GameSession Session, AdventureAi Ai) PlayAiGame(SimGame game, int days)
    {
        var session = new GameSession(game.State);
        var ai = new AdventureAi(session, controlHumanSlots: true);
        if (Environment.GetEnvironmentVariable("EW_SIM_TRACE") is not null)
        {
            session.Events.Subscribe<HeroDefeated>(e => Console.WriteLine($"    day {session.State.Date.AbsoluteDay}: {e.Hero} of {e.Owner} defeated"));
            session.Events.Subscribe<BattleStarted>(e => Console.WriteLine($"    day {session.State.Date.AbsoluteDay}: battle {e.AttackerHero} vs {e.DefenderHero?.ToString() ?? e.Guard?.ToString()} units {string.Join(",", e.Units.Select(u => $"{u.Side}:{u.Count}x{u.Creature}"))}"));
        }

        session.Start();
        while (!session.State.IsOver && session.State.Date.AbsoluteDay <= days)
        {
            if (!ai.PlayTurn())
            {
                throw new InvalidOperationException($"The AI got stuck on {session.State.Date} ({session.State.CurrentPlayer.Name}, decision {session.State.PendingDecision}).");
            }
        }

        return (session, ai);
    }

    /// <summary>
    /// CI check of the shipped templates (or one): <paramref name="count"/> maps each, cycling through the template's sizes and player counts
    /// (from the most players down, so empty slots come up too). Each map must generate without problems and the same way twice, let every
    /// start town reach every other one, and survive <paramref name="days"/> days of AI-vs-AI play with no refused command and a replay of
    /// the record through JSON to the same state.
    /// </summary>
    private static int Maps(string? only, int count, int days, ulong first)
    {
        var templates = only is null ? BuiltInTemplates.All : [LoadTemplate(only)];
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
                    var errors = CheckMap(template, size, players, seed, days, out var summary);
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

    private static List<string> CheckMap(MapTemplate template, int size, int players, ulong seed, int days, out string summary)
    {
        var errors = new List<string>();
        var game = GeneratedGame(template, size, players, seed);
        errors.AddRange(game.Problems.Select(p => $"generator: {p}"));
        if (GameStateDigest.Compute(game.State) != GameStateDigest.Compute(GeneratedGame(template, size, players, seed).State))
        {
            errors.Add("the same seed generated two different games");
        }

        errors.AddRange(Unreachable(game.State));
        errors.AddRange(MapValidator.Validate(game.State).Where(i => i.Severity == IssueSeverity.Error).Select(i => $"map validator: {i.Message}"));

        var (session, ai) = PlayAiGame(game, days);
        if (ai.Refused > 0)
        {
            errors.Add($"the AI issued {ai.Refused} refused commands");
        }

        var record = GameRecordSerializer.Serialize(GameReplay.Record(game.Map, session, generated: game.Generated));
        GameReplay.Replay(GameRecordSerializer.Deserialize(record), creatures: Catalog);

        var state = session.State;
        var outcome = state.Outcome is { HasWinner: true } o ? string.Join("+", o.Winners.Select(w => state.GetPlayer(w).Name)) + " won" : state.IsOver ? "nobody won" : "unfinished";
        summary = $"attempt {game.Generated!.Attempt}, day {state.Date.AbsoluteDay} {outcome}, {session.CommandLog.Count} commands";
        return errors;
    }

    /// <summary>
    /// The players' start towns another player cannot walk to from its hero: passable terrain, around blocked object cells, through guards
    /// (they can be beaten) and pickups (they go away), via gates; any other object entrance ends a route.
    /// </summary>
    private static IEnumerable<string> Unreachable(GameState state)
    {
        foreach (var hero in state.Heroes.GroupBy(h => h.Owner).Select(g => g.First()))
        {
            var reached = new HashSet<MapPosition> { hero.Position };
            var open = new Queue<MapPosition>([hero.Position]);
            while (open.TryDequeue(out var position))
            {
                if (position != hero.Position && state.ObjectCellAt(position) is { Cell: FootprintCell.Visit, Object: var entered })
                {
                    if (entered.TeleportTarget is not null && state.TeleportDestination(entered) is { } exit && reached.Add(exit))
                    {
                        open.Enqueue(exit);
                    }

                    if (entered is not (MonsterGuard or ResourcePile or TreasureChest or ArtifactPickup) && entered.EndsRoute)
                    {
                        continue;
                    }
                }

                foreach (var next in DirectionExtensions.All.Select(position.Step))
                {
                    if (state.Map.Contains(next) && state.Terrains.IsPassable(state.Map.GetTerrain(next))
                        && state.ObjectCellAt(next) is not { Cell: FootprintCell.Blocked } && reached.Add(next))
                    {
                        open.Enqueue(next);
                    }
                }
            }

            foreach (var town in state.ObjectsOf<Town>().Where(t => t.Owner is { } owner && owner != hero.Owner && !reached.Contains(t.VisitPosition)))
            {
                yield return $"{state.GetPlayer(hero.Owner).Name} cannot reach the town of {state.GetPlayer(town.Owner!.Value).Name} at {town.VisitPosition}";
            }
        }
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

        var aiGame = HandMadeGame("towns", seed: 7);
        var (aiFirst, firstAi) = PlayAiGame(aiGame, days: 15);
        var (aiSecond, _) = PlayAiGame(HandMadeGame("towns", seed: 7), days: 15);
        if (firstAi.Refused != 0 || GameStateDigest.Compute(aiFirst.State) != GameStateDigest.Compute(aiSecond.State))
        {
            Console.Error.WriteLine($"selftest: the AI game diverged or issued {firstAi.Refused} refused commands");
            return 1;
        }

        GameReplay.Replay(GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(GameReplay.Record(aiGame.Map, aiFirst))), creatures: Catalog);

        var army = ParseArmy($"{BuiltInCreatures.Pikeman}:20,{BuiltInCreatures.Archer}:10");
        var defender = ParseArmy($"{BuiltInCreatures.HellHound}:10");
        if (Describe(FightOnce(army, defender, 5)) != Describe(FightOnce(army, defender, 5)))
        {
            Console.Error.WriteLine("selftest: the same battle had two outcomes");
            return 1;
        }

        Console.WriteLine($"selftest ok: {first.CommandLog.Count} commands replayed, digest {GameStateDigest.Compute(first.State)}; AI game {aiFirst.CommandLog.Count} commands");
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
                TownGateDecision => new ChooseTownGateCommand(player, null),
                CartographerDecision => new AnswerCartographerCommand(player, Buy: false),
                SphinxDecision => new AnswerSphinxCommand(player, 0),
                WitchHutDecision hut => new ChooseWitchHutSkillCommand(player, hut.Options[0]),
                TrainingDecision training => new ChooseTrainingCommand(player, training.CanDecline ? null : training.Options[0]),
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

    private static string? OptionText(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int Option(string[] args, string name, int fallback)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var value) ? value : fallback;
    }
}
