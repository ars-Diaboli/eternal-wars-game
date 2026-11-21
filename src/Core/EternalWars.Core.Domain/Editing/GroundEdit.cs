using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// One undoable change of the ground's height, part of an editor command. The first <see cref="Apply"/> runs the shaping and keeps every
/// corner it changed with its height before and after; later ones (redo) put the after heights back, <see cref="Revert"/> the before ones.
/// Each publishes <see cref="GroundHeightsChanged"/> per level it touched, nothing when no corner changed.
/// </summary>
internal sealed class GroundEdit(EditorContext context, Action shape)
{
    private List<Corner>? _changed;

    /// <summary>How many corners the shaping changed (0 before the first <see cref="Apply"/>).</summary>
    public int Count => _changed?.Count ?? 0;

    private AdventureMap Map => context.Scenario.Map;

    public void Apply()
    {
        if (_changed is null)
        {
            // The levels copied whole and compared a run of equal bytes at a time: on a large map the shaping touches a few corners of many
            // (ADR-278).
            var before = Enumerable.Range(0, Map.Levels).Select(level => Map.Corners(level).ToArray()).ToArray();
            shape();
            _changed = [];
            var stride = Map.Width + 1;
            for (var level = 0; level < Map.Levels; level++)
            {
                var now = Map.Corners(level);
                ReadOnlySpan<sbyte> was = before[level];
                for (var i = now.CommonPrefixLength(was); i < now.Length; i += 1 + now[(i + 1)..].CommonPrefixLength(was[(i + 1)..]))
                {
                    _changed.Add(new Corner(level, i % stride, i / stride, now[i], was[i]));
                }
            }
        }
        else
        {
            _changed.ForEach(c => Map.SetCorner(c.Level, c.X, c.Y, c.Height));
        }

        Publish();
    }

    public void Revert()
    {
        foreach (var corner in _changed ?? [])
        {
            Map.SetCorner(corner.Level, corner.X, corner.Y, corner.Before);
        }

        Publish();
    }

    private void Publish()
    {
        foreach (var level in (_changed ?? []).GroupBy(c => c.Level))
        {
            context.Publish(new GroundHeightsChanged(level.Key, new CornerArea(
                level.Min(c => c.X), level.Min(c => c.Y), level.Max(c => c.X), level.Max(c => c.Y))));
        }
    }

    /// <summary>A corner and its height (eighths); <see cref="Before"/> is its height before the shaping.</summary>
    private readonly record struct Corner(int Level, int X, int Y, int Height, int Before);
}
