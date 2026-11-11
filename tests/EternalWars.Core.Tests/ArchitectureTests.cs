using System.Xml.Linq;

namespace EternalWars.Core.Tests;

/// <summary>
/// The project dependency rules (docs/technical/architecture.md, "Dependency rules"; ADR-174): each project references only what its tier may.
/// <c>Directory.Build.targets</c> only keeps Godot out of the core; this checks every rule of the table, from the project files.
/// </summary>
public class ArchitectureTests
{
    /// <summary>What each project may reference ("avoid" counts as allowed); an app may reference every library but no other app.</summary>
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["EternalWars.Core.Events"] = [],
        ["EternalWars.Core.Domain"] = ["EternalWars.Core.Events"],
        ["EternalWars.Core.Generation"] = ["EternalWars.Core.Events", "EternalWars.Core.Domain"],
        ["EternalWars.Core.Data"] = ["EternalWars.Core.Events", "EternalWars.Core.Domain", "EternalWars.Core.Generation"],
        ["EternalWars.Godot.Assets"] = [],
        ["EternalWars.Godot.Shared"] = ["EternalWars.Core.Events", "EternalWars.Core.Domain", "EternalWars.Core.Generation", "EternalWars.Core.Data", "EternalWars.Godot.Assets"],
        ["EternalWars.Godot.UI"] =
            ["EternalWars.Core.Events", "EternalWars.Core.Domain", "EternalWars.Core.Generation", "EternalWars.Core.Data", "EternalWars.Godot.Assets", "EternalWars.Godot.Shared"],
    };

    public static TheoryData<string> Projects =>
        [.. Directory.GetFiles(Path.Combine(Root(), "src"), "*.csproj", SearchOption.AllDirectories).Select(p => Path.GetFileNameWithoutExtension(p)).Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Projects))]
    public void AProject_ReferencesOnlyWhatItsTierMay(string project)
    {
        var path = Directory.GetFiles(Path.Combine(Root(), "src"), project + ".csproj", SearchOption.AllDirectories).Single();
        var references = XDocument.Load(path).Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension(r.Attribute("Include")!.Value.Replace('\\', '/')))
            .ToList();

        var isApp = path.Replace('\\', '/').Contains("/src/Apps/", StringComparison.Ordinal);
        var allowed = isApp ? Allowed.Keys.ToArray() : Allowed.GetValueOrDefault(project);
        Assert.True(allowed is not null, $"{project} is in no tier of the dependency table: add it to architecture.md and to this test.");
        Assert.Empty(references.Except(allowed!));
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EternalWars.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("EternalWars.sln not found above the test output.");
    }
}
