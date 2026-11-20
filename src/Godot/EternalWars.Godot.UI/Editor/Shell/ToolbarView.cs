using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// A row of buttons built from the <see cref="CommandRegistry"/>: a command's icon, or its name when it has none. The tooltip names the
/// command and its shortcut; a button whose command cannot run now is disabled and says why instead. A command that is on or off stays
/// pressed while it is on.
/// </summary>
public partial class ToolbarView : HBoxContainer
{
    private readonly List<(Button Button, EditorCommand Command)> _buttons = [];
    private CommandRegistry _commands = null!;

    /// <summary>Builds the buttons of <paramref name="items"/> (command ids; null: a separator).</summary>
    public void Bind(CommandRegistry commands, IReadOnlyList<string?> items)
    {
        _commands = commands;
        foreach (var id in items)
        {
            if (id is null)
            {
                AddChild(new VSeparator());
                continue;
            }

            var command = commands[id];
            var button = new Button { Text = command.Name, ToggleMode = command.IsOn is not null };
            if (command.Icon is { } icon)
            {
                button.WithIcon(icon);
            }

            button.Pressed += () => commands.Run(id);
            AddChild(button);
            _buttons.Add((button, command));
        }

        commands.StateChanged += Refresh;
        TreeExiting += () => commands.StateChanged -= Refresh;
        Refresh();
    }

    private void Refresh()
    {
        foreach (var (button, command) in _buttons)
        {
            var whyNot = command.WhyNot?.Invoke();
            button.Disabled = whyNot is not null;
            button.TooltipText = whyNot is null ? command.Label : $"{command.Label}: {whyNot}";
            if (command.IsOn is { } isOn)
            {
                button.SetPressedNoSignal(isOn());
            }
        }
    }
}
