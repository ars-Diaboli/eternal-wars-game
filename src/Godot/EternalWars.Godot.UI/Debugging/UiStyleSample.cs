using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Debugging;

/// <summary>
/// The procedural panel style on trial (<see cref="BevelFrame"/>), shown alone over a picture by <c>--ui-sample</c>: a minimap frame
/// with its buttons, the heroes and towns lists with a selected hero in red, the hero bar with stats, an action grid and End turn, and
/// sliders that change the rim's light on every frame at once.
/// </summary>
public partial class UiStyleSample : Control
{
    private const string Background = "res://shared/textures/ui/backgrounds/main-menu.jpg";
    private const string Portraits = "res://shared/textures/ui/heroes/";

    private static readonly Color Text = new(0.93f, 0.91f, 0.86f);
    private static readonly Color Divider = new(0.42f, 0.42f, 0.45f, 0.55f);

    private readonly List<BevelFrame> _frames = [];

    /// <summary>
    /// The panels' fill: <c>black</c> (uniform black), <c>clear</c> (uniform black, 60 % transparent), null for the dark gradient. Slots and
    /// buttons take the same black, so in <c>clear</c> they are darker only where they lie over the panel.
    /// </summary>
    public string? Fill { get; set; }

    private BevelFrame Track(BevelFrame frame)
    {
        _frames.Add(frame);
        var alpha = Fill switch { "black" => 1f, "clear" => 0.4f, _ => -1f };
        if (alpha >= 0)
        {
            frame.SetFill(frame.Kind == BevelKind.Button ? new Color(0.07f, 0.07f, 0.075f, Math.Max(alpha, 0.55f)) : new Color(0, 0, 0, alpha));
        }

        return frame;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(new TextureRect
        {
            Texture = GD.Load<Texture2D>(Background),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            AnchorRight = 1,
            AnchorBottom = 1,
        });

        Place(Heroes(), new Vector2(20, 90));
        Place(Minimap(), new Vector2(-400, 20), right: true);
        Place(Towns(), new Vector2(-340, 330), right: true);
        Place(Sliders(), new Vector2(380, 90));
        Place(HeroBar(), new Vector2(20, -175), bottom: true);
        Place(ActionGrid(), new Vector2(560, -205), bottom: true);
        Place(EndTurn(), new Vector2(-200, -205), right: true, bottom: true);
    }

    private void Place(Control control, Vector2 at, bool right = false, bool bottom = false)
    {
        AddChild(control);
        control.AnchorLeft = control.AnchorRight = right ? 1 : 0;
        control.AnchorTop = control.AnchorBottom = bottom ? 1 : 0;
        control.OffsetLeft = at.X;
        control.OffsetTop = at.Y;
        control.GrowHorizontal = GrowDirection.End;
        control.GrowVertical = GrowDirection.End;
    }

    private PanelContainer Framed(BevelKind kind, int padding)
    {
        var panel = new PanelContainer();
        var style = new StyleBoxEmpty();
        style.SetContentMarginAll(padding);
        panel.AddThemeStyleboxOverride("panel", style);
        Track(BevelFrame.Behind(panel, kind));
        return panel;
    }

    private Button FramedButton(string label, UiIcon? icon, Vector2 size, int fontSize = 18)
    {
        var button = new Button { Text = label, CustomMinimumSize = size, FocusMode = FocusModeEnum.None };
        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled", "hover_pressed" })
        {
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }

        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeColorOverride("font_color", Text);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        if (icon is { } i)
        {
            button.WithIcon(i, (int)size.X - 16);
            button.CustomMinimumSize = size;
        }

        Track(BevelFrame.Behind(button, BevelKind.Button)); // it follows the button's hover and press itself
        return button;
    }

