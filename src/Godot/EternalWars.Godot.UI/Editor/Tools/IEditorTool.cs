using EternalWars.Core.Domain.Common;
using Godot;

namespace EternalWars.Godot.UI.Editor.Tools;

/// <summary>
/// One tool of the map editor (EDIT-1, ADR-250). The app only routes the pointer to the tool in use: every edit the tool makes goes through
/// the editor session, and what it has to say goes to its <see cref="EditorToolContext"/>.
/// </summary>
public interface IEditorTool
{
    /// <summary>The tool's name on its button.</summary>
    string Name { get; }

    /// <summary>The tool's icon on its button and in the Tools menu; null: a placeholder.</summary>
    UiIcon? Icon { get; }

    /// <summary>The key that picks the tool; null: none.</summary>
    Key? Shortcut { get; }

    /// <summary>What a click or a drag on the map does with the tool, as the status bar says it when the tool is picked.</summary>
    string ClickHint { get; }

    /// <summary>The tool was picked.</summary>
    void Activate();

    /// <summary>The pointer came over <paramref name="tile"/> with no button held (null: off the map).</summary>
    void Hover(MapPosition? tile);

    /// <summary>The left button went down on <paramref name="tile"/>.</summary>
    void Press(MapPosition tile);

    /// <summary>The pointer is over <paramref name="tile"/> with the button held since <see cref="Press"/>.</summary>
    void Drag(MapPosition tile);

    /// <summary>The button held since <see cref="Press"/> came up.</summary>
    void Release();

    /// <summary>The map, the clipboard or the shown level changed under the tool: what it shows before a click is worked out again.</summary>
    void Refresh();

    /// <summary>Drops what the tool was in the middle of (another tool was picked).</summary>
    void Cancel();

    /// <summary>Whether the tool is in the middle of something Escape drops first (a gate picked, a route started; ADR-290).</summary>
    bool IsBusy { get; }

    /// <summary>Escape while <see cref="IsBusy"/>: drops what the tool is in the middle of; the tool stays picked.</summary>
    void Abort();

    /// <summary>Fills the palette's options with the tool's own.</summary>
    void BuildOptions(ToolOptions options);
}
