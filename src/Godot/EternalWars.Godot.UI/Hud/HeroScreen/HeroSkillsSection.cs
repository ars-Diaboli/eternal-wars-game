using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud.HeroScreen;

/// <summary>The skills by group: each with its level (and what artifacts add), its description and its icon.</summary>
internal sealed class HeroSkillsSection(HeroPanel panel) : HeroSection(panel)
{
    public VBoxContainer Rows { get; } = new();

    protected override Node Anchor => Rows;

    public override void Show(Hero hero)
    {
        Rows.ClearChildren();
        var any = false;
        foreach (var group in BuiltInSkills.Groups)
        {
            // Skills the hero learned, plus ones an artifact adds.
            var rows = BuiltInSkills.SkillsOf(group.Id).Where(s => hero.SkillLevelOf(s.Id) > SkillLevel.None).ToList();
            if (rows.Count == 0)
            {
                continue;
            }

            any = true;
            var points = rows.Sum(s => (int)hero.Skills.Level(s.Id));
            Rows.AddChild(new Label { Text = $"{group.Name} ({points}/{SkillGroup.Points})" });
            foreach (var skill in rows)
            {
                var id = skill.Id;
                var level = hero.SkillLevelOf(skill.Id);
                var learned = hero.Skills.Level(skill.Id);
                var text = new Label
                {
                    Text = $"{skill.Name}: {level}" + (level != learned ? $" (learned {learned})" : "") + $" of {skill.MaxLevel}\n    {skill.Description}",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    CustomMinimumSize = new Vector2(UiTheme.ScreenTextWidth, 0),
                    MouseFilter = Control.MouseFilterEnum.Pass,
                };
                var row = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Stop };
                row.AddThemeConstantOverride("margin_left", UiSpacing.Section);
                row.AddChild(HeroIcons.WithSkillIcon(id, text));
                Rows.AddChild(row.WithInfo(detailed => InfoSheets.Skill(id, level, detailed, hero, learned)));
            }
        }

        if (!any)
        {
            Rows.AddChild(new Label { Text = "    No skills yet." });
        }
    }

    /// <summary>A level-up or a box teaches skills; artifacts add to them.</summary>
    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) =>
        OnArtifacts(refresh, events)
            .On<HeroLeveledUp>(events)
            .On<PandorasBoxOpened>(events);
}
