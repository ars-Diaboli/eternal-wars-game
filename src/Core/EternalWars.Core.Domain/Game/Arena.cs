using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Game;

/// <summary>Where the arena's battle stands (<see cref="ArenaBattle.Result"/>).</summary>
public enum ArenaResult
{
    /// <summary>The battle is being fought.</summary>
    Fighting,

    /// <summary><see cref="ArenaBattle.Winner"/> won it, by the battle or because the other side had no army; retreat and surrender lose.</summary>
    Won,

    /// <summary>Both armies fell: a loss for both.</summary>
    Draw,

    /// <summary>No battle could be fought (no enemy side, or a side without a hero): a loss for everyone (<see cref="ArenaBattle.Reason"/>).</summary>
    NoBattle,
}

/// <summary>
/// The battle of an arena map (ADR-097, ADR-100), chosen at the start of day 2 (<see cref="ArenaRules.Plan"/>): where it is fought, each side's
/// champion and how it ended. The game ends with it (<see cref="GameFlowService"/>).
/// </summary>
public sealed record ArenaBattle(MapObjectId? CombatPlace, PlayerId? Attacker, HeroId? AttackerHero, PlayerId? Defender, HeroId? DefenderHero)
{
    public ArenaResult Result { get; init; }

    /// <summary>The player whose hero won (<see cref="ArenaResult.Won"/>); its team wins the game.</summary>
    public PlayerId? Winner { get; init; }

    /// <summary>Why no battle was fought (<see cref="ArenaResult.NoBattle"/>).</summary>
    public string? Reason { get; init; }

    /// <summary>
    /// What the battle was, for the result screen (ADR-159): "Alpha (Blue) attacked Bravo (Red)."; null when no battle was chosen. Kept here because a
    /// defeated hero leaves the game.
    /// </summary>
    public string? Summary { get; init; }
}

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
    /// team's <see cref="Champion"/>. Players out of the game, or that never took part, are passed over.
    /// </summary>
    public static ArenaBattle Plan(GameState state)
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
                Result = ArenaResult.NoBattle, Reason = "Nobody was left to fight the arena battle.",
            };
        }

        var attackerHero = Champion(state, attacker.Team);
        var defenderHero = Champion(state, defender.Team);
        var plan = new ArenaBattle(place?.Id, attackerHero?.Owner ?? attacker.Id, attackerHero?.Id, defenderHero?.Owner ?? defender.Id, defenderHero?.Id)
        {
            Summary = attackerHero is null || defenderHero is null ? null
                : $"{attackerHero.Name} ({state.GetPlayer(attackerHero.Owner).Name}) attacked {defenderHero.Name} ({state.GetPlayer(defenderHero.Owner).Name}).",
        };
        var missing = new[] { (Hero: attackerHero, Player: attacker), (Hero: defenderHero, Player: defender) }.Where(s => s.Hero is null).Select(s => s.Player.Name).ToList();
        return missing.Count == 0
            ? plan
            : plan with { Result = ArenaResult.NoBattle, Reason = $"{string.Join(" and ", missing)} had no hero for the arena battle." };
    }

    /// <summary>
    /// The team's strongest hero by army value (<see cref="ArmyValue"/>), the lower hero id first on a tie: one on the map, else one in a town's
    /// garrison; null when the team has no hero at all.
    /// </summary>
    public static Hero? Champion(GameState state, TeamId team)
    {
        var members = state.Players.Where(p => p.Team == team && !p.IsDefeated).Select(p => p.Id).ToHashSet();
        var onMap = state.Heroes.Where(h => members.Contains(h.Owner)).ToList();
        var candidates = onMap.Count > 0 ? onMap : state.GarrisonHeroes.Where(h => members.Contains(h.Owner)).ToList();
        return candidates.OrderByDescending(h => ArmyValue(state, h)).ThenBy(h => h.Id.Value).FirstOrDefault();
    }

    /// <summary>Σ count × creature value of the hero's army; a garrison hero leads its town's garrison (ADR-112).</summary>
    public static long ArmyValue(GameState state, Hero hero)
    {
        var army = state.ObjectsOf<Town>().FirstOrDefault(t => t.GarrisonHero == hero) is { } town ? town.Garrison : hero.Army;
        return army.Stacks.Sum(s => (long)s.Stack.Count * state.Creatures.Get(s.Stack.Creature).Value);
    }
}

/// <summary>Starts the arena battle at the start of day 2 (<see cref="ArenaRules"/>); the battle itself is an ordinary hero battle.</summary>
public sealed class ArenaService(GameState state, IEventPublisher events, BattleService battles)
{
    /// <summary>
    /// Chooses the battle (<see cref="ArenaRules.Plan"/>), moves the champions to the combat place, the defender onto its entrance and the attacker
    /// onto its attack cell, and starts the battle there. A side without an army loses at once, both without armies is a draw; with nobody to fight
    /// the game just ends.
    /// </summary>
    internal void StartBattle()
    {
        var plan = ArenaRules.Plan(state);
        state.Arena = plan;
        if (plan is not { Result: ArenaResult.Fighting, Attacker: { } attackerId, AttackerHero: { } attackerHero, Defender: { } defenderId, DefenderHero: { } defenderHero })
        {
            return;
        }

        var place = plan.CombatPlace is { } id ? (CombatPlace)state.GetObject(id) : null;
        var defender = Bring(defenderHero, place?.VisitPosition);
        var attacker = Bring(attackerHero, place?.AttackCell);
        events.Publish(new ArenaChampionsChosen(plan.CombatPlace, attackerId, attackerHero, defenderId, defenderHero));

        if (attacker.Army.IsEmpty || defender.Army.IsEmpty)
        {
            state.Arena = attacker.Army.IsEmpty && defender.Army.IsEmpty
                ? plan with { Result = ArenaResult.Draw }
                : plan with { Result = ArenaResult.Won, Winner = attacker.Army.IsEmpty ? defenderId : attackerId };
            return;
        }

        battles.BeginArenaBattle(attacker, defender, place?.VisitPosition ?? defender.Position);
    }

    /// <summary>
    /// Puts the champion on <paramref name="cell"/>: a garrison hero comes out of its town with the garrison as its army (ADR-112); a hero already
    /// standing there swaps places with it. Without a cell (no combat place) the hero stays, or comes out onto its town's entrance.
    /// </summary>
    private Hero Bring(HeroId id, MapPosition? cell)
    {
        var hero = state.GetHero(id);
        if (state.ObjectsOf<Town>().FirstOrDefault(t => t.GarrisonHero == hero) is { } town)
        {
            for (var slot = 0; slot < Army.SlotCount; slot++)
            {
                hero.Army.Set(slot, town.Garrison[slot]);
                town.Garrison.Set(slot, null);
            }

            town.GarrisonHero = null;
            hero.Position = cell ?? town.VisitPosition;
            state.AddHero(hero);
            events.Publish(new HeroLeftGarrison(town.Id, hero.Id, hero.Position));
            events.Publish(new GarrisonChanged(town.Id, town.Garrison.Slots.ToList()));
            events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
            return hero;
        }

        if (cell is not { } to || hero.Position == to)
        {
            return hero;
        }

        var from = hero.Position;
        var occupant = state.Heroes.FirstOrDefault(h => h != hero && h.Position == to);
        hero.Position = to;
        if (occupant is not null)
        {
            occupant.Position = from;
        }

        events.Publish(new HeroTeleported(hero.Id, from, to));
        if (occupant is not null)
        {
            events.Publish(new HeroTeleported(occupant.Id, to, from));
        }

        return hero;
    }
}
