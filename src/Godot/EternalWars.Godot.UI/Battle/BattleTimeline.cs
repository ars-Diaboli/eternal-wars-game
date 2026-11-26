using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Info;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Hud;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Battle;

/// <summary>
/// The battle's timeline (ADR-314) in a bevelled panel: the active stack first with a bright rim, then the stacks still to act, a bronze
/// divider and the waiting stacks dimmed. Each entry is a slot like the adventure HUD's army strip (<see cref="ArmyStrip"/>): 2:3, as tall as
/// the panel, the stack's portrait filling it (the defender's mirrored to face its enemy, its name while it has none), the count in the corner,
/// rimmed in its side's colour. As many entries as fit the panel's width. The pointer over an
/// entry names the stack on the status bar and raises <see cref="Hovered"/> (the HUD shows it in the stack panel and lights it on the field); a
/// right click opens its information window.
/// </summary>
public partial class BattleTimeline : PanelContainer
{
    /// <summary>An entry's least height (the army strip's slot); it grows to the panel's height.</summary>
    public const int EntrySize = ArmyStrip.SlotSize;

    /// <summary>The size the portraits are loaded at.</summary>
    private const int PictureSize = 128;

    /// <summary>The entries' height now: the row's.</summary>
    private float _slotHeight = EntrySize;

    private const int DividerWidth = 6;

    private const string DividerName = "Divider";

    /// <summary>The divider's rim: bronze (the frames' accent where no faction gives one).</summary>
    private static readonly Color Bronze = UiFrames.Accent(null);

    private int? _hovered;
    private readonly HBoxContainer _row = new() { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Begin };

