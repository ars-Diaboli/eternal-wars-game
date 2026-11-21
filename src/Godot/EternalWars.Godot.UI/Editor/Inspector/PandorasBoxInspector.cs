using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Editor.Inspector;

/// <summary>
/// A Pandora's Box (ADR-100): what it gives, its guard and the message shown when it opens, then the hero event it runs. Skills, artifacts,
/// creatures and guard stacks list what the box has and one empty row to add another; picking "none" removes an entry.
/// </summary>
internal sealed class PandorasBoxInspector : InspectorSection<PandorasBox>
{
    protected override void Show(InspectorForm form, PandorasBox box)
    {
        ShowContents(form, box);
        form.EventChoice("Runs the hero event", MapEventKind.Hero, box.Event, name => form.Editor.Contents.SetPandorasBoxEvent(box.Id, name));
    }

    private static void ShowContents(InspectorForm form, PandorasBox box)
    {
        var panel = form.In(InspectorPart.Contents);
        var scenario = form.Scenario;
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
        var skillItems = new[] { ("— none —", "") }.Concat(BuiltInSkills.All.OrderBy(s => s.Name).Select(s => (s.Name, s.Id))).ToList();
        var levelItems = Enum.GetValues<SkillLevel>().Where(l => l > SkillLevel.None).Select(l => (l.ToString(), l)).ToList();
        var skills = c.Skills.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => (s.Key, s.Value)).Append(("", SkillLevel.Basic)).Select(entry =>
        {
            var row = new HBoxContainer();
            panel.AddChild(row);
            var skill = row.AddChoice("Skill", skillItems, entry.Item1);
            var level = row.AddChoice("at", levelItems, entry.Item2);
            changed.Add(apply => skill.Button.ItemSelected += _ => apply());
            changed.Add(apply => level.Button.ItemSelected += _ => apply());
            return (Skill: skill, Level: level);
        }).ToList();

        panel.AddHeading("Artifacts");
        var artifactItems = new[] { ("— none —", "") }.Concat(scenario.Artifacts.All.OrderBy(a => a.Tier).ThenBy(a => a.Name).Select(a => ($"{a.Name} ({a.Tier})", a.Id.Value))).ToList();
        var artifacts = c.Artifacts.Select(a => a.Value).Append("").Select(id =>
        {
            var row = new HBoxContainer();
            panel.AddChild(row);
            var artifact = row.AddChoice("Artifact", artifactItems, id);
            changed.Add(apply => artifact.Button.ItemSelected += _ => apply());
            return artifact;
        }).ToList();

        var stackItems = new[] { ("— none —", "") }.Concat(form.Creatures()).ToList();
        List<(Choice<string> Creature, SpinBox Count)> StackRows(string title, IReadOnlyList<CreatureStack> stacks)
        {
            panel.AddChild(new Label { Text = title });
            var shown = stacks.Count < Army.SlotCount ? stacks.Append(null) : stacks.Cast<CreatureStack?>();
            return shown.Select(stack =>
            {
                var row = new HBoxContainer();
                panel.AddChild(row);
                var creature = row.AddChoice("", stackItems, stack?.Creature.Value ?? "");
                var count = row.AddSpin("×", stack?.Count ?? 1, 1, 99999);
                changed.Add(apply => creature.Button.ItemSelected += _ => apply());
                changed.Add(apply => count.ValueChanged += _ => apply());
                return (creature, count);
            }).ToList();
        }

        var joining = StackRows("Creatures that join the hero", c.Creatures);
        panel = form.In(InspectorPart.Guard);
        var guard = StackRows("Fought after \"open?\"; none = unguarded", box.Guard);

        panel = form.In(InspectorPart.Messages);
        var messageRow = new HBoxContainer();
        panel.AddChild(messageRow);
        messageRow.AddChild(new Label { Text = "Message" });
        var message = new LineEdit { Text = box.Message ?? "", CustomMinimumSize = new Vector2(UiTheme.InspectorTextWidth, 0), PlaceholderText = "Shown when the box opens (optional)" };
        messageRow.AddChild(message);

        static List<CreatureStack> Read(List<(Choice<string> Creature, SpinBox Count)> rows) =>
            [.. rows.Where(r => r.Creature.Value().Length > 0).Select(r => new CreatureStack(r.Creature.Value(), (int)r.Count.Value))];

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
                Creatures = Read(joining),
            },
            Read(guard),
            message.Text));

        foreach (var hook in changed)
        {
            hook(Apply);
        }

        message.TextSubmitted += _ => Apply();
        message.FocusExited += Apply;
    }
}
