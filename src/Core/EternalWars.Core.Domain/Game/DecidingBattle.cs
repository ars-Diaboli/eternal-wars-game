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

/// <summary>Where the deciding battle stands (<see cref="DecidingBattle.Result"/>).</summary>
public enum DecidingBattleResult
{
    /// <summary>The battle is being fought.</summary>
    Fighting,

    /// <summary><see cref="DecidingBattle.Winner"/> won it, by the battle or because the other side had no army; retreat and surrender lose.</summary>
    Won,

    /// <summary>Both armies fell: a loss for both.</summary>
    Draw,

    /// <summary>No battle could be fought (no enemy side, or a side without a hero): a loss for everyone (<see cref="DecidingBattle.Reason"/>).</summary>
    NoBattle,
}

/// <summary>
/// The battle that decides an arena (ADR-097, ADR-100) or a tournament (ADR-366), chosen at the start of its day (<see cref="DecidingBattles.Plan"/>):
/// where it is fought, each side's champion and how it ended. The game ends with it (<see cref="GameFlowService"/>).
/// </summary>
public sealed record DecidingBattle(MapObjectId? CombatPlace, PlayerId? Attacker, HeroId? AttackerHero, PlayerId? Defender, HeroId? DefenderHero)
{
    public DecidingBattleResult Result { get; init; }

    /// <summary>The player whose hero won (<see cref="DecidingBattleResult.Won"/>); its team wins the game.</summary>
    public PlayerId? Winner { get; init; }

    /// <summary>Why no battle was fought (<see cref="DecidingBattleResult.NoBattle"/>).</summary>
    public string? Reason { get; init; }

    /// <summary>
    /// What the battle was, for the result screen (ADR-159): "Alpha (Blue) attacked Bravo (Red)."; null when no battle was chosen. Kept here because a
    /// defeated hero leaves the game.
    /// </summary>
    public string? Summary { get; init; }
}

/// <summary>
/// The battle that decides an arena (ADR-097) or a tournament (ADR-366): the same battle with two triggers. Its day comes before anyone's turn, the
/// champions fight it without experience, and its result ends the game (<see cref="GameFlowService"/>); the map's conditions do not count.
/// </summary>
public static class DecidingBattles
{
    /// <summary>Whether a map with these rules ends with a deciding battle: an arena or a tournament.</summary>
    public static bool Decides(ScenarioRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.Arena || rules.Tournament;
    }

    /// <summary>The day the battle starts by itself (an arena's <see cref="ArenaRules.BattleDay"/>, a tournament's <see cref="TournamentRules.BattleDay"/>); null on other maps.</summary>
    public static int? Day(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Rules.Arena ? ArenaRules.BattleDay : state.Rules.Tournament ? TournamentRules.BattleDay(state.Options) : null;
    }

    /// <summary>Whether today is the battle's day and the battle has not been chosen yet.</summary>
    public static bool IsDue(GameState state) => state.DecidingBattle is null && Day(state) == state.Date.AbsoluteDay;

    /// <summary>The battle's name in messages: "tournament battle" or "arena battle".</summary>
    public static string Name(ScenarioRules rules) => rules.Tournament ? "tournament battle" : "arena battle";

    /// <summary>The battle chosen at the start of its day; both triggers choose it as the arena does (<see cref="ArenaRules.Plan"/>).</summary>
    public static DecidingBattle Plan(GameState state) => ArenaRules.Plan(state);

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
        return army.Stacks.Sum(s => (long)s.Stack.Count * state.Creatures.Get(s.Stack.Creature).FightValue);
    }
}

/// <summary>
/// Starts the deciding battle at the start of its day (<see cref="DecidingBattles.IsDue"/>): an arena's day 2, a tournament's day after the last
/// (ADR-097, ADR-366). The battle itself is an ordinary hero battle.
/// </summary>
public sealed class DecidingBattleService(GameState state, IEventPublisher events, BattleService battles)
{
    /// <summary>
    /// Chooses the battle (<see cref="DecidingBattles.Plan"/>), moves the champions to the combat place, the defender onto its entrance and the attacker
    /// onto its attack cell, and starts the battle there. A side without an army loses at once, both without armies is a draw; with nobody to fight
    /// the game just ends.
    /// </summary>
    internal void StartBattle()
    {
        var plan = DecidingBattles.Plan(state);
        state.DecidingBattle = plan;
        if (plan is not { Result: DecidingBattleResult.Fighting, Attacker: { } attackerId, AttackerHero: { } attackerHero, Defender: { } defenderId, DefenderHero: { } defenderHero })
        {
            return;
        }

        var place = plan.CombatPlace is { } id ? (CombatPlace)state.GetObject(id) : null;
        var defender = Bring(defenderHero, place?.VisitPosition);
        var attacker = Bring(attackerHero, place?.AttackCell);
        events.Publish(new ChampionsChosen(plan.CombatPlace, attackerId, attackerHero, defenderId, defenderHero));

        if (attacker.Army.IsEmpty || defender.Army.IsEmpty)
        {
            state.DecidingBattle = attacker.Army.IsEmpty && defender.Army.IsEmpty
                ? plan with { Result = DecidingBattleResult.Draw }
                : plan with { Result = DecidingBattleResult.Won, Winner = attacker.Army.IsEmpty ? defenderId : attackerId };
            return;
        }

        battles.BeginDecidingBattle(attacker, defender, place?.VisitPosition ?? defender.Position);
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
