using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Godot.Shared.Cameras;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI;
using EternalWars.Godot.UI.Editor;
using EternalWars.Godot.UI.Editor.Shell;
using EternalWars.Godot.UI.Editor.Tools;
using Godot;

namespace EternalWars.MapEditor;

/// <summary>
/// The View menu (ADR-288): the tabs and the regions, the level shown and the other one ghosted, the grid, the camera (focus, the whole
/// map, the bookmarks, top-down), Go to and Find, the commands that show each panel, and the jumps to a tile, a thing or an event.
/// </summary>
internal sealed class ViewComposer(EditorParts parts)
{
    private MapEditorSession Editor => parts.Editor;

    private MapViewContext Context => parts.Context;

    private EditorCamera Camera => parts.Camera;

    private EditorWorkspace Workspace => parts.Workspace;

    /// <summary>The commands that need no panel.</summary>
    public void Register()
    {
        parts.Command("view.map-tab", "Map tab", () => Workspace.ShowPage(EditorWorkspace.MapPage),
            isOn: () => Workspace.Page == EditorWorkspace.MapPage, isChoice: true, icon: UiIcon.MapTab);
        parts.Command("templates.editor", "Template tab", () => Workspace.ShowPage(parts.TemplatePage),
            isOn: () => Workspace.Page == parts.TemplatePage, isChoice: true, icon: UiIcon.TemplateTab);
        parts.Command("view.level", "Switch level", SwitchLevel, UiIcon.SwitchLevel, new(Key.L),
            () => Editor.Scenario.Map.Levels > 1 ? null : "The map has one level", onMap: true);
        parts.Command("view.ghost-level", "Ghost the other level", () => Context.GhostOtherLevel = !Context.GhostOtherLevel, isOn: () => Context.GhostLevel is not null,
            whyNot: () => Editor.Scenario.Map.Levels > 1 ? null : "The map has one level", onMap: true, icon: UiIcon.SwitchLevel);
        parts.Command("view.grid", "Grid", () => ShowGrid(!parts.ShowGrid), isOn: () => parts.ShowGrid, onMap: true, icon: UiIcon.Grid);
        parts.Command("view.top-down", "Top-down view", () => Camera.TopDown = !Camera.TopDown, isOn: () => Camera.TopDown, onMap: true, icon: UiIcon.TopDown);
        parts.Command("view.focus", "Focus the selection", FocusSelection, shortcut: new(Key.F),
            whyNot: () => Editor.Selection.Cells(Editor.Scenario).Count > 0 ? null : "Nothing is selected", onMap: true, icon: UiIcon.Focus);
        parts.Command("view.whole-map", "Whole map", ShowWholeMap, shortcut: new(Key.Home), onMap: true, icon: UiIcon.WholeMap);
        parts.Command("view.go-to", "Go to…", () => OpenGoTo(find: false), shortcut: new(Key.G, Ctrl: true), onMap: true, icon: UiIcon.GoTo);
        parts.Command("edit.find", "Find on the map…", () => OpenGoTo(find: true), shortcut: new(Key.F, Ctrl: true), onMap: true, icon: UiIcon.Find); // Go to's box that also finds objects by kind (ADR-282)
        for (var slot = CameraBookmarks.First; slot <= CameraBookmarks.Last; slot++)
        {
            var n = slot;
            parts.Command($"view.store-bookmark-{n}", $"Store bookmark {n}", () => StoreBookmark(n), shortcut: new(Key.Key0 + n, Ctrl: true), onMap: true, icon: UiIcon.Bookmark);
            parts.Command($"view.bookmark-{n}", $"Go to bookmark {n}", () => GoToBookmark(n), shortcut: new(Key.Key0 + n),
                whyNot: () => parts.Bookmarks[n] is null
                    ? $"Bookmark {n} is not stored: {StoreBookmarkKey(n)} the view"
                    : null, onMap: true, icon: UiIcon.Bookmark);
        }

        parts.Command("view.left", "Left panel", () => Workspace.SetOpen(EditorRegion.Left, !Workspace.IsShown(EditorRegion.Left)),
            isOn: () => Workspace.IsShown(EditorRegion.Left), onMap: true, icon: UiIcon.PanelLeft);
        parts.Command("view.bottom", "Bottom panel", () => Workspace.SetOpen(EditorRegion.Bottom, !Workspace.IsShown(EditorRegion.Bottom)),
            isOn: () => Workspace.IsShown(EditorRegion.Bottom), onMap: true, icon: UiIcon.PanelBottom);
        parts.Command("view.right", "Right panel", () => Workspace.SetOpen(EditorRegion.Right, !Workspace.IsShown(EditorRegion.Right)),
            isOn: () => Workspace.IsShown(EditorRegion.Right), onMap: true, icon: UiIcon.PanelRight);
        parts.Command("view.panels", "Hide all panels", Workspace.ToggleAll, shortcut: new(Key.F11), isOn: () => Workspace.AllHidden, onMap: true, icon: UiIcon.PanelsHide);
    }

