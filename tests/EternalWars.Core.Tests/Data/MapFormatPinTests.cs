using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using EternalWars.Core.Data;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;

namespace EternalWars.Core.Tests.Data;

/// <summary>
/// The map format is upgraded and pinned (ADR-174): an older file's JSON is upgraded step by step before it is read; a sample file of the current
/// version and of the version before each step reads to a pinned result; and the JSON shape of maps and saves (property names, object kinds,
/// enum names) is pinned, so a change to it without a format version shows here.
/// </summary>
public class MapFormatPinTests
{
    private static readonly string Samples = Path.Combine(Pinned.Folder(), "FormatSamples");

    private static string SamplePath(int version) => Path.Combine(Samples, $"v{version:00}{MapSerializer.FileExtension}");

    /// <summary>The versions with a sample: the first (no version field), the one before each upgrade step and the current one.</summary>
    private static IReadOnlyList<int> Versions =>
        [.. MapMigrations.Steps.Select(s => s.Version - 1).Prepend(MapMigrations.UnversionedFormatVersion).Append(MapDocument.CurrentFormatVersion).Distinct().Order()];

    public static TheoryData<int> SampleVersions => [.. Versions];

    [Fact]
    public void TheSteps_AreInVersionOrder_AndNoneIsNewerThanTheFormat()
    {
        var versions = MapMigrations.Steps.Select(s => s.Version).ToList();
        Assert.Equal(versions.Order().Distinct(), versions);
        Assert.All(versions, v => Assert.InRange(v, MapMigrations.UnversionedFormatVersion + 1, MapDocument.CurrentFormatVersion));
    }

    [Fact]
    public void EverySampleFile_IsOfAVersionThatNeedsOne()
    {
        var files = Directory.GetFiles(Samples, "*" + MapSerializer.FileExtension).Select(Path.GetFileName).Order(StringComparer.Ordinal);
        Assert.Equal(Versions.Select(v => Path.GetFileName(SamplePath(v))).Order(StringComparer.Ordinal), files);
    }

    /// <summary>
    /// Each sample, read and saved again, gives the pinned file: the current sample itself (the format writes what it reads), an older one its
    /// upgraded form (<c>vNN.upgraded.json</c>). A new format version needs its own sample (copy the last one and add what the version adds).
    /// </summary>
    [Theory]
    [MemberData(nameof(SampleVersions))]
    public void ASample_ReadsAndSavesAsPinned(int version)
    {
        var path = SamplePath(version);
        Assert.True(File.Exists(path), $"No sample for format v{version}: add {Path.GetFileName(path)} (see the remarks on MapDocument for what it adds).");
        var document = MapSerializer.Load(path);
        var saved = MapSerializer.Serialize(MapConverter.ToDocument(MapConverter.ToGameState(document)));

        Assert.Equal(MapDocument.CurrentFormatVersion, document.FormatVersion);
        var pinned = version == MapDocument.CurrentFormatVersion ? path : Path.ChangeExtension(Path.ChangeExtension(path, null), ".upgraded.json");
        Pinned.Check(pinned, saved, $"Reading the v{version} sample and saving it gives another file than before: an older map would now load differently.");
    }

    [Fact]
    public void TheUpgrades_TurnTacticsIntoSkills_TheMapWideGuardModeIntoEachMonsters_AndTheRuinsIntoTheCrypt()
    {
        var v4 = MapConverter.ToGameState(MapSerializer.Load(SamplePath(4)));
        Assert.Equal(SkillLevel.Advanced, v4.GetHero(new(1)).Tactics);
        Assert.Equal(SkillLevel.None, v4.GetHero(new(2)).Tactics);
        Assert.All(v4.ObjectsOf<MonsterGuard>(), m => Assert.Equal(GuardMode.Area, m.Mode));

        var v22 = MapConverter.ToGameState(MapSerializer.Load(SamplePath(22)));
        Assert.Equal(BuiltInMapObjects.Crypt, v22.ObjectsOf<CreatureBank>().Single().Definition.Id);
    }

    [Fact]
    public void ACurrentFile_IsReadAsItIs_SoAnOldIdOrFieldInIt_IsNotUpgraded()
    {
        var json = MapSerializer.Serialize(MapConverter.ToDocument(BlankScenario.Create(8, 6)));
        var node = JsonNode.Parse(json)!.AsObject();
        node["guardMode"] = "Area";
        node["objects"] = new JsonArray(
            new JsonObject { ["kind"] = "monster", ["id"] = 1, ["definition"] = "monster", ["x"] = 2, ["y"] = 2, ["creature"] = "neutral.wolf" });
        Assert.Equal(GuardMode.Classic, MapConverter.ToGameState(MapSerializer.Deserialize(node.ToJsonString())).ObjectsOf<MonsterGuard>().Single().Mode);

        node["objects"] = new JsonArray(new JsonObject { ["kind"] = "bank", ["id"] = 1, ["definition"] = "bank.ruins", ["x"] = 2, ["y"] = 2 });
        Assert.Throws<FormatException>(() => MapConverter.ToGameState(MapSerializer.Deserialize(node.ToJsonString())));
    }

