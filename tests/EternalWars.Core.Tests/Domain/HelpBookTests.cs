using EternalWars.Core.Domain.Editing;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The editor's F1 help (ADR-276): <see cref="HelpBook"/>, on a small document and on the design documents it is made from.</summary>
public sealed class HelpBookTests
{
    private const string Doc = """
        # Doc
        ## Skipped
        **Hidden**: not read.

        ## Editor
        The editor's intro.
        - a bullet without a term;

        <a id="doc-brushes"></a>
        **Brushes** (EDIT-1, ADR-250). The **Terrain** and Erase tools share one brush
        across two lines.
        - **Fill** (a box; ADR-12): fills.
          - **Deep**: nested.
        - plain bullet of Brushes.

        A paragraph of Brushes (ADR-9), still.

        - The **Select** tool (V): selects.

        ### Sub
        **Brushes**: again.
        """;

    private static HelpBook Small => HelpBook.Parse([new HelpSource("doc.md", Doc, ["Editor"])]);

    [Fact]
    public void Pages_AreTheHeadingsAndTheParagraphsAndBulletsWithABoldTerm()
    {
        var book = Small;

        Assert.Equal(["Editor", "Brushes", "Fill", "Deep", "Select", "Sub", "Brushes"], book.Pages.Select(p => p.Title));
        Assert.Equal(["doc-editor", "doc-brushes", "doc-fill", "doc-deep", "doc-select", "doc-sub", "doc-brushes-2"], book.Pages.Select(p => p.Id));
        Assert.Null(book.Find("Hidden"));
    }

    [Fact]
    public void APage_HoldsTheTextWithoutABoldTermUnderIt_AndKnowsItsParentAndChildren()
    {
        var book = Small;
        var brushes = book["doc-brushes"]!;

        Assert.Equal("doc-editor", brushes.Parent);
        Assert.Equal(["doc-fill", "doc-select"], brushes.Children); // a bullet with a term under a paragraph is its page's child
        Assert.Equal(["doc-deep"], book["doc-fill"]!.Children);
        Assert.Equal("doc-brushes", book["doc-select"]!.Parent);
        Assert.Equal(
            "**Brushes**. The **Terrain** and Erase tools share one brush across two lines.\n• plain bullet of Brushes.\n\nA paragraph of Brushes, still.",
            brushes.Text);
        Assert.Equal("The editor's intro.\n• a bullet without a term;", book["doc-editor"]!.Text);
        Assert.Equal("**Fill** (a box): fills.", book["doc-fill"]!.Text);
        Assert.Equal("doc-editor", book["doc-sub"]!.Parent);
        Assert.Equal("doc-sub", book["doc-brushes-2"]!.Parent);
    }

    [Fact]
    public void Find_TakesAnId_ATitle_ThenAnAlias_PreferringTheGivenDocument()
    {
        var book = Small;

        Assert.Equal("doc-fill", book.Find("doc-fill")!.Id);
        Assert.Equal("doc-select", book.Find("the select TOOL")!.Id);
        Assert.Equal("doc-brushes", book.Find("Terrain")!.Id);
        Assert.Equal("doc-brushes", book.Find("Brush")!.Id);
        Assert.Null(book.Find("Nothing"));
        Assert.Null(book.Find("  "));

        var two = HelpBook.Parse([new HelpSource("a.md", "## Editor\n**Guard**: a.\n", ["Editor"]), new HelpSource("b.md", "## Editor\n**Guard**: b.\n", ["Editor"])]);
        Assert.Equal("a.md", two.Find("Guard")!.Source);
        Assert.Equal("b.md", two.Find("Guard", "b.md")!.Source);
    }

    [Theory]
    [InlineData("Bans (ADR-133);", "Bans;")]
    [InlineData("**Brushes** (EDIT-1, ADR-250). One", "**Brushes**. One")]
    [InlineData("(the Start zone tool; MP-4, ADR-197): a zone", "(the Start zone tool): a zone")]
    [InlineData("(ADR-112: linked portals)", "(linked portals)")]
    [InlineData("(format v9, ADR-140)", "(format v9)")]
    [InlineData("(EVT-6 c) the events", "the events")]
    [InlineData("(with guard mode, ADR-065; random by tier)", "(with guard mode; random by tier)")]
    [InlineData("(1 to 9), Ctrl+1 … 9 (G, W for lumber, O)", "(1 to 9), Ctrl+1 … 9 (G, W for lumber, O)")]
    [InlineData("the [brush](#editor-brushes) (drag)", "the [brush](#editor-brushes) (drag)")]
    public void Clean_DropsTheDecisionReferences(string text, string expected) => Assert.Equal(expected, HelpBook.Clean(text));

