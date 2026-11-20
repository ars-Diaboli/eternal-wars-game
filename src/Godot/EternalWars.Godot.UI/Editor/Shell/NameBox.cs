using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// A box over the whole editor that asks for a name (ADR-250): a title, a text field with a first proposal, "OK" (or Enter) and
/// "Cancel" (or Escape). An empty name is not taken. The box frees itself, then hands the name over.
/// </summary>
public partial class NameBox : Control
{
    private readonly LineEdit _name = new() { CustomMinimumSize = new Vector2(320, 0), MaxLength = 60 };
    private Action<string> _chosen = _ => { };

    public void Bind(string title, string proposal, Action<string> chosen)
    {
        _chosen = chosen;
        var rows = EditorOverlay.Build(this);
        rows.AddChild(new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center });
        _name.Text = proposal;
        rows.AddChild(_name);
        _name.TextSubmitted += _ => Give();
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        rows.AddChild(buttons);
        var ok = new Button { Text = "OK", CustomMinimumSize = new Vector2(110, 0) };
        ok.Pressed += Give;
        buttons.AddChild(ok);
        var cancel = new Button { Text = "Cancel", CustomMinimumSize = new Vector2(110, 0) };
        cancel.Pressed += () => EditorOverlay.Close(this);
        buttons.AddChild(cancel);
        _name.CallDeferred(Control.MethodName.GrabFocus);
        _name.CallDeferred(LineEdit.MethodName.SelectAll);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            GetViewport()?.SetInputAsHandled();
            EditorOverlay.Close(this);
        }
    }

    private void Give()
    {
        var name = _name.Text.Trim();
        if (name.Length > 0 && EditorOverlay.Close(this))
        {
            _chosen(name);
        }
    }
}
