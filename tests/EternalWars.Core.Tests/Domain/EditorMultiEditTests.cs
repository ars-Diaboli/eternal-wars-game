using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Multi-edit (EDIT-1, ADR-255): one field written to several objects of one kind as one undo step.</summary>
public class EditorMultiEditTests
{
    private static MapEditorSession Editor() => new(BlankScenario.Create(40, 30));

    private static MapObjectId Monster(MapEditorSession editor, int x, string creature, int? count)
    {
        Assert.Equal(EditResult.Ok, editor.Monsters.Place(new MapPosition(x, 10), creature, count, GuardDisposition.Aggressive, false, out var id));
        return id;
    }

    private static MapObjectId Place(MapEditorSession editor, string definition, int x, int y)
    {
        Assert.Equal(EditResult.Ok, editor.Objects.Place(definition, new MapPosition(x, y), out var id));
        return id;
    }

    private static List<MonsterGuard> Monsters(MapEditorSession editor, params MapObjectId[] ids) => [.. ids.Select(id => (MonsterGuard)editor.Scenario.FindObject(id)!)];

    [Fact]
    public void AFieldSetOnSeveralObjects_IsOneUndoStep_AndKeepsEachOnesOtherFields()
    {
        var editor = Editor();
        var ids = new[] { Monster(editor, 5, BuiltInCreatures.Wolf, 10), Monster(editor, 10, BuiltInCreatures.Pikeman, null), Monster(editor, 15, BuiltInCreatures.Wolf, 3) };
        var steps = editor.History.Steps.Count;

        Assert.Equal(EditResult.Ok, ObjectProperties.MonsterDisposition.Apply(editor, Monsters(editor, ids), GuardDisposition.Friendly));

        Assert.Equal(steps + 1, editor.History.Steps.Count);
        Assert.Equal("Set disposition of 3 objects", editor.History.UndoDescription);
        var monsters = Monsters(editor, ids);
        Assert.All(monsters, m => Assert.Equal(GuardDisposition.Friendly, m.Disposition));
        Assert.Equal([BuiltInCreatures.Wolf, BuiltInCreatures.Pikeman, BuiltInCreatures.Wolf], monsters.Select(m => m.Creature.Value));
        Assert.Equal([10, null, 3], monsters.Select(m => m.Count));

        editor.Undo();
        Assert.All(Monsters(editor, ids), m => Assert.Equal(GuardDisposition.Aggressive, m.Disposition));
        editor.Redo();
        Assert.All(Monsters(editor, ids), m => Assert.Equal(GuardDisposition.Friendly, m.Disposition));
    }

    [Fact]
    public void AValueTheObjectsAlreadyHave_RecordsNoStep()
    {
        var editor = Editor();
        var ids = new[] { Monster(editor, 5, BuiltInCreatures.Wolf, 10), Monster(editor, 10, BuiltInCreatures.Wolf, 10) };
        var steps = editor.History.Steps.Count;

        Assert.Equal(EditResult.Ok, ObjectProperties.MonsterCreature.Apply(editor, Monsters(editor, ids), new CreatureId(BuiltInCreatures.Wolf)));

        Assert.Equal(steps, editor.History.Steps.Count);
    }

    [Fact]
    public void SharedValue_IsTheCommonValue_OrDiffers()
    {
        var editor = Editor();
        var ids = new[] { Monster(editor, 5, BuiltInCreatures.Wolf, 10), Monster(editor, 10, BuiltInCreatures.Wolf, 4) };
        var monsters = Monsters(editor, ids);

        Assert.Equal(new SharedValue<CreatureId>(false, new CreatureId(BuiltInCreatures.Wolf)), ObjectProperties.MonsterCreature.Shared(monsters));
        Assert.True(ObjectProperties.MonsterCount.Shared(monsters).Differs);

        Assert.Equal(EditResult.Ok, ObjectProperties.MonsterCount.Apply(editor, monsters, null));
        Assert.Equal(new SharedValue<int?>(false, null), ObjectProperties.MonsterCount.Shared(Monsters(editor, ids)));
        Assert.True(SharedValue<int>.Of([]).Differs);
    }

