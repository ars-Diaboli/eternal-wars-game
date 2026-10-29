using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Settings;
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

    /// <summary>How fast heroes walk (ADR-090, UI-1); the app sets it from the settings file.</summary>
    public SpeedSettings Speeds { get; set; } = SpeedSettings.Default;

    /// <summary>The views' clock in seconds: the frame time the world view has played (the time tweens run on).</summary>
    public double Now { get; private set; }

    /// <summary>When the last queued hero step ends, on <see cref="Now"/>'s clock: every hero's steps play one after another.</summary>
    public double PlaybackEnd { get; private set; }

    /// <summary>When the last queued step of a computer player's hero that is shown ends.</summary>
    public double ComputerPlaybackEnd { get; private set; }

    /// <summary>The viewer or the level changed: views redraw.</summary>
    public event Action? Changed;

    /// <summary>A hero's step starts playing (the app follows computer heroes with the camera).</summary>
    public event Action<Hero, MapPosition>? HeroStepShown;

    /// <summary>Whether computer heroes' steps are still being shown (the app holds the next turn meanwhile).</summary>
    public bool IsShowingComputer => Now < ComputerPlaybackEnd;

    /// <summary>Moves the views' clock on by a frame's time.</summary>
    public void Advance(double seconds) => Now += seconds;

    /// <summary>
    /// Books a step of <paramref name="seconds"/> after every step booked before it, no earlier than <paramref name="earliest"/>; returns when it
    /// starts.
    /// </summary>
    public double Schedule(double earliest, double seconds, bool computer)
    {
        var start = Math.Max(earliest, PlaybackEnd);
        PlaybackEnd = start + seconds;
        if (computer)
        {
            ComputerPlaybackEnd = PlaybackEnd;
        }

        return start;
    }

    internal void ShowStep(Hero hero, MapPosition to) => HeroStepShown?.Invoke(hero, to);

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
