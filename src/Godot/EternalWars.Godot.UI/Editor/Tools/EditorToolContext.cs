using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Map;

namespace EternalWars.Godot.UI.Editor.Tools;

/// <summary>The editor's clipboard (EDIT-4, ADR-250): the fragment copied or cut last. The app keeps one for all the maps it opens.</summary>
public sealed class EditorClipboard
{
    public MapFragment? Fragment { get; set; }
}

/// <summary>
/// What is chosen in the object browser (EDIT-1, ADR-253): the browser sets it, the Object tool places it and says so, which fills the
/// browser's Recent list. The app keeps one, so the choice stays when another map is opened.
/// </summary>
public sealed class BrowserChoice
{
    /// <summary>The chosen entry; null: nothing yet.</summary>
    public BrowserEntry? Entry { get; private set; }

    /// <summary>Another entry was chosen.</summary>
    public event Action? Changed;

    /// <summary>The entry was put on the map.</summary>
    public event Action<BrowserEntry>? Placed;

    public void Choose(BrowserEntry? entry)
    {
        Entry = entry;
        Changed?.Invoke();
    }

    /// <summary>
    /// Another map was opened: the choice becomes <paramref name="entry"/>, the same thing in that map's catalog. Nobody is told, since
    /// the user chose nothing; the new map's browser and tools read it when they are made.
    /// </summary>
    public void Rebase(BrowserEntry? entry) => Entry = entry;

    public void NotePlaced(BrowserEntry entry) => Placed?.Invoke(entry);
}

/// <summary>How the button went down for the press a tool is told of: the app sets it from the pointer's event before every press.</summary>
public readonly record struct PointerPress(bool Add = false, bool Remove = false, bool DoubleClick = false);

/// <summary>
/// What the editor's tools work with: the session they edit, the inspector they open and the status line they write to; for the
/// selecting tools also the marks on the map (<see cref="Marks"/>), how the last press was made (<see cref="Pointer"/>) and what is in
/// view (<see cref="VisibleArea"/>).
/// </summary>
public sealed class EditorToolContext(MapEditorSession editor, InspectorPanel inspector, Action<string> status)
{
    public MapEditorSession Editor { get; } = editor;

    /// <summary>The view that shows the selection, for the marquee and the ghost of a move; null: nothing shows them.</summary>
    public SelectionView? Marks { get; set; }

    /// <summary>The view that draws a route by day, for the Measure tool; null: nothing draws it.</summary>
    public PathPreview? Route { get; set; }

    /// <summary>The tiles of the shown level that are in view now; the whole level when the app does not say.</summary>
    public Func<TileArea> VisibleArea { get; init; } =
        () => new TileArea(0, 0, editor.Scenario.Map.Width - 1, editor.Scenario.Map.Height - 1, 0);

    /// <summary>Shift (add), Alt (remove) and double click of the press the tool is being told of.</summary>
    public PointerPress Pointer { get; set; }

    /// <summary>Which kinds of things clicks, rectangles and Select all pick: one setting for the selecting tools.</summary>
    public SelectionFilter Filter { get; set; } = SelectionFilter.All;

    /// <summary>What is selected, for the status line: "3 objects, 1 hero selected".</summary>
    public static string Describe(MapSelection selection)
    {
        var parts = new[]
        {
            Count(selection.Objects.Count, "object"), Count(selection.Heroes.Count, "hero", "heroes"),
            Count(selection.Events.Count, "event"), Count(selection.Tiles.Count, "tile"),
        }.Where(p => p.Length > 0).ToList();
        return parts.Count == 0 ? "Nothing selected" : $"{string.Join(", ", parts)} selected";

        static string Count(int count, string one, string? many = null) => count == 0 ? "" : count == 1 ? $"1 {one}" : $"{count} {many ?? one + "s"}";
    }

    /// <summary>The height brushes' radius in tiles.</summary>
    public int Radius { get; set; }

    /// <summary>The paints' brush (terrain, roads, rivers, start zones, erase): one size and shape for all of them.</summary>
    public Brush Brush { get; set; } = Brush.One;

    /// <summary>What was copied or cut last; the app's, so it outlives the map (EDIT-4).</summary>
    public EditorClipboard Clipboard { get; init; } = new();

    /// <summary>What the object browser lists for this map: the Object tool's random variants come from it (ADR-286). Null: none.</summary>
    public BrowserCatalog? Browser { get; set; }

    /// <summary>What the object browser has chosen for the Object tool; the app's, so it outlives the map.</summary>
    public BrowserChoice Choice { get; init; } = new();

    /// <summary>Which layers a paste puts on the map.</summary>
    public FragmentLayers PasteLayers { get; set; } = FragmentLayers.All;

    /// <summary>Writes <paramref name="text"/> to the status line.</summary>
    public void Say(string text) => status(text);

    /// <summary>Clears the status line when the edit was made, else says why it was refused. True when it was made.</summary>
    public bool Report(EditResult result)
    {
        status(result == EditResult.Ok ? "" : $"Refused: {result}");
        return result == EditResult.Ok;
    }

    public void Inspect(HeroId hero)
    {
        inspector.InspectHero(hero);
        status($"Inspecting {Editor.Scenario.GetHero(hero).Name}");
    }

    public void Inspect(MapObject mapObject)
    {
        inspector.InspectObject(mapObject.Id);
        status($"Inspecting {mapObject}");
    }

    /// <summary>Several objects in the inspector: of one kind they are edited together (ADR-255). The status line stays as it is.</summary>
    public void Inspect(IReadOnlyCollection<MapObjectId> objects) => inspector.InspectObjects(objects);
}
