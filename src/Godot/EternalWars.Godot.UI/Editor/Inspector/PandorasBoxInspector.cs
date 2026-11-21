using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Editor.Inspector;

/// <summary>
/// Pandora's Boxes (ADR-100): for one box what it gives, its guard and the message shown when it opens; for one or several the hero
/// event they run. Skills, artifacts, creatures and guard stacks list what the box has and one empty row to add another; picking
/// "none" removes an entry.
/// </summary>
internal sealed class PandorasBoxInspector : InspectorSection<PandorasBox>
{
    protected override void Show(InspectorForm form, IReadOnlyList<PandorasBox> boxes)
    {
        form.In(InspectorPart.Contents);
        if (form.Single(boxes, "the contents, guard and message") is { } box)
        {
            ShowContents(form, box);
        }

        form.EventChoice("Runs the hero event", MapEventKind.Hero, ObjectProperties.BoxEvent.Shared(boxes), name => ObjectProperties.BoxEvent.Apply(form.Editor, boxes, name));
    }

    private static void ShowContents(InspectorForm form, PandorasBox box)
    {
        var panel = form.In(InspectorPart.Contents);
        var c = box.Contents;
        var changed = new List<Action<Action>>();
        panel.AddHeading("Gives (hidden from the players until opened; an empty box is an error)");
        var resourceRow = new HFlowContainer();
        panel.AddChild(resourceRow);
        var resources = Enum.GetValues<ResourceType>().Select(r => (Resource: r, Spin: resourceRow.AddSpin(r.ToString(), c.Resources[r], 0, 1000000))).ToList();
        var heroRow = new HFlowContainer();
        panel.AddChild(heroRow);
        var experience = heroRow.AddSpin("XP", c.Experience, 0, 1000000);
        var mana = heroRow.AddSpin("Mana", c.Mana, 0, 999);
        var morale = heroRow.AddSpin("Morale (next battle)", c.Morale, 0, PandorasBoxRules.MaxMoraleOrLuck);
        var luck = heroRow.AddSpin("Luck (next battle)", c.Luck, 0, PandorasBoxRules.MaxMoraleOrLuck);
        var statRow = new HFlowContainer();
        panel.AddChild(statRow);
        var stats = Enum.GetValues<PrimaryStat>().Select(s => (Stat: s, Spin: statRow.AddSpin(s.ToString(), c.Stat(s), 0, 99))).ToList();
        foreach (var spin in resources.Select(r => r.Spin).Concat(stats.Select(s => s.Spin)).Concat([experience, mana, morale, luck]))
        {
            changed.Add(apply => spin.ValueChanged += _ => apply());
        }

        panel.AddHeading("Skills (raised to at least this level)");
        List<PickerItem> skillItems = [PickerItem.None("— none —"), .. InspectorForm.SkillItems()];
        var levelItems = Enum.GetValues<SkillLevel>().Where(l => l > SkillLevel.None).Select(l => (l.ToString(), l)).ToList();
        var skills = c.Skills.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => (s.Key, s.Value)).Append(("", SkillLevel.Basic)).Select(entry =>
        {
            var row = new HBoxContainer();
            panel.AddChild(row);
            var skill = PicturePicker.Add(row, "Skill", skillItems, entry.Item1);
            var level = row.AddChoice("at", levelItems, entry.Item2);
            changed.Add(apply => skill.Picked += _ => apply());
            changed.Add(apply => level.Button.ItemSelected += _ => apply());
            return (Skill: skill, Level: level);
        }).ToList();

        panel.AddHeading("Artifacts");
        List<PickerItem> artifactItems = [PickerItem.None("— none —"), .. form.ArtifactItems()];
        var artifacts = c.Artifacts.Select(a => a.Value).Append("").Select(id =>
        {
            var row = new HBoxContainer();
            panel.AddChild(row);
            var artifact = PicturePicker.Add(row, "Artifact", artifactItems, id);
            changed.Add(apply => artifact.Picked += _ => apply());
            return artifact;
        }).ToList();

        var messages = form.In(InspectorPart.Messages);
        var messageRow = new HBoxContainer();
        messages.AddChild(messageRow);
        messageRow.AddChild(new Label { Text = "Message" });
        var message = new LineEdit { Text = box.Message ?? "", CustomMinimumSize = new Vector2(UiTheme.InspectorTextWidth, 0), PlaceholderText = "Shown when the box opens (optional)" };
        messageRow.AddChild(message);

        List<(PicturePicker Creature, SpinBox Count)>? joining = null, guard = null;
        void Changed() => Apply();
        joining = form.StackRows(panel, "Creatures that join the hero", c.Creatures, Changed);
        guard = form.StackRows(form.In(InspectorPart.Guard), "Fought after \"open?\"; none = unguarded", box.Guard, Changed);

        void Apply() => form.Edit(() => form.Editor.Contents.SetPandorasBox(
            box.Id,
            new BoxContents
            {
                Resources = resources.Aggregate(ResourceSet.Empty, (sum, r) => sum + ResourceSet.Of(r.Resource, (int)r.Spin.Value)),
                Experience = (int)experience.Value,
                Mana = (int)mana.Value,
                Morale = (int)morale.Value,
                Luck = (int)luck.Value,
                Attack = (int)stats.First(s => s.Stat == PrimaryStat.Attack).Spin.Value,
                Defense = (int)stats.First(s => s.Stat == PrimaryStat.Defense).Spin.Value,
                SpellPower = (int)stats.First(s => s.Stat == PrimaryStat.SpellPower).Spin.Value,
                Knowledge = (int)stats.First(s => s.Stat == PrimaryStat.Knowledge).Spin.Value,
                Skills = skills.Where(s => s.Skill.Value().Length > 0).GroupBy(s => s.Skill.Value()).ToDictionary(g => g.Key, g => g.Last().Level.Value()),
                Artifacts = [.. artifacts.Select(a => a.Value()).Where(a => a.Length > 0)],
                Creatures = InspectorForm.Stacks(joining!),
            },
            InspectorForm.Stacks(guard!),
            message.Text));

        foreach (var hook in changed)
        {
            hook(Apply);
        }

        message.TextSubmitted += _ => Apply();
        message.FocusExited += Apply;
    }
}
