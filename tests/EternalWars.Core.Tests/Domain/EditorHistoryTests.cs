using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Events;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The history's steps and jumps in it (EDIT-1, ADR-250).</summary>
public class EditorHistoryTests
{
    private static readonly TerrainType[] Paints = [TerrainType.Water, TerrainType.Sand, TerrainType.Snow, TerrainType.Dirt];

    [Fact]
    public void Steps_ListEveryStepOldestFirstWithThePosition()
    {
        var editor = Painted();

        Assert.Equal(["Edit", "Paint Water", "Paint Sand", "Paint Snow", "Paint Dirt"], [.. editor.History.Steps.Select(Short)]);
        Assert.Equal(5, editor.History.Position);

        editor.Undo();
        editor.Undo();

        Assert.Equal(5, editor.History.Count); // undone steps stay listed
        Assert.Equal(3, editor.History.Position);
        Assert.Equal(editor.History.Steps[2], editor.History.UndoDescription);
        Assert.Equal(editor.History.Steps[3], editor.History.RedoDescription);
    }

    [Fact]
    public void Record_DropsTheStepsAfterThePosition()
    {
        var editor = Painted();
        editor.UndoTo(2);

        editor.Terrain.Paint(new MapPosition(1, 1), TerrainType.Snow);

        Assert.Equal(3, editor.History.Count);
        Assert.Equal(3, editor.History.Position);
        Assert.False(editor.History.CanRedo);
    }

    [Fact]
    public void Record_OverTheCapacityDropsTheOldestStep()
    {
        var history = new UndoHistory(capacity: 2);
        foreach (var name in new[] { "a", "b", "c" })
        {
            history.Record(new NamedStep(name));
        }

        Assert.Equal(["b", "c"], history.Steps);
        Assert.Equal(2, history.Position);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    public void UndoTo_BackAndForth_EqualsSingleUndosAndRedos(int step)
    {
        var jumping = Painted();
        var stepping = Painted();
        var count = stepping.History.Count;

        Assert.True(jumping.UndoTo(step));
        for (var i = count; i > step; i--)
        {
            Assert.True(stepping.Undo());
        }

        Assert.Equal(Snapshot(stepping), Snapshot(jumping));
        Assert.Equal(step, jumping.History.Position);
        Assert.Equal(stepping.History.Steps, jumping.History.Steps);

        Assert.True(jumping.UndoTo(count));
        for (var i = step; i < count; i++)
        {
            Assert.True(stepping.Redo());
        }

        Assert.Equal(Snapshot(stepping), Snapshot(jumping));
        Assert.Equal(Snapshot(Painted()), Snapshot(jumping));
        Assert.Equal(count, jumping.History.Position);
    }

    [Fact]
    public void UndoTo_PublishesOneHistoryChange_AndNoneWhenAlreadyThere()
    {
        var editor = Painted();
        var changes = new List<UndoHistoryChanged>();
        editor.Events.Subscribe<UndoHistoryChanged>(changes.Add);

        Assert.False(editor.UndoTo(5));
        Assert.Empty(changes);

        Assert.True(editor.UndoTo(1));

        var change = Assert.Single(changes);
        Assert.Equal(editor.History.Steps[0], change.UndoDescription);
        Assert.Equal(editor.History.Steps[1], change.RedoDescription);
        Assert.Equal(1, change.Position);
        Assert.Equal(5, change.Count);
    }

    [Fact]
    public void HistoryChanged_FollowsEveryRecordUndoAndRedo()
    {
        var editor = TestEditor.Blank();
        var positions = new List<(int Position, int Count)>();
        editor.Events.Subscribe<UndoHistoryChanged>(e => positions.Add((e.Position, e.Count)));

        editor.Terrain.Paint(new MapPosition(2, 2), TerrainType.Water);
        editor.Terrain.Paint(new MapPosition(3, 2), TerrainType.Sand);
        editor.Undo();
        editor.Redo();
        editor.UndoTo(0);

        Assert.Equal([(1, 1), (2, 2), (1, 2), (2, 2), (0, 2)], positions);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void UndoTo_OutsideTheHistory_Throws(int step) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Painted().UndoTo(step));

    [Fact]
    public void UndoTo_InsideAStroke_Throws()
    {
        var editor = Painted();
        editor.BeginStroke("Paint");

        Assert.Throws<InvalidOperationException>(() => editor.UndoTo(0));
    }

    /// <summary>An editor with five steps: the map's name, then four paints over one another.</summary>
    private static MapEditorSession Painted()
    {
        var editor = TestEditor.Blank();
        editor.Map.SetProperties("Jumps", "");
        for (var i = 0; i < Paints.Length; i++)
        {
            editor.Terrain.Paint(new MapPosition(4 + i, 4), Paints[i], radius: 1);
        }

        return editor;
    }

    /// <summary>The paints as "Paint Water"; any other step as its first word.</summary>
    private static string Short(string description) =>
        description.StartsWith("Paint ", StringComparison.Ordinal) ? description : description.Split(' ')[0];

    private static string Snapshot(MapEditorSession editor) =>
        MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario)) + "\n---\n" + GameStateDigest.Describe(editor.Scenario);

    private sealed class NamedStep(string description) : IEditorCommand
    {
        public string Description { get; } = description;

        public void Execute()
        {
        }

        public void Undo()
        {
        }
    }
}