    [Fact]
    public void ASavedGamesOlderMap_IsUpgradedToo()
    {
        var state = MapConverter.ToGameState(MapSerializer.Load(SamplePath(12)));
        var record = GameReplay.Record(MapConverter.ToDocument(state), new GameSession(state));
        var json = JsonNode.Parse(GameRecordSerializer.Serialize(record))!.AsObject();
        json["map"] = JsonNode.Parse(File.ReadAllText(SamplePath(12)));

        var loaded = GameRecordSerializer.Deserialize(json.ToJsonString());

        Assert.Equal(MapDocument.CurrentFormatVersion, loaded.Map.FormatVersion);
        Assert.All(MapConverter.ToGameState(loaded.Map).ObjectsOf<MonsterGuard>(), m => Assert.Equal(GuardMode.Area, m.Mode));
    }

    /// <summary>
    /// Every type a map or a save writes, with its JSON property names, object kinds and enum names. Renaming a property, a kind or an enum value
    /// changes the files: bump the format version (and add an upgrade step when older files need one) before re-pinning.
    /// </summary>
    [Fact]
    public void TheJsonShape_OfMapsAndSaves_IsPinned()
    {
        var shape = new StringBuilder()
            .Append("# Map files (*.ewmap.json), format v").Append(MapDocument.CurrentFormatVersion).Append('\n')
            .Append(Shape(JsonDefaults.Options, typeof(MapDocument)))
            .Append("\n# Saves and replays (*.ewsave.json, *.ewreplay.json), format v").Append(GameRecordDocument.CurrentFormatVersion).Append('\n')
            .Append(Shape(GameRecordSerializer.Options, typeof(GameRecordDocument)));

        Pinned.Check(
            Path.Combine(Pinned.Folder(), "FormatShape.txt"),
            shape.ToString(),
            "The JSON of maps or saves changed. If only a C# type name changed, re-pin. If a property, kind or enum name in the files changed, bump " +
            "MapDocument.CurrentFormatVersion (or GameRecordDocument's), add a sample, and add an IMapMigration step when older files need upgrading (ADR-174).");
    }

    private static string Shape(JsonSerializerOptions options, Type root)
    {
        var blocks = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var seen = new HashSet<Type>();
        var pending = new Queue<Type>([root]);
        while (pending.TryDequeue(out var type))
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (!seen.Add(type) || type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
            {
                continue;
            }

            if (type.IsEnum)
            {
                blocks[Name(type)] = $"enum {Name(type)}: {string.Join(", ", Enum.GetNames(type))}\n";
                continue;
            }

            var info = options.GetTypeInfo(type);
            switch (info.Kind)
            {
                case JsonTypeInfoKind.Object:
                    var block = new StringBuilder($"object {Name(type)}\n");
                    if (info.PolymorphismOptions is { } polymorphism)
                    {
                        foreach (var derived in polymorphism.DerivedTypes.OrderBy(d => d.TypeDiscriminator?.ToString(), StringComparer.Ordinal))
                        {
                            block.Append($"  {polymorphism.TypeDiscriminatorPropertyName}={derived.TypeDiscriminator} -> {Name(derived.DerivedType)}\n");
                            pending.Enqueue(derived.DerivedType);
                        }
                    }

                    foreach (var property in info.Properties.OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        block.Append($"  {property.Name}: {Name(property.PropertyType)}{(property.IsRequired ? " (required)" : "")}\n");
                        pending.Enqueue(property.PropertyType);
                    }

                    blocks[Name(type)] = block.ToString();
                    break;
                case JsonTypeInfoKind.Enumerable:
                    pending.Enqueue(info.ElementType!);
                    break;
                case JsonTypeInfoKind.Dictionary:
                    pending.Enqueue(info.KeyType!);
                    pending.Enqueue(info.ElementType!);
                    break;
                default:
                    blocks[Name(type)] = $"value {Name(type)}\n";
                    break;
            }
        }

        return string.Concat(blocks.Values);
    }

    private static string Name(Type type) =>
        Nullable.GetUnderlyingType(type) is { } inner ? Name(inner) + "?"
        : type.IsArray ? Name(type.GetElementType()!) + "[]"
        : type.IsGenericType ? $"{type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)]}<{string.Join(", ", type.GetGenericArguments().Select(Name))}>"
        : type.Name;
}
