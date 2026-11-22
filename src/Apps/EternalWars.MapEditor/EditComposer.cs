using EternalWars.Core.Data.Stamps;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.UI;
using EternalWars.Godot.UI.Editor;
using EternalWars.Godot.UI.Editor.Shell;
using EternalWars.Godot.UI.Editor.Tools;
using Godot;

namespace EternalWars.MapEditor;

/// <summary>
/// The Edit menu (ADR-288): Undo and Redo on the tab shown (ADR-268), the selection commands on the tab shown (ADR-271: the map's
/// selection and clipboard, or the template graph's zones), the stamps, and the browser's choice taken by the Object tool.
/// </summary>
internal sealed class EditComposer(EditorParts parts)
{
    /// <summary>The history the Undo and Redo commands work on: the template's on the Template tab, else the map's.</summary>
    private IUndoSession Undoable => parts.OnTemplateTab ? parts.Templates : parts.Editor;

    private MapEditorSession Editor => parts.Editor;

    private ToolPalette Tools => parts.Tools;

    public void Register()
    {
        parts.Command("edit.undo", "Undo", () => Undoable.Undo(), UiIcon.Undo, new(Key.Z, Ctrl: true), () => WhyNoHistory(Undoable.History.CanUndo, "undo"));
        parts.Command("edit.redo", "Redo", () => Undoable.Redo(), UiIcon.Redo, new(Key.Y, Ctrl: true), () => WhyNoHistory(Undoable.History.CanRedo, "redo"));
        parts.Command("edit.select-all", "Select all", () => parts.OnTab(parts.TemplatePanel.SelectAll, () =>
            {
                Editor.Selecting.Set(Editor.Selecting.Everything(parts.Context.Level, parts.ToolContext.Filter));
                parts.Say(EditorToolContext.Describe(Editor.Selection));
            }), shortcut: new(Key.A, Ctrl: true), whyNot: () => parts.OnTemplateTab ? null : parts.OnMapTab(), icon: UiIcon.SelectAll);
        parts.Command("edit.deselect", "Deselect", () => parts.OnTab(parts.TemplatePanel.Deselect, Deselect), shortcut: new(Key.Escape),
            whyNot: () => parts.OnTemplateTab ? parts.TemplatePanel.WhyNothingSelected()
                : parts.OnMapTab() ?? (Editor.Selection.IsEmpty && Tools.Tool is SelectTool ? "Nothing is selected" : null), icon: UiIcon.Deselect);
        parts.Command("edit.cut", "Cut", () => parts.OnTab(() => parts.Say(parts.TemplatePanel.Copy(cut: true)), () => Copy(cut: true)), shortcut: new(Key.X, Ctrl: true),
            whyNot: () => parts.OnTemplateTab ? parts.TemplatePanel.WhyNoZones() : parts.OnMapTab() ?? NothingToCopy(), icon: UiIcon.Cut);
        parts.Command("edit.copy", "Copy", () => parts.OnTab(() => parts.Say(parts.TemplatePanel.Copy(cut: false)), () => Copy(cut: false)), shortcut: new(Key.C, Ctrl: true),
            whyNot: () => parts.OnTemplateTab ? parts.TemplatePanel.WhyNoZones() : parts.OnMapTab() ?? NothingToCopy(), icon: UiIcon.Copy);
        parts.Command("edit.paste", "Paste", () => parts.OnTab(() => parts.Say(parts.TemplatePanel.Paste()), () => Paste(duplicate: false)), shortcut: new(Key.V, Ctrl: true),
            whyNot: () => parts.OnTemplateTab ? (parts.TemplatePanel.Clipboard is null ? "Nothing was copied" : null)
                : parts.OnMapTab() ?? (parts.Clipboard.Fragment is null ? "Nothing was copied" : null), icon: UiIcon.Paste);
        parts.Command("edit.duplicate", "Duplicate", () => parts.OnTab(() => parts.Say(parts.TemplatePanel.Duplicate()), () => Paste(duplicate: true)), shortcut: new(Key.D, Ctrl: true),
            whyNot: () => parts.OnTemplateTab ? parts.TemplatePanel.WhyNoZones() : parts.OnMapTab() ?? NothingToCopy(), icon: UiIcon.Duplicate);
        parts.Command("edit.delete", "Delete", () => parts.OnTab(() => parts.Say(parts.TemplatePanel.Delete()), () => parts.Say($"Deleted {Editor.Selecting.Delete()} thing(s)")),
            shortcut: new(Key.Delete),
            whyNot: () => parts.OnTemplateTab ? parts.TemplatePanel.WhyNothingSelected() : parts.OnMapTab() ?? (Editor.Selection.HasThings ? null : "No object or hero is selected"),
            icon: UiIcon.Delete);
        parts.Command("edit.save-stamp", "Save selection as stamp", SaveStamp, whyNot: NothingToCopy, onMap: true, icon: UiIcon.ToolStamp);
        parts.Command("edit.rotate", "Rotate scenery", () => Turn(mirror: false), shortcut: new(Key.R, Ctrl: true), whyNot: NoScenery, onMap: true, icon: UiIcon.Rotate);
        parts.Command("edit.mirror", "Mirror scenery", () => Turn(mirror: true), shortcut: new(Key.M, Ctrl: true), whyNot: NoScenery, onMap: true, icon: UiIcon.Mirror);
    }

    /// <summary>Why Rotate and Mirror cannot run: only scenery turns, and none is selected.</summary>
    public string? NoScenery() =>
        Editor.Selection.Objects.Any(id => Editor.Scenario.FindObject(id) is Scenery) ? null : "No scenery is selected";

