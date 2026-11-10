using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Heroes;

/// <summary>
/// A hero joining a player during the game: hired in a town or a map tavern, freed from a prison, or given by a map event. The callers check
/// whether it may and publish what happened.
/// </summary>
public sealed class HeroRecruitment(GameState state, ProgressionService progression)
{
    /// <summary>
    /// A roster hero joins <paramref name="owner"/> at <paramref name="position"/>: with <paramref name="experience"/> (its level-ups chosen at
    /// once), with its starter stack when hired (<see cref="TavernRules.StarterStack"/>), and moving today or from its owner's next turn on.
    /// </summary>
    internal Hero Join(PlayerId owner, MapPosition position, HeroDefinition definition, int experience = 0, bool hired = false, bool movesToday = false)
    {
        var hero = Hero.Create(state.NextUnusedHeroId(), owner, position, null, definition);
        hero.Experience = experience;
        return Enlist(hero, definition, hired, movesToday, levelUp: true);
    }

    /// <summary>A retired hero hired again (ADR-081): back as it left, at <paramref name="position"/>, with a starter stack if it has no army, moving today.</summary>
    internal Hero Return(Hero returning, MapPosition position, HeroDefinition definition)
    {
        state.Unretire(returning);
        returning.Position = position;
        return Enlist(returning, definition, hired: true, movesToday: true, levelUp: false);
    }

    private Hero Enlist(Hero hero, HeroDefinition definition, bool hired, bool movesToday, bool levelUp)
    {
        if (hired && hero.Army.IsEmpty && TavernRules.StarterStack(state, definition) is { } starter)
        {
            hero.Army.TryAdd(starter);
        }

        state.AddHero(hero);
        if (levelUp)
        {
            progression.LevelUpAutomatically(hero);
        }

        hero.MaxMovementPoints = MovementAllowance.Daily(state, hero);
        hero.MovementPoints = movesToday ? hero.MaxMovementPoints : 0;
        return hero;
    }
}
