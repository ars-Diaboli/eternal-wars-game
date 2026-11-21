using EternalWars.Core.Domain.Visibility;
using EternalWars.Core.Generation;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// A generated map from above (ADR-274): the tiles of one level as the minimap colours them, the borders between the generator's zones
/// darkened, and (for the preview) each zone's id over it. Fits its rectangle, keeping the map square. The template editor's preview and the
/// batch's thumbnails.
/// </summary>
public partial class ZonePictureView : Control
{
    private static readonly Color Border = new(0.05f, 0.05f, 0.07f);
    private static readonly Color LabelOutline = new(0, 0, 0, 0.85f);

    private ZonePicture? _picture;
    private ImageTexture? _texture;
    private int _level;

    /// <summary>Each zone's id over it (the preview; a thumbnail has none).</summary>
    public bool ShowLabels { get; set; }

    /// <summary>How big the picture is drawn before it is fitted, in pixels a side: a tile is that over the map's side (at least one).</summary>
    public int Resolution { get; set; } = 768;

    public ZonePicture? Picture => _picture;

    public int Level => _level;

    /// <summary>Shows <paramref name="picture"/>'s level <paramref name="level"/> (nothing for null).</summary>
    public void Show(ZonePicture? picture, int level = 0)
    {
        _picture = picture;
        _level = picture is null ? 0 : Math.Clamp(level, 0, picture.Levels - 1);
        _texture = picture is null ? null : ImageTexture.CreateFromImage(Render(picture, _level, Math.Max(1, Resolution / picture.Width)));
        QueueRedraw();
    }

    /// <summary>
    /// The level's image, <paramref name="scale"/> pixels a tile: at three or more a zone's border is the tile's last row or column of
    /// pixels, below that the whole tile is darkened.
    /// </summary>
    public static Image Render(ZonePicture picture, int level, int scale)
    {
        ArgumentNullException.ThrowIfNull(picture);
        var (width, height) = (picture.Width * scale, picture.Height * scale);
        var bytes = new byte[width * height * 4];
        void Put(int px, int py, Color colour)
        {
            var at = ((py * width) + px) * 4;
            bytes[at] = (byte)colour.R8;
            bytes[at + 1] = (byte)colour.G8;
            bytes[at + 2] = (byte)colour.B8;
            bytes[at + 3] = 255;
        }

        for (var y = 0; y < picture.Height; y++)
        {
            for (var x = 0; x < picture.Width; x++)
            {
                var colour = ColourOf(picture, picture.Cell(x, y, level));
                var (right, below) = (picture.BorderRight(x, y, level), picture.BorderBelow(x, y, level));
                if (scale < 3 && (right || below))
                {
                    colour = colour.Lerp(Border, 0.6f);
                }

                for (var dy = 0; dy < scale; dy++)
                {
                    for (var dx = 0; dx < scale; dx++)
                    {
                        var edge = scale >= 3 && ((right && dx == scale - 1) || (below && dy == scale - 1));
                        Put((x * scale) + dx, (y * scale) + dy, edge ? Border : colour);
                    }
                }
            }
        }

        return Image.CreateFromData(width, height, false, Image.Format.Rgba8, bytes);
    }

    private static Color ColourOf(ZonePicture picture, MinimapCell cell) =>
        cell.Marker == MinimapMarker.None ? Palette.ForTerrain(cell.Terrain)
        : cell.Owner is { } owner ? Palette.ForPlayer(picture.State, owner)
        : cell.Marker == MinimapMarker.Object ? Palette.Neutral.Darkened(0.3f)
        : Palette.Neutral;

    /// <summary>Where the map is drawn: the largest square in the middle of the control.</summary>
    public Rect2 MapRect()
    {
        var side = Math.Min(Size.X, Size.Y);
        return new Rect2((Size - new Vector2(side, side)) / 2, new Vector2(side, side));
    }

    public override void _Draw()
    {
        if (_picture is null || _texture is null)
        {
            return;
        }

        var rect = MapRect();
        DrawTextureRect(_texture, rect, tile: false);
        if (!ShowLabels)
        {
            return;
        }

        var font = GetThemeDefaultFont();
        var fontSize = GetThemeDefaultFontSize();
        var tile = rect.Size.X / _picture.Width;
        foreach (var label in _picture.Labels(_level))
        {
            var width = font.GetStringSize(label.Id, HorizontalAlignment.Left, -1, fontSize).X;
            var at = rect.Position + new Vector2(((label.X + 0.5f) * tile) - (width / 2), ((label.Y + 0.5f) * tile) + (fontSize / 3f));
            DrawStringOutline(font, at, label.Id, HorizontalAlignment.Left, -1, fontSize, 4, LabelOutline);
            DrawString(font, at, label.Id, HorizontalAlignment.Left, -1, fontSize, Colors.White);
        }
    }
}
