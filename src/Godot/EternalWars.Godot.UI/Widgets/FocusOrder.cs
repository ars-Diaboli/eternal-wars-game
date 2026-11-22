using Godot;

namespace EternalWars.Godot.UI.Widgets;

/// <summary>
/// A window's focus order for the keyboard and the controller (ADR-294): Tab and Shift+Tab, and the pad's directions, walk the controls in the
/// order given, wrapping at the ends. Controls added later (a section rebuilt) are chained again by the window that owns them.
/// </summary>
public static class FocusOrder
{
    /// <summary>Chains <paramref name="controls"/> (the hidden and the disabled left out) in this order, and lets each take the focus.</summary>
    public static void Chain(params Control?[] controls) => Chain((IEnumerable<Control?>)controls);

    /// <inheritdoc cref="Chain(Control?[])"/>
    public static void Chain(IEnumerable<Control?> controls)
    {
        var chain = controls.OfType<Control>().Where(c => c.FocusMode != Control.FocusModeEnum.None || c is BaseButton or global::Godot.Slider or SpinBox or LineEdit)
            .Where(c => c is not BaseButton { Disabled: true }).ToList();
        for (var i = 0; i < chain.Count; i++)
        {
            var control = chain[i];
            if (control.FocusMode == Control.FocusModeEnum.None)
            {
                control.FocusMode = Control.FocusModeEnum.All;
            }

            var next = chain[(i + 1) % chain.Count];
            var previous = chain[(i - 1 + chain.Count) % chain.Count];
            if (control.IsInsideTree() && next.IsInsideTree())
            {
                control.FocusNext = control.GetPathTo(next);
                control.FocusPrevious = control.GetPathTo(previous);
            }
            else
            {
                // Paths need the tree: chained once both are in it.
                var (from, to, back) = (control, next, previous);
                void Link()
                {
                    if (from.IsInsideTree() && to.IsInsideTree() && back.IsInsideTree())
                    {
                        from.FocusNext = from.GetPathTo(to);
                        from.FocusPrevious = from.GetPathTo(back);
                    }
                }

                Callable.From(Link).CallDeferred();
            }
        }
    }

    /// <summary>The focusable controls under <paramref name="root"/> in tree order: what <see cref="Chain(IEnumerable{Control?})"/> takes for a window.</summary>
    public static IEnumerable<Control> Focusable(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Control { Visible: false })
            {
                continue;
            }

            if (child is BaseButton or global::Godot.Slider or SpinBox or LineEdit or TextEdit or ItemList or Tree)
            {
                yield return (Control)child;
                continue; // a button's own parts (the cost row) are not stops
            }

            foreach (var inner in Focusable(child))
            {
                yield return inner;
            }
        }
    }
}
