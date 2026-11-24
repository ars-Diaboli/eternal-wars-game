using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// A bottom-row part that a narrow screen may cut (ADR-309): the army strip. The layout gives it less than its <see cref="FullWidth"/>; it
/// then shows its far part, fades out over the map at the cut end (the end towards the screen's middle), and shows whole, over its
/// neighbours, while the pointer is on it.
/// </summary>
public partial class CutPart : Control
{
    /// <summary>The width of the fade at the cut end.</summary>
    public const float FadeWidth = 40;

    private static GradientTexture2D? _fadeOut;
    private static GradientTexture2D? _fadeIn;
    private readonly Control _content;
    private Rect2 _placed;
    private bool _cutAtRight = true;
    private bool _whole;

    public CutPart(string name, Control content)
    {
        Name = name;
        _content = content;
        ClipChildren = ClipChildrenMode.Only; // what this draws is the mask its content shows through: opaque, then fading at the cut end
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(content);
        content.MinimumSizeChanged += () =>
        {
            CustomMinimumSize = new Vector2(0, content.GetCombinedMinimumSize().Y);
            (GetParent() as Container)?.QueueSort(); // a new width changes the cut
        };
        CustomMinimumSize = new Vector2(0, content.GetCombinedMinimumSize().Y);
        SetProcess(false);
    }

    /// <summary>The width the part takes uncut.</summary>
    public float FullWidth => _content.GetCombinedMinimumSize().X;

    /// <summary>Whether the layout gave it less than its full width.</summary>
    public bool IsCut => _placed.Size.X < FullWidth;

    /// <summary>Whether it shows whole now, the pointer on it.</summary>
    public bool IsWhole => _whole && IsCut;

    /// <summary>
    /// Places the part in <paramref name="rect"/>, as wide as the layout leaves it; <paramref name="cutAtRight"/>: the part stands at the left of
    /// the row, so a cut takes its right end (else its left end).
    /// </summary>
    public void Place(Rect2 rect, bool cutAtRight)
    {
        (_placed, _cutAtRight) = (rect, cutAtRight);
        SetProcess(IsCut); // only a cut part watches for the pointer
        ShowWhole(_whole && IsCut);
    }

    public override void _Process(double delta)
    {
        // The slots take the pointer's events, so the part asks what the pointer is over rather than waiting for its own enter and exit.
        var on = IsVisibleInTree() && GetViewport().GuiGetHoveredControl() is { } hovered && IsAncestorOf(hovered);
        if (on != _whole)
        {
            if (on)
            {
                MoveToFront(); // its clicks reach it where it lies over the action buttons
            }

            ShowWhole(on);
        }
    }

    public override void _Draw()
    {
        var cut = !_whole && IsCut;
        var solid = cut ? Math.Max(0, Size.X - FadeWidth) : Size.X;
        DrawRect(new Rect2(_cutAtRight ? 0 : Size.X - solid, 0, solid, Size.Y), Colors.White);
        if (cut)
        {
            var fade = new Rect2(_cutAtRight ? solid : 0, 0, Math.Min(FadeWidth, Size.X), Size.Y);
            DrawTextureRect(_cutAtRight ? _fadeOut ??= Fade(1, 0) : _fadeIn ??= Fade(0, 1), fade, tile: false);
        }
    }

    /// <summary>The mask's fade, its alpha from <paramref name="from"/> at the left to <paramref name="to"/> at the right.</summary>
    private static GradientTexture2D Fade(float from, float to) => new()
    {
        Width = (int)FadeWidth, Height = 4,
        Gradient = new Gradient { Colors = [new Color(1, 1, 1, from), new Color(1, 1, 1, to)], Offsets = [0, 1] },
    };

    private void ShowWhole(bool whole)
    {
        _whole = whole;
        var full = FullWidth;
        var width = whole ? Math.Max(full, _placed.Size.X) : _placed.Size.X;
        var x = _cutAtRight ? _placed.Position.X : _placed.End.X - width; // the far end stays where the layout put it
        Position = new Vector2(x, _placed.Position.Y);
        Size = new Vector2(width, _placed.Size.Y);
        ZIndex = whole && IsCut ? 2 : 0; // over the action buttons while shown whole
        ClipContents = !whole; // cut, the hidden end takes no clicks either (the mask only hides it)

        // The content keeps its full width; the cut end goes out of sight.
        _content.Position = new Vector2(_cutAtRight ? 0 : width - full, 0);
        _content.Size = new Vector2(full, _placed.Size.Y);
        QueueRedraw();
    }
}
