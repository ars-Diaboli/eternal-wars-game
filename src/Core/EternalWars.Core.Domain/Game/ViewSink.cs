using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Turns;
using EternalWars.Core.Domain.Visibility;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// The one way into a LAN client's game (ADR-193, ADR-218): puts what the host sent into the state of a session that follows the host
/// (<see cref="GameSession.ViewSink"/>), past the rules, which never run there. Nothing else outside the Domain changes a game but
/// <see cref="GameSession.Execute"/>.
/// </summary>
public interface IViewSink
{
    /// <summary>The game as a whole: its date, week, whose turn it is, its outcome, the question and the battle the player is shown.</summary>
    void ShowGame(GameView game);

    /// <summary>What the player is shown of <paramref name="player"/>.</summary>
    void ShowPlayer(PlayerId player, PlayerView view);

    /// <summary>A hero comes into the view.</summary>
    void AddHero(Hero hero);

    /// <summary>A hero leaves the view.</summary>
    void RemoveHero(Hero hero);

    /// <summary>An object comes into the view, at <paramref name="index"/> in the objects' order (the end when null).</summary>
    void AddObject(MapObject mapObject, int? index);

    /// <summary>An object leaves the view; its place in the objects' order.</summary>
    int RemoveObject(MapObject mapObject);

    /// <summary><paramref name="player"/> remembers <paramref name="known"/> as last seen.</summary>
    void Remember(PlayerId player, KnownObject known);

    /// <summary><paramref name="player"/> no longer remembers the object.</summary>
    void Forget(PlayerId player, MapObjectId mapObject);

    /// <summary><paramref name="player"/>'s retired heroes are now <paramref name="heroes"/>.</summary>
    void ReplaceRetiredHeroes(PlayerId player, IEnumerable<Hero> heroes);

    /// <summary><paramref name="player"/>'s caravans are now <paramref name="caravans"/>.</summary>
    void ReplaceCaravans(PlayerId player, IEnumerable<Caravan> caravans);

    /// <summary><paramref name="player"/> explored <paramref name="positions"/>.</summary>
    void MarkExplored(PlayerId player, IEnumerable<MapPosition> positions);

    /// <summary><paramref name="player"/> sees <paramref name="positions"/> now, and nothing else.</summary>
    void SetSight(PlayerId player, IEnumerable<MapPosition> positions);
}

/// <summary>The sink of a session that follows the host: it writes straight into its state.</summary>
internal sealed class ViewSink(GameState state) : IViewSink
{
    public void ShowGame(GameView game)
    {
        state.Date = game.Date;
        state.WeekEvent = game.WeekEvent;
        state.CurrentPlayerIndex = game.CurrentPlayerIndex;
        state.IsStarted = game.IsStarted;
        state.Outcome = game.Outcome;
        state.Arena = game.Arena;
        state.PendingDecision = game.Decision;
        state.Battle = game.Battle;
    }

    public void ShowPlayer(PlayerId player, PlayerView view)
    {
        var shown = state.GetPlayer(player);
        shown.Name = view.Name;
        shown.IsHuman = view.IsHuman;
        shown.Team = view.Team;
        shown.IsDefeated = view.IsDefeated;
        shown.MaxHeroes = view.MaxHeroes;
        shown.TownlessSince = view.TownlessSince;
        shown.StartTown = view.StartTown;
        shown.Resources = view.Resources;
        shown.AutoCombat = view.AutoCombat;
    }

    public void AddHero(Hero hero) => state.AddHero(hero);

    public void RemoveHero(Hero hero) => state.RemoveHero(hero);

    public void AddObject(MapObject mapObject, int? index) => state.AddObject(mapObject, index);

    public int RemoveObject(MapObject mapObject) => state.RemoveObject(mapObject);

    public void Remember(PlayerId player, KnownObject known) => state.Memory.Set(player, known);

    public void Forget(PlayerId player, MapObjectId mapObject) => state.Memory.Forget(player, mapObject);

    public void ReplaceRetiredHeroes(PlayerId player, IEnumerable<Hero> heroes)
    {
        state.RemoveRetiredHeroesOf(player);
        foreach (var hero in heroes)
        {
            state.Retire(hero);
        }
    }

    public void ReplaceCaravans(PlayerId player, IEnumerable<Caravan> caravans)
    {
        state.RemoveCaravansOf(player);
        foreach (var caravan in caravans)
        {
            state.AddCaravan(caravan);
        }
    }

    public void MarkExplored(PlayerId player, IEnumerable<MapPosition> positions) => state.Fog.MarkExplored(player, positions);

    public void SetSight(PlayerId player, IEnumerable<MapPosition> positions) => state.Fog.SetSight(player, positions);
}

/// <summary>The game as a whole, as one player may see it (ADR-193).</summary>
public sealed record GameView(
    GameDate Date, WeekEvent WeekEvent, int CurrentPlayerIndex, bool IsStarted, GameOutcome? Outcome, ArenaBattle? Arena,
    PendingDecision? Decision, BattleState? Battle)
{
    /// <summary>
    /// The game as <paramref name="viewer"/> sees it: on a simultaneous day (ADR-197) whose turn is open, it is the current player on its own
    /// computer; a question is shown only to the player it asks.
    /// </summary>
    public static GameView Of(GameState state, PlayerId viewer)
    {
        ArgumentNullException.ThrowIfNull(state);
        var fights = state.Battle is { } battle && (battle.Attacker.Owner == viewer || battle.Defender.Owner == viewer);
        var decision = state.PendingDecision is BattleDecision ? (fights ? state.PendingDecision : null)
            : state.DecisionPlayer == viewer ? state.PendingDecision : null;
        var current = state.OpenTurns.Contains(viewer) ? state.IndexOf(viewer) : state.CurrentPlayerIndex;
        return new(state.Date, state.WeekEvent, current, state.IsStarted, state.Outcome, state.Arena, decision, fights ? state.Battle : null);
    }

    /// <summary>Whom a question asks: its hero's owner, else the current player (whose turn asked it).</summary>
    public static PlayerId Asks(GameState state, PendingDecision decision)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(decision);
        return state.Heroes.Concat(state.GarrisonHeroes).FirstOrDefault(h => h.Id == decision.Hero)?.Owner ?? state.CurrentPlayer.Id;
    }
}

/// <summary>A player as another may see it (ADR-193): never another's resources.</summary>
public sealed record PlayerView(
    string Name, bool IsHuman, TeamId Team, bool IsDefeated, int? MaxHeroes, GameDate? TownlessSince, string? StartTown, ResourceSet Resources,
    AutoCombatMode AutoCombat)
{
    public static PlayerView Of(Player player, PlayerId viewer)
    {
        ArgumentNullException.ThrowIfNull(player);
        var own = player.Id == viewer;
        return new(player.Name, player.IsHuman, player.Team, player.IsDefeated, player.MaxHeroes, player.TownlessSince, player.StartTown,
            own ? player.Resources : default, own ? player.AutoCombat : default);
    }
}
