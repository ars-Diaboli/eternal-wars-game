namespace EW.Scripts.WorldMap;

using Godot;

public static class TerrainExtensions
{
    public static float GetTerrainHeightAt(this Node sceneContext, Vector3 worldPosition)
    {
        Node terrainNode = sceneContext.GetTree().CurrentScene.FindChild("Terrain3D", true, false);
        if (terrainNode == null) 
        {
            GD.PrintErr("TerrainExtensions: Could not find 'Terrain3D' node in the active scene tree.");
            return 0f;
        }

        GodotObject terrainData = terrainNode.Get("data").AsGodotObject();
        
        if (terrainData != null)
        {
            return terrainData.Call("get_height", worldPosition).AsSingle();
        }

        try 
        {
            return terrainNode.Call("get_height", worldPosition).AsSingle();
        }
        catch
        {
            return 0f;
        }
    }

    /// <summary>
    /// Instantly forces a 3D node's vertical Y coordinate to sit directly on top of the Terrain3D mesh.
    /// </summary>
    public static void SnapToTerrain(this Node3D node, float heightOffset = 0.0f)
    {
        Vector3 currentPos = node.GlobalPosition;
        currentPos.Y = node.GetTerrainHeightAt(currentPos) + heightOffset;
        node.GlobalPosition = currentPos;
    }
}