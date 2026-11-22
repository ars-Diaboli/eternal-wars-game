using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Editor.Shell;
using EternalWars.Godot.UI.Editor.Tools;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The object browser (EDIT-1, ADR-253): everything that can be put on the map, by category, with a search, a terrain filter, the
/// Favourites and the Recent list (kept in the editor's settings) and the saved stamps. A click makes an entry the Object tool's
/// choice (<see cref="BrowserChoice"/>); a right click adds it to the favourites or takes it out. The card at the bottom shows the
/// entry under the pointer, or the chosen one: its picture, its footprint, its size and a one-line rule. An entry's picture
/// (ADR-254) is its icon where one exists (a town's crest, a resource, the hero), else its model rendered by
/// <see cref="ThumbnailRenderer"/> when no other entry is drawn alike, else the lettered placeholder: a picture shared by many entries
/// (the dwellings, the mines, every monster) would not tell them apart, the letters of their names do.
/// </summary>
public partial class ObjectBrowser : VBoxContainer
{
    private const string Section = "browser";
    private const string FavouritesKey = "favourites";
    private const string RecentKey = "recent";
    private const string FavouritesHeader = "★ Favourites";
    private const string RecentHeader = "⟲ Recent";
    private const string StampsHeader = "Stamps";

    /// <summary>A picture's side in the list, in pixels.</summary>
    private const int ListPicture = 18;

    /// <summary>The picture's side on the card.</summary>
    private const int CardPicture = 56;

    /// <summary>How many found entries the list shows: more are found by typing more.</summary>
    private const int MaxFound = 60;

