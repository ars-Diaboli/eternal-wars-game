using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Editor.Shell;
using EternalWars.Godot.UI.Editor.Tools;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// A button per tool (its icon and its name) plus the options of the one in use, both wrapping to the width the palette is given. The first tool is in use at the
/// start; picking another cancels what the old one was in the middle of.
/// </summary>
public partial class ToolPalette : VBoxContainer
{
    private readonly HFlowContainer _options = new();
    private readonly Dictionary<IEditorTool, Button> _buttons = [];

    /// <summary>Another tool is in use.</summary>
    public event Action? ToolChanged;

    /// <summary>The tools, in the buttons' order.</summary>
    public IReadOnlyList<IEditorTool> Tools { get; private set; } = [];

    /// <summary>The tool in use.</summary>
    public IEditorTool Tool { get; private set; } = null!;

    public void Bind(IReadOnlyList<IEditorTool> tools)
    {
        Tools = tools;
        var buttons = new HFlowContainer();
        AddChild(buttons);
        var group = new ButtonGroup();
        foreach (var tool in tools)
        {
            var button = new Button
            {
                Text = tool.Name, Icon = PlaceholderIcons.Of(tool.Icon, tool.Name), ToggleMode = true, ButtonGroup = group, ButtonPressed = tool == tools[0],
            };
            button.TooltipText = tool.Shortcut is { } key ? $"{tool.Name} ({key})" : tool.Name;
            button.Pressed += () => Use(tool);
            buttons.AddChild(button);
            _buttons[tool] = button;
        }

        AddChild(_options);
        Tool = tools[0];
        Tool.Activate();
        ShowOptions();
    }

    /// <summary>Puts <paramref name="tool"/> (one of <see cref="Tools"/>) in use.</summary>
    public void Use(IEditorTool tool)
    {
        if (tool == Tool)
        {
            return;
        }

        Tool.Cancel();
        Tool = tool;
        tool.Activate();
        _buttons[tool].ButtonPressed = true; // the group lets go of the old tool's button
        ShowOptions();
        ToolChanged?.Invoke();
    }

    /// <summary>Builds the options of the tool in use again (what they list changed).</summary>
    public void RefreshOptions() => ShowOptions();

    private void ShowOptions()
    {
        _options.ClearChildren();
        Tool.BuildOptions(new ToolOptions(_options));
    }
}
