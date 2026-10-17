using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Generation;

/// <summary>
/// Army power (<c>count × creature value</c>, see <see cref="GuardPicker"/>) that a zone's monster strength means for the things it guards.
/// HoMM3-style placeholders to tune by play-testing: mines of rarer resources are guarded harder, a neutral town's garrison is heavier
/// than a mine's guard.
/// </summary>
public static class ZoneGuards
{
    /// <summary>The guard of a mine, or 0 for none: gold and the rare resources are worth more than lumber and ore.</summary>
    public static int MineGuardValue(MonsterStrength strength, ResourceType resource)
    {
        var baseValue = Base(strength, 800, 2000, 4500);
        return resource switch
        {
            ResourceType.Gold => baseValue * 3 / 2,
            ResourceType.Lumber or ResourceType.Ore => baseValue,
            _ => baseValue * 5 / 4,
        };
    }

    /// <summary>The garrison of a neutral town (two stacks share it). A town is never left empty: a zone without monsters gets the weak garrison.</summary>
    public static int TownGarrisonValue(MonsterStrength strength) => Base(strength == MonsterStrength.None ? MonsterStrength.Weak : strength, 1500, 4000, 9000);

    private static int Base(MonsterStrength strength, int weak, int average, int strong) => strength switch
    {
        MonsterStrength.None => 0,
        MonsterStrength.Weak => weak,
        MonsterStrength.Average => average,
        _ => strong,
    };
}
