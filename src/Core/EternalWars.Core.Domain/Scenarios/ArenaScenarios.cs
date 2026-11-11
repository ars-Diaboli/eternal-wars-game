using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Scenarios;

/// <summary>The shipped arena maps (ADR-100, ADR-180), hand-made; the generator has no arena template.</summary>
public static class ArenaScenarios
{
    public static IReadOnlyList<(string Name, Func<ContentCatalogs?, GameState> Create)> All { get; } =
    [
        (ProvingGroundsScenario.Name, ProvingGroundsScenario.Create),
        (FrostfangPassScenario.Name, FrostfangPassScenario.Create),
        (FourBannersScenario.Name, FourBannersScenario.Create),
    ];
}
