using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Editor.Inspector;

/// <summary>
/// A monster. Identity: creature, count (0 = rolled) and a random tier (ADR-126). Guard: disposition, whether it never flees and its
/// guard mode.
/// </summary>
internal sealed class MonsterInspector : InspectorSection<MonsterGuard>
{
    protected override void Show(InspectorForm form, MonsterGuard monster)
    {
        var row = new HBoxContainer();
        form.Box.AddChild(row);
        var creature = row.AddChoice("Creature", form.Creatures(), monster.Creature.Value);
        var count = row.AddSpin("Count (0 = rolled)", monster.Count ?? 0, 0, 999);
        var row2 = new HBoxContainer();
        form.In(InspectorPart.Guard).AddChild(row2);
        var disposition = row2.AddChoice("Disposition", Enum.GetValues<GuardDisposition>().Select(d => (d.ToString(), d)), monster.Disposition);
        var neverFlees = new CheckBox { Text = "Never flees", ButtonPressed = monster.NeverFlees };
        row2.AddChild(neverFlees);
        void ApplyMonster() => form.Edit(() => form.Editor.Monsters.Set(
            monster.Id, creature.Value(), count.Value > 0 ? (int)count.Value : null, disposition.Value(), neverFlees.ButtonPressed));
        creature.Button.ItemSelected += _ => ApplyMonster();
        count.ValueChanged += _ => ApplyMonster();
        disposition.Button.ItemSelected += _ => ApplyMonster();
        neverFlees.Toggled += _ => ApplyMonster();

        var row3 = new HBoxContainer();
        form.Box.AddChild(row3);
        var mode = row3.AddChoice("Guard mode", new[]
        {
            ("Classic: zone of control, pickups can be stolen", GuardMode.Classic),
            ("Area: treasure guard, the whole 3×3 area is guarded", GuardMode.Area),
        }, monster.Mode);
        mode.Button.ItemSelected += _ => form.Edit(() => form.Editor.Monsters.SetGuardMode(monster.Id, mode.Value()));

        // A random monster (ADR-126): the creature above only shows it; the game start draws one of the tier.
        var row4 = new HBoxContainer();
        form.In(InspectorPart.Identity).AddChild(row4);
        var tiers = new[] { ("Fixed creature", (int?)null), ("Random, any tier", 0) }
            .Concat(Enumerable.Range(1, CreatureDefinition.MaxTier).Select(t => ($"Random tier {t}", (int?)t)));
        var randomTier = row4.AddChoice("Random", tiers, monster.RandomTier);
        randomTier.Button.ItemSelected += _ => form.Edit(() => form.Editor.Monsters.SetRandomTier(monster.Id, randomTier.Value()));
    }
}
