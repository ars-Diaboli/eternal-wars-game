using System.Globalization;

namespace EternalWars.Tools.Sim;

/// <summary>A command line the sim cannot run: a missing or unknown argument, or a bad value. Reported as <c>error: ...</c>, exit code 2.</summary>
public sealed class UsageException(string message) : Exception(message);

/// <summary>What an option's value is.</summary>
public enum OptionKind
{
    /// <summary>No value: present or not.</summary>
    Flag,

    /// <summary>Any text (a file, a name).</summary>
    Text,

    /// <summary>A whole number, at least <see cref="OptionSpec.Min"/>.</summary>
    Number,

    /// <summary>A seed: a whole number from 0 up.</summary>
    Seed,

    /// <summary>One of <see cref="OptionSpec.Choices"/>, without case.</summary>
    Choice,
}

/// <summary>One option a command takes.</summary>
/// <param name="Name">With the dashes: <c>--days</c>.</param>
/// <param name="Kind">What its value is.</param>
/// <param name="Min">The smallest <see cref="OptionKind.Number"/> it takes.</param>
/// <param name="Choices">The values a <see cref="OptionKind.Choice"/> takes.</param>
/// <param name="Repeatable">May be given more than once (each value is kept, in order).</param>
/// <param name="Required">Must be given.</param>
public sealed record OptionSpec(string Name, OptionKind Kind, int Min = int.MinValue, IReadOnlyList<string>? Choices = null, bool Repeatable = false, bool Required = false)
{
    public static OptionSpec Flag(string name) => new(name, OptionKind.Flag);

    public static OptionSpec Text(string name, bool required = false, bool repeatable = false) => new(name, OptionKind.Text, Required: required, Repeatable: repeatable);

    public static OptionSpec Number(string name, int min = int.MinValue) => new(name, OptionKind.Number, min);

    public static OptionSpec Seed(string name) => new(name, OptionKind.Seed);

    public static OptionSpec Choice(string name, params string[] choices) => new(name, OptionKind.Choice, Choices: choices);

    /// <summary>The value it takes, in words, for error messages.</summary>
    public string ValueText => Kind switch
    {
        OptionKind.Number when Min == int.MinValue => "a whole number",
        OptionKind.Number => $"a whole number from {Min} up",
        OptionKind.Seed => "a whole number from 0 up",
        OptionKind.Choice => $"one of {string.Join(", ", Choices ?? [])}",
        _ => "a value",
    };
}

/// <summary>
/// A command's arguments after the command name, checked strictly against its options: arguments that do not start with <c>--</c> are the
/// positionals, in order; every other argument must be one of the options, given once (unless repeatable), followed by a value of its kind
/// unless it is a flag. Anything else is a <see cref="UsageException"/> naming the argument and listing what the command takes.
/// </summary>
public sealed class CommandLine
{
    private readonly Dictionary<string, List<string>> _values = [];
    private readonly List<string> _positionals = [];

    private CommandLine()
    {
    }

    /// <summary>The positional arguments, in order.</summary>
    public IReadOnlyList<string> Positionals => _positionals;

    /// <summary>
    /// Parses <paramref name="args"/> for <paramref name="command"/>, which takes the positionals <paramref name="positionals"/> (names in angle
    /// brackets for messages, all required) and <paramref name="options"/>.
    /// </summary>
    public static CommandLine Parse(string command, IReadOnlyList<string> args, IReadOnlyList<string> positionals, IReadOnlyList<OptionSpec> options)
    {
        var line = new CommandLine();
        var known = options.ToDictionary(o => o.Name, StringComparer.Ordinal);
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                if (line._positionals.Count == positionals.Count)
                {
                    throw new UsageException($"{command}: unexpected argument '{arg}' ({Takes(command, positionals, options)}).");
                }

                line._positionals.Add(arg);
                continue;
            }

            if (!known.TryGetValue(arg, out var option))
            {
                throw new UsageException($"{command}: unknown option '{arg}' ({Takes(command, positionals, options)}).");
            }

            if (line._values.ContainsKey(arg) && !option.Repeatable)
            {
                throw new UsageException($"{command}: {arg} is given twice.");
            }

            var values = line._values.TryGetValue(arg, out var list) ? list : line._values[arg] = [];
            if (option.Kind == OptionKind.Flag)
            {
                continue;
            }

            if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new UsageException($"{command}: {arg} needs a value ({option.ValueText}).");
            }

            values.Add(Check(command, option, args[++i]));
        }

        if (line._positionals.Count < positionals.Count)
        {
            throw new UsageException($"{command}: missing {positionals[line._positionals.Count]} ({Takes(command, positionals, options)}).");
        }

        if (options.FirstOrDefault(o => o.Required && !line._values.ContainsKey(o.Name)) is { } missing)
        {
            throw new UsageException($"{command}: {missing.Name} is required ({Takes(command, positionals, options)}).");
        }

        return line;
    }

    /// <summary>The flag was given.</summary>
    public bool Has(string name) => _values.ContainsKey(name);

    /// <summary>The option's text, or null when it was not given.</summary>
    public string? Text(string name) => _values.TryGetValue(name, out var values) ? values[0] : null;

    /// <summary>Every value of a repeatable option, in order.</summary>
    public IReadOnlyList<string> All(string name) => _values.TryGetValue(name, out var values) ? values : [];

    /// <summary>The option's number, or <paramref name="fallback"/> when it was not given.</summary>
    public int Number(string name, int fallback) => Text(name) is { } text ? int.Parse(text, CultureInfo.InvariantCulture) : fallback;

    /// <summary>The option's seed, or <paramref name="fallback"/> when it was not given.</summary>
    public ulong Seed(string name, ulong fallback) => Text(name) is { } text ? ulong.Parse(text, CultureInfo.InvariantCulture) : fallback;

    /// <summary>What a command takes, for messages: its positionals and options.</summary>
    public static string Takes(string command, IReadOnlyList<string> positionals, IReadOnlyList<OptionSpec> options) =>
        (positionals.Count, options.Count) switch
        {
            (0, 0) => $"{command} takes no arguments",
            (_, 0) => $"{command} takes {string.Join(" ", positionals)}",
            (0, _) => $"valid options: {string.Join(", ", options.Select(o => o.Name))}",
            _ => $"{command} takes {string.Join(" ", positionals)}; valid options: {string.Join(", ", options.Select(o => o.Name))}",
        };

    /// <summary>The value, checked against the option's kind (a choice comes back as written in the choices).</summary>
    private static string Check(string command, OptionSpec option, string value)
    {
        var ok = option.Kind switch
        {
            OptionKind.Number => int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) && number >= option.Min,
            OptionKind.Seed => ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _),
            OptionKind.Choice => option.Choices!.Any(c => c.Equals(value, StringComparison.OrdinalIgnoreCase)),
            _ => true,
        };
        if (!ok)
        {
            throw new UsageException($"{command}: '{value}' is not a value for {option.Name} ({option.ValueText}).");
        }

        return option.Kind == OptionKind.Choice ? option.Choices!.First(c => c.Equals(value, StringComparison.OrdinalIgnoreCase)) : value;
    }
}