    /// <summary>A stamp picked in the browser: the Paste tool takes it, and every click puts it on the map.</summary>
    public void PasteStamp(string name)
    {
        MapFragment fragment;
        try
        {
            fragment = StampFolder.Load(EditorFolders.Stamps, name, Editor.Scenario);
        }
        catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException)
        {
            parts.Say($"Could not read stamp {name}: {e.Message}");
            return;
        }

        var tool = Tools.Tools.OfType<PasteTool>().First();
        if (Tools.Tool != tool)
        {
            Tools.Use(Tools.Tools.OfType<SelectTool>().First()); // the tool in use lets go; the Paste tool is told its fragment before it comes in
        }

        tool.UseFragment(fragment);
        Tools.Use(tool);
        tool.Hover(parts.Context.Pointed);
        parts.Say($"Stamp {name}: click where it goes; Escape ends");
    }

    /// <summary>Something was chosen in the browser: the Object tool comes in and shows it.</summary>
    public void UseChoice()
    {
        var tool = Tools.Tools.OfType<ObjectTool>().First();
        Tools.Use(tool);
        Tools.RefreshOptions();
        tool.Hover(parts.Context.Pointed);
        tool.Refresh();
        if (parts.Choice.Entry is { } entry)
        {
            parts.Say($"{entry.Name}: click the map to place it");
        }
    }

    /// <summary>Why Undo or Redo cannot run: nothing to <paramref name="what"/>.</summary>
    private static string? WhyNoHistory(bool can, string what) => can ? null : $"Nothing to {what}";

    /// <summary>Why Copy, Cut and Duplicate cannot run: a fragment holds tiles and objects, and none is selected.</summary>
    private string? NothingToCopy() =>
        Editor.Selection.Tiles.Count + Editor.Selection.Objects.Count > 0 ? null : "No tile and no object is selected";

    /// <summary>Rotate or Mirror: the selected scenery turns a quarter turn clockwise, or is mirrored left to right.</summary>
    private void Turn(bool mirror)
    {
        var pieces = Editor.Selection.Objects.Count(id => Editor.Scenario.FindObject(id) is Scenery);
        parts.Say((mirror ? Editor.Selecting.Mirror() : Editor.Selecting.Rotate()) switch
        {
            EditResult.Ok => $"{(mirror ? "Mirrored" : "Rotated")} {pieces} piece(s) of scenery",
            EditResult.Overlaps => "No room: turned, a piece would lie on another object or a hero",
            EditResult.OutOfBounds => "No room: turned, a piece would reach off the map",
            var refused => $"Refused: {refused}",
        });
    }

    /// <summary>Copy or Cut: the selection goes to the clipboard as a fragment.</summary>
    private void Copy(bool cut)
    {
        if ((cut ? Editor.Fragments.Cut() : Editor.Fragments.Copy()) is not { } fragment)
        {
            return;
        }

        parts.Clipboard.Fragment = fragment;
        parts.Commands.NotifyStateChanged();
        Tools.Tool.Refresh();
        parts.Say($"{(cut && fragment.Objects.Count > 0 ? "Cut" : "Copied")} {fragment.Tiles.Count} tile(s) and {fragment.Objects.Count} object(s); {(parts.Commands["edit.paste"].Shortcut is { } paste ? $"{paste.Text} pastes" : "Paste pastes")}");
    }

    /// <summary>Paste or Duplicate: the Paste tool takes the clipboard or a copy of the selection, and every click puts it on the map.</summary>
    private void Paste(bool duplicate)
    {
        var tool = Tools.Tools.OfType<PasteTool>().First();
        Tools.Use(tool); // first: leaving the tool that was in use lets go of what it was doing
        if (duplicate)
        {
            tool.UseSelection();
        }
        else
        {
            tool.UseClipboard();
        }

        tool.Hover(parts.Context.Pointed); // the ghost shows at once, where the pointer is
        parts.Say("Click where it goes; Escape ends");
    }

    /// <summary>
    /// Escape, "drop the selection or the tool" (ADR-290): first what the tool is in the middle of (a gate picked, a route started), then
    /// pasting back to the Select tool, then the selection, and with nothing selected the tool, back to Select.
    /// </summary>
    private void Deselect()
    {
        var select = Tools.Tools.OfType<SelectTool>().First();
        if (Tools.Tool.IsBusy)
        {
            Tools.Tool.Abort();
        }
        else if (Tools.Tool is PasteTool || Editor.Selection.IsEmpty)
        {
            Tools.Pick(select);
        }
        else
        {
            Editor.Selecting.Clear();
        }
    }

    /// <summary>"Save selection as stamp": asks for a name, then writes the selection's copy to the stamps folder.</summary>
    private void SaveStamp()
    {
        if (parts.IsBusy || Editor.Fragments.Copy() is not { } fragment)
        {
            return;
        }

        var folder = EditorFolders.Stamps;
        var box = new NameBox { Name = "StampName" };
        parts.Overlays.AddChild(box);
        var taken = StampFolder.Names(folder).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var free = Enumerable.Range(1, taken.Count + 1).Select(n => $"Stamp {n}").First(n => !taken.Contains(n));
        box.Bind("Save selection as stamp", free, name =>
        {
            try
            {
                var replaced = File.Exists(StampFolder.PathOf(folder, name));
                var path = StampFolder.Save(folder, name, fragment);
                parts.Say($"{(replaced ? "Replaced" : "Saved")} stamp {Path.GetFileName(path)}; the browser lists it under Stamps");
                parts.Browser.RefreshStamps();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                parts.Say($"Could not save the stamp: {e.Message}");
            }
        });
    }
}
