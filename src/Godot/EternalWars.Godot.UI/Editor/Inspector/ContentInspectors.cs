using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Editor.Inspector;

/// <summary>What signs say.</summary>
internal sealed class SignPostInspector : InspectorSection<SignPost>
{
    protected override void Show(InspectorForm form, IReadOnlyList<SignPost> signs)
    {
        var row = new HBoxContainer();
        form.In(InspectorPart.Messages).AddChild(row);
        form.Text(row, "Message", ObjectProperties.SignMessage, signs, "What the sign says", UiTheme.InspectorTextWidth);
    }
}

/// <summary>Prisons' hero (or one drawn at start) and level (0 = rolled).</summary>
internal sealed class PrisonInspector : InspectorSection<Prison>
{
    protected override void Show(InspectorForm form, IReadOnlyList<Prison> prisons)
    {
        var row = new HBoxContainer();
        form.In(InspectorPart.Contents).AddChild(row);
        form.Picker(row, "Prisoner", [PickerItem.None("Random (drawn at start)"), .. form.HeroItems()], ObjectProperties.PrisonHero, prisons, h => h ?? "", k => k.Length > 0 ? k : null);
        form.Spin(row, "Level (0 = rolled)", 0, 75, ObjectProperties.PrisonLevel, prisons, l => l ?? 0, n => n == 0 ? null : n);
    }
}

/// <summary>Artifact pickups' artifact (or one of its tier drawn at start), and one scroll's spell (ADR-136).</summary>
internal sealed class ArtifactPickupInspector : InspectorSection<ArtifactPickup>
{
    protected override void Show(InspectorForm form, IReadOnlyList<ArtifactPickup> pickups)
    {
        var row = new HBoxContainer();
        form.In(InspectorPart.Contents).AddChild(row);
        var random = pickups.Select(p => p.Definition.Name).Distinct().Count() == 1 ? $"Random ({pickups[0].Definition.Name}, drawn at start)" : "Random (drawn at start)";

        // A written scroll counts as its blank artifact here; the spell row below sets what is written on it.
        var artifact = new ObjectProperty<ArtifactPickup, ArtifactId?>(
            ObjectProperties.PickupArtifact.Name, p => p.Artifact is { } current ? ArtifactCatalog.BaseOf(current) : (ArtifactId?)null, (e, p, v) => e.Contents.SetArtifactPickup(p.Id, v));
        form.Picker(row, "Artifact", [PickerItem.None(random), .. form.ArtifactItems()], artifact, pickups, a => a?.Value ?? "", k => k.Length > 0 ? new ArtifactId(k) : (ArtifactId?)null);

        // A spell scroll (ADR-136): the spell written on it, or a random one of its level drawn at game start.
        var catalog = form.Scenario.Artifacts;
        if (pickups.Count == 1 && pickups[0].Artifact is { } chosen && catalog.TryGet(chosen, out var scroll) && scroll.ScrollLevel is { } level)
        {
            var spellRow = new HBoxContainer();
            form.Box.AddChild(spellRow);
            var spells = InspectorForm.SpellItems(catalog.ScrollSpells(level).Select(id => catalog.Spells.Get(id)));
            var spell = PicturePicker.Add(spellRow, "Spell", [PickerItem.None($"Random level {level} spell (drawn at start)"), .. spells], scroll.Spell?.Value ?? "");
            spell.Picked += written => form.Edit(() => form.Editor.Contents.SetArtifactPickup(pickups[0].Id,
                written.Length > 0 ? ArtifactCatalog.WithSpell(chosen, new SpellId(written)) : ArtifactCatalog.BaseOf(chosen)));
        }
    }
}

/// <summary>Witch huts' two skills, both set or both rolled at start.</summary>
internal sealed class WitchHutInspector : InspectorSection<WitchHut>
{
    protected override void Show(InspectorForm form, IReadOnlyList<WitchHut> huts)
    {
        var row = new HBoxContainer();
        form.In(InspectorPart.Contents).AddChild(row);
        List<PickerItem> skills = [PickerItem.None("Rolled at start"), .. InspectorForm.SkillItems()];
        var shared = ObjectProperties.WitchHutSkills.Shared(huts);
        var first = PicturePicker.Add(row, "Skills", skills, shared.Differs ? null : shared.Value.ElementAtOrDefault(0) ?? "");
        var second = PicturePicker.Add(row, "and", skills, shared.Differs ? null : shared.Value.ElementAtOrDefault(1) ?? "");
        form.Box.AddChild(new Label { Text = "Set both, or leave both rolled; the two must differ." });
        void ApplyHut()
        {
            // Both or neither: a half-set hut waits for its second skill.
            if (!first.Differs && !second.Differs && (first.Value().Length > 0) == (second.Value().Length > 0))
            {
                form.Edit(() => ObjectProperties.WitchHutSkills.Apply(form.Editor, huts, first.Value().Length > 0 ? [first.Value(), second.Value()] : []));
            }
        }

        first.Picked += _ => ApplyHut();
        second.Picked += _ => ApplyHut();
    }
}

