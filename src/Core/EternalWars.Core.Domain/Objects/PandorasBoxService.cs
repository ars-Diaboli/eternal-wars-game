using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Objects;

/// <summary>Opening a Pandora's Box (ADR-100, MAP-35): after the hero said yes to an unguarded box, or won against its guard.</summary>
public sealed class PandorasBoxService(ObjectInteractionContext context, ProgressionService progression, ArtifactService artifacts)
{
    /// <summary>
    /// Opens the box for the hero: it vanishes; resources, primary stats, mana (up to twice the maximum), morale and luck for the next battle, skills
    /// (lost where the hero has no room for their group), artifacts (lost to a full backpack) and creatures (lost without a slot) go to the hero; then
    /// <see cref="PandorasBoxOpened"/> with what it got; the experience comes last, for its level-ups.
    /// </summary>
    internal void Open(Hero hero, PandorasBox box)
    {
        var events = context.Events;
        context.RemoveFromMap(box);
        var contents = box.Contents;
        if (!contents.Resources.IsEmpty)
        {
            context.Economy.Grant(hero.Owner, contents.Resources);
        }

        foreach (var stat in Enum.GetValues<PrimaryStat>().Where(s => contents.Stat(s) > 0))
        {
            progression.RaiseStat(hero, stat, contents.Stat(stat));
        }

        if (contents.Mana > 0 && Math.Min(2 * hero.MaxMana, hero.Mana + contents.Mana) is var mana && mana > hero.Mana)
        {
            hero.Mana = mana;
            events.Publish(new HeroManaRestored(hero.Id, hero.Mana, hero.MaxMana));
        }

        if (contents.Morale > 0)
        {
            progression.GrantStatus(hero, new HeroStatus(box.Definition.Id, HeroStatusKind.Morale, contents.Morale, StatusExpiry.NextBattle));
        }

        if (contents.Luck > 0)
        {
            progression.GrantStatus(hero, new HeroStatus(box.Definition.Id, HeroStatusKind.Luck, contents.Luck, StatusExpiry.NextBattle));
        }

        var skills = new Dictionary<string, SkillLevel>(StringComparer.Ordinal);
        foreach (var (skill, level) in contents.Skills.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            if (hero.Skills.Level(skill) < level && hero.Skills.CanRaise(skill))
            {
                hero.Skills.Set(skill, level);
                skills[skill] = level;
            }
        }

        var found = contents.Artifacts.Where(a => artifacts.Grant(hero, a, ArtifactSource.Found)).ToList();
        var joined = contents.Creatures.Where(s => hero.Army.TryAdd(s)).ToList();
        if (joined.Count > 0)
        {
            events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
        }

        events.Publish(new PandorasBoxOpened(box.Id, hero.Id, contents with { Skills = skills, Artifacts = found, Creatures = joined }, box.Message) { Event = box.Event });
        progression.GrantExperience(hero, contents.Experience);
    }
}
