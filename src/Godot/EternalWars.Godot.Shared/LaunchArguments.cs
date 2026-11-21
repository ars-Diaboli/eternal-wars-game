using EternalWars.Core.Domain.Game;
using Godot;

namespace EternalWars.Godot.Shared;

/// <summary>User command-line arguments, i.e. those after <c>--</c>: <c>godot --path . -- --map=foo.ewmap.json</c>.</summary>
public static class LaunchArguments
{
    public static string? MapPath => Value("--map=");

    /// <summary>A settings file to use instead of the one in the user data folder (smoke tests start from the defaults).</summary>
    public static string? SettingsPath => Value("--settings=");

    /// <summary>A folder for the game's saves and autosaves instead of the one in the user data folder (smoke tests run side by side and leave the player's saves alone).</summary>
    public static string? SavesPath => Value("--saves=");

    /// <summary>A built-in scenario by name when no map is given (2D harness: <c>towns</c> for <c>TownsScenario</c>).</summary>
    public static string? Scenario => Value("--scenario=");

    /// <summary>A creature table to use instead of the shipped one (<see cref="GameContent.Creatures"/>).</summary>
    public static string? CreaturesPath => Value("--creatures=");

    /// <summary>A hero roster to use instead of the shipped one (<see cref="GameContent.Heroes"/>).</summary>
    public static string? HeroesPath => Value("--heroes=");

    /// <summary>A building table to use instead of the shipped one (<see cref="GameContent.Buildings"/>).</summary>
    public static string? BuildingsPath => Value("--buildings=");

    /// <summary>An artifact table to use instead of the shipped one (<see cref="GameContent.Artifacts"/>).</summary>
    public static string? ArtifactsPath => Value("--artifacts=");

    /// <summary>An artifact set table to use instead of the shipped one (<see cref="GameContent.Artifacts"/>).</summary>
    public static string? ArtifactSetsPath => Value("--artifact-sets=");

    /// <summary><c>--new-game</c>: the game opens on the new-game screen instead of the main menu (the smoke scripts, a map tried from the editor).</summary>
    public static bool NewGame => OS.GetCmdlineUserArgs().Contains("--new-game");

    /// <summary><c>--replay=&lt;file&gt;</c>: the game opens the replay viewer on this replay or save (the smoke scripts, ADR-164).</summary>
    public static string? ReplayPath => Value("--replay=");

    /// <summary>
    /// <c>--host</c> or <c>--host=&lt;port&gt;</c>: the game opens on the host's LAN lobby (ADR-194) instead of the main menu.
    /// </summary>
    public static bool Host => OS.GetCmdlineUserArgs().Any(a => a == "--host" || a.StartsWith("--host=", StringComparison.Ordinal));

    /// <summary>The port of <c>--host=&lt;port&gt;</c>; null for the default one.</summary>
    public static int? HostPort => int.TryParse(Value("--host="), out var port) ? port : null;

    /// <summary><c>--join=&lt;address&gt;[:&lt;port&gt;]</c>: the game joins the LAN lobby or game hosted there (ADR-194) instead of opening the main menu.</summary>
    public static string? Join => Value("--join=");

    /// <summary><c>--name=&lt;name&gt;</c>: the player's name in LAN games (the Multiplayer screen starts with it); the computer's user name when not given.</summary>
    public static string? PlayerName => Value("--name=");

    /// <summary>
    /// <c>--seed=&lt;number&gt;</c>: the new-game screen's games use this seed instead of a new one each, so a smoke scenario plays the same battles every
    /// run (ADR-235); null when not given or not a number.
    /// </summary>
    public static ulong? Seed => ulong.TryParse(Value("--seed="), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seed) ? seed : null;

    /// <summary>
    /// A play-test from the map editor (EDIT-5, ADR-267): <c>--player=N</c> with <c>--others=ai|off</c>, <c>--reveal</c>,
    /// <c>--start=x,y,level</c>, <c>--day=N</c>, <c>--cheats</c>; both null when there is no <c>--player=</c>, the reason when one cannot be read.
    /// </summary>
    public static (PlayTestArguments? Arguments, string? Error) PlayTest => PlayTestArguments.Parse(OS.GetCmdlineUserArgs());

    /// <summary>
    /// <c>--perf=&lt;report&gt;</c>: the map editor times opening the <c>--map=</c> file, painting and validating, writes the report there and quits
    /// (ADR-278, <c>tools/perf-editor.ps1</c>).
    /// </summary>
    public static string? PerfReport => Value("--perf=");

    /// <summary>Scripted input for smoke tests (<see cref="Controls.AutoInput"/>).</summary>
    public static string? AutoInput => Value("--autoinput=");

    private static string? Value(string prefix) =>
        OS.GetCmdlineUserArgs()
            .Where(a => a.StartsWith(prefix, StringComparison.Ordinal))
            .Select(a => a[prefix.Length..])
            .FirstOrDefault();
}
