using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Turns;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The hot-seat hand-over screen of a tournament (ADR-366): before each person's turn it covers the whole window, the map and the HUD, with
/// whose turn it is, the day and the countdown, and the button that starts it (Enter or Space too). The map stays drawn for the last person
/// under it until the next takes the screen (<see cref="Taken"/>), so neither sees the other's zone. Every other key waits meanwhile.
/// </summary>
public partial class HandOverPanel : Control
{
    private const int CrestSize = 64;

    private readonly TextureRect _crest = new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        CustomMinimumSize = new Vector2(CrestSize, CrestSize),
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
    };

    private readonly Label _title = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Label _day = new() { HorizontalAlignment = HorizontalAlignment.Center };

    private readonly Label _text = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 0),
    };

    private readonly Button _start = new();
    private GameSession _session = null!;

    /// <summary>The person waiting has taken the screen: their turn is shown now.</summary>
    public event Action? Taken;

    public void Bind(GameSession session)
    {
        _session = session;
        Name = "HandOver";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop; // nothing under it takes a click
        AddChild(new ColorRect { Color = UiTheme.Background, MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 });

        var panel = new PanelContainer();
        panel.CenterAsScreen();
        AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", UiSpacing.Gap);
        panel.AddChild(box);
        _title.AddThemeFontSizeOverride("font_size", 28);
        _day.AddThemeColorOverride("font_color", UiTheme.TextQuiet);
        box.AddChild(_crest);
        box.AddChild(_title);
        box.AddChild(_day);
        box.AddChild(_text);
        _start.WithIcon(UiIcon.Confirm, keepText: true);
        _start.Pressed += Take;
        var row = Screens.ButtonRow(_start);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        box.AddChild(row);
        Visible = false;
    }

    /// <summary>Whether the screen is up.</summary>
    public bool IsOpen => Visible;

    /// <summary>Covers the window before <paramref name="player"/>'s turn.</summary>
    public void Open(PlayerId player)
    {
        var state = _session.State;
        var (title, day, text, button) = TournamentRules.HandOver(state, player);
        _title.Text = title;
        _title.AddThemeColorOverride("font_color", Palette.ForPlayer(state, player).Lightened(0.35f));
        _day.Text = day;
        _text.Text = text;
        _start.Text = button;
        _crest.Texture = Heraldry.Crest(TurnBanner.CrestOf(state, state.GetPlayer(player)), CrestSize);
        _crest.Visible = _crest.Texture is not null;
        Visible = true;
        _start.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>Enter or Space starts the turn whatever has the focus (a question opened under the screen may take it); other keys wait.</summary>
    public override void _Input(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey key || WindowModes.IsToggleKey(@event) || key.Keycode == Key.F3)
        {
            return; // Alt+Enter and F3 work on every screen
        }

        GetViewport().SetInputAsHandled();
        if (key is { Pressed: true, Echo: false, Keycode: Key.Enter or Key.KpEnter or Key.Space })
        {
            Take();
        }
    }

    private void Take()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        Taken?.Invoke();
    }
}
