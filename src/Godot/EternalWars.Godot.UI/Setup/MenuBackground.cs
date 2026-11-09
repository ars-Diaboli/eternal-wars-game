using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>
/// The picture behind the main menu and the screens before a game (ADR-163): it covers the whole window at any size, cropped rather than
/// stretched, with a dark wash so the panels in front stay readable. Without the picture file the window is a plain dark colour.
/// </summary>
public partial class MenuBackground : Control
{
    /// <summary>The picture, in the shared assets (<c>textures/ui/backgrounds/CREDITS.md</c>).</summary>
    public const string PicturePath = "res://shared/textures/ui/backgrounds/main-menu.jpg";

    public MenuBackground()
    {
        Name = "MenuBackground";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(Fill(new ColorRect { Color = new Color(0.08f, 0.09f, 0.1f) }));
        if (ResourceLoader.Exists(PicturePath) && GD.Load<Texture2D>(PicturePath) is { } picture)
        {
            AddChild(Fill(new TextureRect
            {
                Texture = picture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            }));
        }

        AddChild(Fill(new ColorRect { Color = new Color(0, 0, 0, 0.25f) }));
    }

    private static Control Fill(Control control)
    {
        control.MouseFilter = MouseFilterEnum.Ignore;
        control.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return control;
    }
}
