namespace EternalWars.Godot.UI.Editor.Tools;

/// <summary>The editor's tools, in the palette's order.</summary>
public static class EditorTools
{
    public static IReadOnlyList<IEditorTool> All(EditorToolContext context) =>
    [
        new TerrainTool(context),
        new RoadTool(context),
        new RiverTool(context),
        new StartZoneTool(context),
        new HeightTool(context),
        new ObjectTool(context),
        new MonsterTool(context),
        new HeroTool(context),
        new EraseTool(context),
        new LinkGatesTool(context),
        new SelectTool(context),
        new MoveTool(context),
        new PasteTool(context),
        new GrailTool(context),
    ];
}
