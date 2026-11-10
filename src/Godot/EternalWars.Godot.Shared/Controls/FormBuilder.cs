using Godot;

namespace EternalWars.Godot.Shared.Controls;

/// <summary>
/// The small building blocks of the code-built forms (editor panels, setup and settings screens): labelled rows, number boxes and drop-downs,
/// and emptying a container before it is filled again.
/// </summary>
public static class FormBuilder
{
    /// <summary>
    /// Adds a row to <paramref name="parent"/>: the label (at least <paramref name="labelWidth"/> wide; 0 fits the text), then
    /// <paramref name="control"/> if given, filling the rest of the row with <paramref name="fill"/>.
    /// </summary>
    public static HBoxContainer AddRow(this Node parent, string label, Control? control = null, float labelWidth = 0, bool fill = false)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(labelWidth, 0) });
        if (control is not null)
        {
            if (fill)
            {
                control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            }

            row.AddChild(control);
        }

        parent.AddChild(row);
        return row;
    }

    /// <summary>Adds a whole-number box to <paramref name="row"/>, after <paramref name="label"/> if given.</summary>
    public static SpinBox AddSpin(this Container row, string? label, int value, int min, int max)
    {
        AddLabel(row, label);
        var spin = new SpinBox { MinValue = min, MaxValue = max, Value = value };
        row.AddChild(spin);
        return spin;
    }

    /// <summary>Adds a drop-down of <paramref name="items"/> to <paramref name="row"/>, after <paramref name="label"/> if given.</summary>
    public static OptionButton AddOptions(this Container row, IEnumerable<string> items, string? label = null)
    {
        AddLabel(row, label);
        var button = new OptionButton { FitToLongestItem = false, CustomMinimumSize = new Vector2(150, 0) };
        foreach (var item in items)
        {
            button.AddItem(item);
        }

        row.AddChild(button);
        return button;
    }

    /// <summary>Adds a drop-down of values shown by their texts to <paramref name="row"/>, after <paramref name="label"/>, with <paramref name="selected"/> picked.</summary>
    public static Choice<T> AddChoice<T>(this Container row, string? label, IEnumerable<(string Text, T Value)> items, T selected)
    {
        var list = items.ToList();
        var button = row.AddOptions(list.Select(i => i.Text), label);
        button.Selected = Math.Max(0, list.FindIndex(i => EqualityComparer<T>.Default.Equals(i.Value, selected)));
        return new Choice<T>(button, list);
    }

    /// <summary>
    /// Frees every child of <paramref name="node"/>. With <paramref name="detach"/> they leave it at once, so its size and child list are the new
    /// content's in this frame (freeing alone waits for the frame's end).
    /// </summary>
    public static void ClearChildren(this Node node, bool detach = false)
    {
        foreach (var child in node.GetChildren())
        {
            if (detach)
            {
                node.RemoveChild(child);
            }

            child.QueueFree();
        }
    }

    private static void AddLabel(Container row, string? label)
    {
        if (label is not null)
        {
            row.AddChild(new Label { Text = label });
        }
    }
}

/// <summary>A drop-down of values (<see cref="FormBuilder.AddChoice"/>): <see cref="Value"/> is the picked one.</summary>
public sealed record Choice<T>(OptionButton Button, List<(string Text, T Value)> Items)
{
    public T Value() => Items[Math.Max(0, Button.Selected)].Value;
}
