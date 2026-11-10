using System.Reflection;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;

namespace EternalWars.Core.Tests.Domain;

/// <summary>A static <c>*Rules</c> class is pure queries (constants, checks, costs, odds); the changes they lead to are services' (code review R9).</summary>
public class RulesPurityTests
{
    private static readonly List<Type> RulesClasses =
        typeof(GameState).Assembly.GetTypes().Where(t => t is { IsAbstract: true, IsSealed: true } && t.Name.EndsWith("Rules", StringComparison.Ordinal)).ToList();

    private static IEnumerable<MethodInfo> Methods(Type type) =>
        type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName);

    [Fact]
    public void TheDomain_HasRulesClasses() => Assert.NotEmpty(RulesClasses);

    [Fact]
    public void NoRulesClass_PublishesEvents()
    {
        var publishing = RulesClasses.SelectMany(Methods)
            .Where(m => m.GetParameters().Any(p => typeof(IEventPublisher).IsAssignableFrom(p.ParameterType)))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}");

        Assert.Empty(publishing);
    }

    [Fact]
    public void NoRulesClass_HasAMethodWithoutAResult()
    {
        // A void method of a static class can only be there for its side effects. Local functions and lambdas compile to static
        // methods with '<' in their names; they belong to a query and are not looked at.
        var effects = RulesClasses.SelectMany(Methods)
            .Where(m => m.ReturnType == typeof(void) && !m.Name.Contains('<', StringComparison.Ordinal))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}");

        Assert.Empty(effects);
    }
}
