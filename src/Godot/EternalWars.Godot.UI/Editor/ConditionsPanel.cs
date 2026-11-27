using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.UI.Editor.Inspector;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The map settings window's Victory and loss tab (ADR-057): the default rules (the last team standing wins, no towns and no heroes means
/// defeat) can be switched off, and special victory and defeat conditions added or removed. Every change goes through the session's rules
/// edit, so it is undoable. On an arena or a tournament map (ADR-097, ADR-366, the General tab's switches) the conditions stay stored but count
/// for nothing, so their controls are off.
/// </summary>
public partial class ConditionsPanel : VBoxContainer
{
    private static readonly (ConditionKind Kind, string Label)[] Kinds =
    [
        (ConditionKind.CaptureTown, "Victory: capture a town"),
        (ConditionKind.DefeatHero, "Victory: defeat a hero"),
        (ConditionKind.GatherResources, "Victory: gather resources"),
        (ConditionKind.SurviveUntilDay, "Victory: survive until a day"),
        (ConditionKind.CaptureObject, "Victory: capture a map object"),
        (ConditionKind.ObtainArtifact, "Victory: obtain an artifact"),
        (ConditionKind.ReleaseHero, "Victory: release a prisoner"),
        (ConditionKind.LoseTown, "Defeat: lose a town"),
        (ConditionKind.LoseHero, "Defeat: lose a hero"),
        (ConditionKind.LoseArtifact, "Defeat: lose an artifact"),
        (ConditionKind.TimeLimit, "Defeat: time limit"),
    ];

    /// <summary>The "Any player" item's id: not -1, which <see cref="OptionButton.AddItem"/> takes as "number it by its index" (player 0's id).</summary>
    private const int AnyPlayer = int.MaxValue;

    private readonly Label _decidingNote = new();
    private readonly Button _add = new() { Text = "Add condition" };
    private readonly CheckBox _standard = new() { Text = "Last team standing wins (defeat every enemy)" };
    private readonly CheckBox _elimination = new() { Text = "No towns and no heroes means defeat" };
    private readonly SpinBox _grace = new() { MinValue = 0, MaxValue = 99, Step = 1, Suffix = "days without a town (0 = no countdown)" };
    private readonly VBoxContainer _list = new();
    private readonly OptionButton _kind = new();
    private readonly OptionButton _player = new();
    private readonly OptionButton _town = new();
    private readonly OptionButton _resource = new();
    private readonly SpinBox _amount = new() { MinValue = 1, MaxValue = 1_000_000, Step = 1, Value = 1000, Prefix = "x " };
    private readonly SpinBox _day = new() { MinValue = 1, MaxValue = 1000, Step = 1, Value = 30, Prefix = "day " };
    private readonly OptionButton _object = new();
    private readonly OptionButton _prison = new();
    private readonly SpinBox _deadline = new() { MinValue = 0, MaxValue = 1000, Step = 1, Prefix = "by day", Suffix = "(0 = no deadline)" };
    private readonly Label _status = new();
    private PicturePicker _hero = null!;
    private PicturePicker _artifact = null!;
    private MapEditorSession _editor = null!;

    private GameState Scenario => _editor.Scenario;

