using System.Globalization;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Game.Conditions;

/// <summary>
/// The defaults of a <see cref="IScenarioConditionHandler"/>: a deadline for every victory, nothing to track, never holding, the tracked hero's name, the ends of
/// surviving and of a time limit, no issue; and the names the sentences use.
/// </summary>
internal abstract class ScenarioConditionHandler : IScenarioConditionHandler
{
    public abstract ConditionKind Kind { get; }

    public abstract bool IsVictory { get; }

    public virtual bool CanHaveDeadline => IsVictory;

    public virtual bool DefeatsEveryone => false;

    public abstract string Describe(GameState state, ScenarioCondition condition);

    public virtual ScenarioTracking Track(GameState state, ScenarioCondition condition, ScenarioTracking tracking) => tracking;

    public virtual bool WonBy(GameState state, ScenarioCondition condition, Player player) => false;

    public virtual bool LostBy(GameState state, ScenarioCondition condition, Player player) => false;

    public virtual string? HeroName(GameState state, ScenarioCondition condition) =>
        condition.Hero is { } hero && state.Tracking.Heroes.TryGetValue(hero, out var known) ? known.Name : null;

    public virtual string Achieved(GameState state, Player player, ScenarioCondition condition, string? heroName) => Survived(player, condition);

    public virtual string Lost(GameState state, ScenarioCondition condition, string? heroName) => OutOfTime(condition);

    public virtual MapIssue? Problem(GameState scenario, ScenarioCondition condition, string name) => null;

    /// <summary>The end of a victory that is no other kind's: the player survived.</summary>
    internal static string Survived(Player player, ScenarioCondition condition) =>
        $"{player.Name} survived until day {condition.Day.ToString(CultureInfo.InvariantCulture)}.";

    /// <summary>The end of a defeat that is no other kind's: the time ran out.</summary>
    internal static string OutOfTime(ScenarioCondition condition) => $"Ran out of time (day {condition.Day.ToString(CultureInfo.InvariantCulture)}).";

    /// <summary>Whether the player, or an ally, is the owner.</summary>
    protected static bool OwnTeam(GameState state, PlayerId owner, Player player) => owner == player.Id || state.AreAllies(owner, player.Id);

    /// <summary>Whether a flag owned at the start would end the game at once: the condition's player, or any player when it names none.</summary>
    protected static bool OwnedAtStart(GameState scenario, ScenarioCondition condition, PlayerId owner) =>
        condition.Player is null ? scenario.HasPlayer(owner) : owner == condition.Player;

    protected static string Day(ScenarioCondition condition) => condition.Day.ToString(CultureInfo.InvariantCulture);

    protected static string TownName(GameState state, MapObjectId? id) =>
        id is { } town && state.FindObject(town) is Towns.Town found ? found.Name : "a town";

    protected static string ObjectName(GameState state, MapObjectId? id) => id is { } target && state.FindObject(target) is { } found
        ? found.Name ?? $"the {found.Definition.Name} at {found.Position.X.ToString(CultureInfo.InvariantCulture)}, {found.Position.Y.ToString(CultureInfo.InvariantCulture)}"
        : "an object";

    protected static string ArtifactName(GameState state, Common.ArtifactId? id) =>
        id is { } known && state.Artifacts.TryGet(known, out var artifact) ? artifact.Name : "an artifact";

    /// <summary>The roster hero held in a prison, while the prison stands and its prisoner is known.</summary>
    protected static string PrisonerName(GameState state, MapObjectId? id) =>
        id is { } prison && state.FindObject(prison) is Prison { Hero: { } hero } && state.HeroRoster.Contains(hero) ? state.HeroRoster.Get(hero).Name : "the prisoner";

    protected static string HeroOnMapName(GameState state, HeroId? id) =>
        id is { } hero && state.Heroes.FirstOrDefault(h => h.Id == hero) is { } found ? found.Name : "a hero";

    /// <summary>The validator's error for a condition that points at nothing of its kind.</summary>
    protected static MapIssue Missing(ScenarioCondition condition, string what) => new(IssueSeverity.Error, $"Condition '{condition.Kind}' {what}.");

    /// <summary>The validator's error for a defeat condition without its loser.</summary>
    protected static MapIssue NeedsLoser(string name) => new(IssueSeverity.Error, $"'{name}' needs the player who loses.");
}
