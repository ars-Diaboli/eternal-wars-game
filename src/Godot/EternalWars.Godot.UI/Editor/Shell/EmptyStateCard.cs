using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// The editor's empty state (ADR-259): over the blank map the editor starts with, a card that offers the new-map wizard, Open and the sample
/// maps (<see cref="SampleMaps"/>), each opened as a new, unsaved map. It goes at the map's first edit, or with its close button; the map
/// under it can be painted on at once.
/// </summary>
public partial class EmptyStateCard : PanelContainer
{
    public void Bind(MapEditorSession editor, Action newMap, Action open, IReadOnlyList<SampleMap> samples, Action<SampleMap> openSample)
    {
        Name = "EmptyState";
        AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(UiTheme.PanelBackground, 0.94f), UiTheme.Border, UiSpacing.Padding * 2));
        var rows = new VBoxContainer();
        AddChild(rows);

        var top = new HBoxContainer();
        rows.AddChild(top);
        top.AddChild(new Label { Text = "Start with a template, or paint a terrain.", SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var close = new Button { Text = "×", TooltipText = "Close", Flat = true };
        close.Pressed += QueueFree;
        top.AddChild(close);

        var buttons = new HBoxContainer();
        rows.AddChild(buttons);
        var wizard = new Button { Text = "New map…", TooltipText = "The new-map wizard: size, ground, players and a template (Ctrl+N)" };
        wizard.Pressed += newMap;
        buttons.AddChild(wizard);
        var opening = new Button { Text = "Open…" };
        opening.Pressed += open;
        buttons.AddChild(opening);

        rows.AddChild(new Label { Text = "Sample maps:" });
        var list = new HFlowContainer();
        rows.AddChild(list);
        foreach (var sample in samples)
        {
            var button = new Button { Text = sample.Name, TooltipText = sample.About };
            button.Pressed += () => openSample(sample);
            list.AddChild(button);
        }

        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => QueueFree());
    }
}
