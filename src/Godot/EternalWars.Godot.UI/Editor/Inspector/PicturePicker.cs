using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Editor.Shell;
using Godot;

namespace EternalWars.Godot.UI.Editor.Inspector;

/// <summary>
/// One thing a <see cref="PicturePicker"/> offers: its key (empty for "none", "random" and the like), its name, the group it is listed
/// under and its picture.
/// </summary>
internal sealed record PickerItem(string Key, string Text, string Group, Texture2D Picture)
{
    /// <summary>The picture's side in the grid.</summary>
    public const int PictureSize = 32;

    /// <summary>An item drawn with the lettered placeholder of its name (no portrait or icon exists yet, ADR-245).</summary>
    public static PickerItem Of(string key, string text, string group) => new(key, text, group, PlaceholderIcons.Of(text, PictureSize));

    /// <summary>The choice of nothing in particular ("— none —", "Random"), listed first.</summary>
    public static PickerItem None(string text) => new("", text, "", UiIcons.Get(UiIcon.Ban, PictureSize) ?? PlaceholderIcons.Of(text, PictureSize));
}

/// <summary>The things a <see cref="PicturePicker"/> offers, by kind: each item with its group and its placeholder picture.</summary>
internal static class PickerItems
{
    /// <summary>The creatures by town and tier (<paramref name="where"/> narrows them).</summary>
    public static List<PickerItem> Creatures(GameState scenario, Func<CreatureDefinition, bool>? where = null) =>
    [
        .. scenario.Creatures.All.Where(c => where?.Invoke(c) ?? true).OrderBy(c => c.Town ?? "~").ThenBy(c => c.Tier)
            .Select(c => PickerItem.Of(c.Id.Value, $"{c.Name} (T{c.Tier})", Capital(c.Town ?? "neutral"))),
    ];

    /// <summary>The roster's heroes by town.</summary>
    public static List<PickerItem> Heroes(GameState scenario) =>
        [.. scenario.HeroRoster.All.OrderBy(h => h.Town).ThenBy(h => h.Name).Select(h => PickerItem.Of(h.Id, h.Name, $"{Capital(h.Town)}, {h.Class}"))];

    /// <summary>The artifacts by slot, else by tier.</summary>
    public static List<PickerItem> Artifacts(GameState scenario, bool bySlot = false) =>
    [
        .. scenario.Artifacts.All.OrderBy(a => bySlot ? (int)a.Slot : (int)a.Tier).ThenBy(a => a.Name)
            .Select(a => PickerItem.Of(a.Id.Value, a.Name, bySlot ? $"{a.Slot}" : $"{a.Tier}")),
    ];

    /// <summary><paramref name="spells"/> by level and school.</summary>
    public static List<PickerItem> Spells(IEnumerable<SpellDefinition> spells) =>
    [
        .. spells.OrderBy(s => s.Level).ThenBy(s => s.School).ThenBy(s => s.Name).Select(s =>
            SpellIcons.Get(s.Id, PickerItem.PictureSize) is { } icon
                ? new PickerItem(s.Id.Value, s.Name, $"Level {s.Level}, {s.School}", icon)
                : PickerItem.Of(s.Id.Value, s.Name, $"Level {s.Level}, {s.School}")),
    ];

    /// <summary>The secondary skills by group.</summary>
    public static List<PickerItem> Skills() =>
        [.. BuiltInSkills.All.OrderBy(s => s.Group).ThenBy(s => s.Name).Select(s => PickerItem.Of(s.Id, s.Name, Capital(BuiltInSkills.GetGroup(s.Group).Name)))];

    public static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}

/// <summary>
/// A picker with pictures (EDIT-1, ADR-255) in place of a drop-down, for creatures, heroes, artifacts, spells and skills: a button
/// with the picked item's picture and name; a click opens a grid under the row with a search field, the items by group, each with
/// its picture. Picking one closes the grid and raises <see cref="Picked"/>. When the objects edited together differ, the button
/// shows "—" until something is picked.
/// </summary>
internal sealed partial class PicturePicker : Button
{
    /// <summary>How many items the grid shows at most; typing narrows it.</summary>
    private const int MaxShown = 90;

    private const int ItemWidth = 104;

    /// <summary>The picker whose grid is open: one at a time.</summary>
    private static PicturePicker? _opened;

    private IReadOnlyList<PickerItem> _items = [];
    private string? _value;
    private VBoxContainer? _grid;

    /// <summary>An item was picked; its key.</summary>
    public event Action<string>? Picked;

    /// <summary>The picked key ("" for none), or "" while the edited objects differ.</summary>
    public string Value() => _value ?? "";

    /// <summary>True while the edited objects differ and nothing was picked.</summary>
    public bool Differs => _value is null;

