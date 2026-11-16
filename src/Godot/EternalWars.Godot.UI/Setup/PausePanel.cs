using EternalWars.Core.Domain.Common;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>A colour a LAN game waits for, which the host may let the computer play.</summary>
public sealed record PausedColour(PlayerId Id, string Colour);

/// <summary>
/// A LAN game paused for a colour nobody plays (ADR-195), at the top of the screen over the map and the battle: why, and on the host one button
/// per colour that lets the computer play it. Waiting needs no button: the game goes on by itself once a player joins the colour. The chat stays
/// usable meanwhile.
/// </summary>
public partial class PausePanel : PanelContainer
{
    private readonly Label _text = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(520, 0) };
    private readonly HBoxContainer _buttons = new() { Alignment = BoxContainer.AlignmentMode.Center };

    public PausePanel()
    {
        Name = "Pause";
        Visible = false;
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        GrowHorizontal = GrowDirection.Both;
        OffsetTop = 48;
        this.UseOpaqueBackground();
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", UiSpacing.Gap);
        AddChild(rows);
        rows.AddChild(new Label { Text = "Game paused", HorizontalAlignment = HorizontalAlignment.Center });
        rows.AddChild(_text);
        _buttons.AddThemeConstantOverride("separation", UiSpacing.Gap);
        rows.AddChild(_buttons);
    }

    /// <summary>The host lets the computer play a colour.</summary>
    public event Action<PlayerId>? HandOverRequested;

    /// <summary>Shows why the game is paused (null hides the panel) and, on the host, a button for each colour the computer may take over.</summary>
    public void ShowPause(string? reason, IReadOnlyList<PausedColour> handOver)
    {
        Visible = reason is not null;
        _text.Text = reason ?? "";
        _buttons.ClearChildren(detach: true);
        foreach (var colour in handOver)
        {
            var button = new Button { Text = $"Computer plays {colour.Colour}", FocusMode = FocusModeEnum.None };
            button.Pressed += () => HandOverRequested?.Invoke(colour.Id);
            _buttons.AddChild(button);
        }
    }
}
