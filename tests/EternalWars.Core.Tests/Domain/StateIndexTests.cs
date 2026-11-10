using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Objects;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The lookups kept beside the lists they index (section 4 of the code review): they follow every change to the list.</summary>
public class StateIndexTests
{
    [Fact]
    public void AStacksSpellSum_FollowsEveryChange_ToItsSpells()
    {
        var spells = new ActiveSpellList();
        Assert.Same(SpellModifiers.None, spells.Modifiers);

        var haste = new ActiveSpell("haste", BattleSide.Attacker, new SpellModifiers(Speed: 3), 5);
        var shield = new ActiveSpell("shield", BattleSide.Attacker, new SpellModifiers(Defense: 2, Status: SpellStatus.Phase), 5);
        spells.Add(haste);
        spells.AddRange([shield]);
        Assert.Equal(new SpellModifiers(Defense: 2, Speed: 3, Status: SpellStatus.Phase), spells.Modifiers);

        spells[0] = haste with { Modifiers = new SpellModifiers(Speed: 5) };
        Assert.Equal(5, spells.Modifiers.Speed);

        Assert.Equal(1, spells.RemoveAll(s => s.Spell == "shield"));
        Assert.False(spells.Modifiers.Has(SpellStatus.Phase));

        spells.Clear();
        Assert.Same(SpellModifiers.None, spells.Modifiers);
    }

    [Fact]
    public void HeroesAndObjects_AreFoundById_UntilTheyLeaveTheMap()
    {
        var state = Create("A....");
        var hero = state.Heroes.Single();
        var guard = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 3, 0);
        Assert.Same(hero, state.GetHero(hero.Id));
        Assert.Same(guard, state.FindObject(guard.Id));

        state.RemoveHero(hero);
        state.RemoveObject(guard);

        Assert.Throws<CommandRefusedException>(() => state.GetHero(hero.Id));
        Assert.Null(state.FindObject(guard.Id));
        Assert.Null(state.ObjectCellAt(new MapPosition(3, 0)));
    }
}
