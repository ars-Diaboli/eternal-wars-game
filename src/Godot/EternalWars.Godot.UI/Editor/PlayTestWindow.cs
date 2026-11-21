using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.UI.Editor.Shell;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The Test window (F5, EDIT-5, ADR-267), a box over the editor: which player the play-tester plays, whether the others are the computer or
/// off, the map revealed, where the hero starts (where the map puts it, the selected hero's tile, or the selected cell), the starting day and
/// the cheats; under them the command the game is started with. Test hands the choices to the app, which saves the map to a temporary file
/// and starts the game as a separate process; the editor stays as it is.
/// </summary>
public partial class PlayTestWindow : Control
{
    private readonly SpinBox _day = new() { Name = "PlayTestDay", MinValue = 1, MaxValue = PlayTestArguments.MaxDay, Rounded = true, CustomMinimumSize = new Vector2(110, 0) };
    private readonly CheckBox _reveal = new() { Name = "PlayTestReveal", Text = "Reveal the map" };
    private readonly CheckBox _cheats = new() { Name = "PlayTestCheats", Text = "Cheat panel (resources, levels, creatures)" };
    private readonly CheckBox _othersOff = new() { Name = "PlayTestOthersOff", Text = "The other players do nothing (off)" };
    private readonly Label _command = new() { Name = "PlayTestCommand", AutowrapMode = TextServer.AutowrapMode.Arbitrary, CustomMinimumSize = new Vector2(560, 0) };
    private readonly Label _problem = new() { Name = "PlayTestProblem", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(560, 0) };
    private readonly List<(CheckBox Box, PlayerId Player)> _players = [];
    private readonly List<(CheckBox Box, MapPosition? Start, PlayerId? Player)> _starts = [];
    private Button _test = null!;
    private Func<PlayTestArguments, (PlayTestCommand? Command, string? Error)> _preview = null!;
    private Core.Domain.Game.GameState _state = null!;
    private int _errors;

