using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Editor.Inspector;

/// <summary>
/// A hero on the map (ADR-223: the hero's form; the objects' are <see cref="InspectorSections"/>). Identity: type or placeholder, stats,
/// Tactics, morale and luck. Contents: war machines, seven army slots, the spell book and spells (ADR-120), worn and carried artifacts
/// (ADR-061).
/// </summary>
internal static class HeroInspector
{
    public static void Show(InspectorForm form, Hero hero)
    {
        var panel = form.In(InspectorPart.Identity);
        var scenario = form.Scenario;
        var heroes = form.Editor.Heroes;
        panel.AddChild(new Label { Text = $"{hero.Name} ({scenario.GetPlayer(hero.Owner).Name}) at {hero.Position}" });
        var identity = new HBoxContainer();
        panel.AddChild(identity);
        var type = PicturePicker.Add(identity, "Hero", [PickerItem.None("(custom)"), .. form.HeroItems()], hero.Definition ?? "");
        type.Picked += picked => form.Edit(() => heroes.SetType(hero.Id, picked.Length > 0 ? picked : null));
        var placeholder = new CheckBox
        {
            Text = "Placeholder",
            ButtonPressed = hero.IsPlaceholder,
            TooltipText = "Becomes a random roster hero of its owner's faction when the game starts (ADR-126); its place, army and artifacts stay.",
        };
        placeholder.Toggled += on => form.Edit(() => heroes.SetPlaceholder(hero.Id, on));
        identity.AddChild(placeholder);
        if (hero.IsPlaceholder)
        {
            panel.AddChild(new Label { Text = "Placeholder: a random hero of the owner's faction replaces its type, stats, skills and spells at game start." });
        }

        if (hero.Definition is not null)
        {
            panel.AddChild(new Label { Text = $"{hero.Class}; prefers {hero.PreferredCreature}; specialty {hero.Specialty}; skills {hero.Skills}" });
        }

        var stats = new HBoxContainer();
        panel.AddChild(stats);
        var attack = stats.AddSpin("Attack", hero.Attack, 0, 99);
        var defense = stats.AddSpin("Defense", hero.Defense, 0, 99);
        var spellPower = stats.AddSpin("Power", hero.SpellPower, 0, 99);
        var knowledge = stats.AddSpin("Knowledge", hero.Knowledge, 0, 99);
        var tactics = stats.AddChoice("Tactics", Enum.GetValues<SkillLevel>().Where(s => s <= SkillLevel.Expert).Select(s => (s.ToString(), s)), hero.Tactics);
        void ApplyStats() => form.Edit(() => heroes.SetStats(
            hero.Id, (int)attack.Value, (int)defense.Value, tactics.Value(), (int)spellPower.Value, (int)knowledge.Value));
        attack.ValueChanged += _ => ApplyStats();
        defense.ValueChanged += _ => ApplyStats();
        spellPower.ValueChanged += _ => ApplyStats();
        knowledge.ValueChanged += _ => ApplyStats();
        tactics.Button.ItemSelected += _ => ApplyStats();

        var spirit = new HBoxContainer();
        panel.AddChild(spirit);
        var morale = spirit.AddSpin("Morale", hero.Morale, -5, 5);
        var luck = spirit.AddSpin("Luck", hero.Luck, -5, 5);
        void ApplySpirit() => form.Edit(() => heroes.SetMoraleAndLuck(hero.Id, (int)morale.Value, (int)luck.Value));
        morale.ValueChanged += _ => ApplySpirit();
        luck.ValueChanged += _ => ApplySpirit();

        panel = form.In(InspectorPart.Contents);
        var machines = new HBoxContainer();
        panel.AddChild(machines);
        machines.AddChild(new Label { Text = "War machines" });
        var boxes = Enum.GetValues<WarMachineSlot>().Select(slot =>
        {
            var box = new CheckBox { Text = slot.ToString(), ButtonPressed = hero.WarMachines.Contains(slot) };
            machines.AddChild(box);
            return (Slot: slot, Box: box);
        }).ToList();
        foreach (var (_, box) in boxes)
        {
            box.Toggled += _ => form.Edit(() => heroes.SetWarMachines(hero.Id, boxes.Where(b => b.Box.ButtonPressed).Select(b => b.Slot).ToHashSet()));
        }

        panel.AddHeading("Army");
        var creatures = form.ArmyItems();
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            var row = new HBoxContainer();
            panel.AddChild(row);
            var stack = hero.Army[slot];
            var creature = PicturePicker.Add(row, $"{slot + 1}", creatures, stack?.Creature.Value ?? "");
            var count = row.AddSpin("×", stack?.Count ?? 1, 1, 9999);
            var index = slot;
            void ApplySlot() => form.Edit(() => heroes.SetArmySlot(
                hero.Id, index, creature.Value() is { Length: > 0 } id ? new CreatureStack(id, (int)count.Value) : null));
            creature.Picked += _ => ApplySlot();
            count.ValueChanged += _ => ApplySlot();
        }

