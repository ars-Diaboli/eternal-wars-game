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
            var before = Snapshot();
            shape();
            _changed = [];
            var i = 0;
            for (var level = 0; level < Map.Levels; level++)
            {
                for (var y = 0; y <= Map.Height; y++)
                {
                    for (var x = 0; x <= Map.Width; x++, i++)
                    {
                        if (Map.GetCorner(level, x, y) is var height && height != before[i])
                        {
                            _changed.Add(new Corner(level, x, y, height, before[i]));
                        }
                    }
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

    /// <summary>Every corner's height, level by level, row by row.</summary>
    private sbyte[] Snapshot()
    {
        var heights = new sbyte[(Map.Width + 1) * (Map.Height + 1) * Map.Levels];
        var i = 0;
        for (var level = 0; level < Map.Levels; level++)
        {
            for (var y = 0; y <= Map.Height; y++)
            {
                for (var x = 0; x <= Map.Width; x++)
                {
                    heights[i++] = (sbyte)Map.GetCorner(level, x, y);
                }
            }
        }

        return heights;
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