    /// <summary>
    /// The row's holder: a plain control whose least width is none, so the entries never widen the panel past the room the layout gives it;
    /// <see cref="Fit"/> hides those that do not fit.
    /// </summary>
    private readonly Control _clip = new() { ClipContents = true, MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, EntrySize) };

    public BattleTimeline()
    {
        Name = "Timeline";
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeStyleboxOverride("panel", UiFrames.Themed(BevelKind.Panel, UiSpacing.Padding, opacity: null));
        _row.AddThemeConstantOverride("separation", UiSpacing.Cell);
        _clip.AddChild(_row);
        _row.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide);
        AddChild(_clip);
        _clip.Resized += () =>
        {
            var height = Math.Max(EntrySize, _clip.Size.Y);
            if (Math.Abs(height - _slotHeight) > 0.5f)
            {
                _slotHeight = height; // as tall as the row, like the army strip's slots
                foreach (var child in _row.GetChildren().OfType<Control>())
                {
                    child.CustomMinimumSize = child.Name == DividerName ? new Vector2(DividerWidth, height) : Slot(height);
                }
            }

            Fit();
        };
    }

    /// <summary>The pointer came over an entry (its stack's id), or left it (null).</summary>
    public event Action<int?>? Hovered;

    /// <summary>The entries shown, in order (for the smoke checks): "Swordsmen ×7", the waiting ones with "(waiting)".</summary>
    public IEnumerable<string> Shown => _row.GetChildren().OfType<Control>().Where(c => c.Visible && c.HasMeta(UiIcons.StatusLabelMeta))
        .Select(c => c.GetMeta(UiIcons.StatusLabelMeta).AsString());

    /// <summary>Shows <paramref name="battle"/>'s order: the active stack, the queue, then the waiting stacks; <paramref name="color"/> gives a side's colour.</summary>
    public void Show(BattleState battle, Func<BattleSide, Color> color)
    {
        _row.ClearChildren(detach: true);
        if (_hovered is not null)
        {
            _hovered = null;
            Hovered?.Invoke(null); // the entry under the pointer is gone
        }

        var divided = false;
        if (battle.ActiveUnit is { IsAlive: true } active)
        {
            _row.AddChild(Entry(battle, active, color(active.Side), isActive: true, waiting: false));
        }

        foreach (var unit in battle.Queue.Select(battle.GetUnit).Where(u => u.IsAlive && u != battle.ActiveUnit))
        {
            _row.AddChild(Entry(battle, unit, color(unit.Side), isActive: false, waiting: false));
        }

        foreach (var unit in battle.Waiting.Select(battle.GetUnit).Where(u => u.IsAlive && u != battle.ActiveUnit))
        {
            if (!divided)
            {
                divided = true;
                // A groove of the bevel shader's (ADR-310) with a bronze rim.
                var divider = new PanelContainer
                {
                    Name = DividerName,
                    CustomMinimumSize = new Vector2(DividerWidth, _slotHeight), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore,
                };
                UiFrames.Bevel(divider, BevelKind.Slot, padding: 0).Tint(Bronze, 0.8f);
                _row.AddChild(divider);
            }

            _row.AddChild(Entry(battle, unit, color(unit.Side), isActive: false, waiting: true));
        }

        Visible = _row.GetChildCount() > 0;
        Fit();
    }

    /// <summary>Hides the entries that do not fit the panel's width, from the end, and the divider when no waiting stack is left after it.</summary>
    private void Fit()
    {
        var room = _clip.Size.X;
        var used = 0f;
        Control? last = null;
        foreach (var child in _row.GetChildren().OfType<Control>())
        {
            var width = child.CustomMinimumSize.X + (used > 0 ? UiSpacing.Cell : 0);
            child.Visible = room <= 0 || used + width <= room; // not placed yet: all, until the first placing
            used += width;
            last = child.Visible ? child : last;
        }

        if (last is { Name: var name } && name == DividerName)
        {
            last.Visible = false;
        }
    }

    /// <summary>A slot <paramref name="height"/> tall: 2:3, the army strip's.</summary>
    private static Vector2 Slot(float height) => new(MathF.Round(height * 2 / 3), height);

    private Control Entry(BattleState battle, BattleUnit unit, Color side, bool isActive, bool waiting)
    {
        var entry = new PanelContainer
        {
            CustomMinimumSize = Slot(_slotHeight),
            ClipContents = true,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Stop,
            Modulate = waiting ? new Color(1, 1, 1, 0.55f) : Colors.White,
        };
        entry.AddThemeStyleboxOverride("panel", UiFrames.Themed(BevelKind.Slot, 2, tint: isActive ? side.Lightened(0.35f) : side));
        var label = $"{unit.Creature.Name} ×{unit.Count}{(waiting ? " (waiting)" : string.Empty)}";
        entry.SetMeta(UiIcons.StatusLabelMeta, label);
        entry.TooltipText = isActive ? "Acts now" : waiting ? "Waits: acts at the end of the round" : "Acts later this round";
        var portrait = CreatureIcons.Of(unit, PictureSize);
        if (portrait is not null)
        {
            entry.AddChild(new TextureRect
            {
                Texture = portrait,
                FlipH = unit.Side == BattleSide.Defender, // the portrait faces right, like the attacker
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, // filling the slot, as on the army strip
                MouseFilter = MouseFilterEnum.Ignore,
            });
        }

        var count = new Label
        {
            Text = portrait is null ? $"{unit.Creature.Name}\n{unit.Count}" : unit.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            HorizontalAlignment = portrait is null ? HorizontalAlignment.Center : HorizontalAlignment.Right,
            VerticalAlignment = portrait is null ? VerticalAlignment.Center : VerticalAlignment.Bottom,
            ClipText = true,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        if (portrait is not null)
        {
            count.AddThemeFontSizeOverride("font_size", 18); // the army strip's
        }

        count.AddThemeConstantOverride("outline_size", 4);
        count.AddThemeColorOverride("font_outline_color", Colors.Black);
        entry.AddChild(count);

        var id = unit.Id;
        entry.MouseEntered += () => Hovered?.Invoke(_hovered = id);
        entry.MouseExited += () =>
        {
            if (_hovered == id)
            {
                Hovered?.Invoke(_hovered = null);
            }
        };
        entry.WithInfo(detailed => battle.FindUnit(id) is { IsAlive: true } shown ? InfoSheets.BattleStack(battle, shown, detailed) : null);
        return entry;
    }
}