    /// <summary>
    /// <paramref name="last"/> are the choices of the last Test (null: the defaults); <paramref name="preview"/> gives the command the choices
    /// start; <paramref name="errors"/> is how many errors the map has (a map with errors is not tested); <paramref name="test"/> runs it.
    /// </summary>
    public void Bind(
        MapEditorSession editor, PlayTestArguments? last, int errors, Func<PlayTestArguments, (PlayTestCommand? Command, string? Error)> preview,
        Action<PlayTestArguments> test)
    {
        _preview = preview;
        _errors = errors;
        var state = _state = editor.Scenario;
        var rows = EditorOverlay.Build(this);
        rows.AddChild(new Label { Text = "Test the map", HorizontalAlignment = HorizontalAlignment.Center });

        rows.AddChild(new Label { Text = "Play as", ThemeTypeVariation = "HeaderSmall" });
        var players = new ButtonGroup();
        var line = new HFlowContainer();
        rows.AddChild(line);
        foreach (var player in state.Players)
        {
            var box = new CheckBox { Name = $"PlayTestPlayer{player.Id.Value + 1}", Text = $"{player.Id.Value + 1} {player.Name}", ButtonGroup = players };
            box.Toggled += _ => ShowCommand();
            line.AddChild(box);
            _players.Add((box, player.Id));
        }

        var chosen = last is { } l && state.HasPlayer(l.PlayerId) ? l.PlayerId : state.Players.FirstOrDefault()?.Id;
        _players.FirstOrDefault(p => p.Player == chosen).Box?.SetPressedNoSignal(true);
        rows.AddChild(_othersOff);

        // Where the hero starts: the map's own place, the selected hero (whose owner is then played), or the first selected cell.
        rows.AddChild(new Label { Text = "Start", ThemeTypeVariation = "HeaderSmall" });
        var starts = new ButtonGroup();
        AddStart(rows, starts, "Where the map puts the heroes", null, null, "PlayTestStartMap");
        if (editor.Selection.Heroes.Select(id => state.Heroes.FirstOrDefault(h => h.Id == id)).FirstOrDefault(h => h is not null) is { } hero)
        {
            AddStart(rows, starts, $"At the selected hero, {hero.Name} {hero.Position}", hero.Position, hero.Owner, "PlayTestStartHero");
        }

        if (editor.Selection.Cells(state).FirstOrDefault() is var cell && editor.Selection.Tiles.Count > 0)
        {
            AddStart(rows, starts, $"On the selected cell {cell}", cell, null, "PlayTestStartCell");
        }

        var start = _starts.FirstOrDefault(s => last?.Start is { } at && s.Start == at);
        (start.Box ?? _starts[0].Box).SetPressedNoSignal(true);

        var dayRow = new HBoxContainer();
        dayRow.AddChild(new Label { Text = "Starting day" });
        dayRow.AddChild(_day);
        rows.AddChild(dayRow);
        rows.AddChild(_reveal);
        rows.AddChild(_cheats);
        _othersOff.ButtonPressed = last?.Others == PlayTestOthers.Off;
        _reveal.ButtonPressed = last?.Reveal == true;
        _cheats.ButtonPressed = last?.Cheats == true;
        _day.Value = last?.Day ?? 1;
        foreach (var box in new[] { _othersOff, _reveal, _cheats })
        {
            box.Toggled += _ => ShowCommand();
        }

        _day.ValueChanged += _ => ShowCommand();
        rows.AddChild(new Label { Text = "The game starts with", ThemeTypeVariation = "HeaderSmall" });
        rows.AddChild(_command);
        rows.AddChild(_problem);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        rows.AddChild(buttons);
        _test = Button(buttons, "Test", () =>
        {
            if (Chosen() is { } choices && EditorOverlay.Close(this))
            {
                test(choices);
            }
        });
        _test.Name = "PlayTestStart";
        _test.CallDeferred(Control.MethodName.GrabFocus);
        Button(buttons, "Cancel", () => EditorOverlay.Close(this));
        ShowCommand();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            GetViewport()?.SetInputAsHandled();
            EditorOverlay.Close(this);
        }
    }

    private void AddStart(VBoxContainer rows, ButtonGroup group, string text, MapPosition? start, PlayerId? player, string name)
    {
        var box = new CheckBox { Name = name, Text = text, ButtonGroup = group };
        box.Toggled += on =>
        {
            if (on && player is { } owner)
            {
                // The selected hero's owner is the one played: only its own hero may start on a hero's tile.
                foreach (var (playerBox, id) in _players)
                {
                    playerBox.SetPressedNoSignal(id == owner);
                }
            }

            ShowCommand();
        };
        rows.AddChild(box);
        _starts.Add((box, start, player));
    }

    /// <summary>The choices made; null when no player can be chosen (a map without players).</summary>
    private PlayTestArguments? Chosen()
    {
        if (_players.FirstOrDefault(p => p.Box.ButtonPressed) is not { Box: not null } player)
        {
            return null;
        }

        _day.Apply(); // a number typed but not yet entered counts
        var start = _starts.FirstOrDefault(s => s.Box.ButtonPressed).Start;
        return new PlayTestArguments(
            player.Player.Value + 1, _othersOff.ButtonPressed ? PlayTestOthers.Off : PlayTestOthers.Ai, _reveal.ButtonPressed, start, (int)_day.Value,
            _cheats.ButtonPressed);
    }

    /// <summary>The command the choices start, and why Test is off when it is.</summary>
    private void ShowCommand()
    {
        var choices = Chosen();
        var (command, error) = choices is null ? (null, "The map has no player to play.") : _preview(choices);
        _command.Text = command?.Line ?? "—";
        var why = (choices is null ? null : choices.WhyNot(_state)) ?? error
            ?? (_errors > 0 ? $"The map has {_errors} error(s): fix them first (Problems)." : null);
        _problem.Text = why ?? "";
        _problem.Visible = why is not null;
        _problem.AddThemeColorOverride("font_color", UiTheme.IssueError);
        _test.Disabled = why is not null;
        _test.TooltipText = why ?? "Save the map to a temporary file and start the game on it";
    }

    /// <summary>Starts <paramref name="command"/> as a separate process, its arguments passed as they are; null, or why it could not start.</summary>
    public static string? Start(PlayTestCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var start = new System.Diagnostics.ProcessStartInfo(command.Program) { UseShellExecute = false };
        foreach (var argument in command.Arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = System.Diagnostics.Process.Start(start);
            return null;
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return e.Message;
        }
    }

    private static Button Button(Container parent, string text, Action pressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(110, 0) };
        button.Pressed += pressed;
        parent.AddChild(button);
        return button;
    }
}
