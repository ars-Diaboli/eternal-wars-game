using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>One answer of a <see cref="QuestionBox"/>: the button's text and what it does (nothing: the question just closes).</summary>
public sealed record QuestionAnswer(string Text, Action? Run = null);

/// <summary>
/// A question over the whole editor (EDIT-1, ADR-250): a title, a text and a button for each answer. Nothing under it takes the pointer
/// while it is open. The last answer is the one Escape gives; the first has the focus. The box frees itself once answered, then runs the answer.
/// </summary>
public partial class QuestionBox : Control
{
    private IReadOnlyList<QuestionAnswer> _answers = [];

    public void Bind(string title, string text, IReadOnlyList<QuestionAnswer> answers)
    {
        _answers = answers;
        var rows = EditorOverlay.Build(this);
        rows.AddChild(new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center });
        rows.AddChild(new Label { Text = text, CustomMinimumSize = new Vector2(420, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        rows.AddChild(buttons);
        foreach (var answer in answers)
        {
            var button = new Button { Text = answer.Text, CustomMinimumSize = new Vector2(110, 0) };
            buttons.AddChild(button);
            button.Pressed += () => Give(answer);
        }

        buttons.GetChild<Button>(0).CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            Give(_answers[^1]);
        }
    }

    private void Give(QuestionAnswer answer)
    {
        if (EditorOverlay.Close(this)) // gone before the answer runs: the answer may ask the next question
        {
            answer.Run?.Invoke();
        }
    }
}
