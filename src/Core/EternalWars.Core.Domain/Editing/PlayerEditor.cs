using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Editing;

/// <summary>Edits the player slots and teams. Every change publishes <see cref="PlayerSlotsChanged"/>.</summary>
public sealed class PlayerEditor
{
    private readonly EditorContext _context;

    internal PlayerEditor(EditorContext context) => _context = context;

    private GameState Scenario => _context.Scenario;

    public EditResult Add(string name, bool isHuman, out PlayerId id)
    {
        id = default;
        if (Scenario.Players.Count >= GameState.MaxPlayers)
        {
            return EditResult.TooManyPlayers;
        }

        var player = new Player(new PlayerId(Scenario.Players.Count), name, isHuman);
        id = player.Id;
        _context.Run($"Add player {name}", () => AddSlot(player), () => RemoveSlot(player));
        return EditResult.Ok;
    }

    /// <summary>Only the last slot can be removed (player ids are turn-order indices), and only when unused.</summary>
    public EditResult RemoveLast()
    {
        var player = Scenario.Players[^1];
        if (Scenario.Players.Count == 1 || Scenario.HeroesOf(player.Id).Any() ||
            Scenario.Objects.OfType<IFlaggable>().Any(o => o.Owner == player.Id))
        {
            return EditResult.PlayerInUse;
        }

        _context.Run($"Remove player {player.Name}", () => RemoveSlot(player), () => AddSlot(player));
        return EditResult.Ok;
    }

    public EditResult Update(PlayerId id, string name, bool isHuman)
    {
        if (!Scenario.HasPlayer(id))
        {
            return EditResult.UnknownPlayer;
        }

        var player = Scenario.GetPlayer(id);
        Edit($"Edit player {player.Name}", () => (player.Name, player.IsHuman), value => (player.Name, player.IsHuman) = value, (name, isHuman));
        return EditResult.Ok;
    }

    /// <summary>
    /// Gives a player its own hero limit (campaign-like maps, ADR-068), or null for the map's. Refused below the heroes it already has.
    /// </summary>
    public EditResult SetHeroLimit(PlayerId id, int? limit)
    {
        if (!Scenario.HasPlayer(id))
        {
            return EditResult.UnknownPlayer;
        }

        if (limit is < 1 or > ScenarioRules.HeroLimit)
        {
            return EditResult.InvalidValue;
        }

        if (Scenario.HeroesOf(id).Count() > (limit ?? Scenario.Rules.MaxHeroes))
        {
            return EditResult.HeroLimit;
        }

        var player = Scenario.GetPlayer(id);
        Edit($"Set hero limit of {player.Name}", () => player.MaxHeroes, value => Scenario.SetHeroLimit(id, value), limit);
        return EditResult.Ok;
    }

    /// <summary>Sets a player's default team (the setup screen may still override it unless teams are locked).</summary>
    public EditResult SetTeam(PlayerId id, TeamId team)
    {
        if (!Scenario.HasPlayer(id))
        {
            return EditResult.UnknownPlayer;
        }

        var player = Scenario.GetPlayer(id);
        Edit($"Set team of {player.Name}", () => player.Team, value => player.Team = value, team);
        return EditResult.Ok;
    }

    /// <summary>Locks the map's teams so the game setup cannot change them (story/scenario maps).</summary>
    public void SetTeamsLocked(bool locked) =>
        Edit(locked ? "Lock teams" : "Unlock teams", () => Scenario.TeamsLocked, Scenario.SetTeamsLocked, locked);

    /// <summary>
    /// Sets the team counts a game of the map may have (ADR-070, ADR-117), 2 to 8 each, null for no limit; overrides what a template gave a
    /// generated map. Refused (false) for counts out of range or the fewest above the most.
    /// </summary>
    public bool SetTeamCounts(int? min, int? max)
    {
        if (min is < 2 or > GameState.MaxPlayers || max is < 2 or > GameState.MaxPlayers || (min is { } low && max is { } high && low > high))
        {
            return false;
        }

        Edit("Set team counts", () => (Scenario.MinTeams, Scenario.MaxTeams), value => Scenario.SetTeamCounts(value.Item1, value.Item2), (min, max));
        return true;
    }

    private void Edit<T>(string description, Func<T> get, Action<T> set, T next) =>
        _context.Edit(description, get, value =>
        {
            set(value);
            _context.Publish(new PlayerSlotsChanged());
        }, next);

    private void AddSlot(Player player)
    {
        Scenario.AddPlayer(player);
        _context.Publish(new PlayerSlotsChanged());
    }

    private void RemoveSlot(Player player)
    {
        Scenario.RemovePlayer(player);
        _context.Publish(new PlayerSlotsChanged());
    }
}
