using System.Collections;
using System.Reflection;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// A map object hands out its collections read-only (ADR-239): what it holds changes only through its own internal steps, which the rule
/// services take, never through a list or set a view or the AI could change.
/// </summary>
public class MapObjectCollectionsTests
{
    [Fact]
    public void NoMapObject_HandsOutACollectionThatCanBeChanged()
    {
        var mutable = typeof(MapObject).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(MapObject)) || t == typeof(MapObject))
            .SelectMany(t => t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(p => p.PropertyType is { IsInterface: false } type && (type.IsArray || typeof(ICollection).IsAssignableFrom(type)
                || type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        Assert.Empty(mutable);
    }
}
