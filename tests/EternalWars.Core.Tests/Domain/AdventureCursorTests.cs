using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Cursors;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The adventure map's cursors (ADR-204): what the chosen hero would do where the pointer is.</summary>
public class AdventureCursorTests
{
    private static GameSession World(out Town own, out Town enemy)
    {
        var state = Create(
            "A.C.......B.........",
            "..........#.........",
            "....................",
            "....................",
            "....................",
            "....................",
            "....................",
            "....................",
            "....................",
            "....................");
        own = AddTown(state, TownIds.Haven, 0, 2, Blue);
        enemy = AddTown(state, TownIds.Haven, 12, 2, Red);
        var monster = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 7, 0);
        (monster.Creature, monster.Count) = (BuiltInCreatures.Footman, 1);
        return Start(state);
    }

    private static CursorChoice At(GameSession session, HeroId? selected, int x, int y, bool spell = false)
    {
        var cell = new MapPosition(x, y, 0);
        var path = selected is { } hero ? session.Movement.FindPath(hero, cell) : null;
        return AdventureCursors.For(session.State, Blue, selected, cell, path, spell);
    }

    [Fact]
    public void TheChosenHero_MovesAttacksMeetsAndEnters()
    {
        var session = World(out var own, out var enemy);

        Assert.Equal(new CursorChoice(GameCursor.Move), At(session, A, 5, 1));
        Assert.Equal(GameCursor.Exchange, At(session, A, 2, 0).Cursor);
        Assert.Equal(GameCursor.Attack, At(session, A, 7, 0).Cursor); // the neutral stack
        Assert.Equal(GameCursor.Attack, At(session, A, 10, 0).Cursor); // the enemy hero
        Assert.Equal(GameCursor.EnterTown, At(session, A, own.VisitPosition.X, own.VisitPosition.Y).Cursor);
        Assert.Equal(GameCursor.Attack, At(session, A, enemy.VisitPosition.X, enemy.VisitPosition.Y).Cursor);
        Assert.Equal(GameCursor.SelectHero, At(session, A, 0, 0).Cursor); // itself
        Assert.Equal(GameCursor.Blocked, At(session, A, 10, 1).Cursor); // rock
        Assert.Equal(GameCursor.SelectTown, At(session, A, own.Position.X + 3, own.Position.Y + 3).Cursor); // the town's body: no way in, but it opens
        Assert.Equal(GameCursor.Blocked, At(session, A, enemy.Position.X + 3, enemy.Position.Y + 3).Cursor); // an enemy town's body
        Assert.Equal(GameCursor.AdventureSpell, At(session, A, 5, 1, spell: true).Cursor);
    }

    [Fact]
    public void AMoveBeyondToday_SaysHowManyDays()
    {
        var session = World(out _, out _);

        var choice = At(session, A, 19, 9);

        Assert.Equal(GameCursor.Move, choice.Cursor);
        Assert.True(choice.Days >= 2, $"{choice.Days} days");
    }

    [Fact]
    public void WithoutAChosenHero_OwnThingsAreSelected_AndTheRestIsThePointer()
    {
        var session = World(out var own, out var enemy);

        Assert.Equal(GameCursor.SelectHero, At(session, null, 2, 0).Cursor);
        Assert.Equal(GameCursor.SelectTown, At(session, null, own.VisitPosition.X, own.VisitPosition.Y).Cursor);
        Assert.Equal(GameCursor.Pointer, At(session, null, 4, 9).Cursor);
        Assert.Equal(GameCursor.Pointer, At(session, null, enemy.VisitPosition.X, enemy.VisitPosition.Y).Cursor);
    }
}
