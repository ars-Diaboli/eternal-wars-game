using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.UI;
using EternalWars.Godot.UI.Editor;
using EternalWars.Godot.UI.Editor.Shell;
using Godot;

namespace EternalWars.MapEditor;

/// <summary>
/// Test (F5, ADR-267; ADR-288): the Test window; its Test saves the map to a temporary file and starts the game on it as a separate
/// process, the editor staying as it is.
/// </summary>
internal sealed class TestComposer(EditorParts parts)
{
    /// <summary>The choices of the last Test, offered again by the next.</summary>
    private PlayTestArguments? _last;

    /// <summary>Where Test saves the map: one file in the system's temporary folder, written again by every Test.</summary>
    private static string PlayTestMap => Path.Combine(Path.GetTempPath(), "EternalWars", "playtest" + MapSerializer.FileExtension);

    /// <summary>The game Test starts: the one in Preferences, else the game project beside the editor's when there is one.</summary>
    private string? GamePath
    {
        get
        {
            if (parts.Preferences.GamePath.Length > 0)
            {
                return parts.Preferences.GamePath;
            }

            var beside = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "EternalWars.Game"));
            return File.Exists(Path.Combine(beside, PlayTestLaunch.ProjectFile)) ? beside : null;
        }
    }

    public void Register() =>
        parts.Command("test.play", "Test the map…", Open, shortcut: new(Key.F5), onMap: true, icon: UiIcon.Play);

    private void Open()
    {
        if (parts.IsBusy)
        {
            return;
        }

        var errors = ProblemsPanel.Validate(parts.Editor.Scenario).Count(i => i.Severity == IssueSeverity.Error);
        var window = new PlayTestWindow { Name = "PlayTest" };
        parts.Overlays.AddChild(window);
        window.Bind(parts.Editor, _last, errors, CommandFor, Run);
    }

    private (PlayTestCommand? Command, string? Error) CommandFor(PlayTestArguments test) =>
        PlayTestLaunch.For(GamePath, OS.GetExecutablePath(), PlayTestMap, test);

    /// <summary>Saves the map as it is to <see cref="PlayTestMap"/> and starts the game on it.</summary>
    private void Run(PlayTestArguments test)
    {
        _last = test;
        if (CommandFor(test) is not ({ } command, _))
        {
            parts.Say($"Cannot test the map: {CommandFor(test).Error}");
            return;
        }

        try
        {
            parts.Files.SaveCopy(PlayTestMap);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException)
        {
            parts.Say($"Could not save the map for the test: {e.Message}");
            return;
        }

        parts.Say(PlayTestWindow.Start(command) is { } error ? $"Could not start the test: {error}" : $"Testing the map as player {test.Player}: {command.Line}");
    }
}
