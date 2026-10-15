using Godot;
using System.Collections.Generic;
using System.IO;
using EW.CoreLib;
using EW.CoreLib.Events;
using EW.CoreLib.Map;
using EW.CoreLib.Messaging;
using EW.CoreLib.Tests;
using EW.CoreLib.Types;

public partial class GameController : Node
{
   private const string SaveDirectoryPath = "user://saves";
   private const string SaveFileName = "terrain2d_scenario.json";

   [Export]
   public TerrainGrid TerrainGrid { get; set; } = null!;

   [Export]
   public Camera2D Camera { get; set; } = null!;

   [Export]
   public Button SaveButton { get; set; } = null!;

   [Export]
   public Button LoadButton { get; set; } = null!;

   [Export]
   public Button EndTurnButton { get; set; } = null!;

   [Export]
   public float CameraMoveSpeed { get; set; } = 600f;

   [Export]
   public float ZoomStep { get; set; } = 0.1f;

   [Export]
   public float MinZoom { get; set; } = 1f;

   [Export]
   public float MaxZoom { get; set; } = 5f;

   public GameScenarioController ScenarioController { get; private set; } = null!;

   public override void _Ready()
   {
      ResolveReferences();
      ScenarioController = CreateScenarioController();
      TerrainGrid.BindScenario(ScenarioController);

      SaveButton.Pressed += OnSavePressed;
      LoadButton.Pressed += OnLoadPressed;
      EndTurnButton.Pressed += OnEndTurnPressed;

      base._Ready();
   }

   public override void _Process(double delta)
   {
      base._Process(delta);
      ScenarioController?.ProcessAnimations(delta);

      var movement = Vector2.Zero;

      if (Input.IsPhysicalKeyPressed(Key.Left))
      {
         movement.X -= 1f;
      }

      if (Input.IsPhysicalKeyPressed(Key.Right))
      {
         movement.X += 1f;
      }

      if (Input.IsPhysicalKeyPressed(Key.Up))
      {
         movement.Y -= 1f;
      }

      if (Input.IsPhysicalKeyPressed(Key.Down))
      {
         movement.Y += 1f;
      }

      if (movement != Vector2.Zero)
      {
         Camera.Position += movement.Normalized() * CameraMoveSpeed * (float)delta;
      }
   }

   public override void _UnhandledInput(InputEvent @event)
   {
      base._UnhandledInput(@event);

      if (@event is not InputEventMouseButton mouseButtonEvent || !mouseButtonEvent.Pressed)
      {
         return;
      }

      switch (mouseButtonEvent.ButtonIndex)
      {
         case MouseButton.WheelUp:
            AdjustZoom(-ZoomStep);
            GetViewport().SetInputAsHandled();
            break;
         case MouseButton.WheelDown:
            AdjustZoom(ZoomStep);
            GetViewport().SetInputAsHandled();
            break;
         case MouseButton.Left:
            PublishMapClick<LeftClickOnMap>();
            GetViewport().SetInputAsHandled();
            break;
         case MouseButton.Right:
            PublishMapClick<RightClickOnMap>();
            GetViewport().SetInputAsHandled();
            break;
      }
   }

   public override void _ExitTree()
   {
      if (SaveButton != null)
      {
         SaveButton.Pressed -= OnSavePressed;
      }

      if (LoadButton != null)
      {
         LoadButton.Pressed -= OnLoadPressed;
      }

      if (EndTurnButton != null)
      {
         EndTurnButton.Pressed -= OnEndTurnPressed;
      }

      base._ExitTree();
   }

   private static GameScenarioController CreateScenarioController()
   {
      var redPlayer = new GamePlayer
      {
         DisplayName = "Red Player",
         ControlType = PlayerControlType.Human,
         PlayerColor = PlayerColor.Red,
      };

      var bluePlayer = new GamePlayer
      {
         DisplayName = "Blue Player",
         ControlType = PlayerControlType.Human,
         PlayerColor = PlayerColor.Blue,
      };

      var redHero = new Hero(Faction.Temple, new Position(4, 4), redPlayer)
      {
         ObjectId = "hero-red-1"
      };

      var blueHero = new Hero(Faction.Academy, new Position(4, 8), bluePlayer)
      {
         ObjectId = "hero-blue-1"
      };

      redPlayer.Heroes.Add(redHero);
      bluePlayer.Heroes.Add(blueHero);

      var players = new List<GamePlayer> { redPlayer, bluePlayer };
      var map = new GameMap(MapSize._128x128.FillLayer(TerrainSurface.Grassland), redHero, blueHero);
      var scenarioController = new GameScenarioController(map, players, redPlayer)
      {
         Metadata = new ScenarioMetadata
         {
            DisplayName = "Terrain 2D Test",
            Description = "Two-player sandbox scenario for the 2D toolkit.",
            MaxPlayers = players.Count
         }
      };

      scenarioController.UiHost.SelectHero(redHero);
      return scenarioController;
   }

   private void OnSavePressed()
   {
      var savePath = GetSaveFilePath();
      ScenarioController.SaveToJsonFile(savePath);
      GD.Print($"Saved scenario to '{savePath}'.");
   }

   private void OnLoadPressed()
   {
      var savePath = GetSaveFilePath();
      if (!File.Exists(savePath))
      {
         GD.PushWarning($"Save file '{savePath}' does not exist.");
         return;
      }

      ScenarioController = GameScenarioController.LoadFromJsonFile(savePath);
      TerrainGrid.BindScenario(ScenarioController);
      GD.Print($"Loaded scenario from '{savePath}'.");
   }

   private void OnEndTurnPressed()
   {
      Hub.Instance.Publish(new PlayerEndedTurn { Player = ScenarioController.TurnController.CurrentPlayer });
   }

   private static string GetSaveFilePath()
   {
      var saveDirectory = ProjectSettings.GlobalizePath(SaveDirectoryPath);
      Directory.CreateDirectory(saveDirectory);
      return Path.Combine(saveDirectory, SaveFileName);
   }

   private void ResolveReferences()
   {
      TerrainGrid ??= GetNode<TerrainGrid>("../TerrainGrid");
      Camera ??= GetNode<Camera2D>("../Camera2D");
      SaveButton ??= GetNode<Button>("../UI/PanelContainer/MarginContainer/HBoxContainer/SaveButton");
      LoadButton ??= GetNode<Button>("../UI/PanelContainer/MarginContainer/HBoxContainer/LoadButton");
      EndTurnButton ??= GetNode<Button>("../UI/PanelContainer/MarginContainer/HBoxContainer/EndTurnButton");
   }

   private void AdjustZoom(float delta)
   {
      var nextZoom = Mathf.Clamp(Camera.Zoom.X + delta, MinZoom, MaxZoom);
      Camera.Zoom = new Vector2(nextZoom, nextZoom);
   }

   private void PublishMapClick<TEvent>() where TEvent : class, new()
   {
      if (!TerrainGrid.TryGetMapPosition(GetViewport().GetMousePosition(), out var mapPosition))
      {
         return;
      }

      var eventData = new TEvent();
      switch (eventData)
      {
         case LeftClickOnMap leftClickOnMap:
            leftClickOnMap.Position = mapPosition;
            Hub.Instance.Publish(leftClickOnMap);
            break;
         case RightClickOnMap rightClickOnMap:
            rightClickOnMap.Position = mapPosition;
            Hub.Instance.Publish(rightClickOnMap);
            break;
      }
   }
}
