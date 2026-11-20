using EternalWars.Core.Domain.Common;

namespace EternalWars.Godot.UI.Editor.Tools;

/// <summary>
/// One tool of the map editor (EDIT-1, ADR-250). The app only routes the pointer to the tool in use: every edit the tool makes goes through
/// the editor session, and what it has to say goes to its <see cref="EditorToolContext"/>.
/// </summary>
public interface IEditorTool
{
    /// <summary>The tool's name on its button.</summary>
    string Name { get; }

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

    /// <summary>Drops what the tool was in the middle of (another tool was picked).</summary>
    void Cancel();

    /// <summary>Fills the palette's options with the tool's own.</summary>
    void BuildOptions(ToolOptions options);
}
