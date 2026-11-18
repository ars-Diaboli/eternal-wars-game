using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Editing.Checks;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// Every validator check is registered once (<see cref="MapChecks"/>), in the order the editor lists their issues: a check left out would let its
/// problems through unnoticed.
/// </summary>
public sealed class MapChecksTests
{
    private static readonly Type[] CheckTypes =
        [.. typeof(IMapCheck).Assembly.GetTypes().Where(t => !t.IsAbstract && t.IsAssignableTo(typeof(IMapCheck))).OrderBy(t => t.FullName, StringComparer.Ordinal)];

    public static TheoryData<string> Checks() => [.. CheckTypes.Select(t => t.Name)];

    [Theory]
    [MemberData(nameof(Checks))]
    public void EveryCheck_IsRegisteredOnce(string check) => Assert.Single(MapChecks.All, c => c.GetType().Name == check);

    [Fact]
    public void TheChecks_RunInTheEditorsOrder() =>
        Assert.Equal(
            [
                typeof(HeroCheck), typeof(ObjectCheck), typeof(GroundCheck), typeof(ColourCheck), typeof(StartCheck), typeof(GracePeriodCheck),
                typeof(HeroLimitCheck), typeof(RulesCheck), typeof(BansCheck), typeof(PlaceholderCheck), typeof(EventCheck), typeof(GrailCheck),
                typeof(ArenaCheck), typeof(StartZoneCheck),
            ],
            MapChecks.All.Select(c => c.GetType()));

    [Fact]
    public void Validate_ListsTheChecksIssuesInOrder()
    {
        var scenario = TestEditor.Blank().Scenario;
        Assert.Equal(MapChecks.All.SelectMany(c => c.Check(scenario)), MapValidator.Validate(scenario));
    }
}