    /// <summary>
    /// The commands that show each panel (<paramref name="panels"/>: the command, its name, its icon, the panel), registered once the panels
    /// exist; the panel of each command, for the panels' context menus.
    /// </summary>
    public Dictionary<Control, string> RegisterPanels(params (string Id, string Name, UiIcon Icon, Control Panel)[] panels)
    {
        var commands = new Dictionary<Control, string>();
        foreach (var (id, name, icon, panel) in panels)
        {
            parts.Command(id, name, () => Workspace.ShowTab(panel), icon: icon);
            commands[panel] = id;
        }

        return commands;
    }

    /// <summary>The tile grid on or off, kept from map to map: the view, the Layers tab and the commands follow.</summary>
    public void ShowGrid(bool on)
    {
        Context.ShowGrid = parts.ShowGrid = on;
        parts.Layers.ShowGrid(on);
        parts.Commands.NotifyStateChanged();
    }

    /// <summary>The other level was ghosted or no longer is (the command or the minimap's box): kept for the next map, the commands follow.</summary>
    public void GhostChanged()
    {
        parts.GhostLevel = Context.GhostOtherLevel;
        parts.Commands.NotifyStateChanged();
    }

    /// <summary>The view shows <paramref name="position"/>'s level with the tile in the middle of the map view.</summary>
    public void ShowTile(MapPosition position)
    {
        ShowLevel(position.Level);
        Camera.Centre(MapSpace.ToGround(Editor.Scenario.Map, position), Workspace.MapView.GetGlobalRect());
    }

    /// <summary>
    /// A click on a problem (ADR-290): the view goes to its tile, and the hero or object there, the culprit, becomes the selection and the
    /// inspected one as a jump link makes it; a problem of the ground alone just shows its tile.
    /// </summary>
    public void ShowIssue(MapPosition position)
    {
        if (Editor.Scenario.HeroAt(position) is { } hero)
        {
            Jump(new MapLink(hero.Name, position, Hero: hero.Id));
        }
        else if (Editor.Scenario.ObjectCellAt(position)?.Object is { } culprit)
        {
            Jump(new MapLink(culprit.ToString() ?? "", position, culprit.Id));
        }
        else
        {
            ShowTile(position);
        }
    }

    /// <summary>
    /// Go to (Ctrl+G, ADR-261): a tile or a named thing, which the view shows and, for a thing, selects (as a jump link does). With
    /// <paramref name="find"/>, Find on the map (Ctrl+F, ADR-282): objects by their kind too.
    /// </summary>
    public void OpenGoTo(bool find, string query = "")
    {
        if (parts.IsBusy)
        {
            return;
        }

        var box = new GoToBox { Name = "GoTo" };
        parts.Overlays.AddChild(box);
        box.Bind(Editor.Scenario, Context.Level, Jump, find, query);
    }

    /// <summary>A jump link of the inspector (ADR-254): the view goes to the linked thing, which becomes the selection and the inspected one.</summary>
    public void Jump(MapLink link)
    {
        ShowTile(link.Position);
        var tools = parts.Tools;
        var target = link.Object is not null || link.Hero is not null;
        if (target && tools.Tool is not SelectTool)
        {
            tools.Use(tools.Tools.OfType<SelectTool>().First()); // the next click selects, not places or links
        }

        // A thing whose layer is hidden or locked (ADR-262) is shown but not selected; the selection stays as it was.
        var layer = link.Object is { } linked && Editor.Scenario.FindObject(linked) is { } found ? MapLayers.Of(found) : MapLayer.Heroes;
        if (target && !Editor.Layers.IsEditable(layer))
        {
            parts.Say($"{link.Text}: shown, not selected, as its layer is hidden or locked");
        }
        else if (link.Object is { } id && Editor.Scenario.FindObject(id) is { } mapObject)
        {
            Editor.Selecting.Set(new SelectionItems([], [id], [], []));
            parts.ToolContext.Inspect(mapObject);
        }
        else if (link.Hero is { } hero && Editor.Scenario.Heroes.Any(h => h.Id == hero))
        {
            Editor.Selecting.Set(new SelectionItems([], [], [hero], []));
            parts.ToolContext.Inspect(hero);
        }
        else
        {
            parts.Say(link.Text);
        }
    }

    /// <summary>An event link (inspector, validation): the "Events" tab with that event open.</summary>
    public void OpenEvent(string name)
    {
        Workspace.ShowTab(parts.Events);
        parts.Events.Select(name);
    }

