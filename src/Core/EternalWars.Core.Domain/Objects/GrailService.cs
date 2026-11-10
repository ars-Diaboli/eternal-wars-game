using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Objects;

/// <summary>The hunt for the Grail on the adventure map: obelisk visits and digging (ADR-140); the rules themselves are in <see cref="GrailRules"/>.</summary>
public sealed class GrailService(ObjectInteractionContext context, ArtifactService artifacts)
{
    /// <summary>A hero at an obelisk: its player sees the obelisk's share of the puzzle (<see cref="GrailRules"/>).</summary>
    internal void VisitObelisk(Hero hero, Obelisk obelisk) => GrailRules.Visit(context.State, context.Events, hero, obelisk);

    /// <summary>
    /// The hero digs where it stands (ADR-140): it needs its whole day's movement, which the dig spends, and open ground (<see cref="GrailRules.CheckDig"/>).
    /// The hole stays; on the Grail's cell the hero takes the Grail, unless its backpack is full, which leaves the Grail buried and the cell undug.
    /// </summary>
    internal void Dig(HeroId heroId)
    {
        var state = context.State;
        var hero = state.GetHero(heroId);
        if (GrailRules.CheckDig(state, hero) is { } refusal)
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