    private readonly LineEdit _search = new() { PlaceholderText = "Search objects…", ClearButtonEnabled = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly OptionButton _terrain = new() { TooltipText = "Shows only the scenery made for a terrain", SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly VBoxContainer _list = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly ThumbnailRenderer _thumbnails = new() { Name = "Thumbnails" };
    private readonly TextureRect _preview = new()
    {
        CustomMinimumSize = new Vector2(CardPicture, CardPicture), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, SizeFlagsVertical = SizeFlags.ShrinkBegin,
    };

    private readonly FootprintPicture _picture = new();

    /// <summary>The entry the card shows, so a picture that arrives late goes to the card only while it still shows that entry.</summary>
    private BrowserEntry? _shown;
    private readonly Label _name = new() { ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Label _detail = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Button _favourite = new() { Text = "★", ToggleMode = true, TooltipText = "Favourite: keeps the chosen entry under Favourites (also a right click on an entry)" };

    /// <summary>The headings that are open; the kept lists and the stamps are at the start.</summary>
    private readonly HashSet<string> _open = [FavouritesHeader, RecentHeader, StampsHeader];
    private readonly List<TerrainType> _terrains = [];

    /// <summary>The entry buttons the list shows now (an entry may have several: under Favourites, Recent and its category).</summary>
    private readonly List<(BrowserEntry Entry, Button Button)> _entryButtons = [];

    private BrowserCatalog _catalog = null!;
    private BrowserChoice _choice = null!;
    private EditorSettingsFile _settings = null!;
    private Func<IReadOnlyList<string>> _stampNames = () => [];
    private Action<string> _pasteStamp = _ => { };
    private IReadOnlyList<string> _favourites = [];
    private IReadOnlyList<string> _recent = [];
    private TerrainType? _pointed;

    /// <summary>The models (<see cref="ThumbnailRenderer.KeyOf"/>) that two or more entries share.</summary>
    private HashSet<string> _alike = [];

    /// <summary>
    /// Shows <paramref name="catalog"/>; a click chooses through <paramref name="choice"/>, the kept lists are read from and written to
    /// <paramref name="settings"/>; <paramref name="stampNames"/> lists the saved stamps and <paramref name="pasteStamp"/> puts one on
    /// the pointer. The rendered pictures are kept in <paramref name="thumbnailFolder"/> (null: not kept).
    /// </summary>
    public void Bind(
        BrowserCatalog catalog, BrowserChoice choice, EditorSettingsFile settings, Func<IReadOnlyList<string>> stampNames, Action<string> pasteStamp,
        string? thumbnailFolder = null)
    {
        AddChild(_thumbnails);
        _thumbnails.Bind(thumbnailFolder);
        (_catalog, _choice, _settings, _stampNames, _pasteStamp) = (catalog, choice, settings, stampNames, pasteStamp);
        _alike =
        [
            .. catalog.Entries.Select(e => e.Definition is { } d ? ThumbnailRenderer.KeyOf(d) : null).OfType<string>()
                .GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key),
        ];
        _favourites = settings.Get(Section, FavouritesKey, System.Array.Empty<string>()).AsStringArray();
        _recent = settings.Get(Section, RecentKey, System.Array.Empty<string>()).AsStringArray();

        AddChild(_search);
        _search.TextChanged += _ => Rebuild();
        _terrain.AddItem("Any terrain");
        _terrain.AddItem("Terrain under the pointer");
        foreach (var terrain in catalog.Entries.SelectMany(e => e.Terrains).Distinct().Order())
        {
            _terrains.Add(terrain);
            _terrain.AddItem(Spaced(terrain.ToString()));
        }

        _terrain.ItemSelected += _ => Rebuild();
        AddChild(_terrain);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill }.KeepBarRoom();
        scroll.AddChild(_list);
        AddChild(scroll);

        var card = new PanelContainer { ThemeTypeVariation = UiTheme.TilePanel };
        var row = new HBoxContainer();
        card.AddChild(row);
        row.AddChild(_preview);
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(text);
        var title = new HBoxContainer();
        title.AddChild(_name);
        title.AddChild(_picture);
        title.AddChild(_favourite);
        text.AddChild(title);
        _detail.ThemeTypeVariation = UiTheme.QuietLabel;
        _detail.CustomMinimumSize = new Vector2(100, 0);
        text.AddChild(_detail);
        AddChild(card);
        _favourite.WithIcon(UiIcons.Get(UiIcon.FavouriteOff, UiIcons.ButtonSize) ?? PlaceholderIcons.Of("Favourite"), UiIcons.ButtonSize);
        _favourite.Pressed += () =>
        {
            if (_choice.Entry is { } chosen)
            {
                ToggleFavourite(chosen.Id);
            }

            ShowFavouriteIcon();
        };

        _choice.Changed += OnChoice;
        _choice.Placed += OnPlaced;
        TreeExiting += () =>
        {
            _choice.Changed -= OnChoice; // the choice outlives this browser when another map is opened
            _choice.Placed -= OnPlaced;
        };
        Rebuild();
    }

    /// <summary>The terrain of the tile the pointer is over on the map, for the filter that follows it; null leaves the last one.</summary>
    public void PointAt(TerrainType? terrain)
    {
        if (terrain is null || terrain == _pointed)
        {
            return;
        }

        _pointed = terrain;
        if (_terrain.Selected == 1)
        {
            Rebuild();
        }
    }

    /// <summary>A stamp was saved: the list reads the stamps again.</summary>
    public void RefreshStamps() => Rebuild();

    private TerrainType? Filter => _terrain.Selected switch
    {
        <= 0 => null,
        1 => _pointed,
        var index => _terrains[index - 2],
    };

    /// <summary>Another entry is the choice: its buttons show it, and the card. The list stays as it is.</summary>
    private void OnChoice()
    {
        foreach (var (entry, button) in _entryButtons)
        {
            var chosen = entry.Id == _choice.Entry?.Id;
            button.SetPressedNoSignal(chosen);
            button.Flat = !chosen;
        }

        ShowCard(_choice.Entry);
    }

    private void OnPlaced(BrowserEntry entry)
    {
        if (_recent.Count > 0 && _recent[0] == entry.Id)
        {
            return; // placed again: the list stays as it is
        }

        _recent = BrowserLists.WithRecent(_recent, entry.Id);
        _settings.Set(Section, RecentKey, _recent.ToArray());
        Rebuild();
    }

    /// <summary>The favourite button's star: solid while the chosen entry is a favourite, hollow when not.</summary>
    private void ShowFavouriteIcon()
    {
        if (UiIcons.Get(_favourite.ButtonPressed ? UiIcon.Favourite : UiIcon.FavouriteOff) is { } star)
        {
            _favourite.Icon = star;
        }
    }

    private void ToggleFavourite(string id)
    {
        _favourites = BrowserLists.Toggled(_favourites, id);
        _settings.Set(Section, FavouritesKey, _favourites.ToArray());
        Rebuild();
    }

    private void Rebuild()
    {
        _list.ClearChildren(detach: true);
        _entryButtons.Clear();
        var stamps = _stampNames();
        var query = _search.Text.Trim();
        var fitting = _catalog.Search(query, Filter);
        if (query.Length > 0)
        {
            foreach (var entry in fitting.Take(MaxFound))
            {
                _list.AddChild(EntryButton(entry, withCategory: true));
            }

            var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            foreach (var stamp in stamps.Where(s => words.All(w => s.Contains(w, StringComparison.OrdinalIgnoreCase))))
            {
                _list.AddChild(StampButton(stamp));
            }

            if (fitting.Count > MaxFound)
            {
                _list.AddChild(Quiet($"…and {fitting.Count - MaxFound} more: type more to narrow the list"));
            }
            else if (_list.GetChildCount() == 0)
            {
                _list.AddChild(Quiet("Nothing fits"));
            }
        }
        else
        {
            var suits = fitting.ToHashSet();
            Heading(FavouritesHeader, Known(_favourites, suits), "right-click an entry to keep it here");
            Heading(RecentHeader, Known(_recent, suits), "what was placed last shows here");
            if (Header(StampsHeader, stamps.Count, "\"Save selection as stamp\" in the Edit menu keeps a piece of the map here"))
            {
                foreach (var stamp in stamps)
                {
                    _list.AddChild(StampButton(stamp));
                }
            }

            foreach (var category in Enum.GetValues<BrowserCategory>())
            {
                Heading(BrowserCatalog.NameOf(category), [.. fitting.Where(e => e.Category == category)], "nothing here suits the terrain", grouped: true);
            }
        }

        ShowCard(_choice.Entry);
    }

    private List<BrowserEntry> Known(IReadOnlyList<string> ids, HashSet<BrowserEntry> suits) =>
        [.. ids.Select(_catalog.Find).OfType<BrowserEntry>().Where(suits.Contains)];

    /// <summary>A heading with its entries under it while it is open; with <paramref name="grouped"/> each group gets its own line.</summary>
    private void Heading(string title, IReadOnlyList<BrowserEntry> entries, string whenEmpty, bool grouped = false)
    {
        if (!Header(title, entries.Count, whenEmpty))
        {
            return;
        }

        var group = "";
        foreach (var entry in entries)
        {
            if (grouped && entry.Group != group)
            {
                group = entry.Group;
                if (group.Length > 0)
                {
                    _list.AddChild(Quiet(group));
                }
            }

            _list.AddChild(EntryButton(entry, withCategory: !grouped));
        }
    }

    /// <summary>The button that opens and closes a heading, its tooltip saying how many it holds or why none; true when it is open.</summary>
    private bool Header(string title, int count, string whenEmpty)
    {
        var open = _open.Contains(title);
        var header = new Button
        {
            Text = title, Alignment = HorizontalAlignment.Left, ToggleMode = true, ButtonPressed = open,
            TooltipText = count > 0 ? $"{title}: {count}" : $"{title}: {whenEmpty}", Disabled = count == 0 && !open, SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        header.WithIcon(open ? UiIcon.ArrowDown : UiIcon.ArrowRight, UiIcons.ButtonSize, keepText: true);
        header.Pressed += () =>
        {
            _ = open ? _open.Remove(title) : _open.Add(title);
            Rebuild();
        };
        _list.AddChild(header);
        return open;
    }

    private Button EntryButton(BrowserEntry entry, bool withCategory)
    {
        var button = new Button
        {
            Text = entry.Name, Alignment = HorizontalAlignment.Left, ClipText = true,
            ToggleMode = true, ButtonPressed = entry.Id == _choice.Entry?.Id, Flat = entry.Id != _choice.Entry?.Id, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = withCategory ? $"{entry.Name} ({Where(entry)})" : entry.Name, // tells the monster Skeleton from the scenery of that name
        };
        button.AddThemeConstantOverride("icon_max_width", ListPicture);
        button.Icon = Picture(entry, ListPicture, texture =>
        {
            if (IsInstanceValid(button))
            {
                button.Icon = texture;
            }

            if (_shown == entry)
            {
                _preview.Texture = texture;
            }
        });
        _entryButtons.Add((entry, button));
        button.Pressed += () => _choice.Choose(entry);
        button.MouseEntered += () => ShowCard(entry);
        button.MouseExited += () => ShowCard(_choice.Entry);
        button.GuiInput += input =>
        {
            if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
            {
                ToggleFavourite(entry.Id);
            }
        };
        return button;
    }

    /// <summary>
    /// The entry's picture: its icon, else its rendered model when no other entry is drawn alike, else (and until the model is
    /// rendered, when <paramref name="whenRendered"/> gets it) the lettered placeholder, drawn at <paramref name="placeholderSize"/> pixels.
    /// </summary>
    private Texture2D Picture(BrowserEntry entry, int placeholderSize, Action<Texture2D> whenRendered)
    {
        var icon = entry switch
        {
            { Placement: BrowserPlacement.Hero } => UiIcons.Get(UiIcon.Hero, CardPicture),
            { Definition: { Kind: MapObjectKind.Town, Town: { } town } } => Heraldry.Crest(town, CardPicture),
            { Definition: { Kind: MapObjectKind.ResourcePile, Resource: { } resource } } => ResourceIcons.Get(resource, CardPicture),
            _ => null,
        };
        var own = entry.Definition is { } definition && ThumbnailRenderer.KeyOf(definition) is { } key && !_alike.Contains(key);
        return icon
            ?? (own ? _thumbnails.Get(entry.Definition!, whenRendered) : null)
            ?? PlaceholderIcons.Of(entry.Name, placeholderSize);
    }

    private Button StampButton(string name)
    {
        var button = new Button
        {
            Text = name, Icon = PlaceholderIcons.Of(name, ListPicture), Alignment = HorizontalAlignment.Left, ClipText = true, Flat = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = $"Stamp \"{name}\": click the map where it goes",
        };
        button.AddThemeConstantOverride("icon_max_width", ListPicture);
        button.Pressed += () => _pasteStamp(name);
        button.MouseEntered += () =>
        {
            _picture.Show(null);
            (_shown, _preview.Texture) = (null, PlaceholderIcons.Of(name, CardPicture));
            _name.Text = name;
            _detail.Text = "Stamp: a saved piece of the map. Click it, then click the map where it goes.";
            _favourite.Visible = false;
        };
        button.MouseExited += () => ShowCard(_choice.Entry);
        return button;
    }

    /// <summary>The card shows <paramref name="entry"/>: its picture, its footprint, where it belongs, its size and its rule.</summary>
    private void ShowCard(BrowserEntry? entry)
    {
        _shown = entry;
        _preview.Texture = entry is null ? null : Picture(entry, CardPicture, texture =>
        {
            if (_shown == entry)
            {
                _preview.Texture = texture;
            }
        });
        _picture.Show(entry?.Footprint);
        _favourite.Visible = entry is not null && entry == _choice.Entry;
        _favourite.SetPressedNoSignal(entry is not null && _favourites.Contains(entry.Id));
        ShowFavouriteIcon();
        if (entry is null)
        {
            _name.Text = "Nothing chosen";
            _detail.Text = "Click an entry, then click the map to place it.";
            return;
        }

        var footprint = entry.Footprint;
        var where = Where(entry);
        var size = $"{footprint.Width} × {footprint.Height}" + (footprint.HasVisit && footprint.OccupiedCells.Count > 1 ? ", entered at the light cell" : "");
        var terrains = entry.Terrains.Count > 0 ? $"\nFor {string.Join(", ", entry.Terrains.Select(t => Spaced(t.ToString()).ToLowerInvariant()))}." : "";
        _name.Text = entry.Name;
        _detail.Text = $"{where} · {size}\n{entry.Rule}{terrains}";
    }

    /// <summary>An entry's category, and its group when it has one: "Monsters, Haven".</summary>
    private static string Where(BrowserEntry entry) =>
        entry.Group.Length > 0 ? $"{BrowserCatalog.NameOf(entry.Category)}, {entry.Group}" : BrowserCatalog.NameOf(entry.Category);

    private static Label Quiet(string text)
    {
        return new Label
        {
            Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(100, 0), ThemeTypeVariation = UiTheme.QuietLabel,
        };
    }

    /// <summary><c>ForestFloor</c> → <c>Forest floor</c>.</summary>
    private static string Spaced(string name) => string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? $" {char.ToLowerInvariant(c)}" : c.ToString()));

