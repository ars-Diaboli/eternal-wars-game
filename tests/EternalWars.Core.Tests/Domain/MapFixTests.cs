using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Fix-its (ADR-265): issues whose answer is clear carry a fix, which the session runs as one undo step.</summary>
public class MapFixTests
{
    private static readonly string[] Rows = [.. Enumerable.Repeat("..............................", 10)];

    private static MapIssue IssueAt(MapEditorSession session, MapObject mapObject, string text) =>
        MapValidator.Validate(session.Scenario).Single(i => i.Position == mapObject.Position && i.Message.Contains(text, StringComparison.Ordinal));

    private static MapEditorSession Session(Core.Domain.Game.GameState state) => new(state);

    [Fact]
    public void AnUnlinkedGate_IsLinkedWithTheOnlyOtherUnlinkedGate_AsOneUndoStep()
    {
        var state = TestWorld.Create(Rows);
        var a = TestWorld.Add<TwoWayGate>(state, BuiltInMapObjects.TwoWayMonolith, 2, 2);
        var b = TestWorld.Add<TwoWayGate>(state, BuiltInMapObjects.TwoWayMonolith, 20, 2);
        var session = Session(state);

        var issue = IssueAt(session, a, "is not linked");
        Assert.Equal(new LinkGatesFix("Link it with the portal at 20, 2", a.Id, b.Id), issue.Fix);

        Assert.Equal(EditResult.Ok, session.Fix(issue.Fix!));
        Assert.Equal((b.Id, a.Id), (a.LinkedGate, b.LinkedGate));
        Assert.Equal(1, session.History.Count);
        Assert.StartsWith("Fix: Link it with", session.History.UndoDescription, StringComparison.Ordinal);
        Assert.DoesNotContain(MapValidator.Validate(state), i => i.Message.Contains("link", StringComparison.OrdinalIgnoreCase));

        session.Undo();
        Assert.Equal((null, null), (a.LinkedGate, b.LinkedGate));
    }

    [Fact]
    public void AGate_WithMoreThanOnePossiblePartner_HasNoFix()
    {
        var state = TestWorld.Create(Rows);
        var a = TestWorld.Add<TwoWayGate>(state, BuiltInMapObjects.TwoWayMonolith, 2, 2);
        TestWorld.Add<TwoWayGate>(state, BuiltInMapObjects.TwoWayMonolith, 10, 2);
        TestWorld.Add<TwoWayGate>(state, BuiltInMapObjects.TwoWayMonolith, 20, 2);

        Assert.Null(IssueAt(Session(state), a, "is not linked").Fix);
    }

    [Fact]
    public void ALinkThatIsNotMutual_IsLinkedBack_WhenItsGateIsLinkedToNothing()
    {
        var state = TestWorld.Create(Rows);
        var a = TestWorld.Add<TwoWayGate>(state, BuiltInMapObjects.TwoWayMonolith, 2, 2);
        var b = TestWorld.Add<TwoWayGate>(state, BuiltInMapObjects.TwoWayMonolith, 20, 2);
        TestWorld.Add<TwoWayGate>(state, BuiltInMapObjects.TwoWayMonolith, 10, 6); // a third, unlinked: b's partner is still clear, a points at it
        a.LinkedGate = b.Id;
        var session = Session(state);

        var notMutual = IssueAt(session, a, "not mutual");
        Assert.Equal(new LinkGatesFix("Link the portal at 20, 2 back to it", a.Id, b.Id), notMutual.Fix);
        Assert.Equal(new LinkGatesFix("Link it with the portal at 2, 2", b.Id, a.Id), IssueAt(session, b, "is not linked").Fix);

        Assert.Equal(EditResult.Ok, session.Fix(notMutual.Fix!));
        Assert.Equal((b.Id, a.Id), (a.LinkedGate, b.LinkedGate));
    }

    [Fact]
    public void AOneWayEntranceWithoutExit_IsPointedAtTheOnlyUnusedExit()
    {
        var state = TestWorld.Create(Rows);
        var entrance = TestWorld.Add<OneWayGateEntrance>(state, BuiltInMapObjects.OneWayMonolithEntrance, 2, 2);
        var exit = TestWorld.Add<OneWayGateExit>(state, BuiltInMapObjects.OneWayMonolithExit, 20, 2);
        var session = Session(state);

        var fix = IssueAt(session, entrance, "has no exit").Fix;
        Assert.Equal(new GateExitFix("Point the entrance at 2, 2 at the exit at 20, 2", entrance.Id, exit.Id), fix);
        Assert.Equal(fix, IssueAt(session, exit, "is not used").Fix);
        Assert.Equal(EditResult.Ok, session.Fix(fix!));
        Assert.Equal(exit.Id, entrance.Exit);

        // A second exit makes the answer unclear.
        var other = TestWorld.Create(Rows);
        var lone = TestWorld.Add<OneWayGateEntrance>(other, BuiltInMapObjects.OneWayMonolithEntrance, 2, 2);
        TestWorld.Add<OneWayGateExit>(other, BuiltInMapObjects.OneWayMonolithExit, 10, 2);
        TestWorld.Add<OneWayGateExit>(other, BuiltInMapObjects.OneWayMonolithExit, 20, 2);
        Assert.Null(IssueAt(Session(other), lone, "has no exit").Fix);
    }

