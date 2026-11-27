using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// The arena mode (ADR-097, ADR-100, HotA-like), switched on by <see cref="ScenarioRules.Arena"/>. Day 1 is the preparation: heroes have nearly
/// unlimited movement and towns build any number of buildings, with the usual starting resources. At the start of day 2 one battle between two
/// heroes decides the game: its winner's team wins; a draw, or a side without a hero, is a loss for everyone. The map's victory and defeat
/// conditions do not count.
/// </summary>
public static class ArenaRules
{
    /// <summary>The day of preparation.</summary>
    public const int PreparationDay = 1;

    /// <summary>The day the battle starts by itself, before anyone's turn.</summary>
    public const int BattleDay = 2;

    /// <summary>A hero's movement points on the preparation day: "nearly unlimited" (HotA), some 6 000 grass cells.</summary>
    public const int PreparationMovement = 1_000_000;

    /// <summary>Whether today is an arena map's preparation day: unlimited movement and building.</summary>
    public static bool IsPreparationDay(GameState state) => state.Rules.Arena && state.Date.AbsoluteDay == PreparationDay;

    /// <summary>
    /// HotA's choice of the battle: the <b>defender</b> is the first player in turn order with a hero standing on a combat place's entrance, and
    /// that combat place is the battlefield; the <b>attacker</b> is the first player in turn order who is its enemy. Without a hero on a combat place
    /// the first player in turn order attacks and the next one that is its enemy defends, at the first combat place. Each side fights with its
    /// team's <see cref="DecidingBattles.Champion"/>. Players out of the game, or that never took part, are passed over.
    /// </summary>
    public static DecidingBattle Plan(GameState state)
    {
        var players = state.Players.Where(p => p.TookPart && !p.IsDefeated).ToList();
        var places = state.ObjectsOf<CombatPlace>().OrderBy(c => c.Id.Value).ToList();
        Player? defender = null;
        CombatPlace? place = null;
        foreach (var player in players)
        {
            if (places.FirstOrDefault(c => state.HeroesOf(player.Id).Any(h => h.Position == c.VisitPosition)) is { } taken)
            {
                (defender, place) = (player, taken);
                break;
            }
        }

        Player? attacker;
        if (defender is not null)
        {
            attacker = players.FirstOrDefault(p => state.AreEnemies(p.Id, defender.Id));
        }
        else
        {
            attacker = players.FirstOrDefault();
            defender = attacker is null ? null : players.Skip(1).FirstOrDefault(p => state.AreEnemies(p.Id, attacker.Id));
            place = places.FirstOrDefault();
        }

        if (attacker is null || defender is null)
        {
            return new(place?.Id, attacker?.Id, null, defender?.Id, null)
            {
                Result = DecidingBattleResult.NoBattle, Reason = $"Nobody was left to fight the {DecidingBattles.Name(state.Rules)}.",
            };
        }

        var attackerHero = DecidingBattles.Champion(state, attacker.Team);
        var defenderHero = DecidingBattles.Champion(state, defender.Team);
        var plan = new DecidingBattle(place?.Id, attackerHero?.Owner ?? attacker.Id, attackerHero?.Id, defenderHero?.Owner ?? defender.Id, defenderHero?.Id)
        {
            Summary = attackerHero is null || defenderHero is null ? null
                : $"{attackerHero.Name} ({state.GetPlayer(attackerHero.Owner).Name}) attacked {defenderHero.Name} ({state.GetPlayer(defenderHero.Owner).Name}).",
        };
        var missing = new[] { (Hero: attackerHero, Player: attacker), (Hero: defenderHero, Player: defender) }.Where(s => s.Hero is null).Select(s => s.Player.Name).ToList();
        return missing.Count == 0
            ? plan
            : plan with { Result = DecidingBattleResult.NoBattle, Reason = $"{string.Join(" and ", missing)} had no hero for the {DecidingBattles.Name(state.Rules)}." };
    }
}
