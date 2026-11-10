using EternalWars.Core.Domain.Battles.Spells;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// Casting in battle (ADR-110, ADR-120, ADR-123, ADR-149–152): a hero's or a creature's cast, paid and reported, then worked by its effect kind's
/// handler (<see cref="SpellEffectHandlers"/>) inside the shared frames here: the reach (resisting stacks reported, Earthquake, wake-ups) and Raise
/// Dead's rising as rounds start. <see cref="BattleEngine"/> ends the turn afterwards; validation is <see cref="BattleMagic"/>'s.
/// </summary>
internal sealed class BattleSpellResolver(BattleState battle, IEventPublisher events, BattleWounds wounds)
{
    /// <summary>
    /// The side's hero casts a spell (ADR-110): it pays the mana, then the spell works on every stack in its reach it affects, by id; a stack it
    /// does not affect is reported as resisting. Null on success, else why it was refused (nothing changed then).
    /// </summary>
    public string? Cast(BattleSide side, string spellId, int? targetId, CellPosition? cell, int? second)
    {
        if (BattleMagic.CastRefusal(battle, side, spellId, targetId, cell, second) is { } refusal)
        {
            return refusal;
        }

        var known = battle.Participant(side).Caster!.Find(spellId)!;
        var cost = BattleMagic.Cost(battle, side, known);
        battle.ManaLeft[side] -= cost;
        battle.CastRounds[side] = battle.Round;
        var seconds = known.Effect!.SecondIsStack ? second : null;
        events.Publish(new BattleSpellCast(side, spellId, targetId, cell, cost, battle.ManaOf(side), seconds));
        Work(side, known, targetId, cell, null, seconds);
        return null;
    }

    /// <summary>
    /// The active creature caster casts its spell (ADR-120): no mana, with its own Spell Power (<see cref="BattleMagic.CreatureSpellPower"/>);
    /// one of its casts is used up. Null on success, else why it was refused (nothing changed then).
    /// </summary>
    public string? CreatureCast(int? targetId, CellPosition? cell)
    {
        var unit = battle.ActiveUnit!;
        if (BattleMagic.CreatureCastRefusal(battle, unit, targetId, cell) is { } refusal)
        {
            return refusal;
        }

        var known = BattleMagic.CreatureSpell(battle, unit)!;
        unit.MutableCastsLeft[known.Spell.Id]--;
        events.Publish(new BattleCreatureCast(unit.Id, known.Spell.Id, targetId, cell, unit.CastsLeft[known.Spell.Id]));
        Work(unit.Side, known, targetId, cell, BattleMagic.CreatureSpellPower(unit));
        return null;
    }

    /// <summary>The battle the spells are cast in.</summary>
    public BattleState Battle => battle;

    /// <summary>Where what the spells do is published.</summary>
    public IEventPublisher Events => events;

    /// <summary>The battle's damage, deaths and healing, shared with the engine.</summary>
    public BattleWounds Wounds => wounds;

    /// <summary>What a cast does, the hero's or a creature's: its kind's handler (<see cref="SpellEffectHandlers"/>) works it.</summary>
    private void Work(BattleSide side, KnownSpell known, int? targetId, CellPosition? cell, int? creaturePower, int? second = null) =>
        SpellEffectHandlers.For(known.Effect!.Kind).Apply(new CastContext(battle, side, known, targetId, cell, second, creaturePower), this);

    /// <summary>
    /// The frame of a cast worked on the stacks in its reach: each stack in it the spell does not affect is reported as resisting, by id;
    /// <paramref name="work"/> then works the spell on the reach; Earthquake shakes the walls; stacks blinded and hit wake up.
    /// </summary>
    public void OnReach(CastContext cast, Action<IReadOnlyList<BattleUnit>> work)
    {
        var reach = BattleMagic.Reach(battle, cast.Side, cast.Known, cast.Target, cast.Cell);
        foreach (var unit in reach.Where(u => !BattleMagic.Affects(battle, cast.Side, cast.Known, u)))
        {
            events.Publish(new BattleSpellResisted(unit.Id, cast.Known.Spell.Id));
        }

        work(reach);
        if (cast.Effect.Walls > 0)
        {
            Quake(cast.Known.Spell.Id, cast.Effect.Walls);
        }

        wounds.WakeHit();
    }

    /// <summary>
    /// Raise Dead during the battle (ADR-152): while the side's spell still raises (rank II once, rank III each round), the destroyed stack worth most
    /// rises as a new stack of skeletons for the side, on its place; they act from the next round and join the army when the side's hero wins.
    /// </summary>
    public void Rise(BattleSide side)
    {
        if (!battle.Raising.TryGetValue(side, out var raise) || raise.Rises == Magic.SpellRises.None
            || BattleMagic.NextRising(battle, raise.Percent) is not { } rising)
        {
            return;
        }

        var (corpse, skeleton, count) = rising;
        battle.Risen.Add(corpse.Id);
        var unit = new BattleUnit(battle.NextUnitId(), side, skeleton, count, -1, corpse.Position) { IsRaised = true };
        battle.MutableUnits.Add(unit);
        if (raise.Rises == Magic.SpellRises.Once)
        {
            battle.Raising[side] = raise with { Rises = Magic.SpellRises.None };
        }

        events.Publish(new BattleUnitRaised(unit.Info(), corpse.Id, raise.Spell));
    }

    /// <summary>Earthquake (ADR-120): <paramref name="sections"/> standing wall sections, drawn at random, take 1 damage each.</summary>
    private void Quake(string spell, int sections)
    {
        if (battle.Siege is not { } siege)
        {
            return;
        }

        var walls = siege.Structures.Where(s => s.IsStanding && s.Kind == SiegeStructureKind.Wall).OrderBy(s => s.Id).ToList();
        for (var i = 0; i < sections && walls.Count > 0; i++)
        {
            var wall = walls[battle.Random.Next(walls.Count)];
            walls.Remove(wall);
            wall.Health -= 1;
            events.Publish(new SiegeStructureDamaged(wall.Id, null, spell, 1, wall.Health));
        }
    }
}