    [Fact]
    public void SharedValue_ComparesListsByTheirItems()
    {
        var editor = Editor();
        var huts = new[] { Place(editor, BuiltInMapObjects.WitchHut, 5, 20), Place(editor, BuiltInMapObjects.WitchHut, 10, 20) }
            .Select(id => (WitchHut)editor.Scenario.FindObject(id)!).ToList();
        Assert.Equal(EditResult.Ok, ObjectProperties.WitchHutSkills.Apply(editor, huts, [BuiltInSkills.Archery, BuiltInSkills.Logistics]));

        var shared = ObjectProperties.WitchHutSkills.Shared(huts);
        Assert.False(shared.Differs);
        Assert.Equal([BuiltInSkills.Archery, BuiltInSkills.Logistics], shared.Value);
    }

    [Fact]
    public void WhenOneObjectRefuses_NoneIsChanged_AndNoStepIsRecorded()
    {
        var editor = Editor();
        var (first, second) = (Place(editor, BuiltInMapObjects.MineId(ResourceType.Gold), 5, 20), Place(editor, BuiltInMapObjects.MineId(ResourceType.Ore), 12, 20));
        var steps = editor.History.Steps.Count;

        var result = editor.EditAll("Owner", [first, second], o => o.Id == first ? editor.Objects.SetMineOwner(o.Id, new PlayerId(1)) : EditResult.InvalidValue);

        Assert.Equal(EditResult.InvalidValue, result);
        Assert.Null(((Mine)editor.Scenario.FindObject(first)!).Owner);
        Assert.Equal(steps, editor.History.Steps.Count);
    }

    [Fact]
    public void WhenAnEditThrows_NoneIsChanged_AndNoStepIsRecorded()
    {
        var editor = Editor();
        var (first, second) = (Place(editor, BuiltInMapObjects.MineId(ResourceType.Gold), 5, 20), Place(editor, BuiltInMapObjects.MineId(ResourceType.Ore), 12, 20));
        var steps = editor.History.Steps.Count;

        Assert.Throws<InvalidOperationException>(() => editor.EditAll("Owner", [first, second],
            o => o.Id == first ? editor.Objects.SetMineOwner(o.Id, new PlayerId(1)) : throw new InvalidOperationException()));

        Assert.Null(((Mine)editor.Scenario.FindObject(first)!).Owner);
        Assert.Equal(steps, editor.History.Steps.Count);
    }

    [Fact]
    public void RetypingSelectedTowns_KeepsThemSelected_AlsoWhenRefused()
    {
        var editor = Editor();
        var towns = new[] { Place(editor, BuiltInMapObjects.TownId(TownIds.Haven), 5, 5), Place(editor, BuiltInMapObjects.TownId(TownIds.Haven), 20, 5) };
        editor.Selecting.Set(new SelectionItems([], towns, [], []));

        Assert.Equal(EditResult.Ok, ObjectProperties.TownFaction.Apply(editor, [.. towns.Select(id => (Town)editor.Scenario.FindObject(id)!)], TownIds.Inferno));
        Assert.Equal(towns.Select(i => i.Value).Order(), editor.Selection.Objects.Select(i => i.Value).Order());

        var result = editor.EditAll("Faction", towns, o => o.Id == towns[0] ? editor.Towns.SetType(o.Id, TownIds.Haven) : EditResult.RequirementMissing);
        Assert.Equal(EditResult.RequirementMissing, result);
        Assert.All(towns, id => Assert.Equal(TownIds.Inferno, ((Town)editor.Scenario.FindObject(id)!).TownType));
        Assert.Equal(towns.Select(i => i.Value).Order(), editor.Selection.Objects.Select(i => i.Value).Order());
    }

    [Fact]
    public void AMineGuardCreature_KeepsEachMinesCount_AndACount_LeavesUnguardedMinesAlone()
    {
        var editor = Editor();
        var ids = new[] { Place(editor, BuiltInMapObjects.MineId(ResourceType.Gold), 5, 20), Place(editor, BuiltInMapObjects.MineId(ResourceType.Ore), 12, 20), Place(editor, BuiltInMapObjects.MineId(ResourceType.Crystal), 19, 20) };
        editor.Objects.SetMineGuard(ids[0], new GuardStack(BuiltInCreatures.Wolf, 5));
        editor.Objects.SetMineGuard(ids[1], new GuardStack(BuiltInCreatures.Pikeman, 12));
        List<Mine> Mines() => [.. ids.Select(id => (Mine)editor.Scenario.FindObject(id)!)];

        Assert.Equal(EditResult.Ok, ObjectProperties.MineGuardCreature.Apply(editor, Mines()[..2], new CreatureId(BuiltInCreatures.Wolf)));
        Assert.Equal([new GuardStack(BuiltInCreatures.Wolf, 5), new GuardStack(BuiltInCreatures.Wolf, 12), null], Mines().Select(m => m.Guard));

        Assert.Equal(EditResult.Ok, ObjectProperties.MineGuardCount.Apply(editor, Mines(), 7));
        Assert.Equal([new GuardStack(BuiltInCreatures.Wolf, 7), new GuardStack(BuiltInCreatures.Wolf, 7), null], Mines().Select(m => m.Guard));

        Assert.Equal(EditResult.Ok, ObjectProperties.MineGuardCreature.Apply(editor, Mines(), null));
        Assert.All(Mines(), m => Assert.Null(m.Guard));
    }

