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
}
