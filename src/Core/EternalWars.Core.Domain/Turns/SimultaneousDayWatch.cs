using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Turns;

/// <summary>
/// Watches a simultaneous day (MP-2 b, ADR-197) for what one person does that concerns another, which the day played at once would hide or get
/// wrong: a hero entering another person's start zone, a hero visiting an object another person's hero visited that day, a person seeing another
/// person's hero it did not see when the day began. The first one is the day's conflict (<see cref="GameState.SimultaneousConflict"/>,
/// <see cref="SimultaneousDayConflicted"/>): the session then refuses every command, and the LAN host plays the day again turn by turn.
/// </summary>
internal sealed class SimultaneousDayWatch(GameState state, IEventPublisher events)
{
    private readonly Dictionary<MapObjectId, PlayerId> _visited = [];
    private HashSet<(PlayerId Viewer, HeroId Hero)> _seen = [];
    private HashSet<(HeroId Hero, PlayerId Zone)> _inZones = [];

    /// <summary>The people who play today at once: those with an open turn and those who have ended theirs.</summary>
    private IEnumerable<PlayerId> People => state.OpenTurns.Concat(state.DoneToday).Distinct();

    /// <summary>A simultaneous day opened: what each person sees and where the heroes stand now are not conflicts.</summary>
    internal void DayOpened()
    {
        _visited.Clear();
        _seen = Seen();
        _inZones = InOthersZones();
    }

    /// <summary>A hero visited an object: a conflict when another person's hero visited it today.</summary>
    internal void Visited(HeroId hero, MapObjectId mapObject)
    {
        if (!state.IsSimultaneous || state.SimultaneousConflict is not null
            || state.Heroes.FirstOrDefault(h => h.Id == hero) is not { } visitor || !People.Contains(visitor.Owner))
        {
            return;
        }

        if (_visited.TryGetValue(mapObject, out var first) && first != visitor.Owner)
        {
            var name = state.FindObject(mapObject)?.Definition.Name ?? "an object";
            Conflict($"{state.GetPlayer(visitor.Owner).Name} visited {name}, which {state.GetPlayer(first).Name} visited today.");
            return;
        }

        _visited.TryAdd(mapObject, visitor.Owner);
    }

    /// <summary>After a command of a simultaneous day: a hero in another person's start zone, or a person seeing another's hero, is a conflict.</summary>
    internal void Check()
    {
        if (!state.IsSimultaneous || state.SimultaneousConflict is not null)
        {
            return;
        }

        foreach (var (heroId, zone) in InOthersZones().Where(p => !_inZones.Contains(p)))
        {
            var hero = state.GetHero(heroId);
            Conflict($"{state.GetPlayer(hero.Owner).Name}'s hero {hero.Name} entered {state.GetPlayer(zone).Name}'s start zone.");
            return;
        }

        foreach (var (viewer, heroId) in Seen().Where(p => !_seen.Contains(p)))
        {
            var hero = state.GetHero(heroId);
            Conflict($"{state.GetPlayer(viewer).Name} saw {state.GetPlayer(hero.Owner).Name}'s hero {hero.Name}.");
            return;
        }
    }

    private void Conflict(string reason)
    {
        state.SimultaneousConflict = reason;
        events.Publish(new SimultaneousDayConflicted(reason));
    }

    /// <summary>Each person and each other person's hero on the map it sees.</summary>
    private HashSet<(PlayerId, HeroId)> Seen()
    {
        var people = People.ToList();
        return
        [
            .. from viewer in people
               from hero in state.Heroes
               where hero.Owner != viewer && people.Contains(hero.Owner) && state.CanSee(viewer, hero)
               select (viewer, hero.Id),
        ];
    }

    /// <summary>Each person's hero standing in another person's start zone, with that zone.</summary>
    private HashSet<(HeroId, PlayerId)> InOthersZones()
    {
        var people = People.ToList();
        return
        [
            .. from hero in state.Heroes
               where people.Contains(hero.Owner) && state.Map.Contains(hero.Position)
               let zone = state.Map.GetStartZone(hero.Position)
               where zone is { } owner && owner != hero.Owner && people.Contains(owner)
               select (hero.Id, zone!.Value),
        ];
    }
}
