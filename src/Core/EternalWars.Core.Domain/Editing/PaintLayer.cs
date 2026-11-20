using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// One per-tile layer of the map (terrain, roads, rivers) painted on a set of tiles. A paint is one undoable command over the tiles it changes;
/// it publishes <typeparamref name="TEvent"/> for them, and undo publishes one per value it restores. <paramref name="ground"/>, when given and
/// it returns an edit, also shapes the ground after the painted tiles in the same command (terrain's Auto height).
/// </summary>
internal sealed class PaintLayer<TValue, TEvent>(
    EditorContext context,
    Func<MapPosition, TValue> get,
    Action<MapPosition, TValue> set,
    Func<IReadOnlyList<MapPosition>, TValue, TEvent> painted,
    Func<IReadOnlyList<MapPosition>, GroundEdit?>? ground = null)
    where TEvent : IEvent
{
    /// <summary>
    /// Paints <paramref name="value"/> on those of <paramref name="tiles"/> that are on the map; returns how many changed.
    /// <paramref name="shape"/> false leaves the ground as it is (the caller brings the heights itself).
    /// </summary>
    public int Paint(IEnumerable<MapPosition> tiles, TValue value, string description, bool shape = true)
    {
        var same = EqualityComparer<TValue>.Default;
        var changes = tiles
            .Where(context.Scenario.Map.Contains)
            .Distinct()
            .Where(p => !same.Equals(get(p), value))
            .Select(p => (Position: p, Previous: get(p)))
            .ToList();

        if (changes.Count == 0)
        {
            return 0;
        }

        var positions = changes.Select(c => c.Position).ToList();
        var shaping = shape ? ground?.Invoke(positions) : null;
        context.Run(
            description,
            () =>
            {
                positions.ForEach(p => set(p, value));
                context.Publish(painted(positions, value));
                shaping?.Apply();
            },
            () =>
            {
                shaping?.Revert();
                foreach (var group in changes.GroupBy(c => c.Previous))
                {
                    var restored = group.Select(c => c.Position).ToList();
                    restored.ForEach(p => set(p, group.Key));
                    context.Publish(painted(restored, group.Key));
                }
            });
        return changes.Count;
    }
}
