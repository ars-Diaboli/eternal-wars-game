using EW.Scripts.DataTypes;
using EW.Scripts.WorldMap;

namespace EW.Scripts;

using Godot;
using System.Collections.Generic;

public partial class FogOfWarTextureSystem : Node
{
    [Export] public WorldMapSettings WorldMap { get; set; }
    [Export] public int VisionRadius { get; set; } = 8;

    private SubViewport _subViewport;
    private TextureRect _canvas;
    
    private Image _fogImage;
    private ImageTexture _fogTexture;
    private HashSet<Vector2I> _revealedCells = new();
    private List<IWorldMapObject> _registeredObjects = new();

    private int MapWidth => WorldMap?.WidthInTiles ?? 64;
    private int MapHeight => WorldMap?.HeightInTiles ?? 64;

    public override void _Ready()
    {
        WorldMap ??= GetTree().CurrentScene.GetNodeOrNull<WorldMapSettings>("WorldMapSettings");

        _subViewport = GetNode<SubViewport>("FogViewport");
        _canvas = _subViewport.GetNode<TextureRect>("FogCanvas");

        // 1. Create a pure-data grayscale image buffer initialized to solid black (unrevealed)
        _fogImage = Image.CreateEmpty(MapWidth, MapHeight, false, Image.Format.L8);
        _fogImage.Fill(Colors.Black);

        _fogTexture = ImageTexture.CreateFromImage(_fogImage);
        _canvas.Texture = _fogTexture;

        // 2. Set up global variables to expose our size and map buffer directly to the GPU
        RenderingServer.GlobalShaderParameterSet("fog_map_size", new Vector2(MapWidth, MapHeight));
        
        // Pass the live view-render output buffer straight to our shader engine globally
        RenderingServer.GlobalShaderParameterSet("fog_texture_mask", _subViewport.GetTexture());
    }

    public void RevealVisionAroundPosition(Vector3 worldPosition)
    {
        if (WorldMap == null || _fogImage == null) return;

        Vector2I heroGridPos = WorldMap.WorldToTilePosition(worldPosition);
        bool changesMade = false;

        for (int x = -VisionRadius; x <= VisionRadius; x++)
        {
            for (int z = -VisionRadius; z <= VisionRadius; z++)
            {
                if ((x * x) + (z * z) <= VisionRadius * VisionRadius)
                {
                    int targetX = heroGridPos.X + x;
                    int targetZ = heroGridPos.Y + z;

                    // Bounds checking safety guard
                    if (targetX >= 0 && targetX < MapWidth && targetZ >= 0 && targetZ < MapHeight)
                    {
                        Vector2I discoveredTile = new Vector2I(targetX, targetZ);
                        
                        if (_revealedCells.Add(discoveredTile))
                        {
                            changesMade = true;
                            // Carve a pure white mark (fully explored) directly into our byte mask data stream
                            _fogImage.SetPixel(targetX, targetZ, Colors.White);
                        }
                    }
                }
            }
        }

        if (changesMade)
        {
            // Update the GPU data pipe texture surface layout directly
            _fogTexture.Update(_fogImage);
            
            // Notify polymorphic entities to check visibility against our hashset data
            foreach (var mapObject in _registeredObjects)
            {
                mapObject.OnVisionUpdated(_revealedCells.Contains(mapObject.GridPosition));
            }
        }
    }

    public bool IsCellRevealed(Vector2I mapCoord) => _revealedCells.Contains(mapCoord);
    public void RegisterObject(IWorldMapObject mapObject)
    {
        if (!_registeredObjects.Contains(mapObject))
        {
            _registeredObjects.Add(mapObject);
            mapObject.OnVisionUpdated(IsCellRevealed(mapObject.GridPosition));
        }
    }
    public void UnregisterObject(IWorldMapObject mapObject) => _registeredObjects.Remove(mapObject);
}