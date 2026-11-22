using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Core.Domain.Settings;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.Shared.Towns;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// How the town scene is entered from the adventure map (TOWN-23, ADR-306). Its layout loads in the background ahead of time
/// (<see cref="TownScene3D.Prepare"/>): when a route of a player at this screen is shown ending at one of that player's towns, and at the start of
/// that player's turn for the town T opens (the first in the town list). When the scene opens, the screen fades through black into the hero shot:
/// the last frame of the map darkens to black, then the town comes up out of it, <see cref="FadeSeconds"/> in all at normal speed, by the own heroes'
/// speed setting (<see cref="TownScene3D.Speed"/>, ADR-090), none at instant speed. The fade is only a picture over the screen: the town screen
/// takes the pointer and the keys from the first frame.
/// </summary>
public partial class TownArrival : CanvasLayer
{
    /// <summary>The fade's length at normal speed, through black (TOWN-23).</summary>
    public const double FadeSeconds = 0.5;

    private readonly TextureRect _map = new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.Scale,
        MouseFilter = Control.MouseFilterEnum.Ignore,
        Visible = false,
    };

    private readonly ColorRect _black = new() { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
    private GameSession _session = null!;
    private MapViewContext _context = null!;
    private TownScene3D _scene = null!;
    private Tween? _fade;

    public TownArrival()
    {
        Name = "TownArrival";
        Layer = 6; // over the town screen, the status bar and the turn timer; under dialogs and the info window
    }

    /// <summary>Whether the fade is playing.</summary>
    public bool IsFading => _fade?.IsRunning() == true;

    public void Bind(GameSession session, MapViewContext context, TownScene3D scene, PathPreview routes)
    {
        (_session, _context, _scene) = (session, context, scene);
        AddChild(_map);
        AddChild(_black);
        _map.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _black.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        scene.Opened += FadeIn;
        scene.Closed += StopFade;
        routes.Shown += RouteShown;
        this.SubscribeWhileInTree<TurnStarted>(session.Events, e => PrepareFirstTown(e.Player));
        Callable.From(() => PrepareFirstTown(session.State.CurrentPlayer.Id)).CallDeferred(); // the game's first turn started before this was built
    }

    /// <summary>A route shown on the map: the town it ends at, if it is the mover's own, loads in the background.</summary>
    private void RouteShown(HeroPath path)
    {
        var state = _session.State;
        var player = state.CurrentPlayer.Id;
        if (_context.PlaysHere(player)
            && state.ObjectsOf<Town>().FirstOrDefault(t => t.Owner == player && t.VisitPosition == path.Destination) is { } town)
        {
            _scene.Prepare(town.Id);
        }
    }

    /// <summary>At the turn's start of a player at this screen: the town T opens loads in the background.</summary>
    private void PrepareFirstTown(Core.Domain.Common.PlayerId player)
    {
        if (IsInstanceValid(_scene) && _context.PlaysHere(player) && TownListPanel.TownsOf(_session.State, player) is [var first, ..])
        {
            _scene.Prepare(first.Id);
        }
    }

    /// <summary>The scene opened: the map's last frame darkens to black, then the town comes up out of it.</summary>
    private void FadeIn()
    {
        StopFade();
        var seconds = SpeedSettings.Seconds(_scene.Speed(), FadeSeconds);
        if (seconds <= 0 || GetViewport().GetTexture().GetImage() is not { } frame)
        {
            return; // instant speed, or a renderer that keeps no frame
        }

        _map.Texture = ImageTexture.CreateFromImage(frame); // the frame drawn last: the map
        (_map.Visible, _black.Visible, _black.Color) = (true, true, new Color(0, 0, 0, 0));
        _fade = CreateTween();
        _fade.TweenProperty(_black, "color:a", 1f, seconds / 2);
        _fade.TweenCallback(Callable.From(() => { (_map.Visible, _map.Texture) = (false, null); }));
        _fade.TweenProperty(_black, "color:a", 0f, seconds / 2);
        _fade.TweenCallback(Callable.From(StopFade));
    }

    private void StopFade()
    {
        _fade?.Kill();
        _fade = null;
        (_map.Visible, _map.Texture, _black.Visible) = (false, null, false);
    }
}