    /// <summary>
    /// Adds a picker to <paramref name="row"/>, after <paramref name="label"/> if given, with <paramref name="selected"/> picked; null
    /// shows "—" (the objects differ).
    /// </summary>
    public static PicturePicker Add(Container row, string? label, IReadOnlyList<PickerItem> items, string? selected)
    {
        if (!string.IsNullOrEmpty(label))
        {
            row.AddChild(new Label { Text = label });
        }

        var picker = new PicturePicker
        {
            _items = items,
            Alignment = HorizontalAlignment.Left,
            ClipText = true,
            CustomMinimumSize = new Vector2(150, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ExpandIcon = false,
        };
        picker.AddThemeConstantOverride("icon_max_width", UiIcons.ButtonSize);
        picker.Show(selected);
        picker.Pressed += picker.Toggle;
        row.AddChild(picker);
        return picker;
    }

    private void Show(string? key)
    {
        _value = key;
        var item = key is null ? null : _items.FirstOrDefault(i => i.Key == key);
        Text = key is null ? "—" : item?.Text ?? key; // a key the list lacks still shows, by its own text
        Icon = item?.Picture;
        TooltipText = key is null ? "The selected objects differ: pick one to set it on all of them" : $"{Text}: click to choose another";
    }

    private void Toggle()
    {
        if (_grid is not null)
        {
            Close();
            return;
        }

        if (_opened is not null && IsInstanceValid(_opened))
        {
            _opened.Close();
        }

        _opened = this;

        // The grid goes under the row the picker stands in, in the part's column.
        Control anchor = this;
        while (anchor.GetParent() is Control parent and not VBoxContainer)
        {
            anchor = parent;
        }

        _grid = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var search = new LineEdit { PlaceholderText = "Search…", ClearButtonEnabled = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, ThemeTypeVariation = UiTheme.TilePanel };
        var inner = new VBoxContainer();
        var top = new HBoxContainer();
        top.AddChild(search);
        var close = new Button { Text = "Close", TooltipText = "Close without choosing" };
        close.Pressed += Close;
        top.AddChild(close);
        inner.AddChild(top);
        inner.AddChild(list);
        panel.AddChild(inner);
        _grid.AddChild(panel);
        anchor.AddSibling(_grid);
        search.TextChanged += text => Fill(list, text);
        search.TextSubmitted += text =>
        {
            // Enter takes the first item found by a search; with nothing typed it takes nothing (the first item is often "none").
            if (text.Trim().Length > 0 && Found(text).FirstOrDefault() is { } first)
            {
                Pick(first.Key);
            }
        };
        Fill(list, "");
        search.CallDeferred(Control.MethodName.GrabFocus);

        // The grid scrolls into view once it has its size.
        var grid = _grid;
        for (Node? up = GetParent(); up is not null; up = up.GetParent())
        {
            if (up is ScrollContainer scroll)
            {
                Callable.From(() =>
                {
                    if (IsInstanceValid(grid))
                    {
                        scroll.EnsureControlVisible(grid);
                    }
                }).CallDeferred();
                break;
            }
        }
    }

    private IEnumerable<PickerItem> Found(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return _items.Where(i => words.All(w => i.Text.Contains(w, StringComparison.OrdinalIgnoreCase) || i.Group.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }

    private void Fill(VBoxContainer list, string text)
    {
        list.ClearChildren(detach: true);
        var found = Found(text).ToList();
        foreach (var group in found.Take(MaxShown).GroupBy(i => i.Group))
        {
            if (group.Key.Length > 0)
            {
                list.AddChild(new Label { Text = group.Key, ThemeTypeVariation = UiTheme.QuietLabel });
            }

            var flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            list.AddChild(flow);
            foreach (var item in group)
            {
                var button = new Button
                {
                    Text = item.Text, Icon = item.Picture, IconAlignment = HorizontalAlignment.Center, VerticalIconAlignment = VerticalAlignment.Top,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(ItemWidth, 0), TooltipText = item.Group.Length > 0 ? $"{item.Text} ({item.Group})" : item.Text,
                    ToggleMode = true, ButtonPressed = item.Key == _value,
                };
                button.AddThemeConstantOverride("icon_max_width", PickerItem.PictureSize);
                var key = item.Key;
                button.Pressed += () => Pick(key);
                flow.AddChild(button);
            }
        }

        if (found.Count > MaxShown)
        {
            list.AddChild(new Label { Text = $"{found.Count - MaxShown} more: type to narrow the list", ThemeTypeVariation = UiTheme.QuietLabel });
        }
        else if (found.Count == 0)
        {
            list.AddChild(new Label { Text = "Nothing found", ThemeTypeVariation = UiTheme.QuietLabel });
        }
    }

    private void Pick(string key)
    {
        Close();
        if (key == _value)
        {
            return;
        }

        Show(key);
        Picked?.Invoke(key);
    }

    private void Close()
    {
        _grid?.QueueFree();
        _grid = null;
        if (_opened == this)
        {
            _opened = null;
        }
    }
}
