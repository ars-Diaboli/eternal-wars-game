using System.Globalization;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Visibility;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Game;

/// <summary>Who plays the players a play-tester does not play (EDIT-5, ADR-267).</summary>
public enum PlayTestOthers
{
    /// <summary>The computer, as in any game.</summary>
    Ai,

    /// <summary>Nobody: each of their turns ends at once (<see cref="GameDirector"/>), so the play-tester has the map to themselves.</summary>
    Off,
}

/// <summary>
/// A play-test's rules (EDIT-5, ADR-267), part of <see cref="GameOptions.PlayTest"/>, so a save or replay of the game keeps them: the others
/// idle, the map revealed to the play-tester, the tile their first hero starts on, the starting day, and the cheat commands allowed. A
/// play-tested game is never ranked (<see cref="ScoreRules.IsRanked"/>).
/// </summary>
public sealed record PlayTestRules
{
    /// <summary>The players nobody plays end their turns at once.</summary>
    public bool OthersIdle { get; init; }

    /// <summary>The whole map is revealed to the people playing when the game starts.</summary>
    public bool Reveal { get; init; }

    /// <summary>
    /// Where the first person's first hero starts (after the hero placeholders are drawn); null leaves it. A hero of that person already on the
    /// tile stays and starts there.
    /// </summary>
    public MapPosition? Start { get; init; }

    /// <summary>The day the game starts on (1: the first day of the first week).</summary>
    public int Day { get; init; } = 1;

    /// <summary>The cheat commands (<see cref="CheatResourcesCommand"/>, …) are allowed.</summary>
    public bool Cheats { get; init; }
}

/// <summary>
/// The game app's play-test launch arguments (EDIT-5, ADR-267), which the editor's Test passes: <c>--player=N</c> (the player number the
/// play-tester plays, 1 for the first; it makes the run a play-test), <c>--others=ai|off</c>, <c>--reveal</c>, <c>--start=x,y,level</c>,
/// <c>--day=N</c>, <c>--cheats</c>. The map itself is <c>--map=</c>.
/// </summary>
public sealed record PlayTestArguments(int Player, PlayTestOthers Others = PlayTestOthers.Ai, bool Reveal = false, MapPosition? Start = null, int Day = 1, bool Cheats = false)
{
    /// <summary>The latest day a play-test may start on.</summary>
    public const int MaxDay = 9999;

