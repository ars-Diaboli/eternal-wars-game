namespace EternalWars.Core.Domain.Battles;

/// <summary>The colour band of a stack's health bar (ADR-145).</summary>
public enum HealthBand
{
    /// <summary>Over 50 % of the creature's full health.</summary>
    Green,

    /// <summary>21–50 %.</summary>
    Yellow,

    /// <summary>20 % and below.</summary>
    Red,
}

/// <summary>A stack's health bar in battle (ADR-145): the top creature's hit points against its full health.</summary>
public static class StackHealth
{
    /// <summary>How full the bar is, 0 to 1.</summary>
    public static float Fraction(int topHealth, int fullHealth) =>
        fullHealth <= 0 ? 0f : Math.Clamp(topHealth / (float)fullHealth, 0f, 1f);

    /// <summary>Green over 50 %, yellow over 20 %, red at 20 % and below.</summary>
    public static HealthBand Band(int topHealth, int fullHealth) =>
        topHealth * 2 > fullHealth ? HealthBand.Green
        : topHealth * 5 > fullHealth ? HealthBand.Yellow
        : HealthBand.Red;
}
