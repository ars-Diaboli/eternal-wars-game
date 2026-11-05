using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The map's bans (ADR-126): heroes, artifacts, spells and skills that no pool or roll of the game offers. One list per kind with a filter; ticking
/// a box bans it. Every change goes through <see cref="MapEditorSession.SetScenarioRules"/>, so it is undoable; the validator points out what the
/// author placed by hand that a ban names.
/// </summary>
public partial class BansPanel : VBoxContainer
{
    private readonly OptionButton _kind = new();
    private readonly LineEdit _filter = new() { PlaceholderText = "Filter by name", ClearButtonEnabled = true };
    private readonly CheckBox _bannedOnly = new() { Text = "Banned only" };
    private readonly VBoxContainer _list = new();
    private readonly Label _summary = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Dictionary<string, CheckBox> _boxes = new(StringComparer.Ordinal);
    private MapEditorSession _editor = null!;

    private MapBanKind Kind => (MapBanKind)_kind.GetSelectedId();

    public void Bind(MapEditorSession editor)
    {
        _editor = editor;
        AddChild(new Label
        {
            Text = "Banned from every pool and roll: taverns, prisons, placeholders, artifact rolls, guilds, shrines, level-ups, witch huts.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var row = new HBoxContainer();
        foreach (var kind in Enum.GetValues<MapBanKind>())
        {
            _kind.AddItem(kind == MapBanKind.Hero ? "Heroes" : $"{kind}s", (int)kind);
        }

        _kind.ItemSelected += _ => Rebuild();
        row.AddChild(_kind);
        _filter.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _filter.TextChanged += _ => Rebuild();
        row.AddChild(_filter);
        _bannedOnly.Toggled += _ => Rebuild();
        row.AddChild(_bannedOnly);
        AddChild(row);
        var clear = new Button { Text = "Allow every one of this kind" };
        clear.Pressed += () => Change(bans => bans.Of(Kind).Aggregate(bans, (b, id) => b.With(Kind, id, banned: false)));
        AddChild(clear);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 360) };
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_list);
        AddChild(scroll);
        AddChild(_summary);
        Rebuild();
        this.SubscribeWhileInTree<ScenarioRulesChanged>(editor.Events, _ => Sync());
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => Sync());
    }

    /// <summary>Every id of the selected kind with its label.</summary>
    private IEnumerable<(string Id, string Label)> Entries()
    {
        var scenario = _editor.Scenario;
        return Kind switch
        {
            MapBanKind.Hero => scenario.HeroRoster.All.Select(h => (h.Id, $"{h.Name} ({h.Town}, {h.Class})")),
            MapBanKind.Artifact => scenario.Artifacts.All.Select(a => (a.Id, $"{a.Name} ({a.Slot}, {a.Tier})")),
            MapBanKind.Spell => scenario.Spells.All.Select(s => (s.Id, $"{s.Name} (level {s.Level}, {s.School})")),
            _ => BuiltInSkills.All.Select(s => (s.Id, $"{s.Name} ({s.Group})")),
        };
    }

    /// <summary>The list for the kind, filter and "banned only" box, sorted by label.</summary>
    private void Rebuild()
    {
        foreach (var child in _list.GetChildren())
        {
            child.QueueFree();
        }

        _boxes.Clear();
        var bans = _editor.Scenario.Rules.Bans;
        var kind = Kind;
        var filter = _filter.Text.Trim();
        foreach (var (id, label) in Entries().OrderBy(e => e.Label, StringComparer.OrdinalIgnoreCase))
        {
            var banned = bans.Of(kind).Contains(id, StringComparer.Ordinal);
            if ((_bannedOnly.ButtonPressed && !banned) || (filter.Length > 0 && !label.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var box = new CheckBox { Text = label, ButtonPressed = banned, TooltipText = id };
            box.Toggled += on => Change(b => b.With(kind, id, on));
            _list.AddChild(box);
            _boxes[id] = box;
        }

        ShowSummary();
    }

    /// <summary>After a change or an undo: the boxes follow the bans (the list itself only changes when it shows banned ones only).</summary>
    private void Sync()
    {
        if (_bannedOnly.ButtonPressed)
        {
            Rebuild();
            return;
        }

        var ids = _editor.Scenario.Rules.Bans.Of(Kind);
        foreach (var (id, box) in _boxes)
        {
            box.SetPressedNoSignal(ids.Contains(id, StringComparer.Ordinal));
        }

        ShowSummary();
    }

    private void ShowSummary()
    {
        var bans = _editor.Scenario.Rules.Bans;
        _summary.Text = bans.IsEmpty
            ? "Nothing is banned."
            : $"Banned: {bans.Heroes.Count} heroes, {bans.Artifacts.Count} artifacts, {bans.Spells.Count} spells, {bans.Skills.Count} skills. " +
              "What is placed by hand stays; a pool the bans would empty is left whole.";
    }

    private void Change(Func<MapBans, MapBans> change)
    {
        var rules = _editor.Scenario.Rules;
        _editor.SetScenarioRules(rules with { Bans = change(rules.Bans) });
    }
}
