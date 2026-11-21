using System.Reflection;
using System.Text.RegularExpressions;
using EternalWars.Core.Domain.Editing;
using Godot;

namespace EternalWars.Godot.UI.Editor.Help;

/// <summary>A control whose parts have help topics of their own (a list's rows): the topic at a point of the screen, null for none.</summary>
public interface IHelpTopics
{
    string? HelpTopicAt(Vector2 position);
}

/// <summary>
/// The editor's F1 help (ADR-276): the <see cref="HelpBook"/> made from the design documents built into this assembly, and the page for
/// what is under the pointer. From the hovered control outwards, the first that names a page wins: a control's own topics
/// (<see cref="IHelpTopics"/>), a topic set with <see cref="Tag"/>, the hovered control's text (a button's, a label's, a tab's), then each
/// control's node name ("Inspector", "Problems").
/// </summary>
public static partial class EditorHelp
{
    /// <summary>The map editor's document.</summary>
    public const string Tools = "tools.md";

    /// <summary>The templates' document: its pages come first on the Template tab.</summary>
    public const string RandomMaps = "random-maps.md";

    private const string TopicMeta = "help_topic";

    private static readonly Lazy<HelpBook> LazyBook = new(() => HelpBook.Parse(
    [
        new HelpSource(Tools, Read(Tools), ["Map editor"]),
        new HelpSource(RandomMaps, Read(RandomMaps), ["The template", "Editor", "Template editor"]),
    ]));

    public static HelpBook Book => LazyBook.Value;

    /// <summary>The page the help opens at when nothing under the pointer has one.</summary>
    public static HelpPage Start => Book.Pages[0];

    /// <summary>Gives <paramref name="control"/> (and what is inside it, unless it has its own) the help topic <paramref name="topic"/>: a page id or title.</summary>
    public static T Tag<T>(this T control, string topic) where T : Control
    {
        control.SetMeta(TopicMeta, topic);
        return control;
    }

    /// <summary>The page for <paramref name="hovered"/> at <paramref name="position"/> (null: <see cref="Start"/>), its own document's pages first.</summary>
    public static HelpPage PageAt(Control? hovered, Vector2 position, string preferredSource)
    {
        foreach (var topic in Topics(hovered, position))
        {
            if (Book.Find(topic, preferredSource) is { } page)
            {
                return page;
            }
        }

        return Start;
    }

    /// <summary>The topics <see cref="PageAt"/> tries, in order.</summary>
    public static IEnumerable<string> Topics(Control? hovered, Vector2 position)
    {
        for (Node? node = hovered; node is not null and not Window; node = node.GetParent())
        {
            if (node is IHelpTopics own && own.HelpTopicAt(position) is { } topic)
            {
                yield return topic;
            }

            if (node.HasMeta(TopicMeta))
            {
                yield return node.GetMeta(TopicMeta).AsString();
            }

            // A tab names the page of what it shows: its control's topic, or the topic of the panel a scroll box wraps for it.
            if (node == hovered && TabControl(node, position) is { } tab
                && (tab.HasMeta(TopicMeta) ? tab : tab.GetChildren().OfType<Control>().FirstOrDefault(c => c.HasMeta(TopicMeta))) is { } shown)
            {
                yield return shown.GetMeta(TopicMeta).AsString();
            }

            if (node == hovered && Text(node, position) is { Length: > 0 } text)
            {
                yield return text;
            }

            var name = node.Name.ToString();
            if (!name.StartsWith('@'))
            {
                yield return Spaced().Replace(name, " "); // "MapSettings": "Map Settings"
            }
        }
    }

    /// <summary>What the tab of a tab container under <paramref name="position"/> shows; null off its tabs.</summary>
    private static Control? TabControl(Node node, Vector2 position)
    {
        var bar = node switch { TabBar b => b, TabContainer c => c.GetTabBar(), _ => null };
        var tabs = node as TabContainer ?? (bar?.GetParent() is { } up ? up as TabContainer ?? up.GetParent() as TabContainer : null); // the bar may sit in a box of the container's
        var index = bar is null ? -1 : TabAt(bar, position);
        return tabs is not null && index >= 0 ? tabs.GetTabControl(index) : null;
    }

    private static int TabAt(TabBar bar, Vector2 position) =>
        Enumerable.Range(0, bar.TabCount).FirstOrDefault(i => bar.GetTabRect(i).HasPoint(bar.GetGlobalTransformWithCanvas().AffineInverse() * position), -1);

    private static string? Text(Node node, Vector2 position) => node switch
    {
        TabBar bar => TabAt(bar, position) is >= 0 and var tab ? bar.GetTabTitle(tab) : null,
        TabContainer tabs => Text(tabs.GetTabBar(), position),
        Button { Text.Length: > 0 } button => button.Text,
        Control { TooltipText.Length: > 0 } tipped => TipName().Match(tipped.TooltipText).Value, // an icon button: its tooltip's name
        Label label => label.Text,
        _ => null,
    };

    private static string Read(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"help/{name}")
            ?? throw new InvalidOperationException($"The help's document {name} is not built in.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"^[^(:\n]+")]
    private static partial Regex TipName();

    [GeneratedRegex(@"(?<=[a-z])(?=[A-Z])")]
    private static partial Regex Spaced();
}
