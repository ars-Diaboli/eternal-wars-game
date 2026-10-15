using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Events;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// Presentation state shared by the adventure-map views: whose eyes the map is drawn through and which level is
/// shown. Plain view-side state (not game state), so a C# event is enough to tell the views it changed.
/// </summary>
public sealed class MapViewContext(GameState state, IEventSubscriber events, PlayerId? viewer = null)
{
    public GameState State { get; } = state;

    public IEventSubscriber Events { get; } = events;

    /// <summary>The player whose knowledge is drawn; null = omniscient (map editor).</summary>
    public PlayerId? Viewer { get; private set; } = viewer;

    /// <summary>0 = surface, 1 = underground.</summary>
    public int Level { get; private set; }

    /// <summary>The viewer or the level changed: views redraw.</summary>
    public event Action? Changed;

    public void SetViewer(PlayerId? viewer)
    {
        if (viewer != Viewer)
        {
            Viewer = viewer;
            Changed?.Invoke();
        }
    }

    public void SetLevel(int level)
    {
        level = Math.Clamp(level, 0, State.Map.Levels - 1);
        if (level != Level)
        {
            Level = level;
            Changed?.Invoke();
        }
    }

    public bool CanSee(Hero hero) => Viewer is not { } viewer || State.CanSee(viewer, hero);

    public bool IsExplored(MapPosition position) => Viewer is not { } viewer || State.Fog.IsExplored(viewer, position);

    public bool IsInSight(MapPosition position) => Viewer is not { } viewer || State.Fog.IsInSight(viewer, position);
}
