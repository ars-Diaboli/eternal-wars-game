using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// Edits the selected hero (stats, Tactics, seven army slots) or object (mine owner and guard, chest values,
/// monster creature/count/disposition, town owner, name, buildings, dwellings and garrison; gate links are shown).
/// Every change is an undoable editor edit.
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
        var identity = new HBoxContainer();
        AddChild(identity);
        var type = Options(identity, "Hero", new[] { ("(custom)", (string?)null) }
            .Concat(_editor.Scenario.HeroRoster.All.OrderBy(h => h.Town).ThenBy(h => h.Name)
                .Select(h => ($"{h.Name} ({h.Town}, {h.Class})", (string?)h.Id))), hero.Definition);
        type.Button.ItemSelected += _ => Edit(() => _editor.SetHeroType(hero.Id, type.Value()));
        if (hero.Definition is not null)
        {
            AddChild(new Label { Text = $"{hero.Class}; prefers {hero.PreferredCreature}; specialty {hero.Specialty}; skills {hero.Skills}" });
        }

        var stats = new HBoxContainer();
        AddChild(stats);
        var attack = Spin(stats, "Attack", hero.Attack, 0, 99);
        var defense = Spin(stats, "Defense", hero.Defense, 0, 99);
        var tactics = Options(stats, "Tactics", Enum.GetValues<SkillLevel>().Where(s => s <= SkillLevel.Expert).Select(s => (s.ToString(), s)), hero.Tactics);
        void ApplyStats() => Edit(() => _editor.SetHeroStats(hero.Id, (int)attack.Value, (int)defense.Value, tactics.Value()));
        attack.ValueChanged += _ => ApplyStats();
        defense.ValueChanged += _ => ApplyStats();
        tactics.Button.ItemSelected += _ => ApplyStats();

        var spirit = new HBoxContainer();
        AddChild(spirit);
        var morale = Spin(spirit, "Morale", hero.Morale, -5, 5);
        var luck = Spin(spirit, "Luck", hero.Luck, -5, 5);
        void ApplySpirit() => Edit(() => _editor.SetHeroMoraleAndLuck(hero.Id, (int)morale.Value, (int)luck.Value));
        morale.ValueChanged += _ => ApplySpirit();
        luck.ValueChanged += _ => ApplySpirit();

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
            case Town town:
                ShowTown(town);
                break;
        }
    }

    /// <summary>Owner, name, pre-built buildings and dwellings (7-of-9 rule), garrison.</summary>
    private void ShowTown(Town town)
    {
        var scenario = _editor.Scenario;
        var row = new HBoxContainer();
        AddChild(row);
        var owners = new[] { ("Unowned", (PlayerId?)null) }.Concat(scenario.Players.Select(p => (p.Name, (PlayerId?)p.Id)));
        var owner = Options(row, "Owner", owners, town.Owner);
        owner.Button.ItemSelected += _ => Edit(() => _editor.SetTownOwner(town.Id, owner.Value()));
        row.AddChild(new Label { Text = "Name" });
        var name = new LineEdit { Text = town.Name, CustomMinimumSize = new Vector2(160, 0) };
        name.TextSubmitted += text => Edit(() => _editor.SetTownName(town.Id, text));
        row.AddChild(name);

        AddChild(new Label { Text = "Buildings (level, 0 = not built)" });
        var buildings = new HFlowContainer();
        AddChild(buildings);
        foreach (var building in scenario.Buildings.TownBuildings)
        {
            var id = building;
            var level = Spin(buildings, building, town.Level(building), 0, scenario.Buildings.MaxLevel(building));
            level.ValueChanged += value => Edit(() => _editor.SetTownBuilding(town.Id, id, (int)value));
        }

        AddChild(new Label { Text = "Dwellings (0 none, 1 built, 2 upgraded; at most 7, every tier reachable)" });
        var dwellings = new GridContainer { Columns = 2 };
        AddChild(dwellings);
        foreach (var line in TownRules.Lines(scenario, town))
        {
            var id = line.Id;
            var level = Spin(dwellings, $"T{line.Tier} {line.Name}", town.DwellingLevel(line.Id), 0, line.UpgradesTo is null ? 1 : 2);
            level.ValueChanged += value => Edit(() => _editor.SetTownDwelling(town.Id, id, (int)value));
        }

        AddChild(new Label { Text = "Garrison" });
        var creatures = new[] { ("— empty —", "") }
            .Concat(scenario.Creatures.All.OrderBy(c => c.Town ?? "~").ThenBy(c => c.Tier).Select(c => ($"{c.Name} (T{c.Tier})", c.Id)))
            .ToList();
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            var slotRow = new HBoxContainer();
            AddChild(slotRow);
            var stack = town.Garrison[slot];
            var creature = Options(slotRow, $"{slot + 1}", creatures, stack?.Creature ?? "");
            var count = Spin(slotRow, "×", stack?.Count ?? 1, 1, 9999);
            var index = slot;
            void ApplySlot() => Edit(() => _editor.SetGarrisonSlot(
                town.Id, index, creature.Value() is { Length: > 0 } id ? new CreatureStack(id, (int)count.Value) : null));
            creature.Button.ItemSelected += _ => ApplySlot();
            count.ValueChanged += _ => ApplySlot();
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
