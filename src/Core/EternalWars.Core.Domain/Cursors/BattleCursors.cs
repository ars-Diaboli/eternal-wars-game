using EternalWars.Core.Domain.Battles;

namespace EternalWars.Core.Domain.Cursors;

/// <summary>Which cursor the battlefield shows (ADR-204), from what pointing there means (<see cref="BattleTargeting.ActionAt"/>).</summary>
public static class BattleCursors
{
    /// <summary>A shot that deals this many eighths less, or more, shows the weak-shot cursor: half or less.</summary>
    public const int WeakShotPenaltyEighths = 4;

    /// <summary>
    /// The cursor over the battlefield. <paramref name="acting"/>: whether the side whose turn it is is played here (else the computer or
    /// another person is acting: busy). <paramref name="armedSpell"/>: whether a spell waits for its target, and whether it hits an area
    /// (null: none armed). <paramref name="action"/> is what a click would do (<see cref="BattleTargeting.ActionAt"/>).
    /// </summary>
    public static GameCursor For(BattleState battle, bool acting, bool? armedSpell, BattleAction? action)
    {
        if (!acting || battle.Phase != BattlePhase.Fighting || battle.ActiveUnit is not { } active)
        {
            return GameCursor.Busy;
        }

        if (armedSpell is { } area)
        {
            return area ? GameCursor.SpellArea : GameCursor.Spell;
        }

        return action switch
        {
            ShootAction shoot when battle.FindUnit(shoot.Target) is { } target =>
                BattleGrid.CanShoot(battle, active, target, out var penalty) && penalty >= WeakShotPenaltyEighths ? GameCursor.ShootWeak : GameCursor.Shoot,
            ShootStructureAction => GameCursor.Siege,
            AttackAction or AttackStructureAction when BattleTargeting.SideOf(battle, action) is { } side => Melee(side),
            MoveAction => active.Creature.IsTeleporting ? GameCursor.Teleport : active.Creature.IsFlying ? GameCursor.Fly : GameCursor.Walk,
            _ => GameCursor.Blocked,
        };
    }

    /// <summary>The melee cursor for a blow from <paramref name="side"/>.</summary>
    public static GameCursor Melee(CompassSide side) => side switch
    {
        CompassSide.North => GameCursor.MeleeFromNorth,
        CompassSide.NorthEast => GameCursor.MeleeFromNorthEast,
        CompassSide.East => GameCursor.MeleeFromEast,
        CompassSide.SouthEast => GameCursor.MeleeFromSouthEast,
        CompassSide.South => GameCursor.MeleeFromSouth,
        CompassSide.SouthWest => GameCursor.MeleeFromSouthWest,
        CompassSide.West => GameCursor.MeleeFromWest,
        _ => GameCursor.MeleeFromNorthWest,
    };
}
