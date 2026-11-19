using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The game moved on unheard (ADR-234): the rules still hear every event, only the subscribers outside them (<see cref="GameSession.Events"/>)
/// do not.
/// </summary>
public class UnheardEventsTests
{
    [Fact]
    public void Unheard_TheOutsideHearsNothing_WhileTheRulesStillRun()
    {
        var heardState = Create("A....", ".....", "....B");
        var unheardState = Create("A....", ".....", "....B");
        var heard = Start(heardState, revealAll: false);
        var unheard = Start(unheardState, revealAll: false);
        var moves = 0;
        unheard.Events.Subscribe<HeroMoved>(_ => moves++);

        using (unheard.Unheard())
        {
            unheard.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
            heard.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        }

        Assert.Equal(0, moves);
        Assert.Equal(GameStateDigest.Describe(heardState), GameStateDigest.Describe(unheardState)); // the fog moved with the hero all the same

        unheard.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        Assert.Equal(1, moves);
    }

    [Fact]
    public void UnheardScopes_Nest_AndEachEndsOnce()
    {
        var session = Start(Create("A....", ".....", "....B"));
        var moves = 0;
        session.Events.Subscribe<HeroMoved>(_ => moves++);

        var outer = session.Unheard();
        using (session.Unheard())
        {
        }

        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        Assert.Equal(0, moves); // the outer scope still holds

        outer.Dispose();
        outer.Dispose();
        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        Assert.Equal(1, moves);
    }
}
