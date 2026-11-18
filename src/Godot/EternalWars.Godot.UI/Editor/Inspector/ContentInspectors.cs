using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Editor.Inspector;

/// <summary>What a sign says.</summary>
internal sealed class SignPostInspector : InspectorSection<SignPost>
{
    protected override void Show(InspectorForm form, SignPost sign)
    {
        var row = new HBoxContainer();
        form.Box.AddChild(row);
        row.AddChild(new Label { Text = "Message" });
        var text = new LineEdit { Text = sign.Message, CustomMinimumSize = new Vector2(UiTheme.InspectorTextWidth, 0), PlaceholderText = "What the sign says" };
        text.TextSubmitted += value => form.Edit(() => form.Editor.Contents.SetSignMessage(sign.Id, value));
        text.FocusExited += () => form.Edit(() => form.Editor.Contents.SetSignMessage(sign.Id, text.Text));
        row.AddChild(text);
    }
}

/// <summary>A prison's hero (or one drawn at start) and level (0 = rolled).</summary>
internal sealed class PrisonInspector : InspectorSection<Prison>
{
    protected override void Show(InspectorForm form, Prison prison)
    {
        var row = new HBoxContainer();
        form.Box.AddChild(row);
        var heroes = new[] { ("Random (drawn at start)", (string?)null) }
            .Concat(form.Scenario.HeroRoster.All.OrderBy(h => h.Town).ThenBy(h => h.Name).Select(h => ($"{h.Name} ({h.Town})", (string?)h.Id)));
        var who = row.AddChoice("Prisoner", heroes, prison.Hero);
        var level = row.AddSpin("Level (0 = rolled)", prison.Level ?? 0, 0, 75);
        void ApplyPrison() => form.Edit(() => form.Editor.Contents.SetPrison(prison.Id, who.Value(), level.Value == 0 ? null : (int)level.Value));
        who.Button.ItemSelected += _ => ApplyPrison();
        level.ValueChanged += _ => ApplyPrison();
    }
}

/// <summary>An artifact pickup's artifact (or one of its tier drawn at start), and a scroll's spell (ADR-136).</summary>
internal sealed class ArtifactPickupInspector : InspectorSection<ArtifactPickup>
{
    protected override void Show(InspectorForm form, ArtifactPickup pickup)
    {
        var row = new HBoxContainer();
        form.Box.AddChild(row);
        var artifacts = new[] { ($"Random ({pickup.Definition.Name}, drawn at start)", (string?)null) }
            .Concat(form.Scenario.Artifacts.All.OrderBy(a => a.Tier).ThenBy(a => a.Name).Select(a => ($"{a.Name} ({a.Tier})", (string?)a.Id)));
        var catalog = form.Scenario.Artifacts;
        var artifact = row.AddChoice("Artifact", artifacts, pickup.Artifact is { } current ? ArtifactCatalog.BaseOf(current) : null);
        artifact.Button.ItemSelected += _ => form.Edit(() => form.Editor.Contents.SetArtifactPickup(pickup.Id, artifact.Value()));

        // A spell scroll (ADR-136): the spell written on it, or a random one of its level drawn at game start.
        if (pickup.Artifact is { } chosen && catalog.TryGet(chosen, out var scroll) && scroll.ScrollLevel is { } level)
        {
            var spellRow = new HBoxContainer();
            form.Box.AddChild(spellRow);
            var spells = new[] { ($"Random level {level} spell (drawn at start)", (string?)null) }
                .Concat(catalog.ScrollSpells(level).Select(id => catalog.Spells.Get(id)).OrderBy(s => s.Name).Select(s => ($"{s.Name} ({s.School})", (string?)s.Id)));
            var spell = spellRow.AddChoice("Spell", spells, scroll.Spell);
            spell.Button.ItemSelected += _ => form.Edit(() => form.Editor.Contents.SetArtifactPickup(pickup.Id,
                spell.Value() is { } written ? ArtifactCatalog.WithSpell(chosen, written) : ArtifactCatalog.BaseOf(chosen)));
        }
    }
}

/// <summary>A witch hut's two skills, both set or both rolled at start.</summary>
internal sealed class WitchHutInspector : InspectorSection<WitchHut>
{
    protected override void Show(InspectorForm form, WitchHut hut)
    {
        var row = new HBoxContainer();
        form.Box.AddChild(row);
        var skills = new[] { ("Rolled at start", "") }.Concat(BuiltInSkills.All.OrderBy(s => s.Group).ThenBy(s => s.Name).Select(s => ($"{s.Name} ({s.Group})", s.Id))).ToList();
        var first = row.AddChoice("Skills", skills, hut.Skills.ElementAtOrDefault(0) ?? "");
        var second = row.AddChoice("and", skills, hut.Skills.ElementAtOrDefault(1) ?? "");
        form.Box.AddChild(new Label { Text = "Set both, or leave both rolled; the two must differ." });
        void ApplyHut()
        {
            // Both or neither: a half-set hut waits for its second skill.
            if ((first.Value().Length > 0) == (second.Value().Length > 0))
            {
                form.Edit(() => form.Editor.Contents.SetWitchHutSkills(hut.Id, first.Value().Length > 0 ? [first.Value(), second.Value()] : []));
            }
        }

        first.Button.ItemSelected += _ => ApplyHut();
        second.Button.ItemSelected += _ => ApplyHut();
    }
}

