namespace EternalWars.Core.Domain.Settings;

/// <summary>What a touch becomes for the mouse-driven screens (ADR-190).</summary>
public enum PointerActionKind
{
    /// <summary>The pointer moves to the point, no button held (what the pointer is over shows).</summary>
    Hover,

    /// <summary>The left button goes down at the point.</summary>
    LeftPress,

    /// <summary>The pointer moves to the point with the left button held.</summary>
    LeftDrag,

    /// <summary>The left button comes up at the point.</summary>
    LeftRelease,

    /// <summary>A right click (pressed and released) at the point: the information window of what is there (ADR-154).</summary>
    RightClick,
}

/// <summary>One mouse action a touch turns into, at a screen point.</summary>
public readonly record struct PointerAction(PointerActionKind Kind, float X, float Y);

/// <summary>
/// One finger on a touch screen as the mouse (ADR-190): a tap is a left click, a finger moved more than <see cref="SlopPixels"/> is a left
/// drag from where it went down, and a finger held still for <see cref="HoldSeconds"/> is a right click there, which opens the information
/// window (ADR-154); lifting it then does nothing more, so the window stays open until the next tap. The left press waits until the touch is
/// known to be a tap or a drag, so a long press never also clicks. A second finger while the first is undecided cancels it. Times are the
/// caller's clock in seconds.
/// </summary>
public sealed class TouchGesture
{
    /// <summary>A finger held this long without moving is a long press.</summary>
    public const double HoldSeconds = 0.5;

    /// <summary>A finger that moves farther than this from where it went down drags.</summary>
    public const float SlopPixels = 12;

    private enum Phase
    {
        Idle,
        Undecided,
        Dragging,
        Held,
        Cancelled,
    }

    private Phase _phase = Phase.Idle;
    private int _finger;
    private float _x;
    private float _y;
    private double _downAt;

    /// <summary>A finger goes down.</summary>
    public IReadOnlyList<PointerAction> Down(int finger, float x, float y, double time)
    {
        if (_phase != Phase.Idle)
        {
            if (finger != _finger && _phase == Phase.Undecided)
            {
                _phase = Phase.Cancelled; // two fingers: not a tap, a drag or a long press
            }

            return [];
        }

        (_phase, _finger, _x, _y, _downAt) = (Phase.Undecided, finger, x, y, time);
        return [new PointerAction(PointerActionKind.Hover, x, y)];
    }

    /// <summary>A finger moves.</summary>
    public IReadOnlyList<PointerAction> Move(int finger, float x, float y)
    {
        if (finger != _finger)
        {
            return [];
        }

        switch (_phase)
        {
            case Phase.Undecided when ((x - _x) * (x - _x)) + ((y - _y) * (y - _y)) > SlopPixels * SlopPixels:
                _phase = Phase.Dragging;
                return [new PointerAction(PointerActionKind.LeftPress, _x, _y), new PointerAction(PointerActionKind.LeftDrag, x, y)];
            case Phase.Dragging:
                return [new PointerAction(PointerActionKind.LeftDrag, x, y)];
            default:
                return [];
        }
    }

    /// <summary>A finger comes up.</summary>
    public IReadOnlyList<PointerAction> Up(int finger, float x, float y)
    {
        if (finger != _finger || _phase == Phase.Idle)
        {
            return [];
        }

        var phase = _phase;
        _phase = Phase.Idle;
        return phase switch
        {
            Phase.Undecided => [new PointerAction(PointerActionKind.LeftPress, _x, _y), new PointerAction(PointerActionKind.LeftRelease, _x, _y)],
            Phase.Dragging => [new PointerAction(PointerActionKind.LeftRelease, x, y)],
            _ => [],
        };
    }

    /// <summary>Time passes: a finger held still long enough becomes a right click.</summary>
    public IReadOnlyList<PointerAction> Tick(double time)
    {
        if (_phase != Phase.Undecided || time - _downAt < HoldSeconds)
        {
            return [];
        }

        _phase = Phase.Held;
        return [new PointerAction(PointerActionKind.RightClick, _x, _y)];
    }
}
