using System.Text.RegularExpressions;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The town layout scenes (<c>scenes/towns/&lt;town&gt;.tscn</c>, ADR-297): each holds a plot marker for every building and dwelling line its town
/// type has, named after <see cref="TownOverview.PlotNode"/>, so the 3D town scene can show every plot, and the wall line and street markers
/// of its scenery (ADR-301). Read from the scene files as text.
/// </summary>
public partial class TownLayoutTests
{
    public static TheoryData<string> Layouts =>
        [.. Directory.GetFiles(LayoutFolder(), "*.tscn").Select(p => Path.GetFileNameWithoutExtension(p)!).Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Layouts))]
    public void ALayout_HasAPlotMarkerForEveryBuildingAndLine_AndNoOther(string townType)
    {
        var state = Create([.. Enumerable.Repeat("..........", 10)]);
        var town = AddTown(state, townType, 0, 0, Blue);
        var scene = File.ReadAllText(Path.Combine(LayoutFolder(), townType + ".tscn"));
        var markers = PlotMarker().Matches(scene).Select(m => m.Groups["name"].Value).ToList();

        var plots = TownOverview.PlotsOf(state, town).Select(p => TownOverview.PlotNode(p.Id)).ToList();

        var missing = plots.Except(markers).ToList();
        var unknown = markers.Except(plots).ToList();
        Assert.True(missing.Count == 0, $"{townType}.tscn has no plot for: {string.Join(", ", missing)}");
        Assert.True(unknown.Count == 0, $"{townType}.tscn has plots for what the town has not: {string.Join(", ", unknown)}");
        Assert.Equal(markers.Count, markers.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void ALayout_HasAWallLineWithAGate_AStreetFromIt_AndHousesForEachStep(string townType)
    {
        var scene = File.ReadAllText(Path.Combine(LayoutFolder(), townType + ".tscn"));
        var nodes = SceneNode().Matches(scene).Select(m => (Name: m.Groups["name"].Value, Parent: m.Groups["parent"].Value)).ToList();

        var walls = nodes.Where(n => n.Name == "Walls").Select(n => n.Parent == "." ? "Walls" : $"{n.Parent}/Walls").ToList();
        Assert.True(walls.Count == 1, $"{townType}.tscn needs one Walls node (ADR-301)");
        Assert.True(nodes.Count(n => n.Parent == walls[0] && n.Name.StartsWith("Post", StringComparison.Ordinal)) >= 2, $"{townType}.tscn: the wall needs two posts");
        Assert.Contains(nodes, n => n.Parent == walls[0] && n.Name == "Gate");
        Assert.True(nodes.Count(n => n.Name == "Streets") == 1, $"{townType}.tscn needs one Streets node (ADR-301)");
        foreach (var step in new[] { Settlement.Village, Settlement.Town, Settlement.City })
        {
            Assert.True(
                nodes.Any(n => n.Parent.EndsWith($"Streets/{step}", StringComparison.Ordinal) && n.Name.StartsWith("House", StringComparison.Ordinal)),
                $"{townType}.tscn has no houses for the {step}");
        }

        Assert.Contains(nodes, n => n.Parent.EndsWith("Streets/Village", StringComparison.Ordinal) && n.Name.StartsWith("Street", StringComparison.Ordinal));
    }

    [Fact]
    public void APlotsMarker_IsNamedAfterItsId_WithoutDots()
    {
        Assert.Equal("plot_town-hall", TownOverview.PlotNode("town-hall"));
        Assert.Equal("plot_dwelling-haven_footman", TownOverview.PlotNode(TownOverview.DwellingPlot(new("haven.footman"))));
    }

    /// <summary>A plot marker: a <c>Marker3D</c> node whose name starts with <see cref="TownOverview.PlotNodePrefix"/>.</summary>
    [GeneratedRegex("""\[node name="(?<name>plot_[^"]+)" type="Marker3D"[^\]]*\]""")]
    private static partial Regex PlotMarker();

    /// <summary>A node with its parent's path.</summary>
    [GeneratedRegex("""\[node name="(?<name>[^"]+)" type="\w+" parent="(?<parent>[^"]*)"[^\]]*\]""")]
    private static partial Regex SceneNode();

    private static string LayoutFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EternalWars.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("EternalWars.sln not found above the test output."),
            "src", "Godot", "EternalWars.Godot.Assets", "scenes", "towns");
    }
}
