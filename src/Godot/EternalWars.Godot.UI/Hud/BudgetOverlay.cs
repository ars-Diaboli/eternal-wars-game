using System.Diagnostics;
using EternalWars.Core.Domain.Towns;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The rendering budget overlay (ADR-308): triangles, draw calls, video memory and frame time against the town scene's budget
/// (<see cref="TownSceneBudget"/>), and while a town scene shows the time it took to open (ADR-306), in the top right corner over every screen. F3 shows and hides it; <c>--budget</c> starts the game with it
/// shown and writes its numbers to the log every ten frames (a second of a smoke's movie), so a smoke run can be measured. The counts are the
/// last frame's; the times the mean of the frames since the last refresh (half a second, or the ten frames). It reads the renderer each frame while shown, which has no events to subscribe to. A number over its limit is red.
/// The frame time's limit is the Deck's when the game runs on one (Steam sets <c>SteamDeck=1</c>), the reference PC's otherwise.
/// </summary>
public partial class BudgetOverlay : CanvasLayer
{
    /// <summary>How often the numbers are refreshed, in seconds.</summary>
    private const double RefreshSeconds = 0.5;

    /// <summary>How many frames each logged line covers (<c>--budget</c>): a second of a smoke's movie.</summary>
    private const int LogFrames = 10;

    private readonly Label _title = new();
    private readonly GridContainer _grid = new() { Columns = 3 };
    private readonly List<(Label Name, Label Value, Label Limit)> _rows = [];
    private readonly Stopwatch _sinceRefresh = new();
    private double _cpu;
    private double _gpu;
    private int _frames;
    private bool _warmingUp;
    private Rid _viewport;

    public BudgetOverlay()
    {
        Name = "BudgetOverlay";
        Layer = 100; // over everything, dialogs and the information window included
        Visible = false;
    }

    /// <summary>Where it is measured: the Deck when the game runs on one.</summary>
    public static BudgetTarget Target => OS.GetEnvironment("SteamDeck") == "1" ? BudgetTarget.SteamDeck : BudgetTarget.ReferencePc;

    /// <summary>Writes the numbers to the log every ten frames (<c>--budget</c>).</summary>
    public bool LogNumbers { get; set; }

    /// <summary>What is on screen, for the log's lines (the town scene's town type); null for nothing worth naming.</summary>
    public Func<string?>? Showing { get; set; }

    /// <summary>How long the town scene shown took to open (ADR-306); null while none shows.</summary>
    public Func<double?>? OpenTime { get; set; }

    /// <summary>The numbers shown last; null before the first refresh.</summary>
    public FrameSample? Last { get; private set; }

    public override void _Ready()
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.88f),
            ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 8,
        });
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        panel.GrowHorizontal = Control.GrowDirection.Begin;
        panel.OffsetRight = -12;
        panel.OffsetTop = 12;
        AddChild(panel);
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddChild(column);
        _title.Text = $"Town scene budget ({(Target == BudgetTarget.SteamDeck ? "Steam Deck" : "reference PC")})";
        column.AddChild(_title);
        _grid.AddThemeConstantOverride("h_separation", 16);
        column.AddChild(_grid);
        for (var row = 0; row < 5; row++) // the four numbers, and the opening time while a town scene shows
        {
            var name = new Label();
            var value = new Label { HorizontalAlignment = HorizontalAlignment.Right };
            var limit = new Label { Modulate = UiTheme.TextQuiet };
            _grid.AddChild(name);
            _grid.AddChild(value);
            _grid.AddChild(limit);
            _rows.Add((name, value, limit));
        }

        _viewport = GetViewport().GetViewportRid();
        SetProcess(Visible);
        if (Visible)
        {
            Measure(true);
        }
    }

    /// <summary>
    /// Renders the 3D scenes <paramref name="width"/> pixels wide whatever the window's width, by the viewport's 3D scale (a smoke's movie is the
    /// project's 1600 × 900; at 2560 its 3D costs what 1440p does, the bars over it less).
    /// </summary>
    public void RenderAt(int width)
    {
        var viewport = GetViewport() ?? throw new InvalidOperationException("The overlay is not in the tree.");
        // The 3D renders at the window's own pixels, which a UI scale (ADR-309) makes wider than the visible rect.
        void Scale() => viewport.Scaling3DScale = Mathf.Clamp(width / (float)GetWindow().Size.X, 0.25f, 2f);
        Scale();
        viewport.SizeChanged += Scale; // still that wide after a resize or Alt+Enter
        _title.Text += $", 3D at {width} wide";
    }

    /// <summary>Shows or hides it (F3).</summary>
    public void Toggle()
    {
        Visible = !Visible;
        if (IsInsideTree())
        {
            SetProcess(Visible);
            Measure(Visible);
        }
    }

    public override void _ExitTree() => Measure(false);

    public override void _Process(double delta)
    {
        _cpu += Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000
            + RenderingServer.GetFrameSetupTimeCpu() + RenderingServer.ViewportGetMeasuredRenderTimeCpu(_viewport);
        _gpu += RenderingServer.ViewportGetMeasuredRenderTimeGpu(_viewport);
        _frames++;
        if (_warmingUp)
        {
            if (_frames >= 3)
            {
                (_cpu, _gpu, _frames, _warmingUp) = (0, 0, 0, false); // the GPU's timings come a frame or two after measuring starts: those zeros are dropped
                _sinceRefresh.Restart();
            }

            return;
        }

        if (LogNumbers ? _frames < LogFrames : _sinceRefresh.Elapsed.TotalSeconds < RefreshSeconds)
        {
            return; // logging, it counts frames: a smoke's movie runs at a fixed 10 frames a second of game time, whatever the clock says
        }

        Last = new FrameSample(
            (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalPrimitivesInFrame),
            (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame),
            (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.VideoMemUsed),
            _cpu / _frames, _gpu / _frames)
        {
            ShadowDrawCalls = RenderingServer.ViewportGetRenderInfo(
                _viewport, RenderingServer.ViewportRenderInfoType.Shadow, RenderingServer.ViewportRenderInfo.DrawCallsInFrame),
            OpenMilliseconds = OpenTime?.Invoke(),
        };
        var lines = TownSceneBudget.Lines(Last, Target);
        for (var row = 0; row < _rows.Count; row++)
        {
            var (name, value, limit) = _rows[row];
            name.Visible = value.Visible = limit.Visible = row < lines.Count;
            if (row < lines.Count)
            {
                (name.Text, value.Text, limit.Text) = (lines[row].Label, lines[row].Value, $"of {lines[row].Limit}");
                value.Modulate = lines[row].Over ? UiTheme.IssueError : Colors.White;
            }
        }

        if (LogNumbers)
        {
            GD.Print($"Budget [{Showing?.Invoke() ?? "-"}]: {TownSceneBudget.Describe(Last, Target)}");
        }

        (_cpu, _gpu, _frames) = (0, 0, 0);
        _sinceRefresh.Restart();
    }

    /// <summary>The viewport's render times are measured only while the overlay is shown: measuring costs a little.</summary>
    private void Measure(bool on)
    {
        if (_viewport.IsValid)
        {
            RenderingServer.ViewportSetMeasureRenderTime(_viewport, on);
        }

        (_cpu, _gpu, _frames, _warmingUp) = (0, 0, 0, on);
        _sinceRefresh.Restart();
    }
}
