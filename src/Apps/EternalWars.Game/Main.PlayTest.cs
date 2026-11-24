using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Settings;
using EternalWars.Godot.Shared;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Game;

/// <summary>
/// A play-test from the map editor (EDIT-5, ADR-267): with <c>--player=</c> the game starts the <c>--map=</c> at once with the play-test's
/// rules (<see cref="PlayTestArguments"/>) instead of opening a menu, and with <c>--cheats</c> it shows the cheat panel.
/// </summary>
public partial class Main
{
    /// <summary>Where a play-test's saves and autosaves go: in the system's temporary folder, apart from the player's games.</summary>
    private static string PlayTestSaves => Path.Combine(Path.GetTempPath(), "EternalWars", "playtest-saves");

    /// <summary>
    /// Starts the play-test the launch arguments ask for; when it cannot start (an argument, the map or the start), the main menu opens with a
    /// window that says why.
    /// </summary>
    private void StartPlayTest(PlayTestArguments? test, string? error)
    {
        GameState? state = null;
        var path = LaunchArguments.MapPath;
        if (test is not null && path is null)
        {
            error = "A play-test needs its map: --map=<file>.";
        }
        else if (test is not null && path is not null)
        {
            try
            {
                var options = GameOptions.Default with { Seed = LaunchArguments.Seed ?? (ulong)System.Random.Shared.NextInt64(), PlayTest = test.Rules };
                state = MapConverter.ToGameState(MapSerializer.Load(path), GameContent.All, options);
                test.Prepare(state);
            }
            catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException or InvalidOperationException
                or NotSupportedException or System.Text.Json.JsonException)
            {
                (state, error) = (null, e.Message);
            }
        }

        if (state is null || test is null)
        {
            ShowMainMenu();
            var alert = new AcceptDialog { Title = "The play-test could not start", DialogText = error ?? "" };
            AddChild(alert);
            alert.PopupCentered();
            return;
        }

        _generated = null;
        _campaign = null;
        StartGame(state);

        // A hero of the play-tester on the start tile is the one selected (the first hero was put there when the game started).
        if (test.Start is { } tile && state.Heroes.FirstOrDefault(h => h.Position == tile && h.Owner == test.PlayerId) is { } hero)
        {
            _mapInput.Select(hero.Id, focus: true);
        }

        _status.Text = $"Play-test of {state.Map.Name} as player {test.Player}{(test.Others == PlayTestOthers.Off ? ", the others off" : "")}; not ranked.";
    }

    /// <summary>The cheat panel of a play-test with cheats; null without one.</summary>
    private CheatPanel? _cheats;

    /// <summary>The cheat panel of a play-test with cheats, over the map's edge away from the HUD's column.</summary>
    private void BuildCheats()
    {
        var layer = new CanvasLayer { Name = "Cheats", Layer = 4 };
        _game!.AddChild(layer);
        _cheats = new CheatPanel();
        layer.AddChild(_cheats);
        _cheats.Bind(_session, () => _mapInput.SelectedHero, text => _status.Text = text);
        PlaceCheats();
    }

    /// <summary>Puts the cheat panel on the map's left edge, or its right when the HUD is mirrored (ADR-309).</summary>
    private void PlaceCheats()
    {
        if (!IsInstanceValid(_cheats))
        {
            return;
        }

        var panel = _cheats!;
        if (HudArrangement.SideOf(_layout, HudPart.Minimap) == ScreenSide.Left)
        {
            panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterRight);
            panel.GrowHorizontal = Control.GrowDirection.Begin;
            panel.OffsetRight = -12;
        }
        else
        {
            panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterLeft);
            panel.GrowHorizontal = Control.GrowDirection.End;
            panel.OffsetLeft = 12;
        }
    }
}