    // The design documents the editor's help is made from: every tool, the problems' kinds, the panels and the template's fields have a page.
    private static readonly Lazy<HelpBook> Docs = new(() =>
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "EternalWars.sln")))
        {
            root = Path.GetDirectoryName(root) ?? throw new InvalidOperationException("No repository root above the tests.");
        }

        string Read(string name) => File.ReadAllText(Path.Combine(root, "docs", "design", name));
        return HelpBook.Parse(
        [
            new HelpSource("tools.md", Read("tools.md"), ["Map editor"]),
            new HelpSource("random-maps.md", Read("random-maps.md"), ["The template", "Editor", "Template editor"]),
        ]);
    });

    [Theory]
    [InlineData("Terrain", "Brushes")]
    [InlineData("Road", "Brushes")]
    [InlineData("River", "Brushes")]
    [InlineData("StartZone", "Start zones")]
    [InlineData("Height", "Height")]
    [InlineData("Object", "Object")]
    [InlineData("Monster", "Object")]
    [InlineData("Hero", "Object")]
    [InlineData("LinkGates", "Link gates")]
    [InlineData("Grail", "Grail")]
    [InlineData("Erase", "Erase")]
    [InlineData("Paste", "Clipboard")]
    [InlineData("Scatter", "Scatter")]
    [InlineData("Select", "Select")]
    [InlineData("Move", "Move")]
    public void EveryEditorTool_HasAPage(string tool, string title) => Assert.Equal(title, Docs.Value.Find(tool, "tools.md")?.Title);

    [Theory]
    [InlineData("Hero problems")]
    [InlineData("Object problems")]
    [InlineData("Ground problems")]
    [InlineData("Player problems")]
    [InlineData("Rule problems")]
    [InlineData("Content problems")]
    [InlineData("Event problems")]
    public void EveryKindOfProblem_HasAPage_UnderProblems(string kind)
    {
        var page = Docs.Value.Find(kind)!;
        Assert.Equal("Problems", Docs.Value[page.Parent!]!.Title);
    }

    [Theory]
    [InlineData("Inspector", "tools.md")]
    [InlineData("Problems", "tools.md")]
    [InlineData("History", "tools.md")]
    [InlineData("Output", "tools.md")]
    [InlineData("Layers", "tools.md")]
    [InlineData("Overlays", "tools.md")]
    [InlineData("Fairness", "tools.md")]
    [InlineData("Events", "tools.md")]
    [InlineData("Preferences", "tools.md")]
    [InlineData("Map settings", "tools.md")]
    [InlineData("Help", "tools.md")]
    [InlineData("Generate", "random-maps.md")]
    [InlineData("TemplateEditor", "random-maps.md")]
    [InlineData("Preview", "random-maps.md")]
    [InlineData("Generate many", "random-maps.md")]
    [InlineData("Passages", "random-maps.md")]
    [InlineData("Chance", "random-maps.md")]
    [InlineData("Treasure", "random-maps.md")]
    [InlineData("Obelisks", "random-maps.md")]
    [InlineData("Monster strength", "random-maps.md")]
    [InlineData("Zones", "random-maps.md")]
    [InlineData("Connections", "random-maps.md")]
    public void ThePanelsAndTheTemplatesFields_HaveAPage(string topic, string source)
    {
        var page = Docs.Value.Find(topic, source);
        Assert.NotNull(page);
        Assert.Equal(source, page.Source);
    }

    [Fact]
    public void TheDocumentsPages_HaveTextWithoutDecisionNumbers_AndLinksToPagesThatExist()
    {
        var book = Docs.Value;
        Assert.All(book.Pages, p => Assert.DoesNotMatch(@"\b(?:ADR|EDIT|RMG|MP)-\d+", p.Text));
        Assert.Equal("editor-brushes", book.Find("Brushes")!.Id);
        Assert.Contains("[brush](#editor-brushes)", book.Find("Map editor")!.Text);
        Assert.Equal("Guard", book.Find("Guard", "random-maps.md")!.Title);
        Assert.Equal("random-maps.md", book.Find("Guard", "random-maps.md")!.Source);
        Assert.Equal("random-maps-template-editor", book.Linked("random-maps.md#template-editor")!.Id);
        Assert.Equal("editor-brushes", book.Linked("#editor-brushes")!.Id);
        Assert.Null(book.Linked("adventure-map.md"));
        Assert.Equal("Map events", book.Find("Map events")!.Title);
    }
}