    [Fact]
    public void WhatBelongsToAMissingPlayer_BecomesNeutral()
    {
        var state = TestWorld.Create(Rows);
        var missing = new PlayerId(5);
        var mine = TestWorld.Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Ore), 2, 1);
        mine.Owner = missing;
        var garrison = TestWorld.Add<MapGarrison>(state, BuiltInMapObjects.Garrison, 8, 1);
        garrison.Owner = missing;
        garrison.Troops.Set(0, new Core.Domain.Armies.CreatureStack(BuiltInCreatures.Footman, 3));
        var town = TestWorld.AddTown(state, TownIds.Haven, 15, 1, missing);
        var session = Session(state);

        foreach (var (thing, text) in new (MapObject, string)[] { (mine, "missing player"), (garrison, "missing player"), (town, "missing player") })
        {
            var fix = Assert.IsType<OwnerFix>(IssueAt(session, thing, text).Fix);
            Assert.Equal((thing.Id, missing, (PlayerId?)null), (fix.Object, fix.Was, fix.Owner));
            Assert.Equal(EditResult.Ok, session.Fix(fix));
        }

        Assert.Equal((null, null, null), (mine.Owner, garrison.Owner, town.Owner));
        Assert.DoesNotContain(MapValidator.Validate(state), i => i.Message.Contains("missing player", StringComparison.Ordinal));
    }

    [Fact]
    public void APlayerWithoutAStart_IsGivenTheOneNeutralTownInItsStartZone()
    {
        var state = TestWorld.Create(Rows);
        var blue = TestWorld.AddTown(state, TownIds.Haven, 0, 0, TestWorld.Blue);
        var neutral = TestWorld.AddTown(state, TownIds.Haven, 20, 0);
        state.Map.SetStartZone(neutral.VisitPosition, TestWorld.Red);
        var session = Session(state);

        var issue = MapValidator.Validate(state).Single(i => i.Message.Contains("Player Red has no starting hero or town", StringComparison.Ordinal));
        Assert.Equal(new OwnerFix($"Give {neutral.Name}, the neutral town in the start zone, to Red", neutral.Id, null, TestWorld.Red), issue.Fix);
        Assert.Equal(neutral.VisitPosition, issue.Position);
        Assert.Equal(EditResult.Ok, session.Fix(issue.Fix!));
        Assert.Equal(TestWorld.Red, neutral.Owner);
        Assert.Equal(TestWorld.Blue, blue.Owner);
        Assert.DoesNotContain(MapValidator.Validate(state), i => i.Message.Contains("no starting hero or town", StringComparison.Ordinal));

        // Two neutral towns in the zone: which is the start is not clear.
        var two = TestWorld.Create(Rows);
        foreach (var x in new[] { 0, 20 })
        {
            two.Map.SetStartZone(TestWorld.AddTown(two, TownIds.Haven, x, 0).VisitPosition, TestWorld.Red);
        }

        Assert.All(MapValidator.Validate(two).Where(i => i.Message.Contains("no starting hero or town", StringComparison.Ordinal)), i => Assert.Null(i.Fix));
    }

    [Fact]
    public void AFix_FoundOnACopyOfTheMap_RunsOnTheLiveMap_AndIsRefusedOnceTheMapChanged()
    {
        var state = TestWorld.Create(Rows);
        var a = TestWorld.Add<TwoWayGate>(state, BuiltInMapObjects.TwoWayMonolith, 2, 2);
        var b = TestWorld.Add<TwoWayGate>(state, BuiltInMapObjects.TwoWayMonolith, 20, 2);
        var session = Session(state);

        // The Problems panel validates a copy made through the map format: the ids are the live map's.
        var copy = MapConverter.ToGameState(MapConverter.ToDocument(state), checkEvents: false);
        var fix = MapValidator.Validate(copy).First(i => i.Fix is LinkGatesFix).Fix!;

        var c = TestWorld.Add<TwoWayGate>(state, BuiltInMapObjects.TwoWayMonolith, 10, 6);
        Assert.Equal(EditResult.Ok, session.Objects.LinkGates(b.Id, c.Id)); // b is taken since the check
        var steps = session.History.Count;

        Assert.Equal(EditResult.InvalidTarget, session.Fix(fix));
        Assert.Equal(steps, session.History.Count);
        Assert.Null(a.LinkedGate);
        Assert.Equal(c.Id, b.LinkedGate);
    }
}
