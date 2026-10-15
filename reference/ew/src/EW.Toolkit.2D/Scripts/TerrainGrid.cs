using Godot;
using System;
using System.Linq;
using EW.CoreLib;
using EW.CoreLib.Map;
using EW.CoreLib.Types;

public partial class TerrainGrid : Node2D
{
   private static readonly Color GridBorderColor = new(0.25f, 0.25f, 0.25f);
   private static readonly Color HeroBorderColor = new(0.08f, 0.08f, 0.08f);
   private static readonly Color ReachableCurrentBudgetColor = Colors.LimeGreen;
   private static readonly Color ReachableTotalBudgetColor = Colors.Red;
   private static readonly Color UnreachableWaypointColor = Colors.Gray;
   private const float WaypointAlpha = 0.5f;

   [Export]
   public Vector2 CellSize { get; set; } = new(8, 8);

   private GameScenarioController ScenarioController { get; set; } = null!;
   private int _lastVisualStateHash;

   public override void _Ready()
   {
      base._Ready();
   }

   public override void _Process(double delta)
   {
      base._Process(delta);

      if (ScenarioController == null)
      {
         return;
      }

      var visualStateHash = ComputeVisualStateHash();
      if (visualStateHash == _lastVisualStateHash)
      {
         return;
      }

      _lastVisualStateHash = visualStateHash;
      QueueRedraw();
   }

   public override void _Draw()
   {
      base._Draw();

      if (ScenarioController == null || ScenarioController.Map.Cells == null)
      {
         return;
      }

      var map = ScenarioController.Map;

      var height = map.Cells.GetLength(0);
      var width = map.Cells.GetLength(1);

      for (var row = 0; row < height; row++)
      for (var column = 0; column < width; column++)
      {
         var rect = new Rect2(
            position: new Vector2(column * CellSize.X, row * CellSize.Y),
            size: CellSize);

         DrawRect(rect, ResolveSurfaceColor(map.Cells[row, column].Surface));
         DrawRect(rect, GridBorderColor, filled: false, width: 1f);
      }

      DrawSelectedHeroPath();
      DrawHeroes();
   }

   public void BindScenario(GameScenarioController scenarioController)
   {
      ScenarioController = scenarioController;
      _lastVisualStateHash = ComputeVisualStateHash();
      QueueRedraw();
   }

   public bool TryGetMapPosition(Vector2 screenPosition, out Position mapPosition)
   {
      mapPosition = EW.CoreLib.Types.Position.Zero;

      if (ScenarioController == null || ScenarioController.Map.Cells == null)
      {
         return false;
      }

      var localPosition = MakeCanvasPositionLocal(screenPosition);
      if (localPosition.X < 0 || localPosition.Y < 0)
      {
         return false;
      }

      var column = Mathf.FloorToInt(localPosition.X / CellSize.X);
      var row = Mathf.FloorToInt(localPosition.Y / CellSize.Y);

      if (row < 0 || column < 0
          || row >= ScenarioController.Map.Cells.GetLength(0)
          || column >= ScenarioController.Map.Cells.GetLength(1))
      {
         return false;
      }

      mapPosition = new Position(row, column);
      return true;
   }

   private static Color ResolveSurfaceColor(TerrainSurface surface) =>
      surface switch
      {
         TerrainSurface.Grassland => Colors.DarkGreen,
         TerrainSurface.Highlands => Colors.Green,
         TerrainSurface.Forest => Colors.DarkOliveGreen,
         TerrainSurface.Dirt => Colors.SaddleBrown,
         TerrainSurface.Swamp => Colors.Teal,
         TerrainSurface.Beach => Colors.Bisque,
         TerrainSurface.Desert => Colors.SandyBrown,
         _ => Colors.Black,
      };

   private void DrawHeroes()
   {
      foreach (var player in ScenarioController.Players)
      {
         var heroColor = ResolvePlayerColor(player.PlayerColor);

         foreach (var hero in player.Heroes)
         {
            DrawHeroShield(GetHeroCenter(hero.Path), heroColor);
         }
      }
   }

