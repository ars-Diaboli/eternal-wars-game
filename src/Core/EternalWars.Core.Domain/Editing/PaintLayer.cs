using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// One per-tile layer of the map (terrain, roads, rivers) painted with a square brush. A paint is one undoable command over the tiles it changes;
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
    /// <summary>Paints <paramref name="value"/> on the tiles within <paramref name="radius"/> of <paramref name="center"/>; returns how many changed.</summary>
    public int Paint(MapPosition center, int radius, TValue value, string description)
    {
        var same = EqualityComparer<TValue>.Default;
        var changes = Brush(center, radius)
            .Where(p => !same.Equals(get(p), value))
            .Select(p => (Position: p, Previous: get(p)))
            .ToList();

        if (changes.Count == 0)
        {
            return 0;
        }

        var positions = changes.Select(c => c.Position).ToList();
        var shaping = ground?.Invoke(positions);
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

    private IEnumerable<MapPosition> Brush(MapPosition center, int radius)
    {
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var position = center.Offset(dx, dy);
                if (context.Scenario.Map.Contains(position))
                {
                    yield return position;
                }
            }
        }
    }
}
