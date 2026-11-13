using System.Security.Cryptography;
using System.Text;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>
/// The maps the shipped templates generate are pinned per generator version (ADR-174): a change that makes a seed give another map must bump
/// <see cref="MapGenerator.Version"/>, which saved games and shared seeds are checked against. Re-pinning without the bump is refused.
/// </summary>
public class GeneratorPinTests
{
    private static readonly ulong[] Seeds = [1];

    [Fact]
    public void TheGeneratedMaps_ArePinned_UntilTheGeneratorVersionChanges()
    {
        var header = $"generator v{MapGenerator.Version}";
        var text = new StringBuilder(header).Append('\n');
        foreach (var (key, template) in BuiltInTemplates.Keys.Zip(BuiltInTemplates.All))
        {
            var size = template.Sizes.Min();
            foreach (var seed in Seeds)
            {
                var map = EditorMapGenerator.Generate(template, size, seed).Scenario;
                // The map's file without its format version, so a format bump that leaves generated maps alone does not show here.
                var document = MapConverter.ToDocument(map) with { FormatVersion = 0 };
                var file = MapSerializer.Serialize(document) + GameStateDigest.Describe(map);
                text.Append($"{key} size {size} seed {seed}: {Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(file)))[..16]}\n");
            }
        }

        var path = Path.Combine(Pinned.Folder(), "GeneratedMaps.txt");
        var pinned = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : null;
        if (pinned is not null && pinned.Split('\n')[0] == header && pinned != text.ToString())
        {
            Assert.Fail($"The generator now gives other maps for the same template, size and seed, but MapGenerator.Version is still {MapGenerator.Version}. " +
                        "Bump it (saved games and shared seeds are checked against it), then run the tests with EW_UPDATE_PINNED=1 to re-pin.");
        }

        Pinned.Check(path, text.ToString(), "The generator version changed: re-pin the maps it gives.");
    }
}