    [Fact]
    public void AnObjectNotOnTheMap_RefusesTheWholeEdit()
    {
        var editor = Editor();
        var mine = Place(editor, BuiltInMapObjects.MineId(ResourceType.Gold), 5, 20);

        Assert.Equal(EditResult.InvalidTarget, editor.EditAll("Owner", [mine, new MapObjectId(999)], o => editor.Objects.SetMineOwner(o.Id, new PlayerId(1))));
        Assert.Null(((Mine)editor.Scenario.FindObject(mine)!).Owner);
        Assert.Equal(EditResult.InvalidTarget, editor.EditAll("Owner", [], _ => EditResult.Ok));
    }

    [Fact]
    public void AMultiEdit_InsideAStroke_JoinsIt()
    {
        var editor = Editor();
        var mines = new[] { Place(editor, BuiltInMapObjects.MineId(ResourceType.Gold), 5, 20), Place(editor, BuiltInMapObjects.MineId(ResourceType.Ore), 12, 20) };
        var steps = editor.History.Steps.Count;

        editor.BeginStroke("Both");
        Assert.Equal(EditResult.Ok, editor.EditAll("Owner", mines, o => editor.Objects.SetMineOwner(o.Id, new PlayerId(1))));
        Assert.Equal(EditResult.Ok, editor.EditAll("Guard", mines, o => editor.Objects.SetMineGuard(o.Id, new GuardStack(BuiltInCreatures.Wolf, 5))));
        editor.EndStroke();

        Assert.Equal(steps + 1, editor.History.Steps.Count);
        editor.Undo();
        Assert.All(mines, id => Assert.Null(((Mine)editor.Scenario.FindObject(id)!).Owner));
        Assert.All(mines, id => Assert.Null(((Mine)editor.Scenario.FindObject(id)!).Guard));
    }

    [Fact]
    public void ObjectsOfOneKind_AreObjectsOfOneType()
    {
        var editor = Editor();
        var (gold, ore) = (Place(editor, BuiltInMapObjects.MineId(ResourceType.Gold), 5, 20), Place(editor, BuiltInMapObjects.MineId(ResourceType.Ore), 12, 20));
        var wolf = Monster(editor, 20, BuiltInCreatures.Wolf, 4);

        Assert.Equal([gold, ore], editor.ObjectsOfOneKind([gold, ore])!.Select(o => o.Id));
        Assert.Null(editor.ObjectsOfOneKind([gold, wolf]));
        Assert.Null(editor.ObjectsOfOneKind([gold, new MapObjectId(999)]));
        Assert.Null(editor.ObjectsOfOneKind([]));
    }

    [Fact]
    public void EveryOwnerField_KeepsTheObjectsOtherValues()
    {
        var editor = Editor();
        var dwellings = editor.Catalog.All.Where(d => d.Kind == MapObjectKind.Dwelling).Take(1)
            .SelectMany(d => new[] { Place(editor, d.Id, 5, 20), Place(editor, d.Id, 12, 20) }).ToList();
        Assert.Equal(EditResult.Ok, editor.EditAll("Waiting", [dwellings[0]], o => ObjectProperties.DwellingAvailable.Apply(editor, [(CreatureDwelling)o], 7)));
        var objects = dwellings.Select(id => (CreatureDwelling)editor.Scenario.FindObject(id)!).ToList();

        Assert.Equal(EditResult.Ok, ObjectProperties.DwellingOwner.Apply(editor, objects, new PlayerId(1)));

        Assert.All(objects, d => Assert.Equal(new PlayerId(1), d.Owner));
        Assert.Equal([7, null], objects.Select(d => d.Available));
    }
}
