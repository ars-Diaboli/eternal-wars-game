using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;

namespace EternalWars.Core.Tests.Data;

/// <summary>Map format v14: map marketplaces (ADR-067); older maps still load.</summary>
public class MarketplaceFormatTests
{
    [Fact]
    public void MapMarketplace_RoundTrips()
    {
        var state = BlankScenario.Create(8, 6);
        state.AddObject(new Marketplace(state.NextObjectId(), MapObjectCatalog.Default.Get(BuiltInMapObjects.Marketplace), new(2, 1)));

        var json = MapSerializer.Serialize(MapConverter.ToDocument(state));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Contains("\"kind\": \"marketplace\"", json);
        Assert.Contains($"\"formatVersion\": {MapDocument.CurrentFormatVersion}", json);
        var market = Assert.Single(restored.ObjectsOf<Marketplace>());
        Assert.Equal(new(2, 1), market.Position);
        Assert.Equal(new(3, 2), market.VisitPosition);
    }

    [Fact]
    public void TheEditor_PlacesAMarketplace_UndoablyAndTheValidatorAcceptsIt()
    {
        var editor = new MapEditorSession(BlankScenario.Create(12, 10));

        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.Marketplace, new(4, 4), out var id));

        Assert.IsType<Marketplace>(editor.Scenario.GetObject(id));
        Assert.DoesNotContain(MapValidator.Validate(editor.Scenario), i => i.Severity == IssueSeverity.Error);
        editor.Undo();
        Assert.Empty(editor.Scenario.Objects);
    }

    [Fact]
    public void AV13MapLoads_AndIsSavedInTheCurrentFormat()
    {
        const string old = """
            {
              "formatVersion": 13, "name": "Old", "width": 8, "height": 4, "terrain": [ [ "gggggggg", "gggggggg", "gggggggg", "gggggggg" ] ],
              "players": [ { "name": "Blue", "isHuman": true } ],
              "objects": []
            }
            """;

        var state = MapConverter.ToGameState(MapSerializer.Deserialize(old));

        Assert.True(MapDocument.CurrentFormatVersion >= 14);
        Assert.Equal(MapDocument.CurrentFormatVersion, MapConverter.ToDocument(state).FormatVersion);
    }
}
