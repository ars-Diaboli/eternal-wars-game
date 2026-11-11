using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// Map garrisons (ADR-082): captured by beating their troops, passable both ways by their owner, edited in the editor; and the map format of the
/// second catalogue's objects.
/// </summary>
public class MapGarrisonTests
{
    private static string Red(string kind) => BuiltInMapObjects.ColouredId(kind, ObjectColour.Red);

    /// <summary>A rock wall on row 3 with a horizontal garrison at (4, 3) whose entrance (5, 3) is the only way through.</summary>
    private static (GameState State, GameSession Session, MapGarrison Garrison) Pass(Army? troops = null)
    {
        var state = Create("..........", "..........", ".....A....", "####...###", "..........", ".........B");
        var definition = MapObjectCatalog.Default.Get(BuiltInMapObjects.Garrison);
        var garrison = new MapGarrison(state.NextObjectId(), definition, new MapPosition(4, 3), troops: troops ?? Army.Of(new CreatureStack("neutral.wolf", 5)));
        state.AddObject(garrison);
        state.GetHero(A).Army.Set(1, new CreatureStack("dragon-utopia.black-dragon", 10));
        return (state, Start(state), garrison);
    }

    [Fact]
    public void ANeutralGarrison_MustBeBeaten_ThenItsOwnerPassesBothWays()
    {
        var (state, session, garrison) = Pass();

        Assert.Null(session.Movement.FindPath(A, new MapPosition(5, 5))); // the fight ends any route at the entrance
        Assert.NotNull(session.Movement.FindPath(A, new MapPosition(5, 3)));

        session.MoveTo(A, new MapPosition(5, 3));
        Assert.NotNull(state.Battle);
        session.AutoResolve();

        Assert.Equal(Blue, garrison.Owner);
        Assert.True(garrison.Troops.IsEmpty);
        Assert.True(session.MoveTo(A, new MapPosition(5, 5)).Arrived);
        Assert.True(session.MoveTo(A, new MapPosition(5, 1)).Arrived);
    }

    [Fact]
    public void TheOwner_LeavesAndTakesTroops_AndTheTroopsDefendTheGarrison()
    {
        var (state, session, garrison) = Pass(new Army());
        session.MoveTo(A, new MapPosition(5, 3));
        Assert.Equal(Blue, garrison.Owner); // an empty garrison is flagged at once

        session.Execute(new MoveGarrisonStackCommand(Blue, garrison.Id, 1, ToGarrison: true)).ThrowIfRejected();
        Assert.Equal(new CreatureStack("dragon-utopia.black-dragon", 10), garrison.Troops[0]);
        Assert.False(session.Execute(new MoveGarrisonStackCommand(Blue, garrison.Id, 0, ToGarrison: true)).IsAccepted); // keeps one stack

        session.MoveTo(A, new MapPosition(5, 1));
        session.EndTurn();
        var bravo = state.GetHero(B);
        Assert.Null(session.Movement.FindPath(B, new MapPosition(5, 1))); // not through an enemy's garrison
        session.MoveTo(B, new MapPosition(5, 3));
        session.AutoResolve();

        Assert.DoesNotContain(state.Heroes, h => h.Id == bravo.Id);
        Assert.Equal(Blue, garrison.Owner);
        Assert.Equal("dragon-utopia.black-dragon", garrison.Troops[0]!.Creature);
    }

    [Fact]
    public void GarrisonsAndTheOtherNewObjects_AreInTheMapFormat_AndTheOldRuinsLoadAsTheCrypt()
    {
        var state = BlankScenario.Create(30, 10);
        var definition = MapObjectCatalog.Default.Get(BuiltInMapObjects.VerticalGarrison);
        state.AddObject(new MapGarrison(state.NextObjectId(), definition, new MapPosition(1, 1), new PlayerId(0), Army.Of(new CreatureStack("neutral.wolf", 7))));
        Add<Deposit>(state, DepositRules.Id(ResourceType.Sulfur), 4, 1);
        Add<MagiEye>(state, Red(BuiltInMapObjects.MagiEye), 8, 1);
        Add<MagiWindow>(state, Red(BuiltInMapObjects.MagiWindow), 10, 1);
        Add<KeymasterTent>(state, Red(BuiltInMapObjects.KeymasterTent), 13, 1);
        Add<BorderPost>(state, Red(BuiltInMapObjects.BorderGate), 16, 1);
        Add<MapTavern>(state, BuiltInMapObjects.MapTavern, 18, 1);
        Add<CreatureBank>(state, BuiltInMapObjects.DragonCity, 22, 1);

        var json = TestFormat.Save(state);
        var restored = TestFormat.Load(json);

        Assert.Equal(state.Objects.Select(o => (o.Id, o.Definition.Id, o.Position)), restored.Objects.Select(o => (o.Id, o.Definition.Id, o.Position)));
        var garrison = restored.ObjectsOf<MapGarrison>().Single();
        Assert.Equal((new PlayerId(0), new CreatureStack("neutral.wolf", 7)), (garrison.Owner!.Value, garrison.Troops[0]!));

        var old = json.Replace(BuiltInMapObjects.DragonCity, "bank.ruins", StringComparison.Ordinal).Replace($"\"formatVersion\": {MapDocument.CurrentFormatVersion}", "\"formatVersion\": 22", StringComparison.Ordinal);
        Assert.Equal(BuiltInMapObjects.Crypt, TestFormat.Load(old).ObjectsOf<CreatureBank>().Single().Definition.Id);
    }

    [Fact]
    public void TheEditor_SetsAMapGarrisonsOwnerAndTroops_Undoably()
    {
        var editor = TestEditor.Blank(12, 8);
        editor.Objects.Place(BuiltInMapObjects.Garrison, new MapPosition(2, 2), out var id);

        Assert.Equal(EditResult.Ok, editor.Objects.SetGarrisonOwner(id, new PlayerId(0)));
        Assert.Equal(EditResult.Ok, editor.Towns.SetGarrisonSlot(id, 2, new CreatureStack("neutral.wolf", 4)));
        var garrison = (MapGarrison)editor.Scenario.GetObject(id);
        Assert.Equal((new PlayerId(0), 4), (garrison.Owner!.Value, garrison.Troops[2]!.Count));

        editor.Undo();
        editor.Undo();
        Assert.Equal((null, true), (garrison.Owner, garrison.Troops.IsEmpty));
    }
}
