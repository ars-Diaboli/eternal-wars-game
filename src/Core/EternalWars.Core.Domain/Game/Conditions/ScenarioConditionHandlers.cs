namespace EternalWars.Core.Domain.Game.Conditions;

/// <summary>
/// The registry of scenario conditions: the one <see cref="IScenarioConditionHandler"/> of each <see cref="ConditionKind"/>. The game's end, the
/// condition's sentences and the validator ask it instead of switching on the kind, so a new kind is its enum value plus one handler class
/// registered here (a test checks every kind has exactly one). The editor's panel and the map file's converter, outside the domain, still switch.
/// </summary>
internal static class ScenarioConditionHandlers
{
    /// <summary>Every handler, in <see cref="ConditionKind"/> order.</summary>
    public static IReadOnlyList<IScenarioConditionHandler> All { get; } =
    [
        new CaptureTownHandler(),
        new DefeatHeroHandler(),
        new GatherResourcesHandler(),
        new SurviveUntilDayHandler(),
        new LoseTownHandler(),
        new LoseHeroHandler(),
        new TimeLimitHandler(),
        new CaptureObjectHandler(),
        new ObtainArtifactHandler(),
        new ReleaseHeroHandler(),
        new LoseArtifactHandler(),
    ];

    private static readonly Dictionary<ConditionKind, IScenarioConditionHandler> ByKind = All.ToDictionary(h => h.Kind);

    /// <summary>The handler of the kind, or null for a value no kind has (a map file may hold one).</summary>
    public static IScenarioConditionHandler? Find(ConditionKind kind) => ByKind.GetValueOrDefault(kind);

    /// <summary>The handler of the kind.</summary>
    public static IScenarioConditionHandler For(ConditionKind kind) =>
        Find(kind) ?? throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unregistered scenario condition kind.");
}
