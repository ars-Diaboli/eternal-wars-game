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

/// <summary>Editor support for Phase 2 data: hero stats and armies, monsters, mine guards, chest values, per-monster guard mode.</summary>
public class EditorArmyAndGuardTests
{
    private static MapEditorSession CreateEditor() => new(BlankScenario.Create(12, 10));

    [Fact]
    public void HeroStatsAndArmy_AreUndoable_AndValidated()
    {
        var editor = CreateEditor();
        editor.Heroes.Place(new PlayerId(0), new MapPosition(2, 2), "Orrin", out var id);
        var hero = editor.Scenario.GetHero(id);

        Assert.Equal(EditResult.Ok, editor.Heroes.SetStats(id, 3, 2, SkillLevel.Advanced));
        Assert.Equal(EditResult.Ok, editor.Heroes.SetArmySlot(id, 4, new CreatureStack(BuiltInCreatures.Lancer, 3)));
        Assert.Equal(EditResult.UnknownCreature, editor.Heroes.SetArmySlot(id, 0, new CreatureStack("haven.dragon", 1)));
        Assert.Equal(EditResult.InvalidValue, editor.Heroes.SetArmySlot(id, 7, null));
        Assert.Equal(EditResult.InvalidValue, editor.Heroes.SetStats(id, -1, 0, SkillLevel.None));

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

        Assert.Equal(EditResult.UnknownCreature, editor.Monsters.Place(new(5, 5), "neutral.unicorn", null, GuardDisposition.Friendly, false, out _));
        Assert.Equal(EditResult.Ok, editor.Monsters.Place(new(5, 5), BuiltInCreatures.Troll, null, GuardDisposition.Hostile, false, out var id));
        Assert.Equal(EditResult.Ok, editor.Monsters.Set(id, BuiltInCreatures.Wyvern, 6, GuardDisposition.Savage, neverFlees: true));

        var monster = (MonsterGuard)editor.Scenario.GetObject(id);
        Assert.Equal((BuiltInCreatures.Wyvern, 6, GuardDisposition.Savage, true), (monster.Creature, monster.Count, monster.Disposition, monster.NeverFlees));
        Assert.Contains(new MapObjectChanged(id), changed);

        editor.Undo();
        Assert.Equal((BuiltInCreatures.Troll, (int?)null), (monster.Creature, monster.Count));
        editor.Undo();
        Assert.Empty(editor.Scenario.Objects);
    }

    [Fact]
    public void MineGuards_AndChestValues_AreUndoable()
    {
        var editor = CreateEditor();
        editor.Objects.Place(BuiltInMapObjects.MineId(ResourceType.Gold), new(4, 4), out var mineId);
        editor.Objects.Place(BuiltInMapObjects.TreasureChest, new(8, 8), out var chestId);
        var mine = (Mine)editor.Scenario.GetObject(mineId);
        var chest = (TreasureChest)editor.Scenario.GetObject(chestId);

        editor.Objects.SetMineGuard(mineId, new GuardStack(BuiltInCreatures.Troll, 8));
        editor.Contents.SetChestValues(chestId, new ChestValues(2000, 1500));

        Assert.Equal((new GuardStack(BuiltInCreatures.Troll, 8), new ChestValues(2000, 1500)), (mine.Guard, chest.FixedValues));
        Assert.Equal(EditResult.InvalidTarget, editor.Objects.SetMineGuard(chestId, null));
        Assert.Equal(EditResult.InvalidTarget, editor.Monsters.SetGuardMode(mineId, GuardMode.Area)); // mine guards have no mode

        editor.Undo();
        editor.Undo();

        Assert.Equal((null, null), (mine.Guard, chest.FixedValues));
    }

    [Fact]
    public void MonsterGuardMode_IsSetPerStack_WithUndoAndRedo()
    {
        var editor = CreateEditor();
        editor.Monsters.Place(new(3, 3), BuiltInCreatures.Troll, null, GuardDisposition.Hostile, false, out var treasureGuardId);
        editor.Monsters.Place(new(8, 3), BuiltInCreatures.Wolf, null, GuardDisposition.Hostile, false, out var otherId);
        var treasureGuard = (MonsterGuard)editor.Scenario.GetObject(treasureGuardId);
        var other = (MonsterGuard)editor.Scenario.GetObject(otherId);
        var changed = new List<IEvent>();
        editor.Events.Subscribe<IEvent>(changed.Add);

        Assert.Equal(GuardMode.Classic, treasureGuard.Mode); // the default
        Assert.Equal(EditResult.Ok, editor.Monsters.SetGuardMode(treasureGuardId, GuardMode.Area));
        Assert.Equal(EditResult.InvalidValue, editor.Monsters.SetGuardMode(treasureGuardId, (GuardMode)7));

        Assert.Equal((GuardMode.Area, GuardMode.Classic), (treasureGuard.Mode, other.Mode));
        Assert.Contains(new MapObjectChanged(treasureGuardId), changed);

        editor.Undo();
        Assert.Equal(GuardMode.Classic, treasureGuard.Mode);
        editor.Redo();
        Assert.Equal(GuardMode.Area, treasureGuard.Mode);
        Assert.Equal(GuardMode.Classic, other.Mode);
    }
}
