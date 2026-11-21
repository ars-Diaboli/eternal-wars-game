using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Editor.Inspector;

/// <summary>
/// Monsters, one or several (ADR-255). Identity: creature, count (0 = rolled) and a random tier (ADR-126). Guard: disposition, whether
/// it never flees and its guard mode. Every field sets the one value on each monster shown.
/// </summary>
internal sealed class MonsterInspector : InspectorSection<MonsterGuard>
{
    protected override void Show(InspectorForm form, IReadOnlyList<MonsterGuard> monsters)
    {
        var row = new HBoxContainer();
        form.Box.AddChild(row);
        form.Picker(row, "Creature", form.CreatureItems(), ObjectProperties.MonsterCreature, monsters, c => c.Value, k => new CreatureId(k));
        form.Spin(row, "Count (0 = rolled)", 0, 999, ObjectProperties.MonsterCount, monsters, c => c ?? 0, n => n > 0 ? n : null);

        var row2 = new HBoxContainer();
        form.In(InspectorPart.Guard).AddChild(row2);
        form.Choice(row2, "Disposition", Enum.GetValues<GuardDisposition>().Select(d => (d.ToString(), d)), ObjectProperties.MonsterDisposition, monsters);
        form.Check(row2, "Never flees", ObjectProperties.MonsterNeverFlees, monsters);

        var row3 = new HBoxContainer();
        form.Box.AddChild(row3);
        form.Choice(row3, "Guard mode", new[]
        {
            ("Classic: zone of control, pickups can be stolen", GuardMode.Classic),
            ("Area: treasure guard, the whole 3×3 area is guarded", GuardMode.Area),
        }, ObjectProperties.MonsterGuardMode, monsters);

        // A random monster (ADR-126): the creature above only shows it; the game start draws one of the tier.
        var row4 = new HBoxContainer();
        form.In(InspectorPart.Identity).AddChild(row4);
        var tiers = new[] { ("Fixed creature", (int?)null), ("Random, any tier", 0) }
            .Concat(Enumerable.Range(1, CreatureDefinition.MaxTier).Select(t => ($"Random tier {t}", (int?)t)));
        form.Choice(row4, "Random", tiers, ObjectProperties.MonsterRandomTier, monsters);
    }
}
