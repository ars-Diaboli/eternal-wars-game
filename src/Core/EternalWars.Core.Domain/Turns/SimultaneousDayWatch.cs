using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Turns;

/// <summary>
/// Watches a simultaneous day (MP-2 b, ADR-197) for what one person does that concerns another, which the day played at once would hide or get
/// wrong: a hero entering another person's start zone, a hero visiting an object another person's hero visited that day, a person seeing another
/// person's hero it did not see when the day began. Allies (one team) share their land and plans, so what an ally does is no conflict (ADR-199),
/// but for a battle against a hero, monster or town another person is fighting at that moment (each turn fights its own, ADR-366); and a
/// battle between two people's armies is always one (one person's battle in the other's turn, ADR-366).
/// The first one is the day's conflict (<see cref="GameState.SimultaneousConflict"/>,
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

    /// <summary>A hero visited an object: a conflict when another person's hero, not an ally's, visited it today.</summary>
    internal void Visited(HeroId hero, MapObjectId mapObject)
    {
        if (!state.IsSimultaneous || state.SimultaneousConflict is not null
            || state.Heroes.FirstOrDefault(h => h.Id == hero) is not { } visitor || !People.Contains(visitor.Owner))
        {
            return;
        }

        if (_visited.TryGetValue(mapObject, out var first) && first != visitor.Owner && !state.AreAllies(first, visitor.Owner))
        {
            var name = state.FindObject(mapObject)?.Definition.Name ?? "an object";
            Conflict($"{state.GetPlayer(visitor.Owner).Name} visited {name}, which {state.GetPlayer(first).Name} visited today.");
            return;
        }

        _visited.TryAdd(mapObject, visitor.Owner);
    }

    /// <summary>
    /// A battle began in the turn the game is in: a conflict when its other side is another person's (a hero, town or garrison of someone who
    /// plays today: that person would fight in another's turn while playing their own), or, allies' too, when another person's battle has one of
    /// its heroes or what it attacks (two battles against one foe at once would both take it).
    /// </summary>
    internal void BattleStarted()
    {
        if (!state.IsSimultaneous || state.SimultaneousConflict is not null || state.Battle is not { } battle)
        {
            return;
        }

        if (battle.Attacker.Owner is { } attacking && battle.Defender.Owner is { } defending && attacking != defending
            && People.Contains(attacking) && People.Contains(defending))
        {
            Conflict($"{state.GetPlayer(attacking).Name} attacked {state.GetPlayer(defending).Name}.");
            return;
        }

        var foes = Parts(battle);
        foreach (var (player, slot) in state.Slots)
        {
            if (slot.Battle is { } other && !ReferenceEquals(other, battle) && player is { } fighting && Parts(other).Intersect(foes).Any())
            {
                var attacker = battle.Attacker.Owner is { } owner ? state.GetPlayer(owner).Name : "A player";
                Conflict($"{attacker} attacked what {state.GetPlayer(fighting).Name} is fighting.");
                return;
            }
        }
    }

    /// <summary>Whether two battles have a hero or a guarded object in common.</summary>
    internal static bool Share(Battles.BattleState one, Battles.BattleState other) => Parts(one).Overlaps(Parts(other));

    /// <summary>The heroes and the guarded object (monster, town, garrison) a battle has, as one set of names.</summary>
    private static HashSet<string> Parts(Battles.BattleState battle) =>
    [
        .. new[] { battle.Attacker.Hero, battle.Defender.Hero }.OfType<HeroId>().Select(h => $"hero {h.Value}"),
        .. new[] { battle.Attacker.Guard, battle.Defender.Guard }.OfType<MapObjectId>().Select(g => $"object {g.Value}"),
    ];

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

    /// <summary>Each person and each other person's hero on the map it sees, allies' left out.</summary>
    private HashSet<(PlayerId, HeroId)> Seen()
    {
        var people = People.ToList();
        return
        [
            .. from viewer in people
               from hero in state.Heroes
               where hero.Owner != viewer && people.Contains(hero.Owner) && !state.AreAllies(viewer, hero.Owner) && state.CanSee(viewer, hero)
               select (viewer, hero.Id),
        ];
    }

    /// <summary>Each person's hero standing in another person's start zone (not an ally's), with that zone.</summary>
    private HashSet<(HeroId, PlayerId)> InOthersZones()
    {
        var people = People.ToList();
        return
        [
            .. from hero in state.Heroes
               where people.Contains(hero.Owner) && state.Map.Contains(hero.Position)
               let zone = state.Map.GetStartZone(hero.Position)
               where zone is { } owner && owner != hero.Owner && people.Contains(owner) && !state.AreAllies(owner, hero.Owner)
               select (hero.Id, zone!.Value),
        ];
    }
}
