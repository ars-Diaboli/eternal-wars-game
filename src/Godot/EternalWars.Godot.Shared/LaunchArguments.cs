using Godot;

namespace EternalWars.Godot.Shared;

/// <summary>User command-line arguments, i.e. those after <c>--</c>: <c>godot --path . -- --map=foo.ewmap.json</c>.</summary>
public static class LaunchArguments
{
    public static string? MapPath => Value("--map=");

    /// <summary>Scripted input for smoke tests (<see cref="Controls.AutoInput"/>).</summary>
    public static string? AutoInput => Value("--autoinput=");

    private static string? Value(string prefix) =>
        OS.GetCmdlineUserArgs()
            .Where(a => a.StartsWith(prefix, StringComparison.Ordinal))
            .Select(a => a[prefix.Length..])
            .FirstOrDefault();
}
