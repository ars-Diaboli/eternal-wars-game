using Godot;

namespace EternalWars.Godot.UI.Editor.Tools;

/// <summary>The options of the tool in use, as the palette shows them: controls in a row that wraps to the palette's width.</summary>
public sealed class ToolOptions(Container row)
{
    public void Add(Control control) => row.AddChild(control);

    /// <summary>A control with its label before it, kept together where the options wrap.</summary>
    public HBoxContainer Labelled(string label, Control control)
    {
        var pair = new HBoxContainer();
        pair.AddChild(new Label { Text = label });
        pair.AddChild(control);
        row.AddChild(pair);
        return pair;
    }

    /// <summary>A line of help for the tool, wrapped to the palette's width.</summary>
    public void Hint(string text) => row.AddChild(new Label
    {
        Text = text,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        CustomMinimumSize = new Vector2(160, 0), // a wrapping label in a flow needs a width, or it wraps every letter
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    });

    /// <summary>
    /// A labelled list of <paramref name="items"/> showing <paramref name="selected"/>; <paramref name="chosen"/> gets the pick. <paramref name="icon"/>
    /// gives an item its icon (null: none).
    /// </summary>
    public void Choice<T>(string label, IEnumerable<(string Text, T Value)> items, T selected, Action<T> chosen, Func<T, UiIcon?>? icon = null)
    {
        var list = items.ToList();
        var option = new OptionButton();
        foreach (var (text, value) in list)
        {
            if (icon?.Invoke(value) is { } own && UiIcons.Get(own) is { } texture)
            {
                option.AddIconItem(texture, text);
            }
            else
            {
                option.AddItem(text);
            }
        }

        option.Selected = Math.Max(0, list.FindIndex(i => EqualityComparer<T>.Default.Equals(i.Value, selected)));
        option.ItemSelected += index => chosen(list[(int)index].Value);
        option.FitToLongestItem = false; // the longest object name would set the palette's width
        option.CustomMinimumSize = new Vector2(140, 0);
        option.ClipText = true;
        option.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        Labelled(label, option).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; // a list takes the row
    }
}
