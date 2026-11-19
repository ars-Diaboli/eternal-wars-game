using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Gives an artifact to the hero, or takes one it wears (first) or carries.</summary>
internal sealed class ArtifactHandler : HeroActionHandler<ArtifactAction>
{
    protected override void ApplyTo(MapEventActionContext context, Hero hero, ArtifactAction action)
    {
        var artifacts = context.Service.Artifacts;
        if (!action.Take)
        {
            artifacts.Grant(hero, action.Artifact, ArtifactSource.MapEvent);
        }
        else if (artifacts.Remove(hero, action.Artifact, wornFirst: true))
        {
            context.Events.Publish(new ArtifactLost(hero.Id, action.Artifact));
        }
    }

    protected override string Describe(GameState state, ArtifactAction action) =>
        $"{(action.Take ? "−" : "+")} {(state.Artifacts.TryGet(action.Artifact, out var artifact) ? artifact.Name : action.Artifact)}";

    protected override string Line(ArtifactAction action, GameState? state) =>
        $"{(action.Take ? "Take" : "Give")} {(state is not null && state.Artifacts.TryGet(action.Artifact, out var artifact) ? artifact.Name : action.Artifact)}";

    /// <summary>Given, if the backpack has room; taken, if the hero has it.</summary>
    protected override EventWorth Worth(IMapEventValuer valuer, ArtifactAction action) => valuer.Hero switch
    {
        null => EventWorth.Nothing,
        { Artifacts.BackpackFull: false } when !action.Take => EventWorth.Gain(valuer.ArtifactWorth(action.Artifact)),
        { } hero when action.Take && (hero.Artifacts.IsWorn(action.Artifact) || hero.Artifacts.Backpack.Contains(action.Artifact)) =>
            EventWorth.Gain(-valuer.ArtifactWorth(action.Artifact)),
        _ => EventWorth.Nothing,
    };
}
