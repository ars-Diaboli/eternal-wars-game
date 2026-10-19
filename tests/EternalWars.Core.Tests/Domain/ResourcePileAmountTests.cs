using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Resource piles whose amount the map leaves open roll it when the game starts (ADR-068).</summary>
public class ResourcePileAmountTests
{
    [Theory]
    [InlineData(ResourceType.Gold, 500, 1000)]
    [InlineData(ResourceType.Lumber, 5, 10)]
    [InlineData(ResourceType.Ore, 5, 10)]
    [InlineData(ResourceType.Crystal, 3, 7)]
    [InlineData(ResourceType.Sulfur, 3, 7)]
    public void AnOpenAmount_IsRolledInTheNotesRange_WhenTheGameStarts(ResourceType resource, int min, int max)
    {
        var amounts = new HashSet<int>();
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var state = Create(new GameOptions(Seed: seed), "A..", "..B");
            var pile = Add<ResourcePile>(state, BuiltInMapObjects.PileId(resource), 2, 0);
            Assert.Null(pile.Amount);

            Start(state);

            amounts.Add(pile.Amount!.Value);
        }

        Assert.All(amounts, a => Assert.InRange(a, min, max));
        Assert.Contains(min, amounts);
        Assert.Contains(max, amounts);
        if (resource == ResourceType.Gold)
        {
            Assert.All(amounts, a => Assert.Equal(0, a % ResourcePile.GoldStep));
        }
    }

    [Fact]
    public void AFixedAmount_IsKept()
    {
        var state = Create("A..", "..B");
        var pile = Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 2, 0);
        pile.Amount = 1234;

        Start(state);

        Assert.Equal(1234, pile.Amount);
    }

    [Fact]
    public void AnOpenAmount_IsLeftOutOfTheMapFile_AndOldFilesKeepTheirAmounts()
    {
        var editor = new MapEditorSession(BlankScenario.Create(8, 8));
        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.PileId(ResourceType.Ore), new(2, 2), out var id));

        var json = MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal(21, MapDocument.CurrentFormatVersion);
        Assert.DoesNotContain("\"amount\"", json, StringComparison.Ordinal);
        Assert.Null(Assert.IsType<ResourcePile>(restored.GetObject(id)).Amount);

        var old = json.Replace("\"formatVersion\": 21", "\"formatVersion\": 20", StringComparison.Ordinal)
            .Replace("\"definition\": \"pile.ore\"", "\"definition\": \"pile.ore\", \"amount\": 9", StringComparison.Ordinal);
        Assert.Equal(9, Assert.IsType<ResourcePile>(MapConverter.ToGameState(MapSerializer.Deserialize(old)).GetObject(id)).Amount);
    }
}
