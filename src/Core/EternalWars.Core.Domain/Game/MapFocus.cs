using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// Where the adventure map's camera goes and which hero is selected, for a game shown on this computer (ADR-090, ADR-164, ADR-231). The
/// front-end asks it at each moment that may move the camera and moves it where it answers: a computer hero starting to walk in sight, a map
/// event's place, the selected hero teleported to another level, a turn shown, the selected hero defeated. Built per shown game, with its
/// <see cref="GameDirector"/> (whose colours play here) and whether a replay is watched.
/// </summary>
public sealed class MapFocus(GameSession session, GameDirector director, bool watching = false)
{
    /// <summary>The computer hero the camera last followed: it is followed once until another walks or a turn starts.</summary>
    private HeroId? _followed;

    /// <summary>A turn started: the next hero walking in sight is followed again.</summary>
    public void TurnStarted() => _followed = null;

    /// <summary>
    /// A hero starts a step to <paramref name="to"/>: true when the camera follows it there (on that level). The replay viewer follows every
    /// hero it can see, on either level; a game follows a computer's heroes on the level shown, when computer heroes are shown
    /// (<paramref name="showComputerHeroes"/>). A hero is followed once, until another walks or a turn starts.
    /// </summary>
    public bool Follows(Hero hero, MapPosition to, bool showComputerHeroes, int levelShown, Func<MapPosition, bool> inSight)
    {
        var follows = (watching || (showComputerHeroes && !session.State.GetPlayer(hero.Owner).IsHuman)) && hero.Id != _followed
            && (watching || to.Level == levelShown) && inSight(to);
        if (follows)
        {
            _followed = hero.Id;
        }

        return follows;
    }

    /// <summary>
    /// Where a turn-start message or question of a map event moves the camera (EVT-2 f): its place, when the current player plays here and the
    /// computer's moves are no longer shown (the turn's showing comes back to it); null otherwise.
    /// </summary>
    public MapPosition? EventPlace(PendingDecision? decision, bool showingComputerMoves) =>
        decision is MapEventDecision { Place: { } place } && !showingComputerMoves && director.PlaysHere(session.State.CurrentPlayer.Id)
            ? place
            : null;

    /// <summary>Where the camera goes when a hero teleports: to the selected hero, when it lands on another level than the one shown.</summary>
    public static MapPosition? AfterTeleport(HeroTeleported teleport, HeroId? selected, int levelShown) =>
        teleport.Hero == selected && teleport.To.Level != levelShown ? teleport.To : null;

    /// <summary>The hero selected when a player's turn is shown or a game is shown again: their first; null when they have none.</summary>
    public HeroId? FirstHeroOf(PlayerId player) => session.State.HeroesOf(player).FirstOrDefault()?.Id;

    /// <summary>
    /// Whether a person's turn is still to be shown: the turn may have passed on while it waited for the computer's moves.
    /// </summary>
    public bool StillTheTurnOf(PlayerId player) => session.State.CurrentPlayer.Id == player;

    /// <summary>
    /// A hero was defeated: true when it was the selected one, with <paramref name="next"/> the current player's first hero (or none) to select
    /// instead.
    /// </summary>
    public bool Reselects(HeroDefeated defeated, HeroId? selected, out HeroId? next)
    {
        next = defeated.Hero == selected ? FirstHeroOf(session.State.CurrentPlayer.Id) : null;
        return defeated.Hero == selected;
    }
}
