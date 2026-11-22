using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
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
    private FogLook _fogLook = FogLook.Dark;
    private bool _showGrid;
    private bool _ghostOtherLevel;
    private MapLayer _hidden;

    public GameState State { get; } = state;

    public IEventSubscriber Events { get; } = events;

    /// <summary>The player whose knowledge is drawn; null = omniscient (map editor).</summary>
    public PlayerId? Viewer { get; private set; } = viewer;

    /// <summary>
    /// Whether a player plays on another computer of a LAN game (ADR-192): on the host the remote colours, on a client every colour but its own.
    /// The app sets it; nobody does on one computer.
    /// </summary>
    public Func<PlayerId, bool> PlaysElsewhere { get; set; } = _ => false;

    /// <summary>Whose heroes, towns and resources the HUD lists: the current player's, unless it plays on another computer; then the viewer's.</summary>
    public PlayerId ListedPlayer => Viewer is { } viewer && PlaysElsewhere(State.CurrentPlayer.Id) ? viewer : State.CurrentPlayer.Id;

    /// <summary>
    /// Whether a player plays at this screen, so its questions and screens open here: every human on one computer (hot-seat), in a LAN game only
    /// this computer's colours.
    /// </summary>
    public bool PlaysHere(PlayerId player) => State.GetPlayer(player).IsHuman && !PlaysElsewhere(player);

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

    /// <summary>How the fog of war is drawn (ADR-176); the app sets it from the settings file.</summary>
    public FogLook FogLook
    {
        get => _fogLook;
        set
        {
            if (value != _fogLook)
            {
                _fogLook = value;
                FogLookChanged?.Invoke();
            }
        }
    }

    /// <summary>Whether the tile grid is drawn over the ground (ADR-183); the game sets it from the settings file, the editor from its toggle.</summary>
    public bool ShowGrid
    {
        get => _showGrid;
        set
        {
            if (value != _showGrid)
            {
                _showGrid = value;
                ShowGridChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// Whether the ground of the shown level mixes in the other level's terrain colours faintly (the map editor's toggle, ADR-287); the
    /// minimap of the editor does the same. Nothing on a map of one level.
    /// </summary>
    public bool GhostOtherLevel
    {
        get => _ghostOtherLevel;
        set
        {
            if (value != _ghostOtherLevel)
            {
                _ghostOtherLevel = value;
                GhostOtherLevelChanged?.Invoke();
            }
        }
    }

    /// <summary>The level whose terrain shows faintly through the shown one: the other of two while <see cref="GhostOtherLevel"/> is on, else null.</summary>
    public int? GhostLevel => _ghostOtherLevel && State.Map.Levels > 1 ? (Level == 0 ? 1 : 0) : null;

    /// <summary>The kinds the views leave out: the map editor's hidden layers (ADR-262); the game hides none.</summary>
    public MapLayer Hidden
    {
        get => _hidden;
        set
        {
            if (value != _hidden)
            {
                _hidden = value;
                HiddenChanged?.Invoke();
            }
        }
    }

    /// <summary>Whether the views draw <paramref name="layer"/>.</summary>
    public bool Shows(MapLayer layer) => (_hidden & layer) == 0;

    /// <summary>The tile under the pointer on the shown level, or null; the input sets it, and the objects view shows that tile's caption from afar.</summary>
    public MapPosition? Pointed { get; private set; }

    /// <summary>When the last booked hero step starts, on <see cref="Now"/>'s clock (the fog opens as the hero walks).</summary>
    public double LastStepStart { get; private set; }

    /// <summary>The fog's look changed.</summary>
    public event Action? FogLookChanged;

    /// <summary>The grid was switched on or off.</summary>
    public event Action? ShowGridChanged;

    /// <summary><see cref="GhostOtherLevel"/> was switched on or off.</summary>
    public event Action? GhostOtherLevelChanged;

    /// <summary>Other kinds are hidden (<see cref="Hidden"/>).</summary>
    public event Action? HiddenChanged;

    /// <summary>The pointer came over another tile (<see cref="Pointed"/>).</summary>
    public event Action? PointedChanged;

    /// <summary>The player whose knowledge is drawn changed: views of fog, memory and sight redraw.</summary>
    public event Action? ViewerChanged;

    /// <summary>The level shown changed: views that draw only the shown level redraw (views of one level each need not).</summary>
    public event Action? LevelChanged;

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
        LastStepStart = start;
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
            ViewerChanged?.Invoke();
        }
    }

    public void Point(MapPosition? tile)
    {
        if (tile != Pointed)
        {
            Pointed = tile;
            PointedChanged?.Invoke();
        }
    }

    public void SetLevel(int level)
    {
        level = Math.Clamp(level, 0, State.Map.Levels - 1);
        if (level != Level)
        {
            Level = level;
            LevelChanged?.Invoke();
        }
    }

    public bool CanSee(Hero hero) => Viewer is not { } viewer || State.CanSee(viewer, hero);

    public bool IsExplored(MapPosition position) => Viewer is not { } viewer || State.Fog.IsExplored(viewer, position);

    public bool IsInSight(MapPosition position) => Viewer is not { } viewer || State.Fog.IsInSight(viewer, position);
}
