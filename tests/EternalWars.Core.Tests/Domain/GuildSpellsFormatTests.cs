using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Guild spells fixed by the map (map format v30) and the spell buildings' spells drawn at game start like the guild's.</summary>
public class GuildSpellsFormatTests
{
    private static readonly string[] WideMap = [.. Enumerable.Repeat("....................", 8), "A..................B"];

    private static List<string> Haven(int level) => [.. SpellCatalog.Default.Of(TownIds.Haven, level).Select(s => s.Id)];

    private static (GameState State, Town Town) World(IReadOnlyList<string> fixedSpells, ulong seed = 1)
    {
        var state = Create(GameOptions.Default with { Seed = seed }, WideMap);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.FixedGuildSpells = fixedSpells;
        return (state, town);
    }

    // Game start ----------------------------------------------------------------------------------

    [Fact]
    public void FixedSpells_AreTheGuilds_AndAPartlyFixedLevelIsFilledByTheDraw_WhileOpenLevelsAreDrawn()
    {
        var (one, four) = (Haven(1), Haven(4));
        var (state, town) = World([one[0], one[1], four[0], four[1]]);
        Start(state);

        Assert.Equal(3, town.GuildSpells[1].Count);
        Assert.Contains(one[0], town.GuildSpells[1]);
        Assert.Contains(one[1], town.GuildSpells[1]);
        Assert.Equal(3, town.GuildSpells[1].Distinct().Count());
        Assert.Equal(new[] { four[0], four[1] }.Order(StringComparer.Ordinal), town.GuildSpells[4]);
        Assert.Equal(3, town.GuildSpells[2].Count);
        Assert.Equal(2, town.GuildSpells[3].Count);
        Assert.All(town.GuildSpells.SelectMany(l => l.Value), s => Assert.Equal(TownIds.Haven, state.Spells.Get(s).School));
    }

