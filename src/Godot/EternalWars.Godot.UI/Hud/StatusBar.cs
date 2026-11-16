using EternalWars.Core.Domain.Game;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The adventure map's status bar (ADR-184): a narrow stripe along the bottom of the screen, as wide as the screen up to 16:9 and a centred
/// 16:9 width on a wider screen. On the left the status line (<see cref="Message"/>): the HUD's messages, and what the pointer is over (a HUD
/// control's tooltip, or the map's cell through <see cref="ShowHover"/>), whichever came last. On the right the <see cref="ResourceBar"/>.
/// </summary>
public partial class StatusBar : PanelContainer
{
    /// <summary>The bar's height: one line of text with the resource icons.</summary>
    public const float Height = 32;

    /// <summary>The widest the bar gets, as a share of the screen's height: 16:9.</summary>
    private const float MaxAspect = 16f / 9f;

    private string? _hover;

    /// <summary>The status line; the HUD and the map input write their messages here.</summary>
    public Label Message { get; } = new()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        VerticalAlignment = VerticalAlignment.Center,
        ClipText = true,
        TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        MouseFilter = MouseFilterEnum.Pass,
    };

    /// <summary>Room kept free below the bar (the replay viewer's bar, ADR-164).</summary>
    public float BottomOffset { get; set; }

    public void Bind(GameSession session)
    {
        Name = "StatusBar";
        var style = new StyleBoxFlat { BgColor = new Color(0.13f, 0.13f, 0.15f), BorderColor = new Color(0.35f, 0.35f, 0.4f) };
        style.BorderWidthTop = 1;
        style.SetContentMarginAll(2);
        style.ContentMarginLeft = style.ContentMarginRight = UiSpacing.Gap;
        AddThemeStyleboxOverride("panel", style);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UiSpacing.Section);
        AddChild(row);
        row.AddChild(Message);
        var resources = new ResourceBar { Name = "Resources" };
        row.AddChild(resources);
        resources.Bind(session);
    }

    /// <summary>
    /// Shows what the pointer is over (null or empty: nothing to say). The text replaces the status line only when the pointer comes to
    /// something new, so a message stays until the pointer moves on.
    /// </summary>
    public void ShowHover(string? text)
    {
        if (text == _hover)
        {
            return;
        }

        _hover = text;
        if (!string.IsNullOrEmpty(text))
        {
            Message.Text = text;
        }
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += Place;
        Place();
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= Place;

    /// <summary>A HUD control's tooltip goes to the status line as the pointer comes over it.</summary>
    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventMouseMotion || GetViewport().GuiGetHoveredControl() is not { } control)
        {
            return;
        }

        for (Node? node = control; node is Control item; node = item.GetParent())
        {
            if (item.TooltipText.Length > 0)
            {
                ShowHover(item.TooltipText);
                return;
            }
        }
    }

    private void Place()
    {
        var screen = GetViewportRect().Size;
        var width = Math.Min(screen.X, screen.Y * MaxAspect);
        Position = new Vector2((screen.X - width) / 2, screen.Y - BottomOffset - Height);
        Size = new Vector2(width, Height);
    }
}
