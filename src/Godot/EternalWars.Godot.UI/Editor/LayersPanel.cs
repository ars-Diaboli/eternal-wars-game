using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The editor's layers (ADR-262), a tab of the right region: per kind a box to show it and, where something selects or erases it, a box to
/// lock it. What is hidden or locked cannot be selected or erased (<see cref="MapLayers"/>). The grid is shown or hidden here too.
/// </summary>
public partial class LayersPanel : VBoxContainer
{
    private static readonly (string Text, MapLayer Layer)[] Rows =
    [
        ("Terrain", MapLayer.Terrain), ("Roads", MapLayer.Roads), ("Rivers", MapLayer.Rivers), ("Objects", MapLayer.Objects),
        ("Scenery", MapLayer.Scenery), ("Monsters", MapLayer.Monsters), ("Heroes", MapLayer.Heroes), ("Events", MapLayer.Events),
        ("Start zones", MapLayer.StartZones),
    ];

    private readonly Dictionary<MapLayer, (CheckBox Shown, CheckBox? Locked)> _boxes = [];
    private readonly CheckBox _grid = new() { Name = "Grid", Text = "Grid", TooltipText = "The tile grid over the ground" };
    private MapLayers _layers = null!;

    /// <summary>The session's layers; <paramref name="showGrid"/> switches the grid, which <see cref="ShowGrid"/> then shows.</summary>
    public void Bind(MapEditorSession editor, bool gridShown, Action<bool> showGrid)
    {
        _layers = editor.Layers;
        AddChild(new Label
        {
            Text = "A hidden or locked kind cannot be selected or erased.", AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var table = new GridContainer { Columns = 2 };
        AddChild(table);
        foreach (var (text, layer) in Rows)
        {
            var shown = new CheckBox { Name = $"Show{layer}", Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = $"Show {text.ToLowerInvariant()}" };
            shown.Toggled += on => _layers.Show(layer, on);
            table.AddChild(shown);
            CheckBox? locked = null;
            if ((MapLayers.Lockable & layer) != 0)
            {
                locked = new CheckBox { Name = $"Lock{layer}", Text = "Locked", TooltipText = $"Lock {text.ToLowerInvariant()}: drawn, but not selected or erased" };
                locked.Toggled += on => _layers.Lock(layer, on);
                table.AddChild(locked);
            }
            else
            {
                table.AddChild(new Control()); // only shown or hidden: nothing selects or erases it
            }

            _boxes[layer] = (shown, locked);
        }

        _grid.ButtonPressed = gridShown;
        _grid.Toggled += on => showGrid(on);
        table.AddChild(_grid);
        table.AddChild(new Control());

        var reset = new Button { Text = "Show and unlock all", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        reset.Pressed += () =>
        {
            _layers.Show(MapLayer.All, true);
            _layers.Lock(MapLayer.All, false);
        };
        AddChild(reset);

        this.SubscribeWhileInTree<MapLayersChanged>(editor.Events, _ => Refresh());
        Refresh();
    }

    /// <summary>The grid was switched elsewhere (the View menu, the toolbar).</summary>
    public void ShowGrid(bool on) => _grid.SetPressedNoSignal(on);

    private void Refresh()
    {
        foreach (var (layer, (shown, locked)) in _boxes)
        {
            shown.SetPressedNoSignal(_layers.IsShown(layer));
            locked?.SetPressedNoSignal(_layers.IsLocked(layer));
        }
    }
}
