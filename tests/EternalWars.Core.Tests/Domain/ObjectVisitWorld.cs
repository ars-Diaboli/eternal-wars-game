using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>A wide world with one object to visit, for the cartographer, merchant, refugee camp, altar, sphinx and witch hut tests.</summary>
internal static class ObjectVisitWorld
{
    // Every object here has its entrance at (3, 1) when placed at (2, 0); hero A starts below it, Charlie in the corner.
    public static (GameSession Session, T Object) World<T>(string definition, Action<GameState, T>? setUp = null, bool revealAll = true)
        where T : MapObject
    {
        var state = Create(".............................", ".............................", "...A.........................", ".............................", "C...........................B");
        var mapObject = Add<T>(state, definition, 2, 0);
        setUp?.Invoke(state, mapObject);
        var session = Start(state, revealAll);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 50_000);
        return (session, mapObject);
    }

    public static Hero HeroA(GameSession session) => session.State.GetHero(A);
}
