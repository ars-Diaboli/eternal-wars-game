using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>
/// A small panel of choices under a picker's button, over the screen (ADR-225: the player colours, the town crests): a click outside or a choice
/// closes it. It stands in the picker's canvas layer, so the status bar names what the pointer is over in it.
/// </summary>
internal static class PickerOverlay
{
    public static void Open(Control anchor, Control content)
    {
        Node layer = anchor;
        while (layer.GetParent() is { } parent && layer is not CanvasLayer)
        {
            layer = parent;
        }

        var backdrop = new Control { Name = "PickerOverlay", MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        backdrop.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true })
            {
                backdrop.QueueFree();
            }
        };

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTheme.Background, UiTheme.Border, UiSpacing.Gap));
        panel.AddChild(content);
        backdrop.AddChild(panel);
        layer.AddChild(backdrop);

        // Under the picker, kept on the screen.
        var below = anchor.GetGlobalRect();
        var screen = anchor.GetViewportRect().Size;
        var size = panel.GetCombinedMinimumSize();
        panel.Position = new Vector2(
            Mathf.Clamp(below.Position.X, 0, Math.Max(0, screen.X - size.X)),
            below.End.Y + size.Y <= screen.Y ? below.End.Y : Math.Max(0, below.Position.Y - size.Y));
    }

    /// <summary>Closes the panel holding <paramref name="choice"/>.</summary>
    public static void Close(Node choice)
    {
        for (var node = choice; node is not null; node = node.GetParent())
        {
            if (node.Name == "PickerOverlay")
            {
                node.QueueFree();
                return;
            }
        }
    }
}

/// <summary>
/// A player's colour on the new-game screen (ADR-225): a swatch that opens the eight colours; <see cref="Changed"/> says which was picked (the
/// screen swaps it with the player that had it).
/// </summary>
public partial class ColourPicker : Button
{
    private int _colour;

    public ColourPicker(int colour)
    {
        FocusMode = FocusModeEnum.None;
        CustomMinimumSize = new Vector2(28, 28);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        Colour = colour;
        Pressed += () => PickerOverlay.Open(this, Choices());
    }

    /// <summary>A colour was picked (not the one shown).</summary>
    public event Action<int>? Changed;

    /// <summary>The colour shown, 0 … <see cref="Player.ColourCount"/> − 1.</summary>
    public int Colour
    {
        get => _colour;
        set
        {
            _colour = value;
            Paint(this, value);
            TooltipText = $"Colour: {Player.ColourNames[value]}. Click to pick another; a colour another player has is swapped.";
            SetMeta(AutoInput.LabelMeta, $"Colour {Player.ColourNames[value]}");
        }
    }

    private Control Choices()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UiSpacing.Gap);
        for (var colour = 0; colour < Player.ColourCount; colour++)
        {
            var picked = colour;
            var swatch = new Button { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(32, 32), TooltipText = Player.ColourNames[colour] };
            swatch.SetMeta(AutoInput.LabelMeta, Player.ColourNames[colour]);
            Paint(swatch, colour, selected: colour == _colour);
            swatch.Pressed += () =>
            {
                PickerOverlay.Close(swatch);
                if (picked != _colour)
                {
                    Changed?.Invoke(picked);
                }
            };
            row.AddChild(swatch);
        }

        return row;
    }

    private static void Paint(Button button, int colour, bool selected = false)
    {
        var fill = Palette.ForColour(colour);
        foreach (var (state, shade) in new[] { ("normal", fill), ("hover", fill.Lightened(0.2f)), ("pressed", fill.Darkened(0.2f)), ("focus", fill) })
        {
            button.AddThemeStyleboxOverride(state, UiTheme.Box(shade, selected ? Colors.White : UiTheme.Border, 0, borderWidth: selected ? 3 : 1));
        }
    }
}

/// <summary>
/// A player's town on the new-game screen (ADR-225): the chosen town's crest ("?" for Random), which opens the eight towns' crests in a 2 × 4
/// panel with a "Random" button under it. Hovering a crest puts the town's name on the status bar.
/// </summary>
public partial class CrestPicker : Button
{
    /// <summary>The crest's size on the row.</summary>
    private const int RowCrest = 28;

    /// <summary>A crest's size in the panel.</summary>
    private const int PanelCrest = 48;

    private string? _town;

    public CrestPicker()
    {
        FocusMode = FocusModeEnum.None;
        CustomMinimumSize = new Vector2(RowCrest + (2 * UiIcons.IconPadding), RowCrest + (2 * UiIcons.IconPadding));
        SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        IconAlignment = HorizontalAlignment.Center;
        Town = null;
        Pressed += () => PickerOverlay.Open(this, Choices());
    }

