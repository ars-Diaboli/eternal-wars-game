using System.Text.RegularExpressions;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The town layout scenes (<c>scenes/towns/&lt;town&gt;.tscn</c>, ADR-297): each holds a plot marker for every building and dwelling line its town
/// type has, named after <see cref="TownOverview.PlotNode"/>, so the 3D town scene can show every plot. Read from the scene files as text.
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

    [Fact]
    public void APlotsMarker_IsNamedAfterItsId_WithoutDots()
    {
        Assert.Equal("plot_town-hall", TownOverview.PlotNode("town-hall"));
        Assert.Equal("plot_dwelling-haven_footman", TownOverview.PlotNode(TownOverview.DwellingPlot(new("haven.footman"))));
    }

    /// <summary>A plot marker: a <c>Marker3D</c> node whose name starts with <see cref="TownOverview.PlotNodePrefix"/>.</summary>
    [GeneratedRegex("""\[node name="(?<name>plot_[^"]+)" type="Marker3D"[^\]]*\]""")]
    private static partial Regex PlotMarker();

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
