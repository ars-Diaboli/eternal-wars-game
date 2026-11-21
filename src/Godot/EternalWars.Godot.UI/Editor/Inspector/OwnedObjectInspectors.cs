using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Objects;
using Godot;

namespace EternalWars.Godot.UI.Editor.Inspector;

/// <summary>Creature dwellings: line (base creatures only, ADR-073), owner and the creatures waiting (-1 = one week's growth).</summary>
internal sealed class DwellingInspector : InspectorSection<CreatureDwelling>
{
    protected override void Show(InspectorForm form, IReadOnlyList<CreatureDwelling> dwellings)
    {
        var row = new HBoxContainer();
        form.Box.AddChild(row);
        var lines = form.CreatureItems(c => !form.Scenario.Creatures.IsUpgrade(c.Id.Value));
        form.Picker(row, "Creature", lines, ObjectProperties.DwellingCreature, dwellings, c => c.Value, k => new CreatureId(k));
        form.Owner(row, ObjectProperties.DwellingOwner, dwellings);
        var amountRow = new HBoxContainer();
        form.In(InspectorPart.Contents).AddChild(amountRow);
        form.Spin(amountRow, "Waiting (-1 = one week's growth)", -1, 9999, ObjectProperties.DwellingAvailable, dwellings, a => a ?? -1, n => n >= 0 ? n : null);
    }
}

/// <summary>Resource providers' owner (ADR-079).</summary>
internal sealed class ProviderInspector : InspectorSection<ResourceProvider>
{
    protected override void Show(InspectorForm form, IReadOnlyList<ResourceProvider> providers)
    {
        var row = new HBoxContainer();
        form.Box.AddChild(row);
        form.Owner(row, ObjectProperties.ProviderOwner, providers);
    }
}

/// <summary>Mines' owner and guard (none, or a creature with a fixed or rolled count).</summary>
internal sealed class MineInspector : InspectorSection<Mine>
{
    protected override void Show(InspectorForm form, IReadOnlyList<Mine> mines)
    {
        var row = new HBoxContainer();
        form.Box.AddChild(row);
        form.Owner(row, ObjectProperties.MineOwner, mines);

        // A creature picked keeps each mine's own count; a count is set on the guarded mines.
        var guardRow = new HBoxContainer();
        form.In(InspectorPart.Guard).AddChild(guardRow);
        form.Picker(guardRow, "Guard", [PickerItem.None("None"), .. form.CreatureItems()], ObjectProperties.MineGuardCreature, mines,
            c => c?.Value ?? "", k => k.Length > 0 ? new CreatureId(k) : (CreatureId?)null);
        if (mines.Any(m => m.Guard is not null))
        {
            var guarded = mines.Where(m => m.Guard is not null).ToList();
            form.Spin(guardRow, "Count (0 = rolled)", 0, 999, ObjectProperties.MineGuardCount, guarded, c => c ?? 0, n => n > 0 ? n : null);
        }
    }
}

/// <summary>Map garrisons' owner (neutral or a player); the troops of one garrison (ADR-070).</summary>
internal sealed class MapGarrisonInspector : InspectorSection<MapGarrison>
{
    protected override void Show(InspectorForm form, IReadOnlyList<MapGarrison> garrisons)
    {
        var row = new HBoxContainer();
        form.Box.AddChild(row);
        form.Owner(row, ObjectProperties.GarrisonOwner, garrisons, noOwner: "Neutral");
        form.In(InspectorPart.Guard);
        if (form.Single(garrisons, "the troops") is { } garrison)
        {
            form.GarrisonRows(garrison.Id, garrison.Troops);
        }
    }
}