    /// <summary>The tiles of the shown level under the window's corners: what is in view (the whole level when a corner looks past the map's plane).</summary>
    public TileArea VisibleTiles()
    {
        var map = Editor.Scenario.Map;
        var whole = new TileArea(0, 0, map.Width - 1, map.Height - 1, Context.Level);
        var size = parts.Root.GetViewport().GetVisibleRect().Size;
        var corners = new[] { Vector2.Zero, new Vector2(size.X, 0), new Vector2(0, size.Y), size }
            .Select(c => MapSpace.PickTile(Camera, c, map, Context.Level))
            .ToList();
        return corners.Any(c => c is null)
            ? whole
            : new TileArea(
                Math.Max(0, corners.Min(c => c!.Value.X)), Math.Max(0, corners.Min(c => c!.Value.Y)),
                Math.Min(map.Width - 1, corners.Max(c => c!.Value.X)), Math.Min(map.Height - 1, corners.Max(c => c!.Value.Y)), Context.Level);
    }

    /// <summary>The camera on the middle of the shown level, framed in the map view.</summary>
    public void CentreLevel()
    {
        var map = Editor.Scenario.Map;
        Camera.Centre(MapSpace.ToWorld(new MapPosition(map.Width / 2, map.Height / 2, Context.Level)), Workspace.MapView.GetGlobalRect());
    }

    /// <summary>The next level of the map, with the camera on its middle.</summary>
    private void SwitchLevel()
    {
        Context.SetLevel((Context.Level + 1) % Editor.Scenario.Map.Levels);
        parts.Tools.Tool.Refresh();
        CentreLevel();
    }

    /// <summary>The view shows <paramref name="level"/>: the tool in use follows when it changes.</summary>
    private void ShowLevel(int level)
    {
        if (Context.Level != level)
        {
            Context.SetLevel(level);
            parts.Tools.Tool.Refresh();
        }
    }

    /// <summary>F (ADR-260): the camera frames the selection, on the shown level if it has cells there, else on the level of its first.</summary>
    private void FocusSelection()
    {
        var cells = Editor.Selection.Cells(Editor.Scenario);
        if (cells.Count == 0)
        {
            return;
        }

        var level = cells.Any(c => c.Level == Context.Level) ? Context.Level : cells[0].Level;
        var shown = cells.Where(c => c.Level == level).ToList();
        ShowLevel(level);
        var ground = shown.Average(c => MapSpace.ToGround(Editor.Scenario.Map, c).Y); // raised ground shows nearer: framed at its height
        Camera.Frame(
            MapSpace.ToWorld(new MapPosition(shown.Min(c => c.X), shown.Min(c => c.Y), level)) with { Y = ground },
            MapSpace.ToWorld(new MapPosition(shown.Max(c => c.X), shown.Max(c => c.Y), level)) with { Y = ground }, Workspace.MapView.GetGlobalRect());
    }

    /// <summary>Home (ADR-260): the camera frames the whole shown level.</summary>
    private void ShowWholeMap()
    {
        var map = Editor.Scenario.Map;
        Camera.Frame(
            MapSpace.ToWorld(new MapPosition(0, 0, Context.Level)), MapSpace.ToWorld(new MapPosition(map.Width - 1, map.Height - 1, Context.Level)),
            Workspace.MapView.GetGlobalRect());
    }

    /// <summary>Ctrl+1 … 9 (ADR-260): the view and the shown level go into bookmark <paramref name="slot"/>.</summary>
    private void StoreBookmark(int slot)
    {
        parts.Bookmarks.Store(slot, new CameraBookmark(Camera.View, Context.Level));
        parts.Commands.NotifyStateChanged();
        parts.Say($"Stored the view as bookmark {slot}; {slot} returns to it");
    }

    /// <summary>1 … 9 (ADR-260): the view of bookmark <paramref name="slot"/> again, on its level.</summary>
    private void GoToBookmark(int slot)
    {
        if (parts.Bookmarks[slot] is not { } bookmark)
        {
            return;
        }

        if (bookmark.Level >= Editor.Scenario.Map.Levels)
        {
            parts.Say($"Bookmark {slot} shows a level the map no longer has");
            return;
        }

        ShowLevel(bookmark.Level);
        Camera.Show(bookmark.View);
    }

    /// <summary>What stores bookmark <paramref name="n"/>, as a hint says it: its key (the user may rebind it, ADR-275).</summary>
    private string StoreBookmarkKey(int n) =>
        parts.Commands[$"view.store-bookmark-{n}"].Shortcut is { } key ? $"{key.Text} stores" : $"Store bookmark {n} stores";
}
