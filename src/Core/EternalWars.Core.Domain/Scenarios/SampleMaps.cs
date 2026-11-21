using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Scenarios;

/// <summary>A shipped map the editor offers to open as a start (ADR-259): its name, a line about it and how it is made.</summary>
public sealed record SampleMap(string Name, string About, Func<ContentCatalogs?, GameState> Create);

/// <summary>
/// The sample maps (ADR-259): the shipped hand-made maps, which the editor's empty state offers to open as a new, unsaved map to look at or
/// change. The game lists the same maps on its new-game screen.
/// </summary>
public static class SampleMaps
{
    public static IReadOnlyList<SampleMap> All { get; } =
    [
        new(EmberMarchScenario.Name, "Two players, Haven against Inferno, a ridge with one road and a guarded pass.", EmberMarchScenario.Create),
        new("Two Towns", "Two players with a town each and a neutral town between them.", TownsScenario.Create),
        new(ProvingGroundsScenario.Name, "An arena: a day to prepare, then one battle.", ProvingGroundsScenario.Create),
        new(FrostfangPassScenario.Name, "An arena: snow against wasteland across a ridge.", FrostfangPassScenario.Create),
        new(FourBannersScenario.Name, "An arena for four players.", FourBannersScenario.Create),
    ];
}
