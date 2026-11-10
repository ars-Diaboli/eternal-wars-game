using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Objects;

/// <summary>The upkeep of map objects over time: what they roll at game start, and the refugees and merchant stock of a new week (ADR-079).</summary>
public sealed class ObjectLifecycleService(ObjectInteractionContext context, ArtifactService artifacts) : Turns.ITurnPhaseParticipant
{
    void Turns.ITurnPhaseParticipant.StockWeek() => StartWeek(context.State.Date.IsFirstDayOfMonth);

    /// <summary>
    /// Fills in the prisons the map left open (game start): a random playable faction, then one of its heroes neither on the map nor in another
    /// prison (the players' start heroes are on the map), else any such hero; the level from <see cref="PrisonRules.LevelOdds"/>.
    /// </summary>
    internal void PreparePrisons()
    {
        var state = context.State;
        var taken = state.Heroes.Select(h => h.Definition).OfType<string>()
            .Concat(state.ObjectsOf<Prison>().Select(p => p.Hero).OfType<string>())
            .ToHashSet(StringComparer.Ordinal);
        foreach (var prison in state.ObjectsOf<Prison>().OrderBy(p => p.Id.Value).ToList())
        {
            if (prison.Hero is null)
            {
                var towns = BuiltInMapObjects.PlayableTowns;
                var town = towns[state.Random.Next(towns.Count)];
                var free = MapBans.Allowed(
                    state.HeroRoster.All.Where(h => !taken.Contains(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList(), h => state.Rules.Bans.BansHero(h.Id));
                var native = free.Where(h => h.Town == town).ToList();
                var pool = native.Count > 0 ? native : [.. free];
                if (pool.Count > 0)
                {
                    prison.Hero = pool[state.Random.Next(pool.Count)].Id;
                    taken.Add(prison.Hero);
                }
            }

            prison.Level ??= WeightedRoll.Pick(PrisonRules.LevelOdds, state.Random);
        }
    }

    /// <summary>
    /// Game start (ADR-079): witch huts the map left open roll their skills, refugee camps get their first week and artifact merchants their stock.
    /// </summary>
    internal void PrepareObjects()
    {
        var state = context.State;
        foreach (var hut in state.ObjectsOf<WitchHut>().Where(h => h.Skills.Count == 0).OrderBy(h => h.Id.Value).ToList())
        {
            hut.Skills = WitchHutRules.Roll(state.Random, state.Rules.Bans);
        }

        foreach (var shrine in state.ObjectsOf<Shrine>().Where(s => s.Spell is null).OrderBy(s => s.Id.Value).ToList())
        {
            shrine.Spell = ShrineRules.Roll(state.Random, state.Spells, shrine.SpellLevel, state.Rules.Bans);
        }

        StartWeek(restock: true);
    }

    /// <summary>The first day of a week: new refugees in every camp; on the first day of a month also new merchant stock (ADR-079).</summary>
    internal void StartWeek(bool restock)
    {
        var state = context.State;
        var candidates = RefugeeCampRules.Candidates(state.Creatures);
        foreach (var camp in state.ObjectsOf<RefugeeCamp>().OrderBy(c => c.Id.Value).ToList())
        {
            if (candidates.Count == 0)
            {
                break;
            }

            var creature = candidates[state.Random.Next(candidates.Count)];
            camp.Creature = creature.Id;
            camp.Available = creature.WeeklyGrowth;
            context.Events.Publish(new RefugeesArrived(camp.Id, creature.Id, camp.Available));
        }

        if (!restock)
        {
            return;
        }

        foreach (var merchant in state.ObjectsOf<ArtifactMerchant>().OrderBy(m => m.Id.Value).ToList())
        {
            merchant.Stock.Clear();
            for (var i = 0; i < ArtifactMerchant.StockSize; i++)
            {
                merchant.Stock.Add(artifacts.Draw(state.Random.Next(2) == 0 ? ArtifactTier.Minor : ArtifactTier.Major));
            }

            context.Events.Publish(new MerchantRestocked(merchant.Id, merchant.Stock.ToList()));
        }
    }
}
