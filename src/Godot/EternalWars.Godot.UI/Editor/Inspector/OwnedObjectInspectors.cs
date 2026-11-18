using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Editor.Inspector;

/// <summary>A creature dwelling: its line (base creatures only, ADR-073), owner and the creatures waiting (-1 = one week's growth).</summary>
internal sealed class DwellingInspector : InspectorSection<CreatureDwelling>
{
    protected override void Show(InspectorForm form, CreatureDwelling dwelling)
    {
        var row = new HBoxContainer();
        form.Box.AddChild(row);
        var lines = form.Creatures().Where(c => !form.Scenario.Creatures.IsUpgrade(c.Id)).ToList();
        var creature = row.AddChoice("Creature", lines, dwelling.Creature);
        var owner = row.AddOwnerChoice(form.Scenario.Players, dwelling.Owner);
        var amountRow = new HBoxContainer();
        form.Box.AddChild(amountRow);
        var amount = amountRow.AddSpin("Waiting (-1 = one week's growth)", dwelling.Available ?? -1, -1, 9999);
        void Apply() => form.Edit(() => form.Editor.Objects.SetDwelling(dwelling.Id, creature.Value(), owner.Value(), amount.Value >= 0 ? (int)amount.Value : null));
        creature.Button.ItemSelected += _ => Apply();
        owner.Button.ItemSelected += _ => Apply();
        amount.ValueChanged += _ => Apply();
    }
}

/// <summary>A resource provider's owner (ADR-079).</summary>
internal sealed class ProviderInspector : InspectorSection<ResourceProvider>
{
    protected override void Show(InspectorForm form, ResourceProvider provider)
    {
        var row = new HBoxContainer();
        form.Box.AddChild(row);
        var owner = row.AddOwnerChoice(form.Scenario.Players, provider.Owner);
        owner.Button.ItemSelected += _ => form.Edit(() => form.Editor.Objects.SetProviderOwner(provider.Id, owner.Value()));
    }
}

/// <summary>A mine's owner and guard (none, or a creature with a fixed or rolled count).</summary>
internal sealed class MineInspector : InspectorSection<Mine>
{
    protected override void Show(InspectorForm form, Mine mine)
    {
        var row = new HBoxContainer();
        form.Box.AddChild(row);
        var owner = row.AddOwnerChoice(form.Scenario.Players, mine.Owner);
        owner.Button.ItemSelected += _ => form.Edit(() => form.Editor.Objects.SetMineOwner(mine.Id, owner.Value()));

        var guardRow = new HBoxContainer();
        form.Box.AddChild(guardRow);
        var guardCreature = guardRow.AddChoice("Guard", new[] { ("None", "") }.Concat(form.Creatures()), mine.Guard?.Creature ?? "");
        var guardCount = guardRow.AddSpin("Count (0 = rolled)", mine.Guard?.Count ?? 0, 0, 999);
        void ApplyGuard() => form.Edit(() => form.Editor.Objects.SetMineGuard(mine.Id, guardCreature.Value() is { Length: > 0 } id
            ? new GuardStack(id, guardCount.Value > 0 ? (int)guardCount.Value : null)
            : null));
        guardCreature.Button.ItemSelected += _ => ApplyGuard();
        guardCount.ValueChanged += _ => ApplyGuard();
    }
}

/// <summary>A map garrison's owner (neutral or a player) and troops (ADR-070).</summary>
internal sealed class MapGarrisonInspector : InspectorSection<MapGarrison>
{
    protected override void Show(InspectorForm form, MapGarrison garrison)
    {
        var row = new HBoxContainer();
        form.Box.AddChild(row);
        var owner = row.AddOwnerChoice(form.Scenario.Players, garrison.Owner, noOwner: "Neutral");
        owner.Button.ItemSelected += _ => form.Edit(() => form.Editor.Objects.SetGarrisonOwner(garrison.Id, owner.Value()));
        form.GarrisonRows(garrison.Id, garrison.Troops);
    }
}
