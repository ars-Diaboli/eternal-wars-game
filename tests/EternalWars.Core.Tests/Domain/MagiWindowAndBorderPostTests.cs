using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Windows and Eyes of the Magi, Keymaster's Tents and the border guards and gates they open (ADR-071, ADR-082).</summary>
public class MagiWindowAndBorderPostTests
{
    private static string Red(string kind) => BuiltInMapObjects.ColouredId(kind, ObjectColour.Red);

    private static List<T> Record<T>(GameSession session)
        where T : IEvent
    {
        var list = new List<T>();
        session.Events.Subscribe<T>(list.Add);
        return list;
    }

    // Windows and Eyes of the Magi ----------------------------------------------------------------------

    [Fact]
    public void AWindowOfTheMagi_RevealsAroundEveryEyeOfItsColour_OnEveryVisit()
    {
        var rows = Enumerable.Repeat(new string('.', 40), 12).ToArray();
        rows[2] = "...A" + new string('.', 36);
        rows[11] = new string('.', 39) + "B";
        var state = Create(rows);
        Add<MagiWindow>(state, Red(BuiltInMapObjects.MagiWindow), 2, 0); // entrance (3, 1)
        Add<MagiEye>(state, Red(BuiltInMapObjects.MagiEye), 30, 6);
        Add<MagiEye>(state, BuiltInMapObjects.ColouredId(BuiltInMapObjects.MagiEye, ObjectColour.Blue), 20, 10);
        var session = Start(state, revealAll: false);
        Assert.False(state.Fog.IsExplored(Blue, new MapPosition(30, 6)));

        session.Step(A, Direction.North);

        Assert.True(state.Fog.IsExplored(Blue, new MapPosition(30 + MagiWindow.Radius, 6)));
        Assert.False(state.Fog.IsExplored(Blue, new MapPosition(20, 10))); // another colour
        var revealed = Record<SurroundingsRevealed>(session);
        session.Revisit(A);
        Assert.Single(revealed); // every visit looks again
    }

    [Fact]
    public void TheEditor_RefusesAFifthEyeOfAColour_AndTheValidatorFlagsLonelyWindowsAndPosts()
    {
        var editor = TestEditor.Blank(30, 10);
        for (var i = 0; i < MagiWindow.MaxEyes; i++)
        {
            Assert.Equal(EditResult.Ok, editor.Objects.Place(Red(BuiltInMapObjects.MagiEye), new MapPosition(2 * i, 0), out _));
        }

        Assert.Equal(EditResult.TooManyEyes, editor.Objects.Place(Red(BuiltInMapObjects.MagiEye), new MapPosition(9, 0), out _));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.ColouredId(BuiltInMapObjects.MagiEye, ObjectColour.Green), new MapPosition(9, 0), out _));

        editor.Objects.Place(BuiltInMapObjects.ColouredId(BuiltInMapObjects.MagiWindow, ObjectColour.Black), new MapPosition(12, 2), out _);
        editor.Objects.Place(Red(BuiltInMapObjects.BorderGate), new MapPosition(16, 2), out _);
        var issues = MapValidator.Validate(editor.Scenario).Select(i => i.Message).ToList();
        Assert.Contains(issues, m => m.Contains("no Eye of its colour", StringComparison.Ordinal));
        Assert.Contains(issues, m => m.Contains("no Keymaster's Tent", StringComparison.Ordinal));
    }

    // Keymasters and border posts ----------------------------------------------------------------------

    /// <summary>A wall of rock on row 3 with one gap at (5, 3) holding the border post; a red Keymaster's Tent at (2, 0), entrance (3, 1).</summary>
    private static (GameState State, GameSession Session, BorderPost Post) Border(string post)
    {
        var state = Create("..........", "..........", "A....C....", "#####.####", "..........", ".........B");
        Add<KeymasterTent>(state, Red(BuiltInMapObjects.KeymasterTent), 2, 0);
        var border = Add<BorderPost>(state, Red(post), 5, 3);
        return (state, Start(state), border);
    }

    [Fact]
    public void ABorderGuard_BarsHeroesWithoutTheKey_AndLeavesAfterTheFirstKeyHolder()
    {
        var (state, session, guard) = Border(BuiltInMapObjects.BorderGuard);
        var refused = Record<MapObjectRefused>(session);

        Assert.Equal(MoveResult.NoPath, session.MoveTo(C, new MapPosition(5, 5)).StopReason);
        Assert.Equal(MoveResult.Blocked, session.Step(C, Direction.South));
        Assert.Contains("red key", Assert.Single(refused).Reason, StringComparison.Ordinal);

        session.MoveTo(A, new MapPosition(3, 1));
        Assert.Contains(ObjectColour.Red, state.GetPlayer(Blue).Keys);

        var outcome = session.MoveTo(C, new MapPosition(5, 5));
        Assert.True(outcome.Arrived);
        Assert.Null(state.FindObject(guard.Id));
    }

    [Fact]
    public void ABorderGate_Stays_AndLetsEveryKeyHolderThrough_ButNoOtherPlayer()
    {
        var (state, session, gate) = Border(BuiltInMapObjects.BorderGate);
        session.MoveTo(A, new MapPosition(3, 1));
        var keys = Record<KeyReceived>(session);
        session.Revisit(A);
        Assert.Empty(keys); // a second visit gives nothing new

        Assert.True(session.MoveTo(C, new MapPosition(5, 5)).Arrived);
        Assert.NotNull(state.FindObject(gate.Id));
        Assert.True(session.MoveTo(A, new MapPosition(4, 4)).Arrived);

        session.EndTurn();
        Assert.Equal(MoveResult.NoPath, session.MoveTo(B, new MapPosition(5, 2)).StopReason); // Red has no key
    }
}
