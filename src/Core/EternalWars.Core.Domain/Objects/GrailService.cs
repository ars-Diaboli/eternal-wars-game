using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Domain.Objects.GrailRules;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// The hunt for the Grail on the adventure map (ADR-140): burying it at game start, obelisk visits and digging; the rules themselves are the
/// queries of <see cref="GrailRules"/>. A hero raises the Grail building on its town visit (<see cref="TownVisitService"/>).
/// </summary>
public sealed class GrailService(ObjectInteractionContext context, ArtifactService artifacts)
{
    /// <summary>
    /// Buries the Grail when the game starts (from <see cref="Game.GameState.Random"/>) on a map with an obelisk, large enough and with no Grail set by
    /// its author: on a cell one could dig, far enough from the edges, away from the towns when such a cell exists. A map without obelisks keeps no
    /// Grail unless its author buried one. Then the puzzle's place is drawn around the Grail.
    /// </summary>
    internal void Bury()
    {
        var state = context.State;
        if (state.Grail.Position is null && state.ObjectsOf<Obelisk>().Any() && state.Artifacts.Grail is not null && HoldsGrail(state.Map))
        {
            var cells = Enumerable.Range(0, state.Map.Levels).SelectMany(state.Map.Positions)
                .Where(p => FarFromEdge(state.Map, p) && CheckGround(state, p) is null).ToList();
            var towns = state.ObjectsOf<Town>().Select(t => t.VisitPosition).ToList();
            var away = cells.Where(c => towns.All(t => t.Level != c.Level || Math.Max(Math.Abs(t.X - c.X), Math.Abs(t.Y - c.Y)) > TownDistance)).ToList();
            var pool = away.Count > 0 ? away : cells;
            if (pool.Count > 0)
            {
                state.Grail.Position = pool[state.Random.Next(pool.Count)];
            }
        }

        if (state.Grail.Position is { } grail && state.Grail.PuzzleOrigin is null)
        {
            state.Grail.PuzzleOrigin = new MapPosition(grail.X - Offset(grail.X, state.Map.Width), grail.Y - Offset(grail.Y, state.Map.Height), grail.Level);
        }

        // The Grail's place in the puzzle along one axis: one of the centre's cells (8 … 11), keeping the puzzle on the map where it can.
        int Offset(int at, int side)
        {
            const int first = (PuzzleSize - CentreCells) / 2;
            const int last = first + CentreCells - 1;
            var low = Math.Clamp(at - side + PuzzleSize, first, last);
            var high = Math.Clamp(at, low, last);
            return low + state.Random.Next(high - low + 1);
        }
    }

    /// <summary>
    /// A hero visits an obelisk: its player's first visit opens that player's next share of the puzzle (<see cref="Share"/>), drawn from the ring
    /// the visit's number allows; the last obelisk opens the rest.
    /// </summary>
    internal void VisitObelisk(Hero hero, Obelisk obelisk)
    {
        var (state, events) = (context.State, context.Events);
        if (!obelisk.Visit(hero.Owner))
        {
            events.Publish(new MapObjectFoundEmpty(hero.Id, obelisk.Id));
            return;
        }

        if (state.Grail.PuzzleOrigin is not null)
        {
            var obelisks = state.ObjectsOf<Obelisk>().ToList();
            var visit = obelisks.Count(o => o.WasVisitedBy(hero.Owner));
            var opened = state.Grail.PiecesOf(hero.Owner);
            var closed = Enumerable.Range(0, PieceCount).Where(i => !opened.Contains(i)).ToList();
            if (visit >= obelisks.Count)
            {
                state.Grail.Open(hero.Owner, closed);
            }
            else
            {
                var rings = visit <= obelisks.Count / 2 ? new[] { PuzzleRing.Outer } : [PuzzleRing.Outer, PuzzleRing.Mid];
                var pool = closed.Where(i => rings.Contains(RingOf(i % PiecesPerSide, i / PiecesPerSide))).ToList();
                var drawn = new List<int>();
                for (var n = Math.Min(Share(obelisks.Count, visit), pool.Count); n > 0; n--)
                {
                    var at = state.Random.Next(pool.Count);
                    drawn.Add(pool[at]);
                    pool.RemoveAt(at);
                }

                state.Grail.Open(hero.Owner, drawn);
            }
        }

        events.Publish(new ObeliskVisited(hero.Id, obelisk.Id, hero.Owner));
    }

    /// <summary>
    /// The hero digs where it stands (ADR-140): it needs its whole day's movement, which the dig spends, and open ground (<see cref="GrailRules.CheckDig"/>).
    /// The hole stays; on the Grail's cell the hero takes the Grail, unless its backpack is full, which leaves the Grail buried and the cell undug.
    /// </summary>
    internal void Dig(HeroId heroId)
    {
        var state = context.State;
        var hero = state.GetHero(heroId);
        if (CheckDig(state, hero) is { } refusal)
        {
            throw new CommandRefusedException(refusal);
        }

        hero.MovementPoints = 0;
        var position = hero.Position;
        string? found = null;
        if (!state.Grail.IsFound && state.Grail.Position == position && state.Artifacts.Grail is { } grail)
        {
            if (!artifacts.Grant(hero, grail.Id, ArtifactSource.Found))
            {
                context.Events.Publish(new HoleDug(hero.Id, position, null));
                return;
            }

            state.Grail.IsFound = true;
            found = grail.Id;
        }

        state.Grail.AddHole(position);
        context.Events.Publish(new HoleDug(hero.Id, position, found));
    }
}
