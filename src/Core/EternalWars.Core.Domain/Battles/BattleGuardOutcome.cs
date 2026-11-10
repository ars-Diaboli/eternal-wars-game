using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// The end of a battle as an <see cref="IBattleGuard"/> sees it: the finished battle, the guard's side, and the game's services it may change the
/// map with (publishing after mutating).
/// </summary>
internal sealed class BattleGuardOutcome(
    GameState state, IEventPublisher events, EconomyService economy, ProgressionService progression, ArtifactService artifacts, BattleState battle,
    BattleSide side)
{
    public GameState State => state;

    public IEventPublisher Events => events;

    public EconomyService Economy => economy;

    public ProgressionService Progression => progression;

    public ArtifactService Artifacts => artifacts;

    /// <summary>The guard's creatures that lived through the battle (its summoned ones do not count).</summary>
    public int Survivors => battle.OwnedArmyOf(side).Where(u => !u.IsSummoned).Sum(u => u.KeptCount);

    /// <summary>The troops of a town or a map garrison keep what lived, slot by slot.</summary>
    public void KeepTroops(MapObjectId holder, Army troops) => KeepTroops(battle, side, events, holder, troops);

    /// <summary>The troops of a town or a map garrison (<paramref name="holder"/>) keep what lived on <paramref name="side"/>, slot by slot.</summary>
    internal static void KeepTroops(BattleState battle, BattleSide side, IEventPublisher events, MapObjectId holder, Army troops)
    {
        foreach (var (slot, stack) in troops.Stacks.ToList())
        {
            var survivors = battle.Units.Where(u => u.Owner == side && u.SourceSlot == slot).Sum(u => u.KeptCount);
            troops.Set(slot, survivors > 0 ? new CreatureStack(stack.Creature, survivors) : null);
        }

        events.Publish(new GarrisonChanged(holder, troops.Slots.ToList()));
    }
}
