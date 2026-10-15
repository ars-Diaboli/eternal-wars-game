using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// Complete, serializable snapshot of a running game. Mutated only by game services.
/// </summary>
public sealed class GameState
{
    public GameState(AdventureMap map, IEnumerable<Player> players, IEnumerable<Hero> heroes)
    {
        Map = map ?? throw new ArgumentNullException(nameof(map));
        Players = players.ToList();
        Heroes = heroes.ToList();

        if (Players.Count == 0)
        {
            throw new ArgumentException("A game needs at least one player.", nameof(players));
        }
    }

    public AdventureMap Map { get; }

    public IReadOnlyList<Player> Players { get; }

    public IReadOnlyList<Hero> Heroes { get; }

    public GameDate Date { get; internal set; } = GameDate.First;

    public int CurrentPlayerIndex { get; internal set; }

    public bool IsStarted { get; internal set; }

    public Player CurrentPlayer => Players[CurrentPlayerIndex];

    public Player GetPlayer(PlayerId id) => Players.First(p => p.Id == id);

    public Hero GetHero(HeroId id) => Heroes.First(h => h.Id == id);

    public IEnumerable<Hero> HeroesOf(PlayerId owner) => Heroes.Where(h => h.Owner == owner);

    public Hero? HeroAt(MapPosition position) => Heroes.FirstOrDefault(h => h.Position == position);
}
