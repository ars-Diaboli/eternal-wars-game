using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Turns;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// How the scenario is won and lost (ADR-057): the default rules (the last team standing wins, no towns and no heroes
/// means defeat) can be switched off, and special victory and defeat conditions added or removed. Every change goes
/// through <see cref="MapEditorSession.SetScenarioRules"/>, so it is undoable. The hero limit per player (ADR-068) and the week and month events
/// (on or off, weeks without events first, the factions whose creatures they pick; ADR-073, ADR-075) are rules too.
/// </summary>
public partial class ConditionsPanel : VBoxContainer
{
    private static readonly (ConditionKind Kind, string Label)[] Kinds =
    [
        (ConditionKind.CaptureTown, "Victory: capture a town"),
        (ConditionKind.DefeatHero, "Victory: defeat a hero"),
        (ConditionKind.GatherResources, "Victory: gather resources"),
        (ConditionKind.SurviveUntilDay, "Victory: survive until a day"),
        (ConditionKind.LoseTown, "Defeat: lose a town"),
        (ConditionKind.LoseHero, "Defeat: lose a hero"),
        (ConditionKind.TimeLimit, "Defeat: time limit"),
    ];

    private readonly CheckBox _standard = new() { Text = "Last team standing wins (defeat every enemy)" };
    private readonly CheckBox _elimination = new() { Text = "No towns and no heroes means defeat" };
    private readonly SpinBox _grace = new() { MinValue = 0, MaxValue = 99, Step = 1, Suffix = "days without a town (0 = no countdown)" };
    private readonly SpinBox _maxHeroes = new() { MinValue = 1, MaxValue = ScenarioRules.HeroLimit, Step = 1, Prefix = "at most", Suffix = "heroes per player" };
    private readonly CheckBox _weekEvents = new() { Text = "Week and month events" };
    private readonly SpinBox _skipWeeks = new() { MinValue = 0, MaxValue = WeekEventSettings.MaxSkipWeeks, Step = 1, Suffix = "weeks before the first event" };
    private readonly HFlowContainer _weeklyFactions = new();
    private readonly HFlowContainer _monthlyFactions = new();
    private readonly VBoxContainer _list = new();
    private readonly OptionButton _kind = new();
    private readonly OptionButton _player = new();
    private readonly OptionButton _town = new();
    private readonly OptionButton _hero = new();
    private readonly OptionButton _resource = new();
    private readonly SpinBox _amount = new() { MinValue = 1, MaxValue = 1_000_000, Step = 1, Value = 1000, Prefix = "x " };
    private readonly SpinBox _day = new() { MinValue = 1, MaxValue = 1000, Step = 1, Value = 30, Prefix = "day " };
    private readonly Label _status = new();
    private MapEditorSession _editor = null!;

    private GameState Scenario => _editor.Scenario;

