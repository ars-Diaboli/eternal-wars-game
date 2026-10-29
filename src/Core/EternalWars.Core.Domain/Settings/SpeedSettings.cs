namespace EternalWars.Core.Domain.Settings;

/// <summary>How fast an animation plays (ADR-090, UI-1); <see cref="Normal"/> is the speed the views were made with.</summary>
public enum AnimationSpeed
{
    Slow,
    Normal,
    Fast,
    Instant,
}

/// <summary>
/// The speed settings (ADR-090, UI-1): the human players' heroes on the map, the computer players' heroes and battle animations. They change no
/// rule: they are kept per computer in a settings file, not in saves. <see cref="ShowComputerHeroes"/> false is "don't show": computer turns play
/// out unseen, the map stays with the last human player.
/// </summary>
public sealed record SpeedSettings(
    AnimationSpeed OwnHeroes = AnimationSpeed.Normal,
    AnimationSpeed ComputerHeroes = AnimationSpeed.Normal,
    bool ShowComputerHeroes = true,
    AnimationSpeed Battle = AnimationSpeed.Normal)
{
    public static SpeedSettings Default { get; } = new();

    /// <summary>How many times faster than normal an animation plays; 0 for <see cref="AnimationSpeed.Instant"/> (no animation).</summary>
    public static double Scale(AnimationSpeed speed) => speed switch
    {
        AnimationSpeed.Slow => 0.5,
        AnimationSpeed.Fast => 2,
        AnimationSpeed.Instant => 0,
        _ => 1,
    };

    /// <summary>How long a step that takes <paramref name="normalSeconds"/> at normal speed takes; 0 = it happens at once.</summary>
    public static double Seconds(AnimationSpeed speed, double normalSeconds) => Scale(speed) is var scale and > 0 ? normalSeconds / scale : 0;

    /// <summary>The speed for a hero of a human (<paramref name="human"/>) or a computer player.</summary>
    public AnimationSpeed ForHeroOf(bool human) => human ? OwnHeroes : ComputerHeroes;
}
