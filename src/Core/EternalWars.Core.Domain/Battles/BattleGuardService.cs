using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// Applies what the end of a battle does to a guard (ADR-216): the guard describes it (<see cref="IBattleGuard"/>, a <see cref="GuardOutcome"/>),
/// this service changes the map and publishes, after mutating.
/// </summary>
internal sealed class BattleGuardService(
    GameState state, IEventPublisher events, EconomyService economy, ArtifactService artifacts, TownVisitService towns, PandorasBoxService boxes)
{
    private readonly DecisionSteps _steps = new(state, events);

    /// <summary>
    /// Applies <paramref name="outcome"/> to the guard that fought on <paramref name="side"/> of the finished <paramref name="battle"/>;
    /// <paramref name="winner"/> is the hero that beat it, if any.
    /// </summary>
    internal void Apply(GuardOutcome outcome, BattleState battle, BattleSide side, Hero? winner)
    {
        switch (outcome)
        {
            case GuardUnchanged(var guard):
                events.Publish(new MapObjectChanged(guard.Id));
                break;
            case MonsterSurvives(var monster):
                monster.Count = Survivors(battle, side);
                events.Publish(new MapObjectChanged(monster.Id));
                break;
            case SiteGuardSurvives(var site):
                KeepGuardStack(site, Survivors(battle, side));
                events.Publish(new MapObjectChanged(site.Id));
                break;
            case TroopsSurvive(var holder, var troops):
                KeepTroops(battle, side, holder.Id, troops);
                break;
            case MonsterBeaten(var monster):
                _steps.RemoveObject(monster);
                break;
            case MineGuardBeaten(var mine):
                BeatMineGuard(mine, winner);
                break;
            case DwellingGuardBeaten(var dwelling):
                BeatDwellingGuard(dwelling, winner);
                break;
            case BankPlundered(var bank):
                if (winner is not null)
                {
                    Plunder(bank, winner);
                }

                break;
            case BoxWon(var box):
                if (winner is not null)
                {
                    boxes.Open(winner, box);
                }

                break;
            case TownWon(var town):
                if (winner is not null)
                {
                    towns.Capture(town, winner.Owner);
                }

                break;
            case GarrisonWon(var garrison):
                BeatGarrison(garrison, winner);
                break;
            default:
                throw new InvalidOperationException($"{outcome.GetType().Name} is not a guard outcome this service applies.");
        }
    }

    /// <summary>The troops of a town or a map garrison (<paramref name="holder"/>) keep what lived on <paramref name="side"/>, slot by slot.</summary>
    internal void KeepTroops(BattleState battle, BattleSide side, MapObjectId holder, Army troops)
    {
        BattleOutcome.KeepSurvivors(battle, side, troops);
        events.Publish(new GarrisonChanged(holder, troops.Slots.ToList()));
    }

    /// <summary>The guard's creatures that lived through the battle (its summoned ones do not count).</summary>
    private static int Survivors(BattleState battle, BattleSide side) => battle.OwnedArmyOf(side).Where(u => !u.IsSummoned).Sum(u => u.KeptCount);

    private static void KeepGuardStack(MapObject site, int survivors)
    {
        switch (site)
        {
            case Mine { Guard: { } stack } mine:
                mine.Guard = stack with { Count = survivors };
                break;
            case CreatureDwelling { Guard: { } stack } dwelling:
                dwelling.Guard = stack with { Count = survivors };
                break;
        }
    }

    private void BeatMineGuard(Mine mine, Hero? winner)
    {
        mine.Guard = null;
        mine = ClearAbandoned(mine);
        events.Publish(new MineGuardDefeated(mine.Id));
        if (winner is not null && mine.Owner != winner.Owner)
        {
            var previous = mine.Owner;
            mine.Owner = winner.Owner;
            events.Publish(new MineOwnerChanged(mine.Id, previous, winner.Owner));
        }
    }

    /// <summary>
    /// A beaten abandoned mine becomes the normal mine of its resource (ADR-068): same id, place and owner, the definition it
    /// <see cref="MapObjectDefinition.Becomes"/>. Views redraw it on <see cref="MapObjectChanged"/>.
    /// </summary>
    private Mine ClearAbandoned(Mine mine)
    {
        if (mine.Definition.Becomes is not { } becomes || !state.ObjectCatalog.TryGet(becomes, out var normal))
        {
            return mine;
        }

        var cleared = new Mine(mine.Id, normal, mine.Position, mine.Owner);
        state.RemoveObject(mine);
        state.AddObject(cleared);
        events.Publish(new MapObjectChanged(cleared.Id));
        return cleared;
    }

    private void BeatDwellingGuard(CreatureDwelling dwelling, Hero? winner)
    {
        dwelling.Guard = null;
        events.Publish(new MapObjectChanged(dwelling.Id));
        if (winner is not null && dwelling.Owner != winner.Owner)
        {
            var before = dwelling.Owner;
            dwelling.Owner = winner.Owner;
            events.Publish(new DwellingOwnerChanged(dwelling.Id, before, winner.Owner));
        }
    }

    private void Plunder(CreatureBank bank, Hero winner)
    {
        var (reward, tier, more) = (bank.Reward, bank.RewardArtifact, bank.MoreArtifacts);
        bank.PlunderedOn = state.Date;
        bank.Guard = [];
        bank.Reward = ResourceSet.Empty;
        bank.RewardArtifact = null;
        bank.MoreArtifacts = [];
        economy.Grant(winner.Owner, reward);
        var found = tier is { } t && artifacts.Draw(t, winner.Owner) is { } artifact && artifacts.Grant(winner, artifact, ArtifactSource.Found) ? artifact : (Common.ArtifactId?)null;
        foreach (var extra in more)
        {
            // Further artifacts (Dragon City) go the same way; a full backpack loses them.
            if (artifacts.Draw(extra, winner.Owner) is { } drawn)
            {
                artifacts.Grant(winner, drawn, ArtifactSource.Found);
            }
        }

        events.Publish(new CreatureBankPlundered(bank.Id, winner.Id, reward, found));
        events.Publish(new MapObjectChanged(bank.Id));
    }

    private void BeatGarrison(MapGarrison garrison, Hero? winner)
    {
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            garrison.Troops.Set(slot, null);
        }

        events.Publish(new GarrisonChanged(garrison.Id, garrison.Troops.Slots.ToList()));
        if (winner is not null)
        {
            var previous = garrison.Owner;
            garrison.Owner = winner.Owner;
            events.Publish(new GarrisonOwnerChanged(garrison.Id, previous, winner.Owner));
        }
    }
}
