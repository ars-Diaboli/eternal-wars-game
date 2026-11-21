using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The map settings window's Texts tab (EDIT-11, ADR-258): every text of the map (<see cref="MapTextEditor"/>) in its group, each edited in
/// place and applied on Enter (a one-line text) or when the field is left, as the edit of the panel the text belongs to. The search box
/// keeps the rows whose place or text has the words. A refused text stays in its field, and the status says why.
/// </summary>
public partial class TextsPanel : VBoxContainer
{
    private readonly LineEdit _search = new() { PlaceholderText = "Search the texts", ClearButtonEnabled = true };
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly VBoxContainer _rows = new();
    private readonly List<Row> _shown = [];
    private MapEditorSession _editor = null!;

    public void Bind(MapEditorSession editor)
    {
        _editor = editor;
        AddChild(new Label
        {
            Text = "Every text of the map in one list, so it can be read through and later translated. A change here is the same edit as in its own panel.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _search.TextChanged += _ => Filter();
        AddChild(_search);
        AddChild(_status);
        _status.Visible = false;
        AddChild(_rows);
        Rebuild();
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ => Refresh());
    }

    /// <summary>What a refused text has to be.</summary>
    public static string Refusal(MapText text) => text.Kind switch
    {
        MapTextKind.MapName or MapTextKind.PlayerName => "A name cannot be blank.",
        MapTextKind.HeroName => $"A hero's name has 1 to {Hero.NameLength} characters and is not only digits.",
        MapTextKind.HeroBiography => $"A biography has at most {Hero.BiographyLength} characters.",
        MapTextKind.ObjectName => $"An object's name has at most {MapObject.NameLength} characters and is not only digits.",
        MapTextKind.Rumour => $"A rumour is one line of 1 to {ScenarioRules.RumourLength} characters; the Rumours tab removes one.",
        MapTextKind.RiddleQuestion or MapTextKind.RiddleAnswer or MapTextKind.RiddleWrongAnswer =>
            "A riddle's texts cannot be blank, and its three answers must differ.",
        MapTextKind.EventMessage or MapTextKind.EventQuestion or MapTextKind.EventAnswer or MapTextKind.DialogueLine or MapTextKind.QuestProposal =>
            "This text cannot be blank.",
        _ => "The change was refused.",
    };

    private void Rebuild()
    {
        foreach (var child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }

        _shown.Clear();
        var texts = _editor.Texts.List();
        string? group = null;
        foreach (var text in texts)
        {
            if (text.Group != group)
            {
                group = text.Group;
                _rows.AddChild(new Label { Text = group, ThemeTypeVariation = "HeaderSmall" });
            }

            _shown.Add(AddRow(text));
        }

        Filter();
    }

    private Row AddRow(MapText text)
    {
        var line = new HBoxContainer();
        _rows.AddChild(line);
        var where = new Label
        {
            Text = text.Where,
            CustomMinimumSize = new Vector2(260, 0),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Center,
        };
        line.AddChild(where);

        Control field;
        Func<string> read;
        Action<string> write;
        if (text.Multiline)
        {
            var box = new TextEdit
            {
                Text = text.Text,
                WrapMode = TextEdit.LineWrappingMode.Boundary,
                ScrollFitContentHeight = true,
                CustomMinimumSize = new Vector2(0, 34),
            };
            (field, read, write) = (box, () => box.Text, v => box.Text = v);
        }
        else
        {
            var edit = new LineEdit { Text = text.Text };
            (field, read, write) = (edit, () => edit.Text, v => edit.Text = v);
        }

        field.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        field.TooltipText = text.Where;
        line.AddChild(field);
        var row = new Row(text.Id, line, where, field, read, write) { Shown = text.Text };
        field.FocusExited += () => Apply(row);
        if (field is LineEdit lineEdit)
        {
            lineEdit.TextSubmitted += _ => Apply(row);
        }

        return row;
    }

    /// <summary>
    /// Applies a change typed over the text shown, while the map still has that text: a field freed after an undo must not apply it again.
    /// </summary>
    private void Apply(Row row)
    {
        var typed = row.Read();
        if (typed == row.Shown || _editor.Texts.List().FirstOrDefault(t => t.Id == row.Id) is not { } now || now.Text != row.Shown)
        {
            return;
        }

        var result = _editor.Texts.Set(row.Id, typed, row.Shown);
        if (result == EditResult.Ok)
        {
            row.Write(row.Shown); // the text as the map keeps it (trimmed, a town's own name): the field then follows undo and redo
        }

        _status.Text = result == EditResult.Ok ? "" : $"{now.Where}: {Refusal(now)}";
        _status.Visible = _status.Text.Length > 0;
    }

    /// <summary>After any edit: the same texts are updated in their fields (a field with typing of its own keeps it), other texts rebuild the list.</summary>
    private void Refresh()
    {
        var texts = _editor.Texts.List();
        if (!texts.Select(t => t.Id).SequenceEqual(_shown.Select(r => r.Id)))
        {
            Rebuild();
            return;
        }

        for (var i = 0; i < texts.Count; i++)
        {
            var row = _shown[i];
            if (!row.Field.HasFocus() && row.Read() == row.Shown)
            {
                row.Write(texts[i].Text);
            }

            row.Shown = texts[i].Text;
            row.Where.Text = texts[i].Where; // a moved hero or object
            row.Field.TooltipText = texts[i].Where;
        }

        Filter();
    }

    private void Filter()
    {
        var words = _search.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var texts = _editor.Texts.List().ToDictionary(t => t.Id);
        foreach (var row in _shown)
        {
            var text = texts.GetValueOrDefault(row.Id);
            row.Line.Visible = text is null || words.All(w =>
                text.Where.Contains(w, StringComparison.OrdinalIgnoreCase) || row.Read().Contains(w, StringComparison.OrdinalIgnoreCase));
        }

        // A group heading shows while one of its rows does.
        Label? heading = null;
        var any = false;
        foreach (var child in _rows.GetChildren())
        {
            if (child is Label label)
            {
                if (heading is not null)
                {
                    heading.Visible = any;
                }

                (heading, any) = (label, false);
            }
            else if (child is Control { Visible: true })
            {
                any = true;
            }
        }

        if (heading is not null)
        {
            heading.Visible = any;
        }
    }

    private sealed class Row(string id, HBoxContainer line, Label where, Control field, Func<string> read, Action<string> write)
    {
        public string Id { get; } = id;

        public HBoxContainer Line { get; } = line;

        public Label Where { get; } = where;

        public Control Field { get; } = field;

        public Func<string> Read { get; } = read;

        public Action<string> Write { get; } = write;

        /// <summary>The text last shown from the map.</summary>
        public string Shown { get; set; } = "";
    }
}