        ShowSpells(form, hero);
        ShowArtifacts(form, hero);
    }

    /// <summary>The hero's spell book and the spells in it (ADR-120): a book box, one row per spell and an empty row to add another.</summary>
    private static void ShowSpells(InspectorForm form, Hero hero)
    {
        var book = new CheckBox { Text = "Spell book", ButtonPressed = hero.HasSpellBook };
        form.Box.AddChild(book);
        List<PickerItem> items = [PickerItem.None("— none —"), .. InspectorForm.SpellItems(form.Scenario.Spells.All)];
        var rows = new HFlowContainer();
        form.Box.AddChild(rows);
        var choices = hero.Spells.Keys.Select(k => k.Value).Append("").Select(id => PicturePicker.Add(rows, null, items, id)).ToList();
        rows.Visible = hero.HasSpellBook;
        void Apply() => form.Edit(() => form.Editor.Heroes.SetSpells(
            hero.Id, book.ButtonPressed, book.ButtonPressed ? [.. choices.Select(c => c.Value()).Where(s => s.Length > 0).Distinct()] : []));
        book.Toggled += _ => Apply();
        foreach (var choice in choices)
        {
            choice.Picked += _ => Apply();
        }
    }

    /// <summary>
    /// What the hero wears and carries (ADR-061): one row per worn artifact (it goes to the slot its definition names) and per backpack item, and one
    /// empty row each to add another; "none" removes an entry. A set that does not fit the slots is refused (the warning names it).
    /// </summary>
    private static void ShowArtifacts(InspectorForm form, Hero hero)
    {
        var scenario = form.Scenario;
        List<PickerItem> items = [PickerItem.None("— none —"), .. form.ArtifactItems(bySlot: true)];

        // Written scrolls the hero has (ADR-136) are not catalog rows; list them so an edit keeps them. A blank scroll gets its spell at game start.
        foreach (var id in hero.Artifacts.AllWorn.Select(w => w.Id).Concat(hero.Artifacts.Backpack).Select(a => a.Value).Where(id => items.All(i => i.Key != id)).Distinct())
        {
            items.Add(scenario.Artifacts.TryGet(id, out var written) ? PickerItem.Of(id, written.Name, $"{written.Slot}") : PickerItem.Of(id, id, ""));
        }

        var changed = new List<PicturePicker>();
        List<PicturePicker> Rows(string title, IEnumerable<string> ids)
        {
            form.Box.AddChild(new Label { Text = title });
            var rows = new HFlowContainer();
            form.Box.AddChild(rows);
            return ids.Append("").Select(id =>
            {
                var choice = PicturePicker.Add(rows, null, items, id);
                changed.Add(choice);
                return choice;
            }).ToList();
        }

        var worn = Rows("Worn artifacts", hero.Artifacts.AllWorn.Select(w => w.Id.Value));
        var backpack = Rows("Backpack", hero.Artifacts.Backpack.Select(a => a.Value));
        void Apply() => form.Edit(() => form.Editor.Heroes.SetArtifacts(
            hero.Id,
            [.. worn.Select(w => w.Value()).Where(a => a.Length > 0)],
            [.. backpack.Select(b => b.Value()).Where(a => a.Length > 0)]));
        foreach (var choice in changed)
        {
            choice.Picked += _ => Apply();
        }
    }
}
