using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Widgets;

/// <summary>
/// A building as a tile (ADR-294): its picture, its name, "level n of m", the next level's cost (<see cref="CostLabel"/>, the lack marked) and its
/// state told twice, by the frame's colour and by a symbol, so it reads without colour: built out ✓, can build ⚒, can't afford ¤, needs
/// buildings ⛓, built today ⌛, closed for ever ✗ (a dwelling line the 7-of-9 rule shut; a building refused for another reason, the Grail
/// building without the Grail, shows ✗ with "can't build"). The picture is a greybox
/// block with the building's initials until the building art of step 4. Pressing a tile raises <see cref="BaseButton.Pressed"/>.
/// </summary>
public partial class BuildingTile : Button
{
    /// <summary>A tile's size in the Hall.</summary>
    public static readonly Vector2 TileSize = new(168, 170);

    public BuildingTile(string name, int level, int maxLevel, BuildingStatus status, ResourceSet? cost, ResourceSet treasury, string tooltip)
    {
        Status = status;
        CustomMinimumSize = TileSize;
        ClipContents = true;
        TooltipText = tooltip;
        SetMeta(AutoInput.LabelMeta, name);
        var (symbol, colour, caption) = Look(status);
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
        {
            var lit = state is "hover" or "hover_pressed" or "focus";
            var box = UiTheme.Box(UiTheme.ButtonFace.Darkened(state == "pressed" ? 0.3f : 0.15f), lit ? colour.Lightened(0.3f) : colour, 4, lit ? 3 : 2);
            box.DrawCenter = state != "focus";
            // The shader's button with the state's colour on its rim (ADR-310); the focus stays a coloured line over it.
            AddThemeStyleboxOverride(state, state == "focus" ? box : UiFrames.Themed(BevelKind.Button, 4, tint: colour, flat: box));
        }

        var box2 = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box2.AddThemeConstantOverride("separation", 2);
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        foreach (var side in new[] { "left", "right", "top", "bottom" })
        {
            margin.AddThemeConstantOverride($"margin_{side}", 6);
        }

        margin.AddChild(box2);
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // The picture with the state's symbol in its corner.
        var picture = new Panel { CustomMinimumSize = new Vector2(0, 60), MouseFilter = MouseFilterEnum.Ignore };
        picture.AddThemeStyleboxOverride("panel", UiTheme.Box(Greybox(name), UiTheme.BorderDim, 0));
        var initials = new Label
        {
            Text = Initials(name),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ThemeTypeVariation = UiTheme.HeadingLabel,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        picture.AddChild(initials);
        initials.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var mark = new Label { Text = symbol, TooltipText = caption, MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Right };
        mark.AddThemeColorOverride("font_color", colour.Lightened(0.2f));
        mark.AddThemeColorOverride("font_outline_color", Colors.Black);
        mark.AddThemeConstantOverride("outline_size", 5);
        mark.AddThemeFontSizeOverride("font_size", 24);
        picture.AddChild(mark);
        mark.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
        mark.OffsetLeft = -32;
        mark.OffsetRight = -4;
        box2.AddChild(picture);

        box2.AddChild(new Label { Text = name, ClipText = true, MouseFilter = MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center });
        if (maxLevel > 1)
        {
            box2.AddChild(new Label
            {
                Text = $"Level {Math.Min(level, maxLevel)} of {maxLevel}",
                ThemeTypeVariation = UiTheme.QuietLabel,
                MouseFilter = MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        box2.AddChild(new Label
        {
            Text = caption,
            ThemeTypeVariation = UiTheme.QuietLabel,
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipText = true,
        });
        if (cost is { } price && status is not (BuildingStatus.Complete or BuildingStatus.Closed or BuildingStatus.Refused))
        {
            var row = new CostLabel(price, treasury) { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
            box2.AddChild(row);
        }
    }

    /// <summary>The state the tile shows.</summary>
    public BuildingStatus Status { get; }

    /// <summary>The symbol, the frame's colour and the words of a state.</summary>
    public static (string Symbol, Color Colour, string Caption) Look(BuildingStatus status) => status switch
    {
        BuildingStatus.Complete => ("✓", new Color(0.35f, 0.7f, 0.35f), "built"),
        BuildingStatus.Available => ("⚒", UiTheme.BorderBright, "can build"),
        BuildingStatus.CannotAfford => ("¤", new Color(0.85f, 0.45f, 0.3f), "can't afford"),
        BuildingStatus.NeedsBuildings => ("⛓", new Color(0.45f, 0.45f, 0.5f), "needs buildings"),
        BuildingStatus.BuiltToday => ("⌛", new Color(0.4f, 0.6f, 0.85f), "built today"),
        BuildingStatus.Closed => ("✗", new Color(0.55f, 0.2f, 0.2f), "closed for good"),
        _ => ("✗", new Color(0.55f, 0.2f, 0.2f), "can't build"),
    };

    /// <summary>A building's greybox colour: a muted shade of its own, from its name, so tiles tell apart.</summary>
    private static Color Greybox(string name)
    {
        var hash = name.Aggregate(17, (h, c) => (h * 31) + c);
        return Color.FromHsv((hash & 0xff) / 255f, 0.25f, 0.32f);
    }

    private static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => char.IsLetter(w[0])).Take(2).Select(w => char.ToUpperInvariant(w[0])));
}