    public void Bind(MapEditorSession editor)
    {
        _editor = editor;
        AddChild(_decidingNote);
        _standard.Toggled += on => Change(r => r with { StandardVictory = on });
        AddChild(_standard);
        _elimination.Toggled += on => Change(r => r with { Elimination = on });
        AddChild(_elimination);
        _grace.ValueChanged += v => Change(r => r with { GraceDays = (int)v });
        AddChild(_grace);

        AddChild(_list);

        AddChild(new HSeparator());
        foreach (var (kind, label) in Kinds)
        {
            _kind.AddItem(label, (int)kind);
        }

        _kind.ItemSelected += _ => ShowFields();
        AddChild(_kind);
        this.AddRow("Player", _player, 70, fill: true);
        this.AddRow("Town", _town, 70, fill: true);
        _hero = PicturePicker.Add(this.AddRow("Hero", labelWidth: 70), null, [], ""); // the map's heroes, filled by Refresh
        foreach (var resource in Enum.GetValues<ResourceType>())
        {
            _resource.AddItem(resource.ToString(), (int)resource);
        }

        this.AddRow("Resource", _resource, 70, fill: true);
        this.AddRow("Object", _object, 70, fill: true);
        this.AddRow("Prison", _prison, 70, fill: true);
        var artifacts = PickerItems.Artifacts(Scenario);
        _artifact = PicturePicker.Add(this.AddRow("Artifact", labelWidth: 70), null, artifacts, artifacts.FirstOrDefault()?.Key ?? "");
        AddChild(_amount);
        AddChild(_day);
        AddChild(_deadline);
        _add.Pressed += Add;
        AddChild(_add);
        AddChild(_status);

        Refresh();
        ShowFields();
        this.SubscribeWhileInTree<ScenarioRulesChanged>(editor.Events, _ => Refresh());
        this.SubscribeWhileInTree<PlayerSlotsChanged>(editor.Events, _ => Refresh());
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => Refresh());
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
        _object.GetParent<Control>().Visible = kind == ConditionKind.CaptureObject;
        _prison.GetParent<Control>().Visible = kind == ConditionKind.ReleaseHero;
        _artifact.GetParent<Control>().Visible = kind is ConditionKind.ObtainArtifact or ConditionKind.LoseArtifact;
        _deadline.Visible = new ScenarioCondition(kind).CanHaveDeadline;
    }

    private void Refresh()
    {
        var rules = Scenario.Rules;
        var deciding = DecidingBattles.Decides(rules);
        _decidingNote.Visible = deciding;
        _decidingNote.Text = $"This map is {(rules.Tournament ? "a tournament" : "an arena")} (General tab): it ignores the victory and defeat conditions below.";
        _standard.Disabled = deciding;
        _elimination.Disabled = deciding;
        _add.Disabled = deciding;
        _standard.SetPressedNoSignal(rules.StandardVictory);
        _elimination.SetPressedNoSignal(rules.Elimination);
        _grace.SetValueNoSignal(rules.GraceDays);
        _grace.Editable = rules.Elimination && !deciding;
        _list.ClearChildren();

        AddRows("Victory", rules.Victory, (r, c) => r with { Victory = r.Victory.Where(x => x != c).ToList() });
        AddRows("Defeat", rules.Defeat, (r, c) => r with { Defeat = r.Defeat.Where(x => x != c).ToList() });
        if (!rules.CanBeWon)
        {
            _list.AddChild(new Label { Text = "Nobody can win this scenario." });
        }

        Fill(_player, [(AnyPlayer, "Any player"), .. Scenario.Players.Select(p => (p.Id.Value, p.Name))]);
        Fill(_town, Scenario.ObjectsOf<Town>().Select(t => (t.Id.Value, t.Name)));
        _hero.SetItems([.. Scenario.Heroes.Select(h => PickerItem.Of(
            h.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), h.Name,
            Scenario.HasPlayer(h.Owner) ? Scenario.GetPlayer(h.Owner).Name : ""))], "No heroes on this map");
        // Towns have their own kind; the other objects with a flag: mines, dwellings, providers, garrisons (ADR-073).
        Fill(_object, Scenario.Objects.Where(o => o is IFlaggable and not Town)
            .Select(o => (o.Id.Value, $"{(o.Name is { } name ? $"{name}, " : "")}{o.Definition.Name} ({o.Position.X}, {o.Position.Y})")));
        Fill(_prison, Scenario.ObjectsOf<Prison>()
            .Select(p => (p.Id.Value, $"{(p.Hero is { } h && Scenario.HeroRoster.Contains(h) ? Scenario.HeroRoster.Get(h).Name : "Drawn prisoner")} ({p.Position.X}, {p.Position.Y})")));
    }

    private void AddRows(string heading, IReadOnlyList<ScenarioCondition> conditions, Func<ScenarioRules, ScenarioCondition, ScenarioRules> remove)
    {
        _list.AddChild(new Label { Text = conditions.Count == 0 ? $"{heading}: none" : $"{heading}:" });
        foreach (var condition in conditions)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = ScenarioRules.Describe(Scenario, condition), SizeFlagsHorizontal = SizeFlags.ExpandFill });
            var delete = new Button { Text = "Remove" }.WithIcon(UiIcon.Minus);
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
        PlayerId? player = _player.Selected >= 0 && _player.GetItemId(_player.Selected) != AnyPlayer ? new PlayerId(_player.GetItemId(_player.Selected)) : null;
        MapObjectId? town = _town.Selected >= 0 ? new MapObjectId(_town.GetItemId(_town.Selected)) : null;
        HeroId? hero = int.TryParse(_hero.Value(), System.Globalization.CultureInfo.InvariantCulture, out var heroId) ? new HeroId(heroId) : null;
        var resource = (ResourceType)_resource.GetItemId(Math.Max(0, _resource.Selected));
        MapObjectId? target = _object.Selected >= 0 ? new MapObjectId(_object.GetItemId(_object.Selected)) : null;
        MapObjectId? prison = _prison.Selected >= 0 ? new MapObjectId(_prison.GetItemId(_prison.Selected)) : null;
        var artifact = _artifact.Value() is { Length: > 0 } picked ? picked : null;
        PlayerId? carrier = artifact is null ? null : Scenario.Heroes.FirstOrDefault(h => h.Artifacts.Has(artifact))?.Owner;

        ScenarioCondition? condition = kind switch
        {
            ConditionKind.CaptureTown when town is { } t => ScenarioCondition.CaptureTown(t, player),
            ConditionKind.DefeatHero when hero is { } h => ScenarioCondition.DefeatHero(h, player),
            ConditionKind.GatherResources => ScenarioCondition.GatherResources(resource, (int)_amount.Value, player),
            ConditionKind.SurviveUntilDay => ScenarioCondition.SurviveUntilDay((int)_day.Value, player),
            ConditionKind.LoseTown when town is { } t && (player ?? (Scenario.FindObject(t) as Town)?.Owner) is { } loser => ScenarioCondition.LoseTown(t, loser),
            ConditionKind.LoseHero when hero is { } h && (player ?? Scenario.Heroes.FirstOrDefault(x => x.Id == h)?.Owner) is { } loser => ScenarioCondition.LoseHero(h, loser),
            ConditionKind.TimeLimit => ScenarioCondition.TimeLimit((int)_day.Value, player),
            ConditionKind.CaptureObject when target is { } o => ScenarioCondition.CaptureObject(o, player),
            ConditionKind.ObtainArtifact when artifact is not null => ScenarioCondition.ObtainArtifact(artifact, player),
            ConditionKind.ReleaseHero when prison is { } p => ScenarioCondition.ReleaseHero(p, player),
            ConditionKind.LoseArtifact when artifact is not null && (player ?? carrier) is { } loser => ScenarioCondition.LoseArtifact(artifact, loser),
            _ => null,
        };
        if (condition is null)
        {
            _status.Text = "Pick what the condition is about (and a player who loses, for a town, hero or artifact without an owner).";
            return;
        }

        if (condition.CanHaveDeadline && (int)_deadline.Value > 0)
        {
            condition = condition.By((int)_deadline.Value);
        }

        _status.Text = string.Empty;
        Change(r => condition.IsVictory ? r with { Victory = [.. r.Victory, condition] } : r with { Defeat = [.. r.Defeat, condition] });
    }

    private void Change(Func<ScenarioRules, ScenarioRules> change)
    {
        if (_editor.Map.SetRules(change(Scenario.Rules)) is var result and not EditResult.Ok)
        {
            _status.Text = GeneralSettingsPanel.RulesRefusal(result);
            Refresh();
        }
    }
}
