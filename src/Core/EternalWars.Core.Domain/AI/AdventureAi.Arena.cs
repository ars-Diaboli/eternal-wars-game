using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.AI;

/// <summary>
/// The arena's preparation day (ADR-097, ADR-160): there is no tomorrow, so the AI spends everything on the army that fights at the start of day 2.
/// Towns spend everything on troops (<see cref="TownManager.PrepareTown"/>). Heroes skip what pays only later (mines, providers, flags), fight only for small
/// losses, and see no danger (the map keeps the players apart). At the end of the day the strongest hero collects the towns' troops and every
/// other hero brings it its army: that hero is the one <see cref="DecidingBattles.Champion"/> sends.
/// </summary>
public sealed partial class AdventureAi
{
    /// <summary>Most moves one hero makes on the preparation day, when movement does not stop it.</summary>
    private const int ArenaMovesPerHero = 64;

    /// <summary>
    /// The end of the preparation day: the strongest hero goes round the own towns, buying what waits and taking the garrisons, then every other
    /// hero walks to it and hands over its army and war machines (<see cref="DecisionResponder"/>). False when it has to wait for a person.
    /// </summary>
    private bool GatherForBattle(PlayerId player)
    {
        if (_state.HeroesOf(player).OrderByDescending(h => DecidingBattles.ArmyValue(_state, h)).ThenBy(h => h.Id.Value).FirstOrDefault() is not { } champion)
        {
            return true;
        }

        _ai.Main = champion.Id;
        _towns.Gathering = true;
        foreach (var town in _state.ObjectsOf<Town>().Where(t => t.Owner == player).OrderBy(t => t.Id.Value).ToList())
        {
            if (!_ai.Active(player) || !_state.Heroes.Contains(champion))
            {
                return true;
            }

            // Every town is visited: what it builds and buys now goes straight into the army (another own hero at the entrance takes it, and brings it).
            if (TownRules.VisitingHero(_state, town) is null && _session.Movement.FindPath(champion.Id, town.VisitPosition) is not null)
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
            if (_state.Heroes.Contains(hero) && bringsSomething && _session.Movement.FindPath(hero.Id, champion.Position) is not null)
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
}
