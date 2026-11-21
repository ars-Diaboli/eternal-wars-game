using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Editing;

/// <summary>The program the editor's Test starts and its arguments (<see cref="PlayTestLaunch.For"/>).</summary>
public sealed record PlayTestCommand(string Program, IReadOnlyList<string> Arguments)
{
    /// <summary>The command as one line, for the Test window: arguments with spaces in quotes.</summary>
    public string Line => string.Join(' ', new[] { Program }.Concat(Arguments).Select(a => a.Contains(' ', StringComparison.Ordinal) ? $"\"{a}\"" : a));
}

/// <summary>
/// How the map editor's Test (F5, EDIT-5, ADR-267) starts the game as a separate process: the game is the path in Preferences, either an
/// exported game's program or the game's Godot project (its folder or its <c>project.godot</c>), which runs in the Godot the editor runs in.
/// The map goes as <c>--map=</c>, the play-test's choices as <see cref="PlayTestArguments.ToArguments"/>.
/// </summary>
public static class PlayTestLaunch
{
    public const string ProjectFile = "project.godot";

    /// <summary>
    /// The command that play-tests the map in <paramref name="mapPath"/> with <paramref name="test"/>'s choices, using the game at
    /// <paramref name="game"/> and, for a Godot project, the Godot program <paramref name="godot"/>; or why there is none.
    /// </summary>
    public static (PlayTestCommand? Command, string? Error) For(string? game, string godot, string mapPath, PlayTestArguments test)
    {
        ArgumentNullException.ThrowIfNull(godot);
        ArgumentNullException.ThrowIfNull(mapPath);
        ArgumentNullException.ThrowIfNull(test);
        if (string.IsNullOrWhiteSpace(game))
        {
            return (null, "The game's path is not set: Edit > Preferences, Game.");
        }

        game = game.Trim();
        string[] arguments = ["--", $"--map={mapPath}", .. test.ToArguments()];
        var project = Directory.Exists(game) ? Path.Combine(game, ProjectFile)
            : Path.GetFileName(game).Equals(ProjectFile, StringComparison.OrdinalIgnoreCase) ? game
            : null;
        if (project is not null)
        {
            return File.Exists(project)
                ? (new PlayTestCommand(godot, ["--path", Path.GetDirectoryName(Path.GetFullPath(project))!, .. arguments]), null)
                : (null, $"No Godot project at {game}: {ProjectFile} is missing.");
        }

        return File.Exists(game) ? (new PlayTestCommand(game, arguments), null) : (null, $"The game's path {game} is neither a program nor a Godot project.");
    }
}
