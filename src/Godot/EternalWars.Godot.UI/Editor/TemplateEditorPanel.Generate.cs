using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using EternalWars.Core.Generation;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The template editor's Generate and Generate many (ADR-274): the map generated off the main thread and shown in the Preview tab beside the
/// graph, "Open in map editor" to edit it; and a batch of seeds checked off the main thread, as a gallery of thumbnails with each seed's
/// numbers and their spread in the Seeds tab.
/// </summary>
public partial class TemplateEditorPanel
{
    private const int GraphTab = 0;
    private const int PreviewTab = 1;
    private const int SeedsTab = 2;

    private readonly TabContainer _views = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly ZonePictureView _preview = new()
    {
        Name = "PreviewMap", ShowLabels = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
    };

    private readonly Label _previewInfo = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly OptionButton _previewLevel = new() { Visible = false };
    private readonly SpinBox _count = new()
    {
        MinValue = 1, MaxValue = TemplateBatch.MaxCount, Value = TemplateBatch.DefaultCount, TooltipText = "How many seeds, from the seed in the top row up",
    };

    private readonly Label _batchInfo = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly HFlowContainer _gallery = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly List<BatchMap> _batchMaps = [];

    /// <summary>
    /// The maps handed to the map editor: the editor's from then on, so opening one again generates a fresh copy. Weak, so a map closed in
    /// the editor is not kept alive by the panel.
    /// </summary>
    private readonly ConditionalWeakTable<EditorMap, object> _handedOver = [];

    private Button _open = null!;
    private Button _runBatch = null!;
    private Button _cancelBatch = null!;

    /// <summary>The map in the preview, and what it was generated from.</summary>
    private Shown? _shown;

    /// <summary>The newest Generate: an older one finishing later is dropped.</summary>
    private int _generation;

    /// <summary>Generates whose result has not come back yet.</summary>
    private int _pending;

    private CancellationTokenSource? _batch;

    /// <summary>
    /// What the workers (Generate's and the batch's) hand to the main thread, each action checking it is still wanted: drained each frame
    /// while one runs (worker threads calling into Godot at once is not safe).
    /// </summary>
    private readonly ConcurrentQueue<Action> _arrived = new();
    private (MapTemplate Template, int Size, ulong First, int Count)? _batchOf;

    /// <summary>How the last batch stopped early ("Cancelled", "Failed"); null while it runs or when it ran to the end.</summary>
    private string? _batchStopped;

    private sealed record Shown(MapTemplate Template, int Size, ulong Seed, EditorMap Map);

    /// <summary>The Graph, Preview and Seeds tabs; <paramref name="graph"/> is the graph with its tools.</summary>
    private TabContainer Views(Control graph)
    {
        graph.Name = "Graph";
        _views.AddChild(graph);

        var preview = new VBoxContainer { Name = "Preview" };
        _views.AddChild(preview);
        var row = new HBoxContainer();
        preview.AddChild(row);
        _open = ToolButton(row, "Open in map editor", "Edit this map in the map editor, as a new map", OpenPreview);
        _open.Disabled = true;
        _previewLevel.AddItem("Surface");
        _previewLevel.AddItem("Underground");
        _previewLevel.ItemSelected += level => _preview.Show(_preview.Picture, (int)level);
        row.AddChild(_previewLevel);
        row.AddChild(_previewInfo);
        _previewInfo.Text = "Generate shows the map here: the zones outlined and named.";
        preview.AddChild(_preview);

        var seeds = new VBoxContainer { Name = "Seeds" };
        _views.AddChild(seeds);
        var top = new HBoxContainer();
        seeds.AddChild(top);
        top.AddChild(new Label { Text = "Maps" });
        top.AddChild(_count);
        _runBatch = ToolButton(top, "Generate many", "Generate and check that many seeds from the top row's, at its size", GenerateMany);
        _cancelBatch = ToolButton(top, "Cancel", "Stop the batch; the maps checked stay", () => _batch?.Cancel());
        _cancelBatch.Disabled = true;
        seeds.AddChild(_batchInfo);
        _batchInfo.Text = "Generate many checks the template for many seeds, as sim maps does: problems, the same map twice, starts reachable, and how fair the starts are.";
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        seeds.AddChild(scroll);
        scroll.AddChild(_gallery);

        _views.WithTabIcon(GraphTab, UiIcon.Arrange);
        _views.WithTabIcon(PreviewTab, UiIcon.Generate);
        _views.WithTabIcon(SeedsTab, UiIcon.Generate);
        return _views;
    }

