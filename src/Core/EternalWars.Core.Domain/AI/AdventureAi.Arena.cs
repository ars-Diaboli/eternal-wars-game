using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.AI;

/// <summary>
/// The arena's preparation day (ADR-097, ADR-160) and a tournament's last day (ADR-366): there is no tomorrow, so the AI spends everything on the
/// army that fights at the start of the battle's day. Towns spend everything on troops (<see cref="TownManager.PrepareTown"/>). Heroes skip what
/// pays only later (mines, providers, flags), fight only for small losses, and see no danger (the map keeps the players apart). The strongest
/// hero collects the towns' troops and every other hero brings it its army: that hero is the one <see cref="DecidingBattles.Champion"/> sends.
/// In an arena that is done at the day's end, with movement to spare; in a tournament, where movement is the day's own, at its start (a Town
/// Portal home when no town is in walking reach) and again at its end, each time only where the heroes can still walk today.
/// </summary>
public sealed partial class AdventureAi
{
    /// <summary>Most moves one hero makes on the preparation day, when movement does not stop it.</summary>
    private const int ArenaMovesPerHero = 64;

    /// <summary>
    /// The army gathered for the battle: the strongest hero goes round the own towns it can walk to today, buying what waits and taking the
    /// garrisons, then every other hero that can walk to it today hands over its army and war machines (<see cref="DecisionResponder"/>). False
    /// when it has to wait for a person.
    /// </summary>
    private bool GatherForBattle(PlayerId player)
    {
        if (_state.HeroesOf(player).OrderByDescending(h => DecidingBattles.ArmyValue(_state, h)).ThenBy(h => h.Id.Value).FirstOrDefault() is not { } champion)
        {
            return true;
        }

        _ai.Main = champion.Id;
        _towns.Gathering = true;
        var towns = _state.ObjectsOf<Town>().Where(t => t.Owner == player).OrderBy(t => t.Id.Value).ToList();
        if (_state.Rules.Tournament && !towns.Any(t => t.VisitPosition == champion.Position || WalksToday(champion, t.VisitPosition)))
        {
            PortalForBattle(player, champion);
        }

        foreach (var town in towns)
        {
            if (!_ai.Active(player) || !_state.Heroes.Contains(champion))
            {
                return true;
            }

            // Every town is visited: what it builds and buys now goes straight into the army (another own hero at the entrance takes it, and brings it).
            if (TownRules.VisitingHero(_state, town) is null && WalksToday(champion, town.VisitPosition))
            {
                _ai.Execute(new MoveHeroCommand(player, champion.Id, town.VisitPosition));
                if (!_decisions.ResolvePending(player))
                {
                    return false;
                }
            }

            if (_ai.Active(player) && TownRules.VisitingHero(_state, town) is not null)
            {
                _towns.PrepareTown(player, town);
            }
        }

        foreach (var hero in _state.HeroesOf(player).Where(h => h != champion).OrderBy(h => h.Id.Value).ToList())
        {
            if (!_ai.Active(player) || !_state.Heroes.Contains(champion))
            {
                return true;
            }

            // A hero keeps one creature (a hero must lead a stack), so one with a single creature and no war machine missing has nothing to bring.
            var bringsSomething = hero.Army.Stacks.Sum(s => s.Stack.Count) > 1 || hero.WarMachines.Except(champion.WarMachines).Any();
            if (_state.Heroes.Contains(hero) && bringsSomething && WalksToday(hero, champion.Position))
            {
                _ai.Execute(new MoveHeroCommand(player, hero.Id, champion.Position));
                if (!_decisions.ResolvePending(player))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Whether the hero can walk to <paramref name="target"/> today (on an arena's preparation day: whether it can at all); its last step needs
    /// only some movement left, not the step's whole cost.
    /// </summary>
    private bool WalksToday(Hero hero, MapPosition target) =>
        _session.Movement.FindPath(hero.Id, target) is { IsReachableThisTurn: true };

    /// <summary>
    /// A tournament's champion out of walking reach of every own town on the last day casts Town Portal, when it knows it, to the town it gathers
    /// in (from rank II) or the nearest one (ADR-366).
    /// </summary>
    private void PortalForBattle(PlayerId player, Hero champion)
    {
        if (!CanCast(champion, AdventureMagic.TownPortal))
        {
            return;
        }

        var rank = AdventureMagic.RankOf(_state, champion, AdventureMagic.TownPortal);
        var home = rank >= 2 ? _values.HomeTown(champion) : null;
        if (rank < 2 || (home is not null && AdventureMagic.PortalTown(_state, champion, home.VisitPosition) == home))
        {
            Cast(player, champion, AdventureMagic.TownPortal, home?.VisitPosition);
        }
    }
}
