using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The map settings window's General tab (EDIT-1, ADR-256): the map's name and description, its difficulty (what a game with "Map default"
/// complexity counts as in the ranking, ADR-101), the hero limit per player (ADR-068) and the arena switch (ADR-097, ARN-2: the map becomes
/// one battle and its victory and loss conditions count for nothing). Every change is an undoable <see cref="MapEditorSession"/> edit.
/// </summary>
public partial class GeneralSettingsPanel : VBoxContainer
{
    /// <summary>The "not set" item's id: -1 would make Godot number the item itself, and the row would then show nothing selected.</summary>
    private const int DifficultyNotSet = 1000;

    private readonly LineEdit _name = new();
    private readonly TextEdit _description = new() { CustomMinimumSize = new Vector2(0, 90), WrapMode = TextEdit.LineWrappingMode.Boundary };
    private readonly OptionButton _difficulty = new() { TooltipText = "What a game with \"Map default\" complexity counts as in the ranking" };
    private readonly SpinBox _maxHeroes = new() { MinValue = 1, MaxValue = ScenarioRules.HeroLimit, Step = 1, Prefix = "at most", Suffix = "heroes per player" };

    private readonly CheckBox _arena = new()
    {
        Text = "Arena: a day to prepare, then one battle decides the game",
        TooltipText = "Day 1: unlimited movement and building. Day 2 starts with the battle of the two sides' strongest heroes at a combat place; "
                      + "its winner wins the game. The map needs a combat place.",
    };

    private readonly Label _status = new();
    private MapEditorSession _editor = null!;

    public void Bind(MapEditorSession editor)
    {
        _editor = editor;
        AddChild(new Label { Text = "Name" });
        AddChild(_name);
        AddChild(new Label { Text = "Description" });
        AddChild(_description);
        var apply = new Button { Text = "Apply name and description" };
        apply.Pressed += () => _editor.Map.SetProperties(_name.Text.Trim(), _description.Text);
        AddChild(apply);
        AddChild(new HSeparator());

        _difficulty.AddItem("Difficulty: not set (Normal)", DifficultyNotSet);
        foreach (var level in Enum.GetValues<Difficulty>())
        {
            _difficulty.AddItem($"Difficulty: {level}", (int)level);
        }

        _difficulty.ItemSelected += index => Change(r => r with
        {
            Difficulty = _difficulty.GetItemId((int)index) is var id and not DifficultyNotSet ? (Difficulty)id : null,
        });
        AddChild(_difficulty);
        _maxHeroes.ValueChanged += v => Change(r => r with { MaxHeroes = (int)v });
        AddChild(_maxHeroes);
        _arena.Toggled += on => Change(r => r with { Arena = on });
        AddChild(_arena);
        AddChild(_status);

        Refresh();
        this.SubscribeWhileInTree<ScenarioRulesChanged>(editor.Events, _ => Refresh());
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => Refresh());
    }

    private void Refresh()
    {
        var scenario = _editor.Scenario;
        if (!_name.HasFocus())
        {
            _name.Text = scenario.Map.Name;
        }

        if (!_description.HasFocus())
        {
            _description.Text = scenario.Map.Description;
        }

        var rules = scenario.Rules;
        _difficulty.Selected = _difficulty.GetItemIndex(rules.Difficulty is { } difficulty ? (int)difficulty : DifficultyNotSet);
        _maxHeroes.SetValueNoSignal(rules.MaxHeroes);
        _arena.SetPressedNoSignal(rules.Arena);
    }

    private void Change(Func<ScenarioRules, ScenarioRules> change)
    {
        var result = _editor.Map.SetRules(change(_editor.Scenario.Rules));
        _status.Text = RulesRefusal(result);
        if (result != EditResult.Ok)
        {
            Refresh();
        }
    }

    /// <summary>What the settings tabs show for a rules edit's result: empty for Ok, else why it was refused.</summary>
    internal static string RulesRefusal(EditResult result) => result switch
    {
        EditResult.Ok => "",
        EditResult.HeroLimit => "A player already has more heroes than the hero limit allows.",
        _ => result.ToString(),
    };
}