    /// <summary>The panel is freed: its batch stops (it leaves the tree each time a map opens, and the batch runs on then).</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            _batch?.Cancel();
        }
    }

    public override void _Ready() => SetProcess(false);

    /// <summary>Runs what the workers handed over; idle once no Generate and no batch runs.</summary>
    public override void _Process(double delta)
    {
        while (_arrived.TryDequeue(out var arrived))
        {
            arrived();
        }

        if (_batch is null && _pending == 0)
        {
            SetProcess(false);
        }
    }

    /// <summary>The template, size and seed of the top row; null (and why in the message) when they cannot be generated.</summary>
    private (MapTemplate Template, int Size, ulong Seed)? Request()
    {
        if (!ulong.TryParse(_seed.Text.Trim(), CultureInfo.InvariantCulture, out var seed))
        {
            _message.Text = "Enter a numeric seed.";
            return null;
        }

        if (!TemplateValidator.IsValid(_session.Template, _host.Content))
        {
            _message.Text = "Fix the errors first.";
            return null;
        }

        return (_session.Template, _size.GetSelectedId(), seed);
    }

    /// <summary>Generates the template off the main thread and shows the map in the Preview tab; a newer Generate replaces one still running.</summary>
    private void Generate()
    {
        if (Request() is not var (template, size, seed))
        {
            return;
        }

        var run = ++_generation;
        var content = _host.Content;
        _pending++;
        SetProcess(true);
        _message.Text = $"Generating {template.Name} {size}×{size}, seed {seed}…";
        Task.Run(() =>
        {
            // Nothing may escape the task: the message would say "Generating" for ever.
            EditorMap? map = null;
            ZonePicture? picture = null;
            string? failure = null;
            try
            {
                map = TemplateBatch.Generate(template, size, seed, content);
                picture = ZonePicture.Of(map);
            }
            catch (Exception e)
            {
                failure = e is ArgumentException ? e.Message : $"The generator failed: {e.Message}";
            }

            _arrived.Enqueue(() =>
            {
                _pending--;
                if (run == _generation)
                {
                    Generated(new Shown(template, size, seed, map!), picture, failure);
                }
            });
        });
    }

    private void Generated(Shown shown, ZonePicture? picture, string? failure)
    {
        if (failure is not null)
        {
            _message.Text = failure;
            return;
        }

        ShowPreview(shown, picture!);
        if (_views.CurrentTab != SeedsTab) // not away from a batch being watched
        {
            _views.CurrentTab = PreviewTab;
        }

        var problems = shown.Map.Problems.Count == 0 ? "" : $" Generator problems: {string.Join(" ", shown.Map.Problems)}";
        _message.Text = $"Generated {shown.Template.Name} {shown.Size}×{shown.Size}, seed {shown.Seed}{Attempt(shown.Map)}: in the preview.{problems}";
    }

    private void ShowPreview(Shown shown, ZonePicture picture)
    {
        _shown = shown;
        _preview.Show(picture);
        _previewLevel.Visible = picture.Levels > 1;
        _previewLevel.Select(0);
        _open.Disabled = false;
        ShowPreviewInfo();
    }

    /// <summary>What the preview shows, and whether the template changed since.</summary>
    private void ShowPreviewInfo()
    {
        if (_shown is not { } shown)
        {
            return;
        }

        var issues = shown.Map.Issues.Count == 0 ? "" : $", {shown.Map.Issues.Count} validator issue(s)";
        var stale = ReferenceEquals(shown.Template, _session.Template) ? "" : " The template changed since: Generate again.";
        _previewInfo.Text = $"{shown.Template.Name} {shown.Size}×{shown.Size}, seed {shown.Seed}{Attempt(shown.Map)}{issues}.{stale}";
    }

    private static string Attempt(EditorMap map) => map.Generation.Attempt == 0 ? "" : $" (attempt {map.Generation.Attempt})";

    /// <summary>The previewed map in the map editor; one handed over already is generated again (the same seed makes the same map).</summary>
    private void OpenPreview()
    {
        if (_shown is not { } shown)
        {
            return;
        }

        var map = shown.Map;
        if (!_handedOver.TryAdd(map, true))
        {
            try
            {
                map = TemplateBatch.Generate(shown.Template, shown.Size, shown.Seed, _host.Content);
            }
            catch (ArgumentException e)
            {
                _message.Text = e.Message;
                return;
            }
        }

        _host.Open(shown.Template, shown.Size, shown.Seed, map);
    }

    // Generate many -------------------------------------------------------------------------------------------------------------------

    /// <summary>Starts a batch of seeds from the top row's, at its size.</summary>
    private void GenerateMany()
    {
        if (Request() is not var (template, size, seed))
        {
            return;
        }

        var cancel = _batch = new CancellationTokenSource();
        var count = (int)_count.Value;
        _batchOf = (template, size, seed, count);
        _batchStopped = null;
        _batchMaps.Clear();
        foreach (var card in _gallery.GetChildren())
        {
            _gallery.RemoveChild(card); // out now: the new cards are placed by index
            card.QueueFree();
        }

        _runBatch.Disabled = true;
        _cancelBatch.Disabled = false;
        ShowBatchInfo();
        void Later(Action action) => _arrived.Enqueue(() =>
        {
            if (_batch == cancel)
            {
                action();
            }
        });
        SetProcess(true);

        var run = TemplateBatch.RunAsync(template, size, seed, count, _host.Content, done: map =>
        {
            var picture = ZonePicture.Of(map.Map); // off the main thread, like the map
            Later(() => Checked(map, picture));
        }, cancel: cancel.Token);
        run.ContinueWith(task => Later(() => BatchEnded(task)), TaskScheduler.Default);
    }

    private void Checked(BatchMap map, ZonePicture picture)
    {
        // In seed order, which counts up from the first seed and may wrap past the largest.
        var first = _batchOf!.Value.First;
        var index = _batchMaps.Count(m => unchecked(m.Seed - first) < unchecked(map.Seed - first));
        _batchMaps.Insert(index, map);
        var card = Card(map, picture);
        _gallery.AddChild(card);
        _gallery.MoveChild(card, index);
        ShowBatchInfo();
    }

    private void BatchEnded(Task<IReadOnlyList<BatchMap>> task)
    {
        _batch?.Dispose();
        _batch = null;
        _batchStopped = task.IsCanceled ? "Cancelled" : task.IsFaulted ? "Failed" : null;
        _runBatch.Disabled = false;
        _cancelBatch.Disabled = true;
        if (task.IsFaulted && task.Exception?.InnerException is { } error and not OperationCanceledException)
        {
            _message.Text = error is ArgumentException ? error.Message : $"The batch failed: {error.Message}";
        }

        ShowBatchInfo();
    }

    /// <summary>The batch's numbers so far: how many passed, and the spread of the attempts and of each measure's imbalance.</summary>
    private void ShowBatchInfo()
    {
        if (_batchOf is not var (template, size, _, count))
        {
            return;
        }

        var summary = TemplateBatch.Summarize(_batchMaps);
        var state = _batch is not null ? $"{summary.Maps} of {count} maps checked"
            : _batchStopped is { } stopped ? $"{stopped} after {summary.Maps} of {count} maps"
            : $"{count} maps";
        var stale = ReferenceEquals(template, _session.Template) ? "" : " The template changed since: Generate many again.";
        var lines = new List<string> { $"{template.Name} {size}×{size}: {state}, {summary.Passed} passed.{stale}" };
        if (summary.Maps > 0)
        {
            lines.Add($"Attempts {Range(summary.Attempts, "")}. How far apart the starts are (smallest, mean, largest over the maps): " +
                string.Join(", ", summary.Imbalance.Select(m => $"{MeasureName(m.Key)} {Range(m.Value, "%")}")) + ".");
        }

        _batchInfo.Text = string.Join("\n", lines);
    }

    private static string Range(Spread spread, string unit) =>
        $"{spread.Min:0}{unit} / {spread.Mean:0.#}{unit} / {spread.Max:0}{unit}";

    private static string MeasureName(FairnessMeasure measure) => measure switch
    {
        FairnessMeasure.MineDistance => "mine distance",
        FairnessMeasure.TreasureValue => "treasure",
        _ => "guards",
    };

    /// <summary>A seed's thumbnail and numbers; pressed, the map goes to the preview and its seed to the top row.</summary>
    private Button Card(BatchMap map, ZonePicture thumbnail)
    {
        var card = new Button
        {
            CustomMinimumSize = new Vector2(168, 250),
            ClipContents = true,
            TooltipText = map.Passed ? $"Seed {map.Seed}: every check passed" : $"Seed {map.Seed}:\n{string.Join("\n", map.Failures)}",
        };
        var rows = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        rows.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 4);
        card.AddChild(rows);
        var picture = new ZonePictureView { CustomMinimumSize = new Vector2(160, 160), Resolution = 160, MouseFilter = MouseFilterEnum.Ignore };
        rows.AddChild(picture);
        picture.Show(thumbnail);
        var verdict = map.Passed ? "ok" : map.Failures.Count == 1 ? "1 failure" : $"{map.Failures.Count} failures";
        var text = new Label
        {
            Text = $"Seed {map.Seed}\n{verdict}{Attempt(map.Map)}\n" +
                string.Join("  ", Enum.GetValues<FairnessMeasure>().Select(m => $"{MeasureName(m)[0..1].ToUpperInvariant()} {map.Imbalance(m):0}%")),
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipText = true,
        };
        text.AddThemeFontSizeOverride("font_size", 12);
        if (!map.Passed)
        {
            text.AddThemeColorOverride("font_color", UiTheme.IssueError);
        }

        rows.AddChild(text);
        card.Pressed += () =>
        {
            _seed.Text = map.Seed.ToString(CultureInfo.InvariantCulture);
            if (_batchOf is var (template, size, _, _))
            {
                ShowPreview(new Shown(template, size, map.Seed, map.Map), thumbnail);
                _views.CurrentTab = PreviewTab;
                _message.Text = $"Seed {map.Seed} in the preview";
            }
        };
        return card;
    }
}
