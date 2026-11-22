using EternalWars.Core.Domain.Scenarios;
using EternalWars.Godot.Shared.Cameras;
using EternalWars.Godot.UI.Editor;
using EternalWars.Godot.UI.Editor.Shell;
using EternalWars.Godot.UI.Editor.Tools;

namespace EternalWars.MapEditor;

/// <summary>
/// The menu bar and the toolbar (ADR-288), built from the command registry, with the lists the menus fill when they open (the recent
/// files, the bookmarks, the overlays, the shipped templates, the sample maps); and the commands that pick each tool.
/// </summary>
internal sealed class MenuComposer(EditorParts parts, FileComposer files)
{
    /// <summary>The command that picks <paramref name="tool"/>.</summary>
    public static string ToolCommand(IEditorTool tool) => $"tool.{tool.Name.ToLowerInvariant()}";

    /// <summary>A command for each tool: its key, on while the tool is in use.</summary>
    public void RegisterTools()
    {
        var tools = parts.Tools;
        foreach (var tool in tools.Tools)
        {
            parts.Commands.Register(new EditorCommand(ToolCommand(tool), tool.Name, () => tools.Pick(tool))
            {
                Icon = tool.Icon, Shortcut = tool.Shortcut is { } key ? new CommandShortcut(key) : null, WhyNot = parts.OnMapTab, IsOn = () => tools.Tool == tool, IsChoice = true,
            });
        }
    }

    public void Bind(MenuBarView menuBar, ToolbarView toolbar)
    {
        var commands = parts.Commands;
        var fileService = parts.Files;
        var templateFiles = parts.TemplateFiles;
        menuBar.Bind(commands,
        [
            new("File", ["file.new", null, "file.open", ">Open recent", null, "file.save", "file.save-as", null, "file.export-report", "file.export-picture", null, "file.quit"])
            {
                Lists =
                [
                    new("Open recent", () => [.. fileService.Recent.Select(path => (path, (Action)(() => fileService.Open(path))))], "No file was opened or saved yet"),
                ],
            },
            new("Edit", ["edit.undo", "edit.redo", null, "edit.cut", "edit.copy", "edit.paste", "edit.duplicate", "edit.delete", "edit.save-stamp", null, "edit.rotate", "edit.mirror", null, "edit.select-all", "edit.deselect", "edit.find", null, "edit.preferences"]),
            new("View", ["view.palette", null, "view.map-tab", "templates.editor", null, "view.level", "view.ghost-level", "view.grid", null, "view.top-down",
                "view.focus", "view.whole-map", "view.go-to", ">Bookmarks", ">Store bookmark", null, ">Overlay", null, "view.left", "view.bottom", "view.right", "view.panels", null, "view.inspector", "view.layers", "view.overlays", "view.fairness", "view.problems", "view.history", "view.output"])
            {
                Lists =
                [
                    new("Bookmarks", () => parts.OnMapTab() is not null ? [] : [.. parts.Bookmarks.Stored.Select(n => (parts.KeyHint($"Bookmark {n}", $"view.bookmark-{n}"), (Action)(() => commands.Run($"view.bookmark-{n}"))))],
                        "No view is stored yet, or not on the Map tab: Store bookmark (View menu, or its key) stores one"),
                    new("Store bookmark", () => parts.OnMapTab() is not null ? [] : [.. Enumerable.Range(CameraBookmarks.First, CameraBookmarks.Last - CameraBookmarks.First + 1)
                        .Select(n => (parts.KeyHint($"Bookmark {n}", $"view.store-bookmark-{n}"), (Action)(() => commands.Run($"view.store-bookmark-{n}"))))],
                        "Only on the Map tab"),
                    new("Overlay", () => [.. OverlaysPanel.Names.Select(o => (o.Text, (Action)(() => parts.OverlayPanel.Choose(o.Kind))))], "No overlay"),
                ],
            },
            new("Map", ["map.settings", "map.general", "map.players", "map.conditions", "map.bans", "map.week-events", "map.rumours", "map.texts", null, "map.events", null, "map.resize", "map.crop", null, "view.problems", "view.fairness"]),
            new("Tools", [.. parts.Tools.Tools.Select(ToolCommand)]),
            new("Templates", ["templates.editor", null, "template.new", "template.open", ">Recent templates", ">Shipped templates", null, "template.save", "template.save-as"])
            {
                Lists =
                [
                    new("Recent templates", () => [.. templateFiles.Recent.Select(path => (path, (Action)(() => files.OpenTemplate(() => templateFiles.Open(path)))))],
                        "No template file was opened or saved yet"),
                    new("Shipped templates", () => [.. templateFiles.Shipped.Select(t => (t.Name, (Action)(() => files.OpenTemplate(() => templateFiles.OpenShipped(t)))))], "None"),
                ],
            },
            new("Test", ["test.play"]),
            new("Help", ["help.help", null, ">Sample maps"])
            {
                Lists = [new("Sample maps", () => [.. SampleMaps.All.Select(m => (m.Name, (Action)(() => files.OpenSample(m))))], "None")],
            },
        ]);
        toolbar.Bind(commands,
        [
            "file.new", "templates.editor", "file.open", "file.save", null,
            "edit.undo", "edit.redo", null, "view.level", "view.grid", null, "view.left", "view.bottom", "view.right", null, "test.play",
        ]);
    }
}
