using System.Text;
using System.Text.RegularExpressions;

namespace EternalWars.Core.Domain.Editing;

/// <summary>A design document the editor's help is made from (ADR-276): its file name, its text, and the <c>##</c> sections read.</summary>
public sealed record HelpSource(string Name, string Markdown, IReadOnlyList<string> Sections);

/// <summary>
/// One page of the editor's help (ADR-276): a paragraph or a bullet of a design document that starts with a bold term, or a heading.
/// <see cref="Text"/> is the document's own markdown (bold, code and links kept) without the decision references; <see cref="Aliases"/>
/// are the other bold terms in it, which find the page too.
/// </summary>
public sealed record HelpPage(string Id, string Title, string Source, string Text, string? Parent)
{
    public IReadOnlyList<string> Children { get; init; } = [];

    public IReadOnlyList<string> Aliases { get; init; } = [];
}

/// <summary>
/// The editor's F1 help (ADR-276), made from the design documents' text so it never says other than they do. A heading, and every
/// paragraph or bullet that starts with a bold term ("**Brushes**", "- The **Select** tool"), is a page titled with that term; a bullet
/// with a bold term under such a paragraph (or a nested one under such a bullet) is a page of its own, the paragraph's child. Text with no
/// bold term at its start belongs to the page above it. An anchor (<c>&lt;a id="…"&gt;</c>) right before a page is its id.
/// </summary>
public sealed partial class HelpBook
{
    private readonly Dictionary<string, HelpPage> _byId;

    private HelpBook(IReadOnlyList<HelpPage> pages)
    {
        Pages = pages;
        _byId = pages.ToDictionary(p => p.Id);
    }

    /// <summary>The pages in the documents' order.</summary>
    public IReadOnlyList<HelpPage> Pages { get; }

    public HelpPage? this[string id] => _byId.GetValueOrDefault(id);

    /// <summary>
    /// The page a markdown link's target names: "#editor-brushes" (an anchor, or a page id), "random-maps.md#template-editor" (the
    /// document's heading or page of that slug); null for a link out of the help.
    /// </summary>
    public HelpPage? Linked(string target)
    {
        var hash = target.IndexOf('#');
        if (hash < 0)
        {
            return null;
        }

        var fragment = target[(hash + 1)..];
        var file = hash > 0 ? Path.GetFileNameWithoutExtension(target[..hash]) : null;
        return this[fragment] ?? (file is null ? null : this[$"{file}-{fragment}"]);
    }

