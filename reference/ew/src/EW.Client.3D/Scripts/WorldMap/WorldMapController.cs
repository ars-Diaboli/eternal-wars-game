using EW.Scripts.Interface;
using Godot;

namespace EW.Scripts.WorldMap;

public partial class WorldMapController : Node
{
    private const string TownScreenScenePath = "res://Scenes/town_screen.tscn";
    private TownScreen _townScreen;

    public override void _Ready()
    {
        EnsureTownScreen();
    }

    public void OpenTown(Town town)
    {
        if (town == null || !IsInstanceValid(town))
        {
            return;
        }

        EnsureTownScreen();
        _townScreen?.ShowTown(town);
    }

    public bool IsTownScreenOpen()
    {
        return _townScreen != null && _townScreen.Visible;
    }

    private void EnsureTownScreen()
    {
        if (_townScreen != null && IsInstanceValid(_townScreen))
        {
            return;
        }

        Node sceneRoot = GetTree().CurrentScene ?? GetParent();
        CanvasLayer canvasLayer = sceneRoot?.GetNodeOrNull<CanvasLayer>("CanvasLayer");
        if (canvasLayer == null)
        {
            return;
        }

        _townScreen = canvasLayer.GetNodeOrNull<TownScreen>("TownScreen");
        if (_townScreen != null)
        {
            return;
        }

        PackedScene townScreenScene = GD.Load<PackedScene>(TownScreenScenePath);
        if (townScreenScene == null)
        {
            GD.PushWarning($"{Name}: Failed to load town screen scene at {TownScreenScenePath}.");
            return;
        }

        _townScreen = townScreenScene.Instantiate<TownScreen>();
        canvasLayer.AddChild(_townScreen);
    }
}