    public void Bind(MapEditorSession editor)
    {
        _editor = editor;
        AddChild(new Label { Text = "Victory and defeat" });
        _standard.Toggled += on => Change(r => r with { StandardVictory = on });
        AddChild(_standard);
        _elimination.Toggled += on => Change(r => r with { Elimination = on });
        AddChild(_elimination);
        _grace.ValueChanged += v => Change(r => r with { GraceDays = (int)v });
        AddChild(_grace);
        _maxHeroes.ValueChanged += v => Change(r => r with { MaxHeroes = (int)v });
        AddChild(_maxHeroes);
        _weekEvents.Toggled += on => Change(r => r with { WeekEvents = r.WeekEvents with { Enabled = on } });
        AddChild(_weekEvents);
        _skipWeeks.ValueChanged += v => Change(r => r with { WeekEvents = r.WeekEvents with { SkipWeeks = (int)v } });
        AddChild(_skipWeeks);
        AddChild(new Label { Text = "Weekly events pick creatures of:" });
        AddChild(_weeklyFactions);
        AddChild(new Label { Text = "Monthly events pick creatures of:" });
        AddChild(_monthlyFactions);
        foreach (var faction in WeekEventSettings.Factions)
        {
            _weeklyFactions.AddChild(FactionBox(faction, monthly: false));
            _monthlyFactions.AddChild(FactionBox(faction, monthly: true));
        }

        AddChild(_list);

        AddChild(new HSeparator());
        foreach (var (kind, label) in Kinds)
        {
            _kind.AddItem(label, (int)kind);
        }

        _kind.ItemSelected += _ => ShowFields();
        AddChild(_kind);
        AddChild(Row("Player", _player));
        AddChild(Row("Town", _town));
        AddChild(Row("Hero", _hero));
        foreach (var resource in Enum.GetValues<ResourceType>())
        {
            _resource.AddItem(resource.ToString(), (int)resource);
        }

        AddChild(Row("Resource", _resource));
        AddChild(_amount);
        AddChild(_day);
        var add = new Button { Text = "Add condition" };
        add.Pressed += Add;
        AddChild(add);
        AddChild(_status);

        Refresh();
        ShowFields();
        this.SubscribeWhileInTree<ScenarioRulesChanged>(editor.Events, _ => Refresh());
        this.SubscribeWhileInTree<PlayerSlotsChanged>(editor.Events, _ => Refresh());
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => Refresh());
    }

    /// <summary>A faction's tick box; ticking or unticking stores the whole list, in the order of <see cref="WeekEventSettings.Factions"/>.</summary>
    private CheckBox FactionBox(string faction, bool monthly)
    {
        var box = new CheckBox { Text = faction };
        box.SetMeta("faction", faction);
        box.Toggled += on => Change(r =>
        {
            var current = monthly ? r.WeekEvents.Monthly : r.WeekEvents.Weekly;
            IReadOnlyList<string>? next = WeekEventSettings.Factions.Where(f => f == faction ? on : current.Contains(f)).ToList();
            if (next.Order(StringComparer.Ordinal).SequenceEqual(Core.Domain.Objects.BuiltInMapObjects.PlayableTowns.Order(StringComparer.Ordinal)))
            {
                next = null; // the default stays out of the map file
            }
            return r with { WeekEvents = monthly ? r.WeekEvents with { MonthlyFactions = next } : r.WeekEvents with { WeeklyFactions = next } };
        });
        return box;
    }

    private static HBoxContainer Row(string label, Control control)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(70, 0) });
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(control);
        return row;
    }

    private ConditionKind SelectedKind => (ConditionKind)_kind.GetItemId(_kind.Selected);

    /// <summary>Shows only the fields the selected kind uses.</summary>
    private void ShowFields()
    {
        var kind = SelectedKind;
        var town = kind is ConditionKind.CaptureTown or ConditionKind.LoseTown;
        var hero = kind is ConditionKind.DefeatHero or ConditionKind.LoseHero;
        _town.GetParent<Control>().Visible = town;
        _hero.GetParent<Control>().Visible = hero;
        _resource.GetParent<Control>().Visible = kind == ConditionKind.GatherResources;
        _amount.Visible = kind == ConditionKind.GatherResources;
        _day.Visible = kind is ConditionKind.SurviveUntilDay or ConditionKind.TimeLimit;
    }

    private void Refresh()
    {
        var rules = Scenario.Rules;
        _standard.SetPressedNoSignal(rules.StandardVictory);
        _elimination.SetPressedNoSignal(rules.Elimination);
        _grace.SetValueNoSignal(rules.GraceDays);
        _grace.Editable = rules.Elimination;
        _maxHeroes.SetValueNoSignal(rules.MaxHeroes);
        _weekEvents.SetPressedNoSignal(rules.WeekEvents.Enabled);
        _skipWeeks.SetValueNoSignal(rules.WeekEvents.SkipWeeks);
        _skipWeeks.Editable = rules.WeekEvents.Enabled;
        foreach (var (box, chosen) in new[] { (_weeklyFactions, rules.WeekEvents.Weekly), (_monthlyFactions, rules.WeekEvents.Monthly) })
        {
            foreach (var check in box.GetChildren().OfType<CheckBox>())
            {
                check.SetPressedNoSignal(chosen.Contains((string)check.GetMeta("faction")));
                check.Disabled = !rules.WeekEvents.Enabled;
            }
        }

        foreach (var child in _list.GetChildren())
        {
            child.QueueFree();
        }

        AddRows("Victory", rules.Victory, (r, c) => r with { Victory = r.Victory.Where(x => x != c).ToList() });
        AddRows("Defeat", rules.Defeat, (r, c) => r with { Defeat = r.Defeat.Where(x => x != c).ToList() });
        if (!rules.CanBeWon)
        {
            _list.AddChild(new Label { Text = "Nobody can win this scenario." });
        }

        Fill(_player, [(-1, "Any player"), .. Scenario.Players.Select(p => (p.Id.Value, p.Name))]);
        Fill(_town, Scenario.ObjectsOf<Town>().Select(t => (t.Id.Value, t.Name)));
        Fill(_hero, Scenario.Heroes.Select(h => (h.Id.Value, h.Name)));
    }

    private void AddRows(string heading, IReadOnlyList<ScenarioCondition> conditions, Func<ScenarioRules, ScenarioCondition, ScenarioRules> remove)
    {
        _list.AddChild(new Label { Text = conditions.Count == 0 ? $"{heading}: none" : $"{heading}:" });
        foreach (var condition in conditions)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = ScenarioRules.Describe(Scenario, condition), SizeFlagsHorizontal = SizeFlags.ExpandFill });
            var delete = new Button { Text = "Remove" };
            var captured = condition;
            delete.Pressed += () => Change(r => remove(r, captured));
            row.AddChild(delete);
            _list.AddChild(row);
        }
    }

    /// <summary>Keeps the selection by id when the list is rebuilt.</summary>
    private static void Fill(OptionButton button, IEnumerable<(int Id, string Name)> items)
    {
        var selected = button.Selected >= 0 ? button.GetItemId(button.Selected) : int.MinValue;
        button.Clear();
        foreach (var (id, name) in items)
        {
            button.AddItem(name, id);
        }

        var index = button.GetItemIndex(selected);
        button.Selected = index >= 0 ? index : button.ItemCount > 0 ? 0 : -1;
    }

    private void Add()
    {
        var kind = SelectedKind;
        PlayerId? player = _player.Selected >= 0 && _player.GetItemId(_player.Selected) >= 0 ? new PlayerId(_player.GetItemId(_player.Selected)) : null;
        MapObjectId? town = _town.Selected >= 0 ? new MapObjectId(_town.GetItemId(_town.Selected)) : null;
        HeroId? hero = _hero.Selected >= 0 ? new HeroId(_hero.GetItemId(_hero.Selected)) : null;
        var resource = (ResourceType)_resource.GetItemId(Math.Max(0, _resource.Selected));

        ScenarioCondition? condition = kind switch
        {
            ConditionKind.CaptureTown when town is { } t => ScenarioCondition.CaptureTown(t, player),
            ConditionKind.DefeatHero when hero is { } h => ScenarioCondition.DefeatHero(h, player),
            ConditionKind.GatherResources => ScenarioCondition.GatherResources(resource, (int)_amount.Value, player),
            ConditionKind.SurviveUntilDay => ScenarioCondition.SurviveUntilDay((int)_day.Value, player),
            ConditionKind.LoseTown when town is { } t && (player ?? (Scenario.FindObject(t) as Town)?.Owner) is { } loser => ScenarioCondition.LoseTown(t, loser),
            ConditionKind.LoseHero when hero is { } h && (player ?? Scenario.Heroes.FirstOrDefault(x => x.Id == h)?.Owner) is { } loser => ScenarioCondition.LoseHero(h, loser),
            ConditionKind.TimeLimit => ScenarioCondition.TimeLimit((int)_day.Value, player),
            _ => null,
        };
        if (condition is null)
        {
            _status.Text = "Pick what the condition is about (and a player who loses, for a town or hero without an owner).";
            return;
        }

        _status.Text = string.Empty;
        Change(r => condition.IsVictory ? r with { Victory = [.. r.Victory, condition] } : r with { Defeat = [.. r.Defeat, condition] });
    }

    private void Change(Func<ScenarioRules, ScenarioRules> change)
    {
        if (_editor.SetScenarioRules(change(Scenario.Rules)) is var result and not EditResult.Ok)
        {
            _status.Text = result == EditResult.HeroLimit ? "A player already has more heroes than that." : result.ToString();
            Refresh();
        }
    }
}
