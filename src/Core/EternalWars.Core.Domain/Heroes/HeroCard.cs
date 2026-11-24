using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Heroes;

/// <summary>
/// The adventure map's card of the selected hero (ADR-309): portrait, level, name and the four primary stats with their bonuses; three
/// bars (movement, mana, experience towards the next level); morale and luck as the hero's own totals, as its information window shows
/// them (the army's effect is the battle's); the army strip's
/// <see cref="Army.SlotCount"/> slots, empty ones null. A snapshot: build a new one when the hero changes.
/// </summary>
public sealed record HeroCard(
    HeroId Hero,
    string Name,
    string? Portrait,
    int Level,
    int Attack,
    int Defense,
    int SpellPower,
    int Knowledge,
    Gauge Movement,
    Gauge Mana,
    Gauge Experience,
    int Morale,
    int Luck,
    IReadOnlyList<CreatureStack?> Slots)
{
    public static HeroCard Of(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);
        return new HeroCard(
            hero.Id,
            hero.Name,
            hero.PortraitHero,
            hero.Level,
            hero.TotalAttack,
            hero.TotalDefense,
            hero.TotalSpellPower,
            hero.TotalKnowledge,
            new Gauge(hero.MovementPoints, hero.MaxMovementPoints),
            new Gauge(hero.Mana, hero.MaxMana),
            ExperienceOf(hero),
            hero.TotalMorale,
            hero.TotalLuck,
            [.. hero.Army.Slots]);
    }

    /// <summary>
    /// The experience gained within the hero's shown <see cref="Hero.Level"/>, out of what that level spans: empty at its start, and short
    /// of full by <see cref="TrainingRules.ExperienceToNextLevel"/>. Full while level-ups wait to be chosen, and at the table's last level.
    /// </summary>
    public static Gauge ExperienceOf(Hero hero)
    {
        ArgumentNullException.ThrowIfNull(hero);
        var start = ExperienceTable.For(hero.Level);
        var span = ExperienceTable.For(hero.Level + 1) - start;
        return span <= 0 ? new Gauge(1, 1) : new Gauge(Math.Clamp(hero.Experience - start, 0, span), span);
    }
}

/// <summary>One of the hero card's bars: <see cref="Value"/> out of <see cref="Max"/>.</summary>
public readonly record struct Gauge(int Value, int Max)
{
    /// <summary>How full the bar is, 0 to 1: a value above the maximum (extra movement, a Mana Vortex) fills it; no maximum leaves it empty.</summary>
    public double Fraction => Max <= 0 ? 0 : Math.Clamp((double)Value / Max, 0, 1);
}
