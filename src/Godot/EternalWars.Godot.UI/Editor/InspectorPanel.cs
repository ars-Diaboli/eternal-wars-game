using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// Edits the selected hero (stats, Tactics, seven army slots) or object (mine owner and guard, chest values,
/// monster creature/count/disposition; gate links are shown). Every change is an undoable editor edit.
/// </summary>
public partial class InspectorPanel : VBoxContainer
{
    private MapEditorSession _editor = null!;
    private HeroId? _hero;
    private MapObjectId? _object;
    private bool _refreshing;

    public void Bind(MapEditorSession editor)
    {
        _editor = editor;
        Refresh();
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => Refresh());
    }

    public void InspectHero(HeroId hero)
    {
        (_hero, _object) = (hero, null);
        Refresh();
    }

    public void InspectObject(MapObjectId mapObject)
    {
        (_hero, _object) = (null, mapObject);
        Refresh();
    }

    private void Refresh()
    {
        _refreshing = true;
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        var scenario = _editor.Scenario;
        if (_hero is { } heroId && scenario.Heroes.FirstOrDefault(h => h.Id == heroId) is { } hero)
        {
            ShowHero(hero);
        }
        else if (_object is { } objectId && scenario.FindObject(objectId) is { } mapObject)
        {
            ShowObject(mapObject);
        }
        else
        {
            AddChild(new Label { Text = "Use the Select tool and click a hero or object." });
        }

        _refreshing = false;
    }

    private void ShowHero(Hero hero)
    {
        AddChild(new Label { Text = $"{hero.Name} ({_editor.Scenario.GetPlayer(hero.Owner).Name}) at {hero.Position}" });
        var stats = new HBoxContainer();
        AddChild(stats);
        var attack = Spin(stats, "Attack", hero.Attack, 0, 99);
        var defense = Spin(stats, "Defense", hero.Defense, 0, 99);
        var tactics = Options(stats, "Tactics", Enum.GetValues<SkillLevel>().Select(s => (s.ToString(), s)), hero.Tactics);
        void ApplyStats() => Edit(() => _editor.SetHeroStats(hero.Id, (int)attack.Value, (int)defense.Value, tactics.Value()));
        attack.ValueChanged += _ => ApplyStats();
        defense.ValueChanged += _ => ApplyStats();
        tactics.Button.ItemSelected += _ => ApplyStats();

        AddChild(new Label { Text = "Army" });
        var creatures = new[] { ("— empty —", "") }
            .Concat(_editor.Scenario.Creatures.All.OrderBy(c => c.Town ?? "~").ThenBy(c => c.Tier).Select(c => ($"{c.Name} (T{c.Tier})", c.Id)))
            .ToList();
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            var row = new HBoxContainer();
            AddChild(row);
            var stack = hero.Army[slot];
            var creature = Options(row, $"{slot + 1}", creatures, stack?.Creature ?? "");
            var count = Spin(row, "×", stack?.Count ?? 1, 1, 9999);
            var index = slot;
            void ApplySlot() => Edit(() => _editor.SetArmySlot(
                hero.Id, index, creature.Value() is { Length: > 0 } id ? new CreatureStack(id, (int)count.Value) : null));
            creature.Button.ItemSelected += _ => ApplySlot();
            count.ValueChanged += _ => ApplySlot();
        }
    }

    private void ShowObject(MapObject mapObject)
    {
        AddChild(new Label { Text = $"{mapObject.Definition.Name} #{mapObject.Id.Value} at {mapObject.Position}" });
        var creatures = _editor.Scenario.Creatures.All.OrderBy(c => c.Town ?? "~").ThenBy(c => c.Tier).Select(c => ($"{c.Name} (T{c.Tier})", c.Id)).ToList();
        switch (mapObject)
        {
            case Mine mine:
            {
                var row = new HBoxContainer();
                AddChild(row);
                var owners = new[] { ("Unowned", (PlayerId?)null) }.Concat(_editor.Scenario.Players.Select(p => (p.Name, (PlayerId?)p.Id)));
                var owner = Options(row, "Owner", owners, mine.Owner);
                owner.Button.ItemSelected += _ => Edit(() => _editor.SetMineOwner(mine.Id, owner.Value()));

                var guardRow = new HBoxContainer();
                AddChild(guardRow);
                var guardCreature = Options(guardRow, "Guard", new[] { ("None", "") }.Concat(creatures), mine.Guard?.Creature ?? "");
                var guardCount = Spin(guardRow, "Count (0 = rolled)", mine.Guard?.Count ?? 0, 0, 999);
                void ApplyGuard() => Edit(() => _editor.SetMineGuard(mine.Id, guardCreature.Value() is { Length: > 0 } id
                    ? new GuardStack(id, guardCount.Value > 0 ? (int)guardCount.Value : null)
                    : null));
                guardCreature.Button.ItemSelected += _ => ApplyGuard();
                guardCount.ValueChanged += _ => ApplyGuard();
                break;
            }

            case TreasureChest chest:
            {
                var row = new HBoxContainer();
                AddChild(row);
                var fixedValues = new CheckBox { Text = "Fixed values (else rolled)", ButtonPressed = chest.FixedValues is not null };
                row.AddChild(fixedValues);
                var gold = Spin(row, "Gold", chest.FixedValues?.Gold ?? 1000, 0, 100000);
                var experience = Spin(row, "XP", chest.FixedValues?.Experience ?? 500, 0, 100000);
                void ApplyChest() => Edit(() => _editor.SetChestValues(
                    chest.Id, fixedValues.ButtonPressed ? new ChestValues((int)gold.Value, (int)experience.Value) : null));
                fixedValues.Toggled += _ => ApplyChest();
                gold.ValueChanged += _ => ApplyChest();
                experience.ValueChanged += _ => ApplyChest();
                break;
            }

            case MonsterGuard monster:
            {
                var row = new HBoxContainer();
                AddChild(row);
                var creature = Options(row, "Creature", creatures, monster.Creature);
                var count = Spin(row, "Count (0 = rolled)", monster.Count ?? 0, 0, 999);
                var row2 = new HBoxContainer();
                AddChild(row2);
                var disposition = Options(row2, "Disposition", Enum.GetValues<GuardDisposition>().Select(d => (d.ToString(), d)), monster.Disposition);
                var neverFlees = new CheckBox { Text = "Never flees", ButtonPressed = monster.NeverFlees };
                row2.AddChild(neverFlees);
                void ApplyMonster() => Edit(() => _editor.SetMonster(
                    monster.Id, creature.Value(), count.Value > 0 ? (int)count.Value : null, disposition.Value(), neverFlees.ButtonPressed));
                creature.Button.ItemSelected += _ => ApplyMonster();
                count.ValueChanged += _ => ApplyMonster();
                disposition.Button.ItemSelected += _ => ApplyMonster();
                neverFlees.Toggled += _ => ApplyMonster();
                break;
            }

            case { TeleportTarget: var target } when mapObject is TwoWayGate or OneWayGateEntrance:
                AddChild(new Label { Text = target is { } t ? $"Linked to #{t.Value} (use the LinkGates tool to change)" : "Not linked (use the LinkGates tool)" });
                break;
            case ResourcePile pile:
                AddChild(new Label { Text = $"{pile.Amount} {pile.Resource}" });
                break;
        }
    }

    /// <summary>Applies an edit unless the panel is just filling its fields.</summary>
    private void Edit(Func<EditResult> edit)
    {
        if (!_refreshing && edit() is not EditResult.Ok and var result)
        {
            GD.PushWarning($"Edit refused: {result}");
        }
    }

    private static SpinBox Spin(Container row, string label, int value, int min, int max)
    {
        row.AddChild(new Label { Text = label });
        var spin = new SpinBox { MinValue = min, MaxValue = max, Value = value };
        row.AddChild(spin);
        return spin;
    }

    private static Choice<T> Options<T>(Container row, string label, IEnumerable<(string Text, T Value)> items, T selected)
    {
        row.AddChild(new Label { Text = label });
        var list = items.ToList();
        var button = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(150, 0) };
        foreach (var (text, _) in list)
        {
            button.AddItem(text);
        }

        button.Selected = Math.Max(0, list.FindIndex(i => EqualityComparer<T>.Default.Equals(i.Value, selected)));
        row.AddChild(button);
        return new Choice<T>(button, list);
    }

    private sealed record Choice<T>(OptionButton Button, List<(string Text, T Value)> Items)
    {
        public T Value() => Items[Math.Max(0, Button.Selected)].Value;
    }
}
