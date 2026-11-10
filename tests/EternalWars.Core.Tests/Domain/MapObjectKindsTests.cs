using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The tables keyed by map object kind agree with <see cref="MapObjectKinds"/> (ADR-166): a kind left out of one of them fails here, not
/// silently in a game.
/// </summary>
public sealed class MapObjectKindsTests
{
    public static TheoryData<MapObjectKind> Kinds() => [.. Enum.GetValues<MapObjectKind>()];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryKind_IsRegistered_AndMakesAnInstanceOfItsType(MapObjectKind kind)
    {
        var entry = MapObjectKinds.Get(kind);
        var definition = MapObjectCatalog.Default.OfKind(kind).First();

        var mapObject = MapObject.Create(new MapObjectId(1), definition, new MapPosition(0, 0));

        Assert.IsType(entry.Type, mapObject);
        Assert.Equal(kind, mapObject.Kind);
    }

    [Fact]
    public void EveryInstanceType_HasAVisitRule()
    {
        var session = new GameSession(BlankScenario.Create(8, 6));

        Assert.All(MapObjectKinds.Types, type => Assert.True(session.Interactions.HasVisitRule(type), $"{type.Name} has no visit rule."));
    }

    [Fact]
    public void EveryInstanceType_HasOneMapFormatCodec_UnderItsOwnName()
    {
        var codecs = MapConverter.ObjectCodecs;

        Assert.Equal(MapObjectKinds.Types.Select(t => t.Name).Order(), codecs.Select(c => c.ObjectType.Name).Order());
        Assert.Equal(codecs.Count, codecs.Select(c => c.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(codecs.Count, codecs.Select(c => c.DocumentType).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryKind_ComesBackFromAMapFile_AsItWas(MapObjectKind kind)
    {
        var state = BlankScenario.Create(12, 12);
        var placed = MapObject.Create(new MapObjectId(1), MapObjectCatalog.Default.OfKind(kind).First(), new MapPosition(2, 2));
        state.AddObject(placed);

        var loaded = MapConverter.ToGameState(MapSerializer.Deserialize(MapSerializer.Serialize(MapConverter.ToDocument(state))), checkEvents: false);

        var back = Assert.Single(loaded.Objects);
        Assert.IsType(placed.GetType(), back);
        Assert.Equal((placed.Definition.Id, placed.Position, placed.DigestState()), (back.Definition.Id, back.Position, back.DigestState()));
    }
}
