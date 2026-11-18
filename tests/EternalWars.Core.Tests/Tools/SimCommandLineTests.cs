using EternalWars.Tools.Sim;
using EternalWars.Tools.Sim.Commands;

namespace EternalWars.Core.Tests.Tools;

/// <summary>The sim tool's strict option parsing (ADR-224): an unknown option, a missing value or a bad value is an error naming it.</summary>
public sealed class SimCommandLineTests
{
    private static CommandLine Parse(string command, params string[] args) => SimCommand.Find(command)!.Parse(args);

    private static string Error(string command, params string[] args) => Assert.Throws<UsageException>(() => Parse(command, args)).Message;

    [Fact]
    public void Parse_ValidOptions_GivesTheirValues()
    {
        var line = Parse("game", "--days", "5", "--profile", "--fog", "Refog", "--complexity", "hard", "--seed", "18446744073709551615");

        Assert.Equal(5, line.Number("--days", 40));
        Assert.Equal(1, line.Number("--runs", 1));
        Assert.True(line.Has("--profile"));
        Assert.Equal("refog", line.Text("--fog"));
        Assert.Equal("hard", line.Text("--complexity"));
        Assert.Equal(ulong.MaxValue, line.Seed("--seed", 1));
        Assert.Null(line.Text("--record"));
    }

    [Fact]
    public void Parse_Positionals_AreTakenInOrderAroundTheOptions()
    {
        var line = Parse("battle", "--runs", "3", "haven.pikeman:20", "--seed", "4", "inferno.imp:30");

        Assert.Equal(["haven.pikeman:20", "inferno.imp:30"], line.Positionals);
        Assert.Equal(3, line.Number("--runs", 100));
    }

    [Fact]
    public void Parse_AnUnknownOption_IsAnErrorListingTheValidOnes()
    {
        var message = Error("towns", "--run", "4");

        Assert.Contains("unknown option '--run'", message, StringComparison.Ordinal);
        Assert.Contains("--runs, --upgraded", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--days")]
    [InlineData("--days", "--profile")]
    public void Parse_AMissingValue_IsAnError(params string[] args) =>
        Assert.Equal("game: --days needs a value (a whole number from 0 up).", Error("game", args));

    [Theory]
    [InlineData("game", "--days", "x", "game: 'x' is not a value for --days (a whole number from 0 up).")]
    [InlineData("battle", "--runs", "0", "battle: '0' is not a value for --runs (a whole number from 1 up).")]
    [InlineData("game", "--seed", "-1", "game: '-1' is not a value for --seed (a whole number from 0 up).")]
    [InlineData("game", "--fog", "fogged", "game: 'fogged' is not a value for --fog (one of reveal, refog).")]
    public void Parse_ABadValue_IsAnErrorNamingTheOption(string command, string option, string value, string expected) =>
        Assert.Equal(expected, command == "battle" ? Error(command, "a:1", "b:1", option, value) : Error(command, option, value));

    [Fact]
    public void Parse_AnOptionGivenTwice_IsAnError() =>
        Assert.Equal("maps: --maps is given twice.", Error("maps", "--maps", "2", "--maps", "3"));

    [Fact]
    public void Parse_MissingOrExtraPositionals_AndMissingRequiredOptions_AreErrors()
    {
        Assert.StartsWith("battle: missing <defender army> (battle takes <attacker army> <defender army>; valid options: --runs, --seed)", Error("battle", "a:1"), StringComparison.Ordinal);
        Assert.StartsWith("replay: unexpected argument 'b'", Error("replay", "a", "b"), StringComparison.Ordinal);
        Assert.StartsWith("selftest: unexpected argument 'now' (selftest takes no arguments)", Error("selftest", "now"), StringComparison.Ordinal);
        Assert.StartsWith("generate: --out is required", Error("generate", "duel", "--seed", "2"), StringComparison.Ordinal);
    }

    [Fact]
    public void TakeGlobalOptions_TakesTheCreatureTableAndEveryWeightFromAnywhere()
    {
        var (rest, creatures, weights) = Program.TakeGlobalOptions(["--weight", "A=1", "game", "--creatures", "c.csv", "--days", "3", "--weight", "B=2"]);

        Assert.Equal(["game", "--days", "3"], rest);
        Assert.Equal("c.csv", creatures);
        Assert.Equal(["A=1", "B=2"], weights);
        Assert.Throws<UsageException>(() => Program.TakeGlobalOptions(["game", "--weight"]));
        Assert.Throws<UsageException>(() => Program.TakeGlobalOptions(["game", "--creatures", "--days", "3"]));
        Assert.Throws<UsageException>(() => Program.TakeGlobalOptions(["game", "--creatures", "a", "--creatures", "b"]));
    }

    [Fact]
    public void EveryCommand_HasItsOwnName_AndParsesWithoutOptions()
    {
        Assert.Equal(SimCommand.All.Count, SimCommand.All.Select(c => c.Name).Distinct().Count());
        foreach (var command in SimCommand.All)
        {
            var line = command.Parse([.. command.Positionals.Select(_ => "x"), .. command.Options.Where(o => o.Required).SelectMany(o => new[] { o.Name, "x" })]);
            Assert.Equal(command.Positionals.Count, line.Positionals.Count);
        }
    }
}
