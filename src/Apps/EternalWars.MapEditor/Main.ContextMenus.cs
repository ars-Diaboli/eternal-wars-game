using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.UI;
using EternalWars.Godot.UI.Editor.Shell;
using EternalWars.Godot.UI.Editor.Tools;
using Godot;

namespace EternalWars.MapEditor;

/// <summary>
/// The context menus (ADR-286): a right click on the map, a selection, a browser entry or a panel's header offers the commands that apply
/// there, from the registry like the menu bar. Which commands each one lists is composed here.
/// </summary>
public partial class Main
{
    /// <summary>The command that shows each panel that has one, for the panels' context menus.</summary>
    private Dictionary<Control, string> _panelCommands = [];

    /// <summary>Registers the context menus' own commands and listens for the right clicks of the browser and the panels.</summary>
    private void BindContextMenus(Dictionary<Control, string> panelCommands)
    {
        _panelCommands = panelCommands;
        Command("browser.favourite", "Favourite", _browser.ToggleChosenFavourite, isOn: () => _browser.ChosenIsFavourite,
            whyNot: () => _choice.Entry is null ? "Nothing is chosen in the browser" : null, icon: UiIcon.Favourite);
        Command("browser.find", "Find on the map", () => OpenGoTo(find: true, FindText(_choice.Entry!)),
            whyNot: () => _choice.Entry is null ? "Nothing is chosen in the browser" : null, onMap: true, icon: UiIcon.Find);
        _browser.EntryMenuRequested += (_, at) => OpenContextMenu(["tool.object", null, "browser.favourite", "browser.find"], at);
        _workspace.PanelMenuRequested += (region, panel, at) => OpenContextMenu(PanelMenu(region, panel), at);
    }

    /// <summary>
    /// What Find looks for to list an entry's objects: its kind's name ("Gold mine"; for a monster, dwelling or artifact entry the kind they
    /// share, so every one of it), the hero's for the hero.
    /// </summary>
    private static string FindText(BrowserEntry entry) => entry.Definition?.Name ?? entry.Name;

    /// <summary>
    /// A right click on <paramref name="tile"/>: a hero or object there that is not selected becomes the selection (as a click of Select
    /// would), then the menu for what is selected, or for the map when nothing is.
    /// </summary>
    private void OpenMapMenu(MapPosition tile, Vector2 at)
    {
        if (_editor.Selecting.PickUnlessSelected(tile, _toolContext.Filter))
        {
            Say(EditorToolContext.Describe(_editor.Selection));
        }

        OpenContextMenu(MapMenu(), at);
    }

    /// <summary>The map's context menu: for the things selected, for the tiles selected, or for the map.</summary>
    private IReadOnlyList<string?> MapMenu()
    {
        var selection = _editor.Selection;
        if (selection.HasThings)
        {
            return
            [
                "view.inspector", null, "edit.cut", "edit.copy", "edit.paste", "edit.duplicate", "edit.delete", null,
                .. NoScenery() is null ? new[] { "edit.rotate", "edit.mirror" } : [], "edit.save-stamp", null, "view.focus", "edit.deselect",
            ];
        }

        if (selection.Tiles.Count > 0)
        {
            return ["edit.cut", "edit.copy", "edit.paste", null, "map.crop", "edit.save-stamp", null, "view.focus", "edit.deselect"];
        }

        return
        [
            "edit.paste", "edit.select-all", null, "view.go-to", "edit.find", "view.whole-map", "view.level", null, "view.grid", "view.top-down",
            null, "map.settings", "test.play",
        ];
    }

    /// <summary>
    /// A panel's context menu: the region's other panels, what the panel itself works with (the history: undo and redo), and showing or
    /// hiding the region and all of them.
    /// </summary>
    private IReadOnlyList<string?> PanelMenu(EditorRegion region, Control panel)
    {
        string[] own = _panelCommands.GetValueOrDefault(panel) == "view.history" ? ["edit.undo", "edit.redo"] : [];
        var toggle = region switch
        {
            EditorRegion.Left => "view.left",
            EditorRegion.Bottom => "view.bottom",
            _ => "view.right",
        };
        return
        [
            .. _workspace.PanelsOf(region).Where(p => p != panel && _panelCommands.ContainsKey(p)).Select(p => _panelCommands[p]), null,
            .. own, null, toggle, "view.panels",
        ];
    }

    /// <summary>
    /// Opens a context menu of <paramref name="items"/> at <paramref name="at"/> (the pointer, in the viewport); nothing while a file question,
    /// a window or a stroke is open.
    /// </summary>
    private void OpenContextMenu(IReadOnlyList<string?> items, Vector2 at)
    {
        if (!IsBusy && !_pressed)
        {
            CommandPopup.Open(this, _commands, items, at); // not an overlay: those make the editor busy
        }
    }
}
