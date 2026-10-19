using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Objects;

/// <summary>What a buff site gives (ADR-068); the definition's <see cref="MapObjectDefinition.Buff"/>.</summary>
public enum BuffRule
{
    /// <summary>+1 luck for the next battle.</summary>
    FaerieRing,

    /// <summary>+400 movement points now and +1 morale for the next battle.</summary>
    FountainOfYouth,

    /// <summary>+2 morale for the next battle, +3 on day 7 of the week.</summary>
    Temple,

    /// <summary>+1 luck for the next battle and +600 movement points now.</summary>
    MineralSpring,

    /// <summary>+400 movement points now and every day until the week ends; Lancers become Champions for free.</summary>
    Stables,

    /// <summary>Mana back to full (useful with magic, Phase 5).</summary>
    MagicWell,
}

/// <summary>
/// A temporary buff on the adventure map (ADR-068): Faerie Ring, Fountain of Youth, Temple, Mineral Spring, Stables, Magic Well. Buffs of different
/// sites stack within the ±5 battle cap; visiting the same site again renews its own buff (MAP-10). The definition's visit scope stops a hero from
/// taking it twice a day (the Stables: twice a week).
/// </summary>
public sealed class BuffSite(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.Buff)
{
    public BuffRule Rule => Definition.Buff!.Value;
}

/// <summary>The numbers of <see cref="BuffSite"/>s (ADR-068).</summary>
public static class BuffRules
{
    public const int FountainMovement = 400;

    public const int SpringMovement = 600;

    public const int StablesMovement = 400;

    /// <summary>The creature the Stables upgrade for free (the notes' Cavalier) into its upgrade.</summary>
    public const string StablesCreature = "haven.lancer";

    /// <summary>The statuses a visit on <paramref name="date"/> gives, each lasting as the notes say.</summary>
    public static IReadOnlyList<HeroStatus> Statuses(MapObjectDefinition definition, GameDate date)
    {
        var source = definition.Id;
        return definition.Buff switch
        {
            BuffRule.FaerieRing => [new(source, HeroStatusKind.Luck, 1, StatusExpiry.NextBattle)],
            BuffRule.FountainOfYouth => [new(source, HeroStatusKind.Morale, 1, StatusExpiry.NextBattle)],
            BuffRule.Temple => [new(source, HeroStatusKind.Morale, date.Day == GameDate.DaysPerWeek ? 3 : 2, StatusExpiry.NextBattle)],
            BuffRule.MineralSpring => [new(source, HeroStatusKind.Luck, 1, StatusExpiry.NextBattle)],
            BuffRule.Stables => [new(source, HeroStatusKind.Movement, StablesMovement, StatusExpiry.EndOfWeek)],
            _ => [],
        };
    }

    /// <summary>Movement points added at once (the rest of today's move).</summary>
    public static int MovementNow(BuffRule rule) => rule switch
    {
        BuffRule.FountainOfYouth => FountainMovement,
        BuffRule.MineralSpring => SpringMovement,
        BuffRule.Stables => StablesMovement,
        _ => 0,
    };
}
