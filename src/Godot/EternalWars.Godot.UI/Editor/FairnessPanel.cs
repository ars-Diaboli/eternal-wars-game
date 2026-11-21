using EternalWars.Core.Domain.Analysis;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The map's fairness report (EDIT-9, ADR-266), a tab of the right region: the core's numbers per start (<see cref="MapAnalysis.Fairness"/>,
/// as <see cref="MapReport.Table"/> lays them out) and the warnings where a start is far from the others. It is computed again a moment
/// after the edits stop, and only while the tab shows: a hidden panel computes when it is shown. Selecting a start's row reports its tile.
/// </summary>
public partial class FairnessPanel : VBoxContainer
{
    /// <summary>How long after the last change the numbers are computed again, in seconds.</summary>
    private const double Delay = 0.3;

    private readonly Tree _table = new()
    {
        Name = "FairnessTable", HideRoot = true, ColumnTitlesVisible = true, SelectMode = Tree.SelectModeEnum.Row,
        CustomMinimumSize = new Vector2(0, 80),
    };

    private readonly Label _summary = new() { Name = "FairnessSummary", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly VBoxContainer _warnings = new() { Name = "FairnessWarnings" };
    private readonly global::Godot.Timer _timer = new() { OneShot = true, WaitTime = Delay };
    private readonly List<MapPosition> _starts = [];
    private MapEditorSession _editor = null!;
    private bool _stale = true;

    /// <summary>The numbers shown last; null before the first computation.</summary>
    public FairnessAnalysis? Analysis { get; private set; }

    /// <summary>A start's row was selected: its tile.</summary>
    public event Action<MapPosition>? StartSelected;

    public void Bind(MapEditorSession editor)
    {
        _editor = editor;
        AddChild(new Label
        {
            Text = "Each start's distances, treasure and guards; a warning where a start is far from the others.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        AddChild(_summary);
        AddChild(_table);
        AddChild(new Label { Text = "Warnings", ThemeTypeVariation = "HeaderSmall" });
        AddChild(_warnings);
        AddChild(new Label { Text = MapReport.Legend, AutowrapMode = TextServer.AutowrapMode.WordSmart, ThemeTypeVariation = UiTheme.QuietLabel });
        AddChild(_timer);

        _table.ItemSelected += () =>
        {
            if (_table.GetSelected()?.GetMetadata(0).AsInt32() is { } index && index >= 0 && index < _starts.Count)
            {
                StartSelected?.Invoke(_starts[index]);
            }
        };
        _timer.Timeout += Refresh;
        VisibilityChanged += () =>
        {
            if (_stale && IsVisibleInTree())
            {
                _timer.Start();
            }
        };
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ =>
        {
            _stale = true;
            if (IsVisibleInTree())
            {
                _timer.Start(); // again from the start: the numbers wait for the edits to stop
            }
        });
        Callable.From(() =>
        {
            if (IsVisibleInTree())
            {
                Refresh();
            }
        }).CallDeferred();
    }

    /// <summary>Computes the numbers now and shows them (the timer after edits, and the tab when it is shown).</summary>
    public void Refresh()
    {
        if (!IsInstanceValid(this) || !IsInsideTree())
        {
            return;
        }

        _timer.Stop();
        var state = _editor.Scenario;
        FairnessTable table;
        try
        {
            Analysis = new MapAnalysis(state).Fairness();
            table = MapReport.Table(state, Analysis);
        }
        catch (Exception e)
        {
            // A state the analysis cannot take (in the middle of editing): said so, and tried again after the next edit or when shown again.
            _stale = true;
            _summary.Text = $"The numbers could not be computed: {e.Message}";
            return;
        }

        _stale = false;

        _table.Clear();
        _starts.Clear();
        // Each column as wide as its widest text, the table as high as its rows: it scrolls sideways, not down.
        _table.Columns = table.Headers.Count;
        var font = _table.GetThemeFont("font");
        var size = _table.GetThemeFontSize("font_size");
        for (var column = 0; column < table.Headers.Count; column++)
        {
            var widest = table.Rows.Select(r => r[column]).Append(table.Headers[column])
                .Max(text => font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X);
            _table.SetColumnTitle(column, table.Headers[column]);
            _table.SetColumnExpand(column, false);
            _table.SetColumnCustomMinimumWidth(column, (int)Mathf.Ceil(widest) + 24);
        }

        var line = font.GetHeight(size) + 10;
        _table.CustomMinimumSize = new Vector2(0, (line * (table.Rows.Count + 1)) + 24);

        var root = _table.CreateItem();
        for (var index = 0; index < table.Rows.Count; index++)
        {
            var item = _table.CreateItem(root);
            var row = table.Rows[index];
            for (var column = 0; column < row.Count; column++)
            {
                item.SetText(column, row[column]);
            }

            item.SetMetadata(0, index);
            item.SetTooltipText(0, "Select to show the start on the map");
            _starts.Add(Analysis.Starts[index].Start);
        }

        _summary.Text = Analysis.Starts.Count switch
        {
            0 => "The map has no start: no player has a town or a hero on it.",
            1 => "One start: there is nothing to compare it with.",
            var n => $"{n} starts, {Analysis.Warnings.Count} warning(s).",
        };

        foreach (var child in _warnings.GetChildren())
        {
            _warnings.RemoveChild(child);
            child.QueueFree();
        }

        var warnings = Analysis.Warnings.Count > 0 ? Analysis.Warnings
            : Analysis.Starts.Count > 1 ? [$"None: no start is more than {MapAnalysis.WarningPercent} % from the starts' mean."] : ["None."];
        foreach (var warning in warnings)
        {
            var label = new Label { Text = warning, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            if (Analysis.Warnings.Count > 0)
            {
                label.AddThemeColorOverride("font_color", UiTheme.IssueWarning);
            }

            _warnings.AddChild(label);
        }
    }
}
