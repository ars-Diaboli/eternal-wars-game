using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Godot.UI.Editor.Tools;

/// <summary>What the editor's tools work with: the session they edit, the inspector they open and the status line they write to.</summary>
public sealed class EditorToolContext(MapEditorSession editor, InspectorPanel inspector, Action<string> status)
{
    public MapEditorSession Editor { get; } = editor;

    /// <summary>The brushes' radius in tiles: one value for all of them.</summary>
    public int Radius { get; set; }

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
}
