using EternalWars.Core.Domain.Analysis;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The map's fairness report (EDIT-9, ADR-266), a tab of the right region: the core's numbers per start (<see cref="MapAnalysis.Fairness"/>,
/// as <see cref="MapReport.Table"/> lays them out) and the warnings where a start is far from the others. It is computed again a moment
/// after the edits stop, and only while the tab shows: a hidden panel computes when it is shown. The numbers are worked out on a task over
/// a copy of the map, as the problems are (ADR-290), so a large map never stalls the editor; the status bar shows the work meanwhile.
/// Selecting a start's row reports its tile.
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
    private Func<Func<GameState>>? _snapshot;
    private bool _stale = true;
    private int _run;

    /// <summary>How many edits were made: a run that an edit came after leaves the numbers stale, so the tab computes again when shown.</summary>
    private int _edits;

    /// <summary>The numbers shown last; null before the first computation.</summary>
    public FairnessAnalysis? Analysis { get; private set; }

    /// <summary>A start's row was selected: its tile.</summary>
    public event Action<MapPosition>? StartSelected;

    /// <summary>The numbers are being worked out on a task: <see cref="Analysis"/> is older than the map.</summary>
    public bool IsAnalysing { get; private set; }

    /// <summary><see cref="IsAnalysing"/> changed.</summary>
    public event Action? Changed;

    /// <summary>
    /// <paramref name="snapshot"/> runs on the main thread and copies the map; the work it returns makes the copy's state on a task, where the
    /// numbers are worked out, so it must not touch the live scenario. Null: the numbers are worked out on the main thread.
    /// </summary>
    public void Bind(MapEditorSession editor, Func<Func<GameState>>? snapshot = null)
    {
        _editor = editor;
        _snapshot = snapshot;
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
            _edits++;
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

    /// <summary>
    /// Computes the numbers and shows them (the timer after edits, and the tab when it is shown): on a task over a copy of the map; a result
    /// a newer run has overtaken is dropped. A map that cannot be copied is analysed as it is, on the main thread.
    /// </summary>
    public void Refresh()
    {
        if (!IsInstanceValid(this) || !IsInsideTree())
        {
            return;
        }

        _timer.Stop();
        var run = ++_run;
        var edits = _edits;
        Func<GameState>? work = null;
        try
        {
            work = _snapshot?.Invoke();
        }
        catch (Exception)
        {
            work = null; // the copy is the map's file form, and an edit may leave a state that form refuses
        }

        if (work is null)
        {
            Show(run, edits, Analyse(_editor.Scenario));
            return;
        }

        SetAnalysing(true);
        Task.Run(() =>
        {
            // Nothing may escape the task: an exception lost here would leave the panel analysing for ever.
            (FairnessAnalysis?, FairnessTable?, string?) result;
            try
            {
                result = Analyse(work());
            }
            catch (Exception e)
            {
                result = (null, null, e.Message);
            }

            Callable.From(() => Show(run, edits, result)).CallDeferred();
        });
    }

    private static (FairnessAnalysis? Analysis, FairnessTable? Table, string? Error) Analyse(GameState state)
    {
        try
        {
            var analysis = new MapAnalysis(state).Fairness();
            return (analysis, MapReport.Table(state, analysis), null);
        }
        catch (Exception e)
        {
            return (null, null, e.Message);
        }
    }

    private void SetAnalysing(bool analysing)
    {
        if (IsAnalysing != analysing)
        {
            IsAnalysing = analysing;
            Changed?.Invoke();
        }
    }

    /// <summary>On the main thread: shows a run's numbers, or why there are none.</summary>
    private void Show(int run, int edits, (FairnessAnalysis? Analysis, FairnessTable? Table, string? Error) result)
    {
        if (!IsInstanceValid(this) || run != _run)
        {
            return; // the editor was rebuilt, or the numbers were asked for again since
        }

        SetAnalysing(false);
        if (result is not { Analysis: { } analysis, Table: { } table })
        {
            // A state the analysis cannot take (in the middle of editing): said so, and tried again after the next edit or when shown again.
            _stale = true;
            _summary.Text = $"The numbers could not be computed: {result.Error}";
            return;
        }

        Analysis = analysis;
        _stale = edits != _edits; // an edit made while the numbers were worked out: computed again after it, or when the tab shows

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