    /// <summary>
    /// The page for <paramref name="topic"/>: a page id, else a page whose title is the topic (<see cref="Key"/>: case, articles, a trailing
    /// "tool" or "tab" and a plural do not count), else one that has it as an alias; first in <paramref name="preferredSource"/> (a title,
    /// then an alias), then in the others; the first of several. Null when none fits.
    /// </summary>
    public HelpPage? Find(string? topic, string? preferredSource = null)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return null;
        }

        if (_byId.TryGetValue(topic, out var byId))
        {
            return byId;
        }

        var key = Key(topic);
        if (key.Length == 0)
        {
            return null;
        }

        bool Titled(HelpPage p) => Key(p.Title) == key;
        bool Aliased(HelpPage p) => p.Aliases.Any(a => Key(a) == key);
        return Pages.FirstOrDefault(p => p.Source == preferredSource && Titled(p)) ?? Pages.FirstOrDefault(p => p.Source == preferredSource && Aliased(p))
            ?? Pages.FirstOrDefault(Titled) ?? Pages.FirstOrDefault(Aliased);
    }

    /// <summary>The pages whose titles fit <paramref name="query"/>, the best first (as the command palette searches).</summary>
    public IReadOnlyList<HelpPage> Search(string query) => [.. CommandSearch.Find(query, [.. Pages.Select(p => p.Title)]).Select(i => Pages[i])];

    /// <summary>A title or topic as pages are matched: letters and digits in lower case, without "the", "a" or "an", a trailing tool / tab / panel / window, or a final s.</summary>
    public static string Key(string text)
    {
        var words = Words().Matches(text.ToLowerInvariant()).Select(m => m.Value).ToList();
        if (words.Count > 1 && words[0] is "the" or "a" or "an")
        {
            words.RemoveAt(0);
        }

        if (words.Count > 1 && words[^1] is "tool" or "tab" or "panel" or "window")
        {
            words.RemoveAt(words.Count - 1);
        }

        var key = string.Concat(words);
        return key.Length <= 3 || !key.EndsWith('s') || key.EndsWith("ss", StringComparison.Ordinal) ? key
            : key.EndsWith("shes", StringComparison.Ordinal) || key.EndsWith("ches", StringComparison.Ordinal) || key.EndsWith("xes", StringComparison.Ordinal) || key.EndsWith("sses", StringComparison.Ordinal) ? key[..^2]
            : key[..^1];
    }

    /// <summary>Reads the pages of <paramref name="sources"/>, in order.</summary>
    public static HelpBook Parse(IEnumerable<HelpSource> sources)
    {
        var pages = new List<Builder>();
        foreach (var source in sources)
        {
            Read(source, pages);
        }

        var ids = new HashSet<string>();
        foreach (var page in pages)
        {
            var id = page.Anchor ?? $"{Path.GetFileNameWithoutExtension(page.Source)}-{Slug(page.Title)}";
            var unique = id;
            for (var n = 2; !ids.Add(unique); n++)
            {
                unique = $"{id}-{n}";
            }

            page.Id = unique;
        }

        return new HelpBook([.. pages.Select(p => new HelpPage(p.Id, p.Title, p.Source, Clean(p.Text.ToString().Trim()), p.Parent?.Id)
        {
            Children = [.. pages.Where(c => c.Parent == p).Select(c => c.Id)],
            Aliases = [.. p.Aliases.Distinct().Where(a => Key(a) != Key(p.Title))],
        })]);
    }

    /// <summary>
    /// <paramref name="text"/> without the decision and question references a reader of the help does not need: "(ADR-250)" and
    /// "(EDIT-1, ADR-250)" go, "(the Start zone tool; MP-4, ADR-197)" keeps its words.
    /// </summary>
    public static string Clean(string text)
    {
        text = OnlyReferences().Replace(text, "");
        text = LeadingReferences().Replace(text, "(");
        text = TrailingReferences().Replace(text, ")");
        text = InnerReferences().Replace(text, "");
        return Spaces().Replace(text, " ").Trim().Replace(" ,", ",").Replace(" .", ".").Replace(" ;", ";").Replace(" :", ":");
    }

    private sealed class Builder(string source, string title, int level, Builder? parent, string? anchor)
    {
        public string Source { get; } = source;
        public string Title { get; } = title;
        public int Level { get; } = level;
        public Builder? Parent { get; } = parent;
        public string? Anchor { get; } = anchor;
        public StringBuilder Text { get; } = new();
        public List<string> Aliases { get; } = [];
        public string Id { get; set; } = "";
    }

    private sealed record Block(int Level, string Text, string? Anchor, bool Heading);

    private static void Read(HelpSource source, List<Builder> pages)
    {
        var open = new List<Builder>(); // the pages that own what follows, outermost first
        foreach (var block in Blocks(source))
        {
            if (block.Heading)
            {
                while (open.Count > 0 && open[^1].Level >= block.Level)
                {
                    open.RemoveAt(open.Count - 1);
                }

                var heading = new Builder(source.Name, Title(block.Text), block.Level, open.LastOrDefault(), block.Anchor);
                pages.Add(heading);
                open.Add(heading);
                continue;
            }

            var lead = Lead().Match(block.Text);
            // Text without a bold term belongs to the page above: a paragraph to the paragraph before it, a bullet to what owns its level.
            while (open.Count > 1 && (lead.Success || block.Level > 0 ? open[^1].Level >= block.Level : open[^1].Level > block.Level))
            {
                open.RemoveAt(open.Count - 1);
            }

            if (open.Count == 0)
            {
                continue;
            }

            var owner = open[^1];
            if (!lead.Success)
            {
                Append(owner, block);
                continue;
            }

            var page = new Builder(source.Name, Title(lead.Groups["t"].Value), block.Level, owner, block.Anchor);
            page.Text.Append(block.Text);
            page.Aliases.AddRange(Bold().Matches(block.Text).Select(m => Title(m.Groups["t"].Value)));
            pages.Add(page);
            open.Add(page);
        }
    }

    private static void Append(Builder owner, Block block)
    {
        if (owner.Text.Length > 0)
        {
            owner.Text.Append(block.Level > 0 ? "\n" : "\n\n");
        }

        owner.Text.Append(block.Level > 0 ? $"{new string(' ', (block.Level - 1) * 2)}• {block.Text}" : block.Text);
        owner.Aliases.AddRange(Bold().Matches(block.Text).Select(m => Title(m.Groups["t"].Value)));
    }

    /// <summary>The paragraphs (level 0), bullets (1, nested 2 …) and headings (depth less 10) of the sections read, with their anchors.</summary>
    private static IEnumerable<Block> Blocks(HelpSource source)
    {
        var reading = false;
        string? anchor = null;
        (int Level, StringBuilder Text, string? Anchor)? current = null;
        var blocks = new List<Block>();

        void Close()
        {
            if (current is { } c)
            {
                blocks.Add(new Block(c.Level, c.Text.ToString().Trim(), c.Anchor, false));
                current = null;
            }
        }

        foreach (var raw in source.Markdown.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (Heading().Match(line) is { Success: true } heading)
            {
                Close();
                var depth = heading.Groups["h"].Length;
                var title = heading.Groups["t"].Value.Trim();
                if (depth <= 2)
                {
                    reading = depth == 2 && source.Sections.Any(s => title == s || title.StartsWith(s + " (", StringComparison.Ordinal));
                }

                if (reading)
                {
                    blocks.Add(new Block(depth - 10, title, anchor, true)); // below every paragraph (0) and bullet (1 and up)
                }

                anchor = null;
                continue;
            }

            if (!reading)
            {
                continue;
            }

            if (line.Length == 0)
            {
                Close();
                continue;
            }

            if (Anchor().Match(line) is { Success: true } a)
            {
                Close();
                anchor = a.Groups["id"].Value;
                continue;
            }

            if (Bullet().Match(line) is { Success: true } bullet)
            {
                Close();
                current = (bullet.Groups["i"].Length / 2 + 1, new StringBuilder(bullet.Groups["t"].Value), anchor);
                anchor = null;
                continue;
            }

            if (current is { } open)
            {
                open.Text.Append(line.StartsWith('|') ? "\n" : " ").Append(line.Trim());
            }
            else
            {
                current = (0, new StringBuilder(line.Trim()), anchor);
                anchor = null;
            }
        }

        Close();
        return blocks;
    }

    /// <summary>A bold term or a heading as a title: no quotes, closing punctuation, article, trailing "tool" or parenthesis; capitalised.</summary>
    private static string Title(string term)
    {
        var title = Parenthesis().Replace(term, "").Trim().Trim('"', '“', '”').TrimEnd(':', '.', ',', ';').Trim();
        title = Article().Replace(title, "");
        title = ToolWord().Replace(title, "");
        return title.Length == 0 ? term.Trim() : char.ToUpperInvariant(title[0]) + title[1..];
    }

    private static string Slug(string title) => SlugRun().Replace(title.ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex(@"^(?<h>#{1,6})\s+(?<t>.+)$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^<a id=""(?<id>[^""]+)""></a>$")]
    private static partial Regex Anchor();

    [GeneratedRegex(@"^(?<i>\s*)- (?<t>.*)$")]
    private static partial Regex Bullet();

    [GeneratedRegex(@"^(?:The\s+)?\*\*(?<t>[^*]+)\*\*")]
    private static partial Regex Lead();

    [GeneratedRegex(@"\*\*(?<t>[^*]+)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex(@"\s*\([^)]*\)")]
    private static partial Regex Parenthesis();

    [GeneratedRegex(@"^(?:the|a|an)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex Article();

    [GeneratedRegex(@"\s+tool$", RegexOptions.IgnoreCase)]
    private static partial Regex ToolWord();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugRun();

    [GeneratedRegex(@"\w+")]
    private static partial Regex Words();

    private const string Reference = @"[A-Z]{2,5}-\d+(?:\s[a-z])?";

    [GeneratedRegex(@"\s*\(\s*" + Reference + @"(?:\s*,\s*" + Reference + @")*\s*\)")]
    private static partial Regex OnlyReferences();

    [GeneratedRegex(@"\(\s*" + Reference + @"(?:\s*,\s*" + Reference + @")*\s*[:;,]\s*")]
    private static partial Regex LeadingReferences();

    [GeneratedRegex(@"\s*[;,]\s*" + Reference + @"(?:\s*,\s*" + Reference + @")*\s*\)")]
    private static partial Regex TrailingReferences();

    [GeneratedRegex(@"\s*,\s*" + Reference + @"(?:\s*,\s*" + Reference + @")*(?=\s*;)")]
    private static partial Regex InnerReferences();

    [GeneratedRegex(@"(?<=\S)[ \t]{2,}")]
    private static partial Regex Spaces();
}
