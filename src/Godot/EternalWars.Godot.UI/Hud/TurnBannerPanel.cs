using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Turns;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The turn banner at the top centre of the adventure map (ADR-309, <see cref="TurnBanner"/>): the player's crest and "&lt;Player&gt;'s turn" on
/// a ground of the player's colour in the bronze frame. With one person at the game it shows for a few seconds when that person's turn
/// starts; in hot-seat and LAN games it stays while the turn lasts, and the turn timer's pill (<see cref="TurnTimerPanel"/>) sits under it.
/// The replay viewer has none.
/// </summary>
public partial class TurnBannerPanel : PanelContainer
{
    /// <summary>The banner's height; the timer's pill goes under it.</summary>
    public const float Height = 40;

    /// <summary>Where the banner's bottom edge is: the timer's pill goes under it.</summary>
    public const float Bottom = UiSpacing.ScreenEdge + Height;

    private const int CrestSize = 28;

    private readonly TextureRect _crest = new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        CustomMinimumSize = new Vector2(CrestSize, CrestSize),
        MouseFilter = MouseFilterEnum.Ignore,
    };

    private readonly Label _text = new() { VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
    private GameSession _session = null!;
    private double _left;

    /// <summary>What the banner says now; empty while it is hidden.</summary>
    public string Text => Visible ? _text.Text : "";

    public void Bind(GameSession session)
    {
        _session = session;
        Name = "TurnBanner";
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorLeft = AnchorRight = 0.5f;
        GrowHorizontal = GrowDirection.Both;
        OffsetTop = UiSpacing.ScreenEdge;
        OffsetBottom = Bottom;
        _text.AddThemeFontSizeOverride("font_size", 20);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", UiSpacing.Gap);
        row.AddChild(_crest);
        row.AddChild(_text);
        AddChild(row);
        Visible = false;
        Refresh(started: true);
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Refresh(started: true));
        this.SubscribeWhileInTree<SimultaneousDayStarted>(session.Events, _ => Refresh(started: true));
        this.SubscribeWhileInTree<SimultaneousDaysEnded>(session.Events, _ => Refresh(started: false));
        this.SubscribeWhileInTree<PlayerDefeated>(session.Events, _ => Refresh(started: false));
        this.SubscribeWhileInTree<GameEnded>(session.Events, _ => Refresh(started: false));
        this.SubscribeWhileInTree<PlayerHandedToComputer>(session.Events, _ => Refresh(started: false)); // fewer people: brief, not lasting
        this.SubscribeWhileInTree<PlayerHandedToPerson>(session.Events, _ => Refresh(started: false));
    }

    /// <summary>Shows the banner of the turn under way; <paramref name="started"/>: a turn has just begun, so a brief banner shows again.</summary>
    private void Refresh(bool started)
    {
        if (TurnBanner.Of(_session.State) is not { } banner || (!banner.Lasting && !started && !Visible))
        {
            Visible = false;
            return;
        }

        if (banner.Lasting)
        {
            _left = double.PositiveInfinity;
        }
        else if (started || double.IsPositiveInfinity(_left))
        {
            _left = TurnBanner.BriefSeconds; // a brief banner already counting down keeps its time
        }

        _text.Text = banner.Text;
        _crest.Texture = Heraldry.Crest(banner.Crest, CrestSize);
        _crest.Visible = _crest.Texture is not null;
        var colour = banner.Player is { } player ? Palette.ForPlayer(_session.State, player) : UiTheme.Border;
        var style = UiTheme.Box(colour.Darkened(0.55f), UiTheme.Border, UiSpacing.Cell, borderWidth: 2);
        style.ContentMarginLeft = style.ContentMarginRight = UiSpacing.Section;
        style.SetCornerRadiusAll(4);
        AddThemeStyleboxOverride("panel", style);
        Visible = true;
    }

    public override void _Process(double delta)
    {
        if (!Visible || double.IsPositiveInfinity(_left))
        {
            return;
        }

        _left -= delta;
        if (_left <= 0)
        {
            Visible = false;
        }
    }
}
