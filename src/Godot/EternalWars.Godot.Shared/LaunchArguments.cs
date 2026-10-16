using Godot;

namespace EternalWars.Godot.Shared;

/// <summary>User command-line arguments, i.e. those after <c>--</c>: <c>godot --path . -- --map=foo.ewmap.json</c>.</summary>
public static class LaunchArguments
{
    public static string? MapPath => Value("--map=");

    /// <summary>A built-in scenario by name when no map is given (2D harness: <c>towns</c> for <c>TownsScenario</c>).</summary>
    public static string? Scenario => Value("--scenario=");

    /// <summary>A creature table to use instead of the shipped one (<see cref="GameContent.Creatures"/>).</summary>
    public static string? CreaturesPath => Value("--creatures=");

    /// <summary>A hero roster to use instead of the shipped one (<see cref="GameContent.Heroes"/>).</summary>
    public static string? HeroesPath => Value("--heroes=");

    /// <summary>A building table to use instead of the shipped one (<see cref="GameContent.Buildings"/>).</summary>
    public static string? BuildingsPath => Value("--buildings=");

    /// <summary>Scripted input for smoke tests (<see cref="Controls.AutoInput"/>).</summary>
    public static string? AutoInput => Value("--autoinput=");

    private static string? Value(string prefix) =>
        OS.GetCmdlineUserArgs()
            .Where(a => a.StartsWith(prefix, StringComparison.Ordinal))
            .Select(a => a[prefix.Length..])
            .FirstOrDefault();
}