    /// <summary>A footprint drawn as its cells: the blocked ones dark, the entrance light, the free ones of its box only outlined.</summary>
    private sealed partial class FootprintPicture : Control
    {
        private const float Side = 28;
        private Footprint? _footprint;

        public FootprintPicture() => CustomMinimumSize = new Vector2(Side, Side);

        public void Show(Footprint? footprint)
        {
            _footprint = footprint;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_footprint is not { } footprint)
            {
                return;
            }

            var cell = MathF.Floor(Mathf.Min(Side / footprint.Width, Side / footprint.Height));
            var origin = new Vector2((Size.X - (cell * footprint.Width)) / 2, (Size.Y - (cell * footprint.Height)) / 2);
            for (var y = 0; y < footprint.Height; y++)
            {
                for (var x = 0; x < footprint.Width; x++)
                {
                    DrawRect(new Rect2(origin + new Vector2(x * cell, y * cell), new Vector2(cell - 1, cell - 1)), UiTheme.BorderDim, filled: false);
                }
            }

            foreach (var (dx, dy, kind) in footprint.OccupiedCells)
            {
                var colour = kind == FootprintCell.Visit ? UiTheme.BorderBright : UiTheme.Border;
                DrawRect(new Rect2(origin + new Vector2(dx * cell, dy * cell), new Vector2(cell - 1, cell - 1)), colour);
            }
        }
    }
}
