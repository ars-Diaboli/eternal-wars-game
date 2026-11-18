using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Raises a secondary skill of the hero, up to its maximum, while its group has a free slot (EVT-4 a).</summary>
internal sealed class SkillHandler : HeroActionHandler<SkillAction>
{
    protected override void ApplyTo(MapEventActionContext context, Hero hero, SkillAction action)
    {
        if (!BuiltInSkills.Contains(action.Skill))
        {
            return;
        }

        var id = BuiltInSkills.Resolve(action.Skill);
        var level = (SkillLevel)Math.Min((int)action.Level, (int)BuiltInSkills.Get(id).MaxLevel);
        if (hero.Skills.Level(id) < level && hero.Skills.CanRaise(id))
        {
            hero.Skills.Set(id, level);
            context.Events.Publish(new MapEventSkillGained(hero.Id, id, level));
        }
    }

    protected override string Describe(GameState state, SkillAction action) =>
        $"{(BuiltInSkills.Contains(action.Skill) ? BuiltInSkills.Get(action.Skill).Name : action.Skill)} ({action.Level})";
}
