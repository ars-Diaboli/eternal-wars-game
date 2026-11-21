using EternalWars.Core.Domain.Objects;
using Godot;

namespace EternalWars.Godot.UI.Editor.Inspector;

/// <summary>A sphinx's riddle (ADR-079): the author's question, answer and two wrong answers, or one drawn from the riddle table at start.</summary>
internal sealed class SphinxInspector : InspectorSection<Sphinx>
{
    protected override void Show(InspectorForm form, Sphinx sphinx)
    {
        form.In(InspectorPart.Messages);
        var own = new CheckBox { Text = "Author's riddle (else drawn from the riddle table)", ButtonPressed = sphinx.Riddle is not null };
        form.Box.AddChild(own);
        LineEdit Field(string label, string value, string hint)
        {
            var row = new HBoxContainer();
            form.Box.AddChild(row);
            row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(UiTheme.InspectorFieldLabelWidth, 0) });
            var edit = new LineEdit { Text = value, CustomMinimumSize = new Vector2(UiTheme.InspectorFieldWidth, 0), PlaceholderText = hint, Editable = sphinx.Riddle is not null };
            row.AddChild(edit);
            return edit;
        }

        var question = Field("Question", sphinx.Riddle?.Question ?? "", "What the sphinx asks");
        var answer = Field("Answer", sphinx.Riddle?.Answer ?? "", "The right answer");
        var wrong1 = Field("Wrong", sphinx.Riddle?.Wrong1 ?? "", "A wrong answer");
        var wrong2 = Field("Wrong", sphinx.Riddle?.Wrong2 ?? "", "Another wrong answer");
        LineEdit[] fields = [question, answer, wrong1, wrong2];

        void Apply()
        {
            var riddle = own.ButtonPressed
                ? new Riddle(question.Text.Trim(), answer.Text.Trim(), wrong1.Text.Trim(), wrong2.Text.Trim())
                : null;
            // Nothing changed (also when a field loses focus as the panel refreshes), or the author is still typing.
            if (riddle != sphinx.Riddle && (riddle is null || fields.All(f => f.Text.Trim().Length > 0)))
            {
                form.Edit(() => form.Editor.Contents.SetSphinxRiddle(sphinx.Id, riddle));
            }
        }

        own.Toggled += on =>
        {
            foreach (var field in fields)
            {
                field.Editable = on;
            }

            Apply();
        };
        foreach (var field in fields)
        {
            field.TextSubmitted += _ => Apply();
            field.FocusExited += Apply;
        }
    }
}
