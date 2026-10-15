using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Events;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Editor support for Phase 2 data: hero stats and armies, monsters, mine guards, chest values, guard mode.</summary>
public class EditorArmyAndGuardTests
{
    private static MapEditorSession CreateEditor() => new(BlankScenario.Create(12, 10));

    [Fact]
    public void HeroStatsAndArmy_AreUndoable_AndValidated()
    {
        var editor = CreateEditor();
        editor.PlaceHero(new PlayerId(0), new MapPosition(2, 2), "Orrin", out var id);
        var hero = editor.Scenario.GetHero(id);

        Assert.Equal(EditResult.Ok, editor.SetHeroStats(id, 3, 2, SkillLevel.Advanced));
        Assert.Equal(EditResult.Ok, editor.SetArmySlot(id, 4, new CreatureStack(BuiltInCreatures.Lancer, 3)));
        Assert.Equal(EditResult.UnknownCreature, editor.SetArmySlot(id, 0, new CreatureStack("haven.dragon", 1)));
        Assert.Equal(EditResult.InvalidValue, editor.SetArmySlot(id, 7, null));
        Assert.Equal(EditResult.InvalidValue, editor.SetHeroStats(id, -1, 0, SkillLevel.None));

        Assert.Equal((3, 2, SkillLevel.Advanced), (hero.Attack, hero.Defense, hero.Tactics));
        Assert.Equal(new CreatureStack(BuiltInCreatures.Lancer, 3), hero.Army[4]);

        editor.Undo();
        editor.Undo();

        Assert.Equal((0, 0, SkillLevel.None), (hero.Attack, hero.Defense, hero.Tactics));
        Assert.True(hero.Army.IsEmpty);
        Assert.Contains(MapValidator.Validate(editor.Scenario), i => i.Message.Contains("no army"));
    }

    [Fact]
    public void Monsters_ArePlacedConfiguredAndRemovedWithUndo()
    {
        var editor = CreateEditor();
        var changed = new List<IEvent>();
        editor.Events.Subscribe<IEvent>(changed.Add);

        Assert.Equal(EditResult.UnknownCreature, editor.PlaceMonster(new(5, 5), "neutral.unicorn", null, GuardDisposition.Friendly, false, out _));
        Assert.Equal(EditResult.Ok, editor.PlaceMonster(new(5, 5), BuiltInCreatures.Troll, null, GuardDisposition.Hostile, false, out var id));
        Assert.Equal(EditResult.Ok, editor.SetMonster(id, BuiltInCreatures.Wyvern, 6, GuardDisposition.Savage, neverFlees: true));

        var monster = (MonsterGuard)editor.Scenario.GetObject(id);
        Assert.Equal((BuiltInCreatures.Wyvern, 6, GuardDisposition.Savage, true), (monster.Creature, monster.Count, monster.Disposition, monster.NeverFlees));
        Assert.Contains(new MapObjectChanged(id), changed);

        editor.Undo();
        Assert.Equal((BuiltInCreatures.Troll, (int?)null), (monster.Creature, monster.Count));
        editor.Undo();
        Assert.Empty(editor.Scenario.Objects);
    }

    [Fact]
    public void MineGuards_ChestValues_AndGuardMode_AreUndoable()
    {
        var editor = CreateEditor();
        editor.PlaceObject(BuiltInMapObjects.MineId(ResourceType.Gold), new(4, 4), out var mineId);
        editor.PlaceObject(BuiltInMapObjects.TreasureChest, new(8, 8), out var chestId);
        var mine = (Mine)editor.Scenario.GetObject(mineId);
        var chest = (TreasureChest)editor.Scenario.GetObject(chestId);

        editor.SetMineGuard(mineId, new GuardStack(BuiltInCreatures.Troll, 8));
        editor.SetChestValues(chestId, new ChestValues(2000, 1500));
        editor.SetGuardMode(GuardMode.Area);

        Assert.Equal((new GuardStack(BuiltInCreatures.Troll, 8), new ChestValues(2000, 1500), GuardMode.Area), (mine.Guard, chest.FixedValues, editor.Scenario.GuardMode));
        Assert.Equal(EditResult.InvalidTarget, editor.SetMineGuard(chestId, null));

        editor.Undo();
        editor.Undo();
        editor.Undo();

        Assert.Equal((null, null, GuardMode.Classic), (mine.Guard, chest.FixedValues, editor.Scenario.GuardMode));
    }
}