   private void DrawHeroShield(Vector2 center, Color fillColor)
   {
      var iconWidth = CellSize.X * 0.62f;
      var iconHeight = CellSize.Y * 0.76f;
      var halfWidth = iconWidth / 2f;
      var halfHeight = iconHeight / 2f;

      var points = new[]
      {
         center + new Vector2(0f, -halfHeight),
         center + new Vector2(halfWidth, -halfHeight * 0.35f),
         center + new Vector2(halfWidth * 0.72f, halfHeight * 0.42f),
         center + new Vector2(0f, halfHeight),
         center + new Vector2(-halfWidth * 0.72f, halfHeight * 0.42f),
         center + new Vector2(-halfWidth, -halfHeight * 0.35f)
      };

      DrawColoredPolygon(points, fillColor);
      DrawPolyline([.. points, points[0]], HeroBorderColor, width: 1.2f);
   }

   private void DrawSelectedHeroPath()
   {
      var selectedHero = ScenarioController.UiHost.SelectedHero;
      if (selectedHero == null || selectedHero.Path.MovementWaypoints.Count == 0)
      {
         return;
      }

      var waypoints = selectedHero.Path.MovementWaypoints.ToArray();
      var previousCenter = GetHeroCenter(selectedHero.Path);
      var waypointRadius = Mathf.Min(CellSize.X, CellSize.Y) * 0.25f;
      var lineWidth = Mathf.Max(1f, waypointRadius * 0.5f);

      for (var i = 0; i < waypoints.Length; i++)
      {
         var waypoint = waypoints[i];
         var waypointCenter = GetCellCenter(waypoint.Position);
         var waypointColor = WithAlpha(ResolveWaypointColor(waypoint), WaypointAlpha);

         DrawLine(previousCenter, waypointCenter, waypointColor, lineWidth, antialiased: true);

         if (i == waypoints.Length - 1)
         {
            DrawDestinationCross(waypointCenter, waypointRadius, waypointColor, lineWidth);
         }
         else
         {
            DrawCircle(waypointCenter, waypointRadius, waypointColor);
         }

         previousCenter = waypointCenter;
      }
   }

   private void DrawDestinationCross(Vector2 center, float radius, Color color, float lineWidth)
   {
      var offset = new Vector2(radius, radius);
      DrawLine(center - offset, center + offset, color, lineWidth, antialiased: true);
      DrawLine(
         new Vector2(center.X - radius, center.Y + radius),
         new Vector2(center.X + radius, center.Y - radius),
         color,
         lineWidth,
         antialiased: true);
   }

   private Vector2 GetCellCenter(Position position)
   {
      var topLeft = new Vector2(position.Horizontal * CellSize.X, position.Vertical * CellSize.Y);
      return topLeft + (CellSize / 2f);
   }

   private Vector2 GetHeroCenter(HeroPath hero)
   {
      var progress = hero.GetMovementProgress();
      return progress.To.HasValue 
         ? GetCellCenter(progress.From).Lerp(GetCellCenter(progress.To.Value), progress.Progress) 
         : GetCellCenter(progress.From);
   }

   private int ComputeVisualStateHash()
   {
      if (ScenarioController == null)
      {
         return 0;
      }

      var hash = new HashCode();
      foreach (var hero in ScenarioController.Players.SelectMany(player => player.Heroes))
      {
         hash.Add(hero.ObjectId);
         hash.Add(hero.Position);
         hash.Add(hero.Path.IsMoving);
         hash.Add(hero.Path.MovementProgress);

         foreach (var waypoint in hero.Path.MovementWaypoints)
         {
            hash.Add(waypoint.Position);
            hash.Add(waypoint.CanReachWithCurrentBudget);
            hash.Add(waypoint.CanReachWithTotalBudget);
         }
      }

      return hash.ToHashCode();
   }

   private static Color ResolveWaypointColor(PathWaypoint waypoint)
   {
      if (waypoint.CanReachWithCurrentBudget)
      {
         return ReachableCurrentBudgetColor;
      }

      if (waypoint.CanReachWithTotalBudget)
      {
         return ReachableTotalBudgetColor;
      }

      return UnreachableWaypointColor;
   }

   private static Color WithAlpha(Color color, float alpha) => new(color.R, color.G, color.B, alpha);

   private static Color ResolvePlayerColor(PlayerColor playerColor)
   {
      var value = playerColor.Value;
      var red = ((value >> 24) & 0xFF) / 255f;
      var green = ((value >> 16) & 0xFF) / 255f;
      var blue = ((value >> 8) & 0xFF) / 255f;
      var alpha = (value & 0xFF) / 255f;
      return new Color(red, green, blue, alpha);
   }
}
