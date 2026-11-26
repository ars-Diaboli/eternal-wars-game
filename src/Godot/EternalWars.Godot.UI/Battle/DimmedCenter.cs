using Godot;

namespace EternalWars.Godot.UI.Battle;

/// <summary>A screen-covering centre for a window over the battle (ADR-314): the field behind it dimmed, the clicks kept from it.</summary>
public partial class DimmedCenter : CenterContainer
{
    /// <summary>How dark the field gets behind the window.</summary>
    public static readonly Color Dim = new(0, 0, 0, 0.5f);

    public DimmedCenter()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Resized += QueueRedraw;
    }

    public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), Dim);
}
