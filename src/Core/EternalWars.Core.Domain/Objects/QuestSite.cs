using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.MapEvents;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// A quest object (ADR-115, EVT-5): a <b>Seer's Hut</b> (a building; its quest's stages one after another, each with its reward), a <b>Quest
/// Guard</b> (a one-tile barrier every hero bumps into; it leaves once its last stage is done) or a <b>Quest Gate</b> (a one-tile barrier that heroes
/// meeting its one-stage quest walk through, like a border gate; it stays). <see cref="Quest"/> names its own quest event (null: none yet, an empty
/// hut or a closed barrier); <see cref="Stage"/> is how far the quest has got, for every player.
/// </summary>
public sealed class QuestSite(MapObjectId id, MapObjectDefinition definition, MapPosition position, string? quest = null)
    : MapObject(id, definition, position, QuestRules.Check(definition))
{
    /// <inheritdoc/>
    internal override string DigestState() => $"quest {Quest} stage {Stage}";

    public string? Quest { get; internal set; } = quest;

    /// <summary>The stage the next completion finishes (game state; 0 on the map).</summary>
    public int Stage { get; internal set; }

    public bool IsBarrier => Kind is MapObjectKind.QuestGuard or MapObjectKind.QuestGate;

    /// <summary>A gate is walked through by those who meet its quest; a guard is never entered.</summary>
    public override bool EndsRoute => Kind != MapObjectKind.QuestGate;
}

/// <summary>The rules of the quest objects that routes and steps need (EVT-5).</summary>
public static class QuestRules
{
    /// <summary>
    /// The Quest Guard or Gate on <paramref name="position"/> that stops <paramref name="hero"/>, if any: a guard stops everyone (a bump shows or
    /// completes its quest); a gate stops a hero that does not meet its quest.
    /// </summary>
    public static QuestSite? Barring(GameState state, Hero hero, MapPosition position) =>
        state.ObjectCellAt(position) is { Object: QuestSite { IsBarrier: true } site } && (site.Kind == MapObjectKind.QuestGuard || !Meets(state, hero, site))
            ? site
            : null;

    /// <summary>The quest event a quest object runs now, with its current stage; null when it has none, it is disabled or every stage is done.</summary>
    public static (MapEventDefinition Quest, QuestStage Stage)? Current(GameState state, QuestSite site) =>
        site.Quest is { } name && state.MapEvents.Set.Find(name) is { Kind: MapEventKind.Quest } quest && state.MapEvents.IsEnabled(name)
        && site.Stage < quest.Stages.Count
            ? (quest, quest.Stages[site.Stage])
            : null;

    /// <summary>Whether the hero meets the quest object's current stage (never when there is none).</summary>
    public static bool Meets(GameState state, Hero hero, QuestSite site) =>
        Current(state, site) is { } current && MapEventEvaluator.Test(state, new EventScope(hero.Owner, hero, null), current.Stage.Requirement);

    internal static MapObjectKind Check(MapObjectDefinition definition) =>
        definition.Kind is MapObjectKind.SeerHut or MapObjectKind.QuestGuard or MapObjectKind.QuestGate
            ? definition.Kind
            : throw new ArgumentException($"Definition '{definition.Id}' is a {definition.Kind}, not a quest object.", nameof(definition));
}
