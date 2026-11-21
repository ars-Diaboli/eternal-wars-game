using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Turns;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The map settings window's Week events tab (ADR-073, ADR-075, ADR-084): the week and month events on or off, the weeks without events at
/// the start, and the factions whose creatures the weekly and the monthly events pick. Every change is an undoable rules edit.
/// </summary>
public partial class WeekEventsPanel : VBoxContainer
{
    private readonly CheckBox _enabled = new() { Text = "Week and month events" };
    private readonly SpinBox _skipWeeks = new() { MinValue = 0, MaxValue = WeekEventSettings.MaxSkipWeeks, Step = 1, Suffix = "week(s) without events at the start" };
    private readonly HFlowContainer _weeklyFactions = new();
    private readonly HFlowContainer _monthlyFactions = new();
    private readonly Label _status = new();
    private MapEditorSession _editor = null!;

    public void Bind(MapEditorSession editor)
    {
        _editor = editor;
        _enabled.Toggled += on => Change(r => r with { WeekEvents = r.WeekEvents with { Enabled = on } });
        AddChild(_enabled);
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

        AddChild(_status);
        Refresh();
        this.SubscribeWhileInTree<ScenarioRulesChanged>(editor.Events, _ => Refresh());
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => Refresh());
    }

    /// <summary>A faction's tick box; ticking or unticking stores the whole list, in the order of <see cref="WeekEventSettings.Factions"/>.</summary>
    private CheckBox FactionBox(string faction, bool monthly)
    {
        var box = new CheckBox { Text = faction }.WithCrest(faction, UiIcons.ButtonSize);
        box.SetMeta("faction", faction);
        box.Toggled += on => Change(r =>
        {
            var current = monthly ? r.WeekEvents.Monthly : r.WeekEvents.Weekly;
            IReadOnlyList<string>? next = WeekEventSettings.Factions.Where(f => f == faction ? on : current.Contains(f)).ToList();
            if (next.Order(StringComparer.Ordinal).SequenceEqual(BuiltInMapObjects.PlayableTowns.Order(StringComparer.Ordinal)))
            {
                next = null; // the default stays out of the map file
            }

            return r with { WeekEvents = monthly ? r.WeekEvents with { MonthlyFactions = next } : r.WeekEvents with { WeeklyFactions = next } };
        });
        return box;
    }

    private void Refresh()
    {
        var events = _editor.Scenario.Rules.WeekEvents;
        _enabled.SetPressedNoSignal(events.Enabled);
        _skipWeeks.SetValueNoSignal(events.SkipWeeks);
        _skipWeeks.Editable = events.Enabled;
        foreach (var (box, chosen) in new[] { (_weeklyFactions, events.Weekly), (_monthlyFactions, events.Monthly) })
        {
            foreach (var check in box.GetChildren().OfType<CheckBox>())
            {
                check.SetPressedNoSignal(chosen.Contains((string)check.GetMeta("faction")));
                check.Disabled = !events.Enabled;
            }
        }
    }

    private void Change(Func<ScenarioRules, ScenarioRules> change)
    {
        var result = _editor.Map.SetRules(change(_editor.Scenario.Rules));
        _status.Text = GeneralSettingsPanel.RulesRefusal(result);
        if (result != EditResult.Ok)
        {
            Refresh();
        }
    }
}