    [Fact]
    public void AFullyFixedGuild_IsTheSameWhateverTheSeed()
    {
        List<string> all = [.. Haven(1).Take(3), .. Haven(2).Take(3), .. Haven(3).Take(2), .. Haven(4).Take(2)];
        foreach (var seed in new ulong[] { 1, 2, 3, 99 })
        {
            var (state, town) = World(all, seed);
            Start(state);
            Assert.Equal(all.Order(StringComparer.Ordinal), town.GuildSpells.OrderBy(l => l.Key).SelectMany(l => l.Value).Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void SpellBuildingSpells_AreDrawnAtGameStart_ForEverySpellBuildingOfTheTownType_OnePerLevel()
    {
        var (state, town) = World([]);
        Assert.Empty(MagicRules.SchoolSpells(state, town, TownIds.Sylvan)); // not drawn before the game starts
        Start(state);

        Assert.Equal(new[] { TownIds.Academy, TownIds.Sylvan }.Order(StringComparer.Ordinal), town.SchoolSpells.Keys.Order(StringComparer.Ordinal));
        foreach (var (school, spells) in town.SchoolSpells)
        {
            Assert.Equal([1, 2, 3, 4], spells.Select(s => state.Spells.Get(s).Level));
            Assert.All(spells, s => Assert.Equal(school, state.Spells.Get(s).School));
        }
    }

    // Map format ----------------------------------------------------------------------------------

    [Fact]
    public void FixedGuildSpells_RoundTripThroughTheMapFormat_AndAreOmittedWhenNone()
    {
        var spells = new[] { Haven(1)[0], Haven(3)[1] };
        var (state, _) = World(spells);
        AddTown(state, TownIds.Haven, 10, 0);

        var json = MapSerializer.Serialize(MapConverter.ToDocument(state));
        Assert.True(MapDocument.CurrentFormatVersion >= 30);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(json, "\"guildSpells\""));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        var towns = restored.ObjectsOf<Town>().OrderBy(t => t.Position.X).ToList();
        Assert.Equal(spells, towns[0].FixedGuildSpells);
        Assert.Empty(towns[1].FixedGuildSpells);
    }

    [Fact]
    public void AVersion29Map_WithoutGuildSpells_StillLoads_AndDrawsEverything()
    {
        var (state, _) = World([]);
        var document = MapConverter.ToDocument(state) with { FormatVersion = 29 };

        var loaded = MapConverter.ToGameState(MapSerializer.Deserialize(MapSerializer.Serialize(document)));

        var town = Assert.Single(loaded.ObjectsOf<Town>());
        Assert.Empty(town.FixedGuildSpells);
        Start(loaded);
        Assert.Equal([3, 3, 2, 2], town.GuildSpells.OrderBy(l => l.Key).Select(l => l.Value.Count));
    }

    [Theory]
    [InlineData("no.such-spell")]
    [InlineData("other-school")]
    [InlineData("twice")]
    [InlineData("too-many")]
    public void AMapWithWrongGuildSpells_DoesNotLoad_AndTheValidatorFlagsIt(string wrong)
    {
        var one = Haven(1);
        string[] spells = wrong switch
        {
            "other-school" => [SpellCatalog.Default.Of(TownIds.Inferno, 1)[0].Id],
            "twice" => [one[0], one[0]],
            "too-many" => [one[0], one[1], one[2], one[3]],
            _ => [wrong],
        };
        var (state, _) = World(spells);

        Assert.NotNull(MagicRules.GuildSpellsProblem(state.Spells, TownIds.Haven, spells));
        Assert.Contains(MapValidator.Validate(state), i => i.Severity == IssueSeverity.Error && i.Message.Contains("guild spells", StringComparison.Ordinal));
        Assert.Throws<FormatException>(() => MapConverter.ToGameState(MapConverter.ToDocument(state)));
    }

    [Fact]
    public void GoodGuildSpells_AreNotFlagged()
    {
        var (state, _) = World([.. Haven(1).Take(3), Haven(4)[0]]);

        Assert.DoesNotContain(MapValidator.Validate(state), i => i.Message.Contains("guild spells", StringComparison.Ordinal));
    }

    [Fact]
    public void APrimalFuryTown_MayFixNoGuildSpells() =>
        Assert.NotNull(MagicRules.GuildSpellsProblem(SpellCatalog.Default, TownIds.Stronghold, [Haven(1)[0]]));

    // Editor --------------------------------------------------------------------------------------

    [Fact]
    public void TheEditor_SetsATownsGuildSpells_Undoably_AndRefusesWrongOnes()
    {
        var editor = new MapEditorSession(BlankScenario.Create(20, 12));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TownId(TownIds.Haven), new(2, 2), out var id));
        var town = (Town)editor.Scenario.FindObject(id)!;
        var changed = new List<MapObjectChanged>();
        editor.Events.Subscribe<MapObjectChanged>(changed.Add);
        var one = Haven(1);

        Assert.Equal(EditResult.InvalidValue, editor.Towns.SetGuildSpells(id, ["no.such-spell"]));
        Assert.Equal(EditResult.InvalidValue, editor.Towns.SetGuildSpells(id, [one[0], one[0]]));
        Assert.Equal(EditResult.InvalidValue, editor.Towns.SetGuildSpells(id, [.. one.Take(4)]));
        Assert.Equal(EditResult.InvalidValue, editor.Towns.SetGuildSpells(id, [SpellCatalog.Default.Of(TownIds.Inferno, 1)[0].Id]));
        Assert.Equal(EditResult.InvalidTarget, editor.Towns.SetGuildSpells(new MapObjectId(999), []));
        Assert.Empty(changed);

        Assert.Equal(EditResult.Ok, editor.Towns.SetGuildSpells(id, [one[0], Haven(2)[1]]));
        Assert.Equal([one[0], Haven(2)[1]], town.FixedGuildSpells);
        Assert.Single(changed);
        editor.Undo();
        Assert.Empty(town.FixedGuildSpells);
        editor.Redo();
        Assert.Equal([one[0], Haven(2)[1]], town.FixedGuildSpells);

        // Another faction has another school: the fixed spells are dropped with the old type.
        Assert.Equal(EditResult.Ok, editor.Towns.SetType(id, TownIds.Inferno));
        Assert.Empty(((Town)editor.Scenario.FindObject(id)!).FixedGuildSpells);
        editor.Undo();
        Assert.Equal([one[0], Haven(2)[1]], ((Town)editor.Scenario.FindObject(id)!).FixedGuildSpells);
    }
}
