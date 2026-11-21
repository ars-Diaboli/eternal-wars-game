using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Editor.Inspector;

/// <summary>
/// Towns: owner, faction (or random) for one or several (ADR-255); for one town also its name, pre-built buildings and dwellings
/// (7-of-9 rule), fixed guild spells, garrison and town events.
/// </summary>
internal sealed class TownInspector : InspectorSection<Town>
{
    protected override void Show(InspectorForm form, IReadOnlyList<Town> towns)
    {
        var panel = form.Box;
        var row = new HBoxContainer();
        panel.AddChild(row);
        form.Owner(row, ObjectProperties.TownOwner, towns);
        var one = towns.Count == 1 ? towns[0] : null;
        if (one is not null)
        {
            row.AddChild(new Label { Text = "Name" });
            var name = new LineEdit { Text = one.Name, MaxLength = MapObject.NameLength, CustomMinimumSize = new Vector2(UiTheme.InspectorNameWidth, 0) };
            name.TextSubmitted += text => form.Edit(() => form.Editor.Towns.SetName(one.Id, text));
            row.AddChild(name);
        }

        var shown = towns.All(t => t.IsRandom) ? "Shown as" : "Faction";
        var type = form.Choice(row, shown, BuiltInMapObjects.PlayableTowns.Select(t => (t, t)), ObjectProperties.TownFaction, towns);
        for (var i = 0; i < type.Items.Count; i++)
        {
            if (type.Items[i].Text != InspectorForm.Differs && Heraldry.Crest(type.Items[i].Value, UiIcons.ButtonSize) is { } crest)
            {
                type.Button.SetItemIcon(i, crest);
            }
        }

        var random = form.Check(panel, "Random faction", ObjectProperties.TownRandom, towns);
        random.TooltipText = "Drawn when the game starts: an owned town takes its owner's chosen faction, a neutral one a random playable faction. " +
                             "Buildings carry over; dwellings move to the drawn faction's lines of the same tier.";

        panel = form.In(InspectorPart.Contents);
        if (form.Single(towns, "buildings, dwellings and guild spells") is not { } town)
        {
            form.In(InspectorPart.Guard);
            form.Single(towns, "the garrison");
            return;
        }

        var scenario = form.Scenario;
        var editor = form.Editor;
        panel.AddHeading("Buildings (level, 0 = not built)");
        var buildings = new HFlowContainer();
        panel.AddChild(buildings);
        var catalog = TownRules.Buildings(scenario, town);
        foreach (var building in catalog.TownBuildings)
        {
            var id = building;
            var level = buildings.AddSpin(building.Value, town.Level(building), 0, catalog.MaxLevel(building));
            level.ValueChanged += value => form.Edit(() => editor.Towns.SetBuilding(town.Id, id, (int)value));
        }

        panel.AddHeading("Dwellings (0 none, 1 built, 2 upgraded; at most 7, every tier reachable)");
        var dwellings = new GridContainer { Columns = 2 };
        panel.AddChild(dwellings);
        foreach (var line in TownRules.Lines(scenario, town))
        {
            var id = line.Id;
            var level = dwellings.AddSpin($"T{line.Tier} {line.Name}", town.DwellingLevel(line.Id), 0, line.UpgradesTo is null ? 1 : 2);
            level.ValueChanged += value => form.Edit(() => editor.Towns.SetDwelling(town.Id, id, (int)value));
        }

        ShowGuildSpells(form, town);
        form.GarrisonRows(town.Id, town.Garrison); // the Guard part
        ShowTownEvents(form, town);
    }

    /// <summary>
    /// The spells the map fixes for the town's mage guild (map v30): per spell level as many choices as the guild offers there (3 / 3 / 2 / 2), each
    /// a spell of the town's school or "drawn" (the game start draws the rest). Towns without a school have no guild and no rows.
    /// </summary>
    private static void ShowGuildSpells(InspectorForm form, Town town)
    {
        var spells = form.Scenario.Spells;
        if (town.IsRandom || !Enumerable.Range(1, MagicRules.GuildSpellsPerLevel.Count).Any(level => spells.Of(town.TownType, level).Count > 0))
        {
            return;
        }

        form.Box.AddHeading("Guild spells (— drawn — = drawn when the game starts)");
        var choices = new List<PicturePicker>();
        for (var level = 1; level <= MagicRules.GuildSpellsPerLevel.Count; level++)
        {
            List<PickerItem> items = [PickerItem.None("— drawn —"), .. InspectorForm.SpellItems(spells.Of(town.TownType, level))];
            var fixedHere = town.FixedGuildSpells.Where(s => spells.TryGet(s, out var spell) && spell.Level == level).ToList();
            var row = new HFlowContainer();
            form.Box.AddChild(row);
            for (var slot = 0; slot < MagicRules.GuildSpellsPerLevel[level - 1]; slot++)
            {
                choices.Add(PicturePicker.Add(row, slot == 0 ? $"L{level}" : null, items, fixedHere.Select(s => s.Value).ElementAtOrDefault(slot) ?? ""));
            }
        }

        void Apply() => form.Edit(() => form.Editor.Towns.SetGuildSpells(town.Id, [.. choices.Select(c => c.Value()).Where(s => s.Length > 0).Distinct()]));
        foreach (var choice in choices)
        {
            choice.Picked += _ => Apply();
        }
    }

    /// <summary>The town events that name the town (each with "Edit"), and "New town event" for it.</summary>
    private static void ShowTownEvents(InspectorForm form, Town town)
    {
        var row = new HFlowContainer();
        form.In(InspectorPart.Events).AddChild(row);
        row.AddChild(new Label { Text = "Town events" });
        foreach (var definition in form.Scenario.MapEvents.Set.Events.Where(e => e.Kind == MapEventKind.Town && e.Towns.Contains(town.Id.Value)))
        {
            var name = definition.Name;
            var open = new Button { Text = name };
            open.Pressed += () => form.OpenEvent(name);
            row.AddChild(open);
        }

        var create = new Button { Text = "New town event" };
        create.Pressed += () => form.Edit(() => form.NewEvent(MapEventKind.Town, name =>
        {
            form.OpenEvent(name!);
            return EditResult.Ok;
        }, [town.Id.Value]));
        row.AddChild(create);
    }
}