    /// <summary>The town picked (another than the one shown).</summary>
    public event Action? Changed;

    /// <summary>The chosen town type (<see cref="BuiltInMapObjects.PlayableTowns"/>); null is Random.</summary>
    public string? Town
    {
        get => _town;
        set
        {
            _town = value;
            var crest = Heraldry.Crest(value, RowCrest);
            Icon = crest;
            Text = crest is not null ? "" : value is null ? "?" : NameOf(value);
            var name = value is null ? "Random" : NameOf(value);
            TooltipText = $"Town: {name}. Click to choose.";
            SetMeta(AutoInput.LabelMeta, name);
        }
    }

    /// <summary>The town's name (the faction), or its id when the catalog does not know it.</summary>
    public static string NameOf(string town) =>
        MapObjectCatalog.Default.TryGet(BuiltInMapObjects.TownId(town), out var definition) ? definition.Name : town;

    private Control Choices()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", UiSpacing.Gap);
        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", UiSpacing.Gap);
        grid.AddThemeConstantOverride("v_separation", UiSpacing.Gap);
        foreach (var town in BuiltInMapObjects.PlayableTowns)
        {
            var name = NameOf(town);
            var crest = new Button { FocusMode = FocusModeEnum.None, TooltipText = name, ToggleMode = true, ButtonPressed = town == _town };
            if (Heraldry.Crest(town, PanelCrest) is { } texture)
            {
                crest.Icon = texture;
                crest.IconAlignment = HorizontalAlignment.Center;
                crest.CustomMinimumSize = new Vector2(PanelCrest + (2 * UiIcons.IconPadding), PanelCrest + (2 * UiIcons.IconPadding));
                crest.SetMeta(AutoInput.LabelMeta, name);
            }
            else
            {
                crest.Text = name;
            }

            crest.Pressed += () => Pick(crest, town);
            grid.AddChild(crest);
        }

        box.AddChild(grid);
        var random = new Button { Text = "Random", FocusMode = FocusModeEnum.None, TooltipText = "A town drawn when the game starts." };
        random.Pressed += () => Pick(random, null);
        box.AddChild(random);
        return box;
    }

    private void Pick(Node choice, string? town)
    {
        PickerOverlay.Close(choice);
        if (town != _town)
        {
            Town = town;
            Changed?.Invoke();
        }
    }
}

/// <summary>
/// The game's difficulty on the new-game screen (ADR-225): five square buttons, the chess pieces from pawn to queen, one pressed at a time.
/// Hovering one puts its name and what it means (the starting resources, the score factor) on the status bar.
/// </summary>
public partial class DifficultyRow : HBoxContainer
{
    private readonly Dictionary<Difficulty, Button> _buttons = [];

    public DifficultyRow()
    {
        AddThemeConstantOverride("separation", UiSpacing.Gap);
        var group = new ButtonGroup();
        foreach (var level in Enum.GetValues<Difficulty>())
        {
            var button = new Button { ToggleMode = true, ButtonGroup = group, FocusMode = FocusModeEnum.None };
            if (DifficultyIcons.Get(level) is { } texture)
            {
                button.Icon = texture;
                button.IconAlignment = HorizontalAlignment.Center;
                var side = UiIcons.WindowSize + (2 * UiIcons.IconPadding);
                button.CustomMinimumSize = new Vector2(side, side);
            }
            else
            {
                button.Text = level.ToString();
                button.CustomMinimumSize = new Vector2(76, 0);
            }

            button.SetMeta(AutoInput.LabelMeta, level.ToString());
            button.Pressed += () => Changed?.Invoke();
            _buttons[level] = button;
            AddChild(button);
        }

        Show(Difficulty.Normal);
    }

    /// <summary>The player pressed another level.</summary>
    public event Action? Changed;

    /// <summary>The level pressed.</summary>
    public Difficulty Selected => _buttons.First(b => b.Value.ButtonPressed).Key;

    /// <summary>
    /// Presses <paramref name="level"/>: a hand-made map's own difficulty (Normal when it sets none), Normal on a random map (ADR-225). The level
    /// pressed sets every player's start, the map's own too: a map file has no starting resources of its own (ADR-227).
    /// </summary>
    public void Show(Difficulty level)
    {
        foreach (var (each, button) in _buttons)
        {
            button.SetPressedNoSignal(each == level);
            button.TooltipText = DifficultyText.Describe(each);
        }
    }
}
