using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Editor.Tools;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// A button per tool plus the options of the one in use, both wrapping to the width the palette is given. The first tool is in use at the
/// start; picking another cancels what the old one was in the middle of.
/// </summary>
public partial class ToolPalette : VBoxContainer
{
    private readonly HFlowContainer _options = new();

    /// <summary>The tool in use.</summary>
    public IEditorTool Tool { get; private set; } = null!;

    public void Bind(IReadOnlyList<IEditorTool> tools)
    {
        var buttons = new HFlowContainer();
        AddChild(buttons);
        var group = new ButtonGroup();
        foreach (var tool in tools)
        {
            var button = new Button { Text = tool.Name, ToggleMode = true, ButtonGroup = group, ButtonPressed = tool == tools[0] };
            button.Pressed += () => Use(tool);
            buttons.AddChild(button);
        }

        AddChild(_options);
        Tool = tools[0];
        Tool.Activate();
        ShowOptions();
    }

    private void Use(IEditorTool tool)
    {
        if (tool == Tool)
        {
            return;
        }

        Tool.Cancel();
        Tool = tool;
        tool.Activate();
        ShowOptions();
    }

    private void ShowOptions()
    {
        _options.ClearChildren();
        Tool.BuildOptions(new ToolOptions(_options));
    }
}