    /// <summary>
    /// The play-test in <paramref name="arguments"/> (the user arguments, after <c>--</c>): null with no error when there is no <c>--player=</c>
    /// (not a play-test); null with the reason when one of them cannot be read. Unknown arguments are left to others.
    /// </summary>
    public static (PlayTestArguments? Arguments, string? Error) Parse(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var list = arguments.ToList();
        string? Value(string name) => list.LastOrDefault(a => a.StartsWith($"--{name}=", StringComparison.Ordinal))?[(name.Length + 3)..];
        bool Flag(string name) => list.Contains($"--{name}");

        if (Value("player") is not { } player)
        {
            return (null, null);
        }

        if (!int.TryParse(player, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
        {
            return (null, $"--player={player}: a player number, 1 for the first.");
        }

        var others = PlayTestOthers.Ai;
        if (Value("others") is { } othersText)
        {
            switch (othersText)
            {
                case "ai":
                    break;
                case "off":
                    others = PlayTestOthers.Off;
                    break;
                default:
                    return (null, $"--others={othersText}: ai or off.");
            }
        }

        MapPosition? start = null;
        if (Value("start") is { } startText)
        {
            var parts = startText.Split(',');
            if (parts.Length is not (2 or 3) || !parts.All(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
            {
                return (null, $"--start={startText}: x,y or x,y,level.");
            }

            var numbers = parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToList();
            start = new MapPosition(numbers[0], numbers[1], numbers.Count == 3 ? numbers[2] : 0);
        }

        var day = 1;
        if (Value("day") is { } dayText && (!int.TryParse(dayText, NumberStyles.None, CultureInfo.InvariantCulture, out day) || day is < 1 or > MaxDay))
        {
            return (null, $"--day={dayText}: a day from 1 to {MaxDay}.");
        }

        return (new PlayTestArguments(number, others, Flag("reveal"), start, day, Flag("cheats")), null);
    }

    /// <summary>The arguments that <see cref="Parse"/> reads back as these, the defaults left out (the map's <c>--map=</c> is not among them).</summary>
    public IReadOnlyList<string> ToArguments()
    {
        var arguments = new List<string> { $"--player={Player.ToString(CultureInfo.InvariantCulture)}" };
        if (Others == PlayTestOthers.Off)
        {
            arguments.Add("--others=off");
        }

        if (Reveal)
        {
            arguments.Add("--reveal");
        }

        if (Start is { } start)
        {
            arguments.Add(string.Create(CultureInfo.InvariantCulture, $"--start={start.X},{start.Y},{start.Level}"));
        }

        if (Day != 1)
        {
            arguments.Add($"--day={Day.ToString(CultureInfo.InvariantCulture)}");
        }

        if (Cheats)
        {
            arguments.Add("--cheats");
        }

        return arguments;
    }

    /// <summary>The play-tester's player.</summary>
    public PlayerId PlayerId => new(Player - 1);

    /// <summary>The rules the game keeps (<see cref="GameOptions.PlayTest"/>).</summary>
    public PlayTestRules Rules => new() { OthersIdle = Others == PlayTestOthers.Off, Reveal = Reveal, Start = Start, Day = Day, Cheats = Cheats };

    /// <summary>
    /// Readies <paramref name="state"/> (a map loaded with <see cref="Rules"/> in its options, not started) for the play-test: the play-tester's
    /// player is played by a person, every other by the computer. Throws <see cref="InvalidOperationException"/> with the reason
    /// <see cref="WhyNot"/> gives.
    /// </summary>
    public void Prepare(GameState state)
    {
        if (WhyNot(state) is { } why)
        {
            throw new InvalidOperationException(why);
        }

        state.AssignControllers(state.Players.ToDictionary(p => p.Id, p => p.Id == PlayerId));
    }

    /// <summary>
    /// Why <paramref name="state"/> cannot be play-tested so (the editor's Test window asks before it starts the game); null when it can: the
    /// map has no such player, the start tile is off the map, not walkable or taken (by anything but a hero of the play-tester), or an arena
    /// map would not start on day 1 (its battle is on day 2), or a tournament could not start (<see cref="TournamentRules.WhyNot"/>).
    /// </summary>
    public string? WhyNot(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.HasPlayer(PlayerId))
        {
            return $"The map has no player {Player}: it has {state.Players.Count}.";
        }

        if (Start is { } start && PlayTestService.WhyNotStart(state, PlayerId, start) is { } why)
        {
            return $"The start {start} cannot be used: {why}.";
        }

        if (TournamentRules.WhyNot(state) is { } notATournament)
        {
            return notATournament;
        }

        if (state.Rules.Arena && Day != 1)
        {
            return "An arena map starts on day 1: its battle is on day 2.";
        }

        return state.Rules.Tournament && Day >= TournamentRules.BattleDay(state.Options)
            ? $"A tournament's battle is on day {TournamentRules.BattleDay(state.Options)}: the play-test starts before it."
            : null;
    }
}

/// <summary>A play-test's rules in the running game (ADR-267): the start set up as <see cref="GameOptions.PlayTest"/> says, and the cheats.</summary>
internal sealed class PlayTestService(GameState state, IEventPublisher events, VisibilityService visibility, EconomyService economy, ProgressionService progression)
{
    /// <summary>The most levels and creatures one cheat gives.</summary>
    internal const int MaxLevels = 10;

    internal const int MaxCreatures = 9999;

    private PlayTestRules? Rules => state.Options.PlayTest;

    /// <summary>Why a hero of <paramref name="player"/> cannot start on <paramref name="tile"/>; null when it can.</summary>
    internal static string? WhyNotStart(GameState state, PlayerId player, MapPosition tile)
    {
        if (!state.Map.Contains(tile))
        {
            return "it is not on the map";
        }

        if (state.Heroes.FirstOrDefault(h => h.Position == tile) is { } hero)
        {
            return hero.Owner == player ? null : "another player's hero stands there";
        }

        if (!state.Terrains.IsPassable(state.Map.GetTerrain(tile)))
        {
            return "a hero cannot stand on its terrain";
        }

        return state.ObjectCellAt(tile) is not null ? "an object stands there" : null;
    }

    /// <summary>Before the first turn: the starting day, and the first person's first hero on the start tile (unless one of theirs is there).</summary>
    internal void PrepareStart()
    {
        if (Rules is not { } rules)
        {
            return;
        }

        state.Date = new GameDate(Math.Clamp(rules.Day, 1, PlayTestArguments.MaxDay));
        if (rules.Start is not { } tile || state.Players.FirstOrDefault(p => p.IsHuman) is not { } person
            || WhyNotStart(state, person.Id, tile) is not null || state.Heroes.Any(h => h.Position == tile))
        {
            return;
        }

        if (state.HeroesOf(person.Id).FirstOrDefault() is { } hero)
        {
            hero.Position = tile; // before the game begins: nothing has seen it yet
        }
    }

    /// <summary>Once the game has started: the whole map revealed to the people playing.</summary>
    internal void RevealOnStart()
    {
        if (Rules is not { Reveal: true })
        {
            return;
        }

        var map = state.Map;
        foreach (var person in state.Players.Where(p => p.IsHuman))
        {
            for (var level = 0; level < map.Levels; level++)
            {
                visibility.RevealArea(person.Id, new MapPosition(map.Width / 2, map.Height / 2, level), map.Width + map.Height);
            }
        }
    }

    internal void Resources(CheatResourcesCommand command)
    {
        Allowed();
        var amount = command.Amount;
        if (Enum.GetValues<ResourceType>().Any(r => amount[r] < 0) || amount.IsEmpty)
        {
            throw new CommandRefusedException("A cheat gives resources: no amount may be negative, and one must be more than none.");
        }

        economy.Grant(command.Player, amount);
    }

    internal void Levels(CheatLevelsCommand command)
    {
        Allowed();
        if (command.Levels is < 1 or > MaxLevels)
        {
            throw new CommandRefusedException($"A cheat gives 1 to {MaxLevels} levels.");
        }

        var hero = state.GetHero(command.Hero);
        var reached = ExperienceTable.LevelFor(hero.Experience);
        var needed = ExperienceTable.For(reached + command.Levels) - hero.Experience;
        if (needed <= 0)
        {
            throw new CommandRefusedException($"{hero.Name} is at the highest level.");
        }

        progression.GrantExperience(hero, needed);
    }

    internal void Creatures(CheatCreaturesCommand command)
    {
        Allowed();
        if (command.Count is < 1 or > MaxCreatures)
        {
            throw new CommandRefusedException($"A cheat gives 1 to {MaxCreatures} creatures.");
        }

        if (!state.Creatures.TryGet(command.Creature, out _))
        {
            throw new CommandRefusedException($"Unknown creature {command.Creature}.");
        }

        var hero = state.GetHero(command.Hero);
        if (!hero.Army.TryAdd(new CreatureStack(command.Creature, command.Count)))
        {
            throw new CommandRefusedException($"{hero.Name}'s army has no room for another kind of creature.");
        }

        events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
    }

    private void Allowed()
    {
        if (Rules is not { Cheats: true })
        {
            throw new CommandRefusedException("Cheats are allowed only in a play-test started with --cheats.");
        }

        // A battle rewrites the armies from its units when it ends, and a question decides what comes next: a cheat waits for both.
        if (state.Battle is not null || state.PendingDecision is not null)
        {
            throw new CommandRefusedException("A cheat waits until the battle or the question is over.");
        }
    }
}