/// <summary>A shrine's spell (one of its level, or drawn at start).</summary>
internal sealed class ShrineInspector : InspectorSection<Shrine>
{
    protected override void Show(InspectorForm form, Shrine shrine)
    {
        var row = new HBoxContainer();
        form.Box.AddChild(row);
        var spells = new[] { ("Drawn at start", (string?)null) }
            .Concat(ShrineRules.Candidates(form.Scenario.Spells, shrine.SpellLevel).Select(s => ($"{s.Name} ({s.School})", (string?)s.Id)));
        var spell = row.AddChoice("Spell", spells, shrine.Spell);
        spell.Button.ItemSelected += _ => form.Edit(() => form.Editor.Contents.SetShrineSpell(shrine.Id, spell.Value()));
    }
}

/// <summary>A treasure chest's fixed gold and experience, or rolled values.</summary>
internal sealed class TreasureChestInspector : InspectorSection<TreasureChest>
{
    protected override void Show(InspectorForm form, TreasureChest chest)
    {
        var row = new HBoxContainer();
        form.Box.AddChild(row);
        var fixedValues = new CheckBox { Text = "Fixed values (else rolled)", ButtonPressed = chest.FixedValues is not null };
        row.AddChild(fixedValues);
        var gold = row.AddSpin("Gold", chest.FixedValues?.Gold ?? 1000, 0, 100000);
        var experience = row.AddSpin("XP", chest.FixedValues?.Experience ?? 500, 0, 100000);
        void ApplyChest() => form.Edit(() => form.Editor.Contents.SetChestValues(
            chest.Id, fixedValues.ButtonPressed ? new ChestValues((int)gold.Value, (int)experience.Value) : null));
        fixedValues.Toggled += _ => ApplyChest();
        gold.ValueChanged += _ => ApplyChest();
        experience.ValueChanged += _ => ApplyChest();
    }
}

/// <summary>A two-way gate's link (changed with the LinkGates tool).</summary>
internal sealed class TwoWayGateInspector : InspectorSection<TwoWayGate>
{
    protected override void Show(InspectorForm form, TwoWayGate gate) => GateLink.Show(form, gate);
}

/// <summary>A one-way gate's exit (changed with the LinkGates tool).</summary>
internal sealed class OneWayGateInspector : InspectorSection<OneWayGateEntrance>
{
    protected override void Show(InspectorForm form, OneWayGateEntrance gate) => GateLink.Show(form, gate);
}

/// <summary>Where a gate leads.</summary>
internal static class GateLink
{
    public static void Show(InspectorForm form, MapObject gate) =>
        form.Box.AddChild(new Label { Text = gate.TeleportTarget is { } t ? $"Linked to #{t.Value} (use the LinkGates tool to change)" : "Not linked (use the LinkGates tool)" });
}

/// <summary>A resource pile's fixed amount, or that it is rolled.</summary>
internal sealed class ResourcePileInspector : InspectorSection<ResourcePile>
{
    protected override void Show(InspectorForm form, ResourcePile pile) =>
        form.Box.AddChild(new Label { Text = pile.Amount is { } fixedAmount ? $"{fixedAmount} {pile.Resource}" : $"{pile.Resource}, amount rolled at game start" });
}

/// <summary>The arena place (ADR-097): what it means for the battle of day 2.</summary>
internal sealed class CombatPlaceInspector : InspectorSection<CombatPlace>
{
    protected override void Show(InspectorForm form, CombatPlace place) =>
        form.Box.AddChild(new Label
        {
            Text = $"The arena battle of day 2 (ADR-097): the defender stands on the entrance {place.VisitPosition}, the attacker on "
                   + $"{place.AttackCell}, and the battle is fought on that cell's ground. A hero standing on the entrance at the end "
                   + $"of day 1 makes its owner the defender.{(form.Scenario.Rules.Arena ? "" : " The map is not an arena yet (conditions tab).")}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(UiTheme.InspectorNoteWidth, 0),
        });
}

/// <summary>A quest object's quest: one of the map's quest events.</summary>
internal sealed class QuestSiteInspector : InspectorSection<QuestSite>
{
    protected override void Show(InspectorForm form, QuestSite site) =>
        form.EventChoice("Quest", MapEventKind.Quest, site.Quest, name => form.Editor.Contents.SetQuestSiteQuest(site.Id, name));
}
