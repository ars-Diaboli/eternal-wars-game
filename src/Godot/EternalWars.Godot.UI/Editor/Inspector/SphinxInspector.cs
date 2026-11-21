using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Objects;
using Godot;

namespace EternalWars.Godot.UI.Editor.Inspector;

/// <summary>
/// Sphinxes' riddle (ADR-079): the author's question, answer and two wrong answers, or one drawn from the riddle table at start. With
/// several sphinxes the riddle written is set on all of them.
/// </summary>
internal sealed class SphinxInspector : InspectorSection<Sphinx>
{
    protected override void Show(InspectorForm form, IReadOnlyList<Sphinx> sphinxes)
    {
        form.In(InspectorPart.Messages);
        var shared = ObjectProperties.SphinxRiddle.Shared(sphinxes);
        var riddle = shared.Differs ? null : shared.Value;
        var own = new CheckBox
        {
            Text = shared.Differs ? $"Author's riddle (else drawn from the riddle table) ({InspectorForm.Differs})" : "Author's riddle (else drawn from the riddle table)",
            ButtonPressed = riddle is not null,
        };
        form.Box.AddChild(own);
        LineEdit Field(string label, string value, string hint)
        {
            var row = new HBoxContainer();
            form.Box.AddChild(row);
            row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(UiTheme.InspectorFieldLabelWidth, 0) });
            var edit = new LineEdit
            {
                Text = value, CustomMinimumSize = new Vector2(UiTheme.InspectorFieldWidth, 0), PlaceholderText = shared.Differs ? InspectorForm.Differs : hint,
                Editable = riddle is not null,
            };
            row.AddChild(edit);
            return edit;
        }

        var question = Field("Question", riddle?.Question ?? "", "What the sphinx asks");
        var answer = Field("Answer", riddle?.Answer ?? "", "The right answer");
        var wrong1 = Field("Wrong", riddle?.Wrong1 ?? "", "A wrong answer");
        var wrong2 = Field("Wrong", riddle?.Wrong2 ?? "", "Another wrong answer");
        LineEdit[] fields = [question, answer, wrong1, wrong2];

        // Unticking sets the table's riddle; a written riddle is set once all four fields are filled. Nothing is set when nothing
        // changed (also when a field loses focus as the panel refreshes).
        void Apply()
        {
            if (own.ButtonPressed && fields.All(f => f.Text.Trim().Length > 0)
                && new Riddle(question.Text.Trim(), answer.Text.Trim(), wrong1.Text.Trim(), wrong2.Text.Trim()) is var next
                && (shared.Differs || next != riddle))
            {
                form.Edit(() => ObjectProperties.SphinxRiddle.Apply(form.Editor, sphinxes, next));
            }
        }

        own.Toggled += on =>
        {
            foreach (var field in fields)
            {
                field.Editable = on;
            }

            if (on)
            {
                Apply();
            }
            else
            {
                form.Edit(() => ObjectProperties.SphinxRiddle.Apply(form.Editor, sphinxes, null));
            }
        };
        foreach (var field in fields)
        {
            field.TextSubmitted += _ => Apply();
            field.FocusExited += Apply;
        }
    }
}