/// <summary>Shrines' spell (one of their level, or drawn at start); shrines of different levels are set one level at a time.</summary>
internal sealed class ShrineInspector : InspectorSection<Shrine>
{
    protected override void Show(InspectorForm form, IReadOnlyList<Shrine> shrines)
    {
        var row = new HBoxContainer();
        form.In(InspectorPart.Contents).AddChild(row);
        if (shrines.Select(s => s.SpellLevel).Distinct().Count() > 1)
        {
            row.AddChild(new Label { Text = "The shrines are of different levels: select shrines of one level to set their spell", Modulate = UiTheme.TextQuiet });
            return;
        }

        var spells = InspectorForm.SpellItems(ShrineRules.Candidates(form.Scenario.Spells, shrines[0].SpellLevel));
        form.Picker(row, "Spell", [PickerItem.None("Drawn at start"), .. spells], ObjectProperties.ShrineSpell, shrines, s => s?.Value ?? "", k => k.Length > 0 ? new SpellId(k) : (SpellId?)null);
    }
}

/// <summary>Treasure chests' fixed gold and experience, or rolled values; the three fields are one value set on every chest.</summary>
internal sealed class TreasureChestInspector : InspectorSection<TreasureChest>
{
    protected override void Show(InspectorForm form, IReadOnlyList<TreasureChest> chests)
    {
        var row = new HBoxContainer();
        form.In(InspectorPart.Contents).AddChild(row);
        var shared = ObjectProperties.ChestValues.Shared(chests);
        var values = shared.Differs ? null : shared.Value;
        var fixedValues = new CheckBox { Text = shared.Differs ? $"Fixed values (else rolled) ({InspectorForm.Differs})" : "Fixed values (else rolled)", ButtonPressed = values is not null };
        row.AddChild(fixedValues);
        var gold = row.AddSpin("Gold", values?.Gold ?? 1000, 0, 100000);
        var experience = row.AddSpin("XP", values?.Experience ?? 500, 0, 100000);
        if (shared.Differs)
        {
            // The values are one value: they are set after "Fixed values" is ticked (which sets the shown defaults on every chest).
            InspectorForm.ShowDiffers(gold);
            InspectorForm.ShowDiffers(experience);
            gold.Editable = false;
            experience.Editable = false;
        }

        void ApplyChest() => form.Edit(() => ObjectProperties.ChestValues.Apply(
            form.Editor, chests, fixedValues.ButtonPressed ? new ChestValues((int)gold.Value, (int)experience.Value) : null));
        fixedValues.Toggled += _ => ApplyChest();
        if (!shared.Differs)
        {
            gold.ValueChanged += _ => ApplyChest();
            experience.ValueChanged += _ => ApplyChest();
        }
    }
}

/// <summary>A two-way gate's link (changed with the LinkGates tool).</summary>
internal sealed class TwoWayGateInspector : InspectorSection<TwoWayGate>
{
    protected override void Show(InspectorForm form, IReadOnlyList<TwoWayGate> gates) => GateLink.Show(form, gates);
}

/// <summary>A one-way gate's exit (changed with the LinkGates tool).</summary>
internal sealed class OneWayGateInspector : InspectorSection<OneWayGateEntrance>
{
    protected override void Show(InspectorForm form, IReadOnlyList<OneWayGateEntrance> gates) => GateLink.Show(form, gates);
}

/// <summary>How a gate's link is changed; the link itself is a jump link (<see cref="InspectorForm.Links"/>).</summary>
internal static class GateLink
{
    public static void Show<T>(InspectorForm form, IReadOnlyList<T> gates)
        where T : MapObject
    {
        form.In(InspectorPart.Links);
        if (form.Single(gates, "the link") is not { } gate)
        {
            return;
        }

        form.Box.AddChild(new Label
        {
            Text = gate.TeleportTarget switch
            {
                null => "Not linked (use the LinkGates tool)",
                { } target when form.Scenario.FindObject(target) is null => $"Linked to #{target.Value}, which is not on the map (use the LinkGates tool)",
                _ => "The LinkGates tool changes the link.",
            },
        });
    }
}

/// <summary>A resource pile's fixed amount, or that it is rolled.</summary>
internal sealed class ResourcePileInspector : InspectorSection<ResourcePile>
{
    protected override void Show(InspectorForm form, IReadOnlyList<ResourcePile> piles)
    {
        form.In(InspectorPart.Contents);
        if (form.Single(piles, "the amount") is { } pile)
        {
            form.Box.AddChild(new Label { Text = pile.Amount is { } fixedAmount ? $"{fixedAmount} {pile.Resource}" : $"{pile.Resource}, amount rolled at game start" });
        }
    }
}

/// <summary>The arena place (ADR-097): what it means for the battle of day 2.</summary>
internal sealed class CombatPlaceInspector : InspectorSection<CombatPlace>
{
    protected override void Show(InspectorForm form, IReadOnlyList<CombatPlace> places)
    {
        if (form.Single(places, "the arena") is not { } place)
        {
            return;
        }

        form.Box.AddChild(new Label
        {
            Text = $"The arena battle of day 2 (ADR-097): the defender stands on the entrance {place.VisitPosition}, the attacker on "
                   + $"{place.AttackCell}, and the battle is fought on that cell's ground. A hero standing on the entrance at the end "
                   + $"of day 1 makes its owner the defender.{(form.Scenario.Rules.Arena ? "" : " The map is not an arena yet (conditions tab).")}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(UiTheme.InspectorNoteWidth, 0),
        });
    }
}

/// <summary>Quest objects' quest: one of the map's quest events.</summary>
internal sealed class QuestSiteInspector : InspectorSection<QuestSite>
{
    protected override void Show(InspectorForm form, IReadOnlyList<QuestSite> sites) =>
        form.EventChoice("Quest", MapEventKind.Quest, ObjectProperties.QuestSiteQuest.Shared(sites), name => ObjectProperties.QuestSiteQuest.Apply(form.Editor, sites, name));
}
