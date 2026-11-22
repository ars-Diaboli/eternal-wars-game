using EternalWars.Godot.UI;
using EternalWars.Godot.UI.Editor;
using EternalWars.Godot.UI.Editor.Shell;
using Godot;

namespace EternalWars.MapEditor;

/// <summary>The Map menu (ADR-288): the map settings window at each of its tabs (ADR-256), Resize and Crop to the selection.</summary>
internal sealed class MapComposer(EditorParts parts)
{
    public void Register()
    {
        parts.Command("map.settings", "Map settings", () => OpenSettings(null), UiIcon.Settings, new(Key.F7));
        parts.Command("map.general", "Name, difficulty and arena", () => OpenSettings(MapSettingsTab.General), UiIcon.Map);
        parts.Command("map.players", "Players and teams", () => OpenSettings(MapSettingsTab.Players), UiIcon.Players);
        parts.Command("map.conditions", "Victory and loss", () => OpenSettings(MapSettingsTab.VictoryAndLoss), UiIcon.Conditions);
        parts.Command("map.bans", "Bans", () => OpenSettings(MapSettingsTab.Bans), UiIcon.Ban);
        parts.Command("map.week-events", "Week events", () => OpenSettings(MapSettingsTab.WeekEvents), UiIcon.WeekEvents);
        parts.Command("map.rumours", "Rumours", () => OpenSettings(MapSettingsTab.Rumours), UiIcon.Rumours);
        parts.Command("map.texts", "Texts", () => OpenSettings(MapSettingsTab.Texts), UiIcon.Texts);
        parts.Command("map.resize", "Resize map", OpenResize, onMap: true, icon: UiIcon.Resize);
        parts.Command("map.crop", "Crop to selection", Crop,
            whyNot: () => parts.Editor.Selection.Tiles.Count > 0 ? null : "No tile is selected", onMap: true, icon: UiIcon.Crop);
    }

    private void OpenResize()
    {
        if (parts.IsBusy)
        {
            return;
        }

        var window = new ResizeWindow { Name = "Resize" };
        parts.Overlays.AddChild(window);
        window.Bind(parts.Editor, parts.Say);
    }

    /// <summary>The map settings window (ADR-256) at <paramref name="tab"/>, else at the tab shown last.</summary>
    private void OpenSettings(MapSettingsTab? tab)
    {
        if (parts.IsBusy)
        {
            return;
        }

        parts.Workspace.ShowPage(EditorWorkspace.MapPage); // its Undo and Redo change the map, so the map shows
        var window = new MapSettingsWindow { Name = "MapSettings" };
        parts.Overlays.AddChild(window);
        window.Bind(parts.Editor, tab);
    }

    /// <summary>Crop to the selection: at once when nothing would be lost, else after the window's question.</summary>
    private void Crop()
    {
        if (!parts.IsBusy && ResizeWindow.Crop(parts.Editor, parts.Say) is { } question)
        {
            parts.Overlays.AddChild(question);
        }
    }
}
