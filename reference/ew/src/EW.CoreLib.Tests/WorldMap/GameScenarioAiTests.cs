using System.Collections.Generic;
using System.Linq;
using EW.CoreLib.Events;
using EW.CoreLib.Map;
using EW.CoreLib.Messaging;
using EW.CoreLib.Tests.WorldMap;
using EW.CoreLib.Types;
using NUnit.Framework;

namespace EW.CoreLib.Tests;

[TestFixture]
public class GameScenarioAiTests : GameMapTestsBase
{
    private sealed class PlayerEndedTurnHandler : IHandleEvent<PlayerEndedTurn>
    {
        public bool EventFired { get; private set; } = false;
        public GamePlayer FiredPlayer { get; private set; } = null;

        public PlayerEndedTurnHandler()
        {
            Hub.Instance.Subscribe(this);
        }

        public void Handle(PlayerEndedTurn e)
        {
            EventFired = true;
            FiredPlayer = e.Player;
        }

        public void Unsubscribe()
        {
            Hub.Instance.Unsubscribe(this);
        }
    }

    private PlayerEndedTurnHandler eventHandler;

    [SetUp]
    public void SetUp()
    {
        eventHandler = new PlayerEndedTurnHandler();
    }

    [TearDown]
    public void TearDown()
    {
        if (eventHandler != null)
        {
            eventHandler.Unsubscribe();
        }
    }

    [Test]
    public void UpdateAi_WhenActivePlayerIsAi_ExecutesMovementAndEndsTurn()
    {
        var aiPlayer = new GamePlayer
        {
            DisplayName = "AI Player",
            PlayerColor = PlayerColor.Blue,
            ControlType = PlayerControlType.Ai
        };

        var aiHero = new Hero(Faction.Temple, new Position(0, 0), aiPlayer)
        {
            ObjectId = "ai-hero-1",
            BaseStats = new HeroData { MaxMovementPoints = 3400 },
            Path = { CurrentMovementPoints = 1200 }
        };
        aiPlayer.Heroes.Add(aiHero);

        SetUpMap(aiHero);

        var playersList = new List<GamePlayer>();
        playersList.Add(aiPlayer);

        var sut = new GameScenarioController(Map, playersList, aiPlayer, aiPlayer);

        sut.ProcessAnimations(0.1);

        Assert.That(aiHero.Path.IsMoving, Is.True);
        Assert.That(aiHero.Path.MovementWaypoints, Is.Not.Empty);

        while (aiHero.Path.IsMoving)
        {
            sut.ProcessAnimations(0.25);
        }

        Assert.That(eventHandler.EventFired, Is.True);
        Assert.That(eventHandler.FiredPlayer, Is.EqualTo(aiPlayer));
    }
}
