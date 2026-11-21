using System.Text.RegularExpressions;
using EternalWars.Core.Domain.Editing;
using EternalWars.Godot.UI.Editor.Shell;
using Godot;

namespace EternalWars.Godot.UI.Editor.Help;

/// <summary>
/// The editor's help (ADR-276), a box over the whole editor: the pages of the <see cref="HelpBook"/> by document on the left (a search
/// field over them lists the pages whose titles fit), the page on the right with what it is part of and its sub-pages, whose links, like
/// the page's own links to other pages, open them. Escape or Close closes it.
/// </summary>
public partial class HelpWindow : Control
{
    private readonly LineEdit _search = new() { Name = "Search", PlaceholderText = "Find a page", ClearButtonEnabled = true };
    private readonly Tree _pages = new() { Name = "Pages", HideRoot = true, CustomMinimumSize = new Vector2(260, 460) };
    private readonly RichTextLabel _page = new()
    {
        Name = "Page", BbcodeEnabled = true, CustomMinimumSize = new Vector2(600, 460), SizeFlagsHorizontal = SizeFlags.ExpandFill,
        SelectionEnabled = true, FocusMode = FocusModeEnum.None,
    };

    private readonly Dictionary<string, TreeItem> _items = [];
    private HelpBook _book = null!;

    /// <summary>The page shown.</summary>
    public HelpPage Shown { get; private set; } = null!;

    /// <summary>Opens at <paramref name="page"/> of <paramref name="book"/>.</summary>
    public void Bind(HelpBook book, HelpPage page)
    {
        _book = book;
        var rows = EditorOverlay.Build(this);
        rows.AddChild(new Label { Text = "Help", HorizontalAlignment = HorizontalAlignment.Center });
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", UiSpacing.Section);
        rows.AddChild(columns);
        var left = new VBoxContainer();
        columns.AddChild(left);
        left.AddChild(_search);
        left.AddChild(_pages);
        columns.AddChild(_page);
        _search.TextChanged += _ => List();
        _pages.ItemSelected += () =>
        {
            if (_pages.GetSelected()?.GetMetadata(0).AsString() is { Length: > 0 } id && _book[id] is { } chosen && chosen != Shown)
            {
                Show(chosen);
            }
        };
        _page.MetaClicked += meta =>
        {
            if (_book[meta.AsString()] is { } linked)
            {
                Show(linked);
            }
        };

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        rows.AddChild(buttons);
        var close = new Button { Text = "Close", CustomMinimumSize = new Vector2(110, 0) };
        close.Pressed += () => EditorOverlay.Close(this);
        buttons.AddChild(close);

        List();
        Show(page);
        _search.CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            GetViewport()?.SetInputAsHandled();
            EditorOverlay.Close(this);
        }
    }

    /// <summary>Shows <paramref name="page"/> and picks it in the list.</summary>
    public void Show(HelpPage page)
    {
        Shown = page;
        _page.Text = Render(page);
        _page.ScrollToLine(0);
        Reveal();
    }

    /// <summary>Picks the shown page in the list, its parents opened; nothing picked when the list does not hold it (a search).</summary>
    private void Reveal()
    {
        _pages.DeselectAll();
        if (Shown is not null && _items.TryGetValue(Shown.Id, out var item))
        {
            for (var parent = item.GetParent(); parent is not null; parent = parent.GetParent())
            {
                parent.Collapsed = false;
            }

            item.Select(0);
            _pages.ScrollToItem(item);
        }
    }

    /// <summary>The pages by document and as sub-pages, or with a search typed the pages that fit, the best first.</summary>
    private void List()
    {
        _pages.Clear();
        _items.Clear();
        var root = _pages.CreateItem();
        var query = _search.Text.Trim();
        if (query.Length > 0)
        {
            foreach (var found in _book.Search(query))
            {
                Add(root, found);
            }

            Reveal();
            return;
        }

        foreach (var page in _book.Pages)
        {
            var parent = page.Parent is { } id && _items.TryGetValue(id, out var owner) ? owner : root;
            var item = Add(parent, page);
            item.Collapsed = page.Parent is not null; // the documents' headings open, the rest closed until shown
        }

        Reveal();
    }

    private TreeItem Add(TreeItem parent, HelpPage page)
    {
        var item = _pages.CreateItem(parent);
        item.SetText(0, page.Title);
        item.SetMetadata(0, page.Id);
        _items[page.Id] = item;
        return item;
    }

    /// <summary>The page as rich text: its title, its text, what it is part of and its sub-pages.</summary>
    private string Render(HelpPage page)
    {
        var text = $"[font_size=20][b]{Escape(page.Title)}[/b][/font_size]\n\n{Rich(page.Text)}";
        if (page.Children.Count > 0)
        {
            text += "\n\n[b]More:[/b] " + string.Join(", ", page.Children.Select(id => Link(id, _book[id]!.Title)));
        }

        if (page.Parent is { } parent)
        {
            text += $"\n\n[b]Part of:[/b] {Link(parent, _book[parent]!.Title)}";
        }

        return text + $"\n\n[color=#999999]From {page.Source}[/color]";
    }

    /// <summary>The documents' markdown as rich text: bold, code, links to pages (other links as their words), tables as lines.</summary>
    private string Rich(string markdown)
    {
        // The links first, kept aside, so the other square brackets can be shown as they are rather than read as tags.
        var links = new List<string>();
        var text = MarkdownLink().Replace(markdown, m =>
        {
            var words = Escape(m.Groups["text"].Value);
            links.Add(_book.Linked(m.Groups["url"].Value) is { } linked ? Link(linked.Id, words) : words);
            return $"\u0001{links.Count - 1}\u0002";
        });
        text = Escape(text);
        text = Bold().Replace(text, "[b]$1[/b]");
        text = Code().Replace(text, "[code]$1[/code]");
        text = TableRule().Replace(text, "");
        text = TableCells().Replace(text, m => m.Value.Trim('|').Replace(" | ", " — ").Trim());
        return Kept().Replace(text, m => links[int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)]);
    }

    private static string Link(string id, string text) => $"[url={id}]{text}[/url]";

    /// <summary>A square bracket shown as it is, not read as a tag.</summary>
    private static string Escape(string text) => text.Replace("[", "[lb]");

    [GeneratedRegex(@"\*\*([^*]+)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex Code();

    [GeneratedRegex(@"\[(?<text>[^\]]+)\]\((?<url>[^)]+)\)")]
    private static partial Regex MarkdownLink();

    [GeneratedRegex("\u0001(\\d+)\u0002")]
    private static partial Regex Kept();

    [GeneratedRegex(@"(?m)^\|[-| ]+\|$\n?")]
    private static partial Regex TableRule();

    [GeneratedRegex(@"(?m)^\|.*\|$")]
    private static partial Regex TableCells();
}