    private static Label Title(string text, int size = 24) =>
        WithColor(new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center }, size);

    private static Label WithColor(Label label, int size)
    {
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", Text);
        return label;
    }

    private static ColorRect Line(bool vertical) => new()
    {
        Color = Divider,
        CustomMinimumSize = vertical ? new Vector2(1, 0) : new Vector2(0, 1),
        SizeFlagsVertical = vertical ? SizeFlags.ExpandFill : SizeFlags.ShrinkCenter,
    };

    private static Texture2D? Portrait(string id) =>
        ResourceLoader.Exists($"{Portraits}icon_hero_{id}.png") ? GD.Load<Texture2D>($"{Portraits}icon_hero_{id}.png") : null;

    private static TextureRect Picture(Texture2D? texture, int size) => new()
    {
        Texture = texture,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        CustomMinimumSize = new Vector2(size, size),
    };

    private static ProgressBar Bar(Color color, double value)
    {
        var bar = new ProgressBar { Value = value, ShowPercentage = false, CustomMinimumSize = new Vector2(130, 9) };
        var back = new StyleBoxFlat { BgColor = new Color(0.02f, 0.02f, 0.025f, 0.9f), BorderColor = new Color(0.25f, 0.25f, 0.27f) };
        back.SetBorderWidthAll(1);
        back.SetCornerRadiusAll(5);
        var fill = new StyleBoxFlat { BgColor = color };
        fill.SetCornerRadiusAll(5);
        bar.AddThemeStyleboxOverride("background", back);
        bar.AddThemeStyleboxOverride("fill", fill);
        return bar;
    }

    private Control Heroes()
    {
        var panel = Framed(BevelKind.Panel, 16);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0) };
        column.AddThemeConstantOverride("separation", 10);
        panel.AddChild(column);
        column.AddChild(Title("Heroes"));
        (string Id, string Name, double Move, double Mana)[] heroes =
        [
            ("sylvan.nymara", "Mara", 72, 60), ("haven.aldric", "Aldric", 48, 80), ("haven.rowena", "Vesna", 90, 45),
        ];
        for (var i = 0; i < heroes.Length; i++)
        {
            var (id, name, move, mana) = heroes[i];
            var row = Framed(BevelKind.Slot, 6);
            if (i == 0)
            {
                var frame = _frames[^1];
                frame.SelectedTint = new Color(0.95f, 0.12f, 0.14f);
                frame.Selected = true;
            }

            var line = new HBoxContainer();
            line.AddThemeConstantOverride("separation", 12);
            row.AddChild(line);
            line.AddChild(Picture(Portrait(id), 64));
            var text = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            text.AddChild(WithColor(new Label { Text = name }, 22));
            text.AddChild(Bar(new Color(0.98f, 0.72f, 0.15f), move));
            text.AddChild(Bar(new Color(0.25f, 0.6f, 0.98f), mana));
            line.AddChild(text);
            column.AddChild(row);
        }

        return panel;
    }

    private Control Minimap()
    {
        var panel = Framed(BevelKind.Panel, 14);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        panel.AddChild(row);
        var map = Framed(BevelKind.Slot, 4);
        map.AddChild(Picture(GD.Load<Texture2D>(Background), 0));
        map.CustomMinimumSize = new Vector2(280, 200);
        row.AddChild(map);
        var buttons = new VBoxContainer();
        buttons.AddThemeConstantOverride("separation", 10);
        buttons.AddChild(FramedButton("Zoom in", UiIcon.Plus, new Vector2(48, 48)));
        buttons.AddChild(FramedButton("Zoom out", UiIcon.Minus, new Vector2(48, 48)));
        buttons.AddChild(FramedButton("Centre", UiIcon.Hero, new Vector2(48, 48)));
        row.AddChild(buttons);
        return panel;
    }

    private Control Towns()
    {
        var panel = Framed(BevelKind.Panel, 14);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0) };
        column.AddThemeConstantOverride("separation", 8);
        panel.AddChild(column);
        column.AddChild(Title("Towns", 22));
        foreach (var (name, governor) in new[] { ("Highwatch", "Gov. Aldric"), ("Ashford", "Gov. Vesna") })
        {
            var slot = Framed(BevelKind.Slot, 10);
            var text = new VBoxContainer();
            text.AddChild(WithColor(new Label { Text = name }, 20));
            var quiet = WithColor(new Label { Text = governor }, 15);
            quiet.Modulate = new Color(1, 1, 1, 0.7f);
            text.AddChild(quiet);
            slot.AddChild(text);
            column.AddChild(slot);
        }

        return panel;
    }

    private Control HeroBar()
    {
        var panel = Framed(BevelKind.Panel, 12);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        panel.AddChild(row);
        var portrait = Framed(BevelKind.Slot, 3);
        portrait.AddChild(Picture(Portrait("sylvan.nymara"), 96));
        row.AddChild(portrait);
        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 8);
        column.AddChild(WithColor(new Label { Text = "Lv 6    Mara" }, 28));
        column.AddChild(Line(vertical: false));
        var stats = new HBoxContainer();
        stats.AddThemeConstantOverride("separation", 26);
        stats.AddChild(HeroIcons.StatBadge(HeroStatIcon.Attack, "Attack", "6"));
        stats.AddChild(HeroIcons.StatBadge(HeroStatIcon.Defense, "Defence", "5"));
        stats.AddChild(HeroIcons.StatBadge(HeroStatIcon.SpellPower, "Spell power", "3"));
        stats.AddChild(HeroIcons.StatBadge(HeroStatIcon.Knowledge, "Knowledge", "4"));
        column.AddChild(stats);
        row.AddChild(column);
        return panel;
    }

    private Control ActionGrid()
    {
        var panel = Framed(BevelKind.Panel, 10);
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        panel.AddChild(grid);
        foreach (var icon in new[]
                 {
                     UiIcon.SpellBook, UiIcon.Kingdom, UiIcon.Quests, UiIcon.PuzzleMap, UiIcon.Dig, UiIcon.Marketplace, UiIcon.SwitchLevel,
                     UiIcon.Hero, UiIcon.Menu,
                 })
        {
            grid.AddChild(FramedButton(icon.ToString(), icon, new Vector2(52, 52)));
        }

        return panel;
    }

    private Control EndTurn()
    {
        var button = FramedButton("End turn\n\nDay 5", null, new Vector2(170, 170), 22);
        return button;
    }

    private Control Sliders()
    {
        var panel = Framed(BevelKind.Panel, 14);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0) };
        column.AddThemeConstantOverride("separation", 4);
        panel.AddChild(column);
        column.AddChild(Title(Fill switch { "black" => "Rim · black fill", "clear" => "Rim · black, 60 % clear", _ => "Rim · gradient fill" }, 20));
        Slider(column, "Light angle", "light_angle", 0, 360, 125);
        Slider(column, "Highlight", "highlight", 0, 2, 1);
        Slider(column, "Corner shine (px)", "corner_falloff", 5, 300, 90, kinds: [BevelKind.Panel]);
        Slider(column, "Wear", "rim_noise", 0, 1, 0.65);
        Slider(column, "Rim width", "rim_width", 0.5, 4, 1.5, kinds: [BevelKind.Panel]);
        Slider(column, "Bevel width", "bevel_width", 0, 20, 6, kinds: [BevelKind.Panel, BevelKind.Button]);
        Slider(column, "Bevel light", "bevel_strength", 0, 0.3, 0.03, kinds: [BevelKind.Panel, BevelKind.Button]);
        Slider(column, "Corner radius", "radius", 0, 30, 6);
        return panel;
    }

    private void Slider(VBoxContainer column, string name, string parameter, double min, double max, double value, BevelKind[]? kinds = null)
    {
        var label = WithColor(new Label(), 15);
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = (max - min) / 200, Value = value };
        void Show(double v) => label.Text = $"{name}: {v:0.##}";
        Show(value);
        slider.ValueChanged += v =>
        {
            Show(v);
            foreach (var frame in _frames.Where(f => kinds is null || kinds.Contains(f.Kind)))
            {
                frame.SetParameter(parameter, (float)v);
            }
        };
        column.AddChild(label);
        column.AddChild(slider);
    }
}
