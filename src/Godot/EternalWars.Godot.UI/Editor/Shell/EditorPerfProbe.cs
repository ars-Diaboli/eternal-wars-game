using System.Diagnostics;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>What the <see cref="EditorPerfProbe"/> drives in the editor (ADR-278).</summary>
/// <param name="Open">Opens a map file the way File → Open does.</param>
/// <param name="ReadMs">How long the last opening took to read its file into a game state.</param>
/// <param name="Map">The map being edited.</param>
/// <param name="MapView">The map view's rectangle, in UI points.</param>
/// <param name="UseTerrainBrush">Picks the Terrain tool.</param>
/// <param name="Problems">The Problems panel of the map being edited.</param>
/// <param name="Snapshot">The validation's two parts: the copy on the main thread, returning the check that runs on a task.</param>
public sealed record EditorPerfHost(
    Action<string> Open,
    Func<double> ReadMs,
    Func<AdventureMap> Map,
    Func<Rect2> MapView,
    Action UseTerrainBrush,
    Func<ProblemsPanel> Problems,
    Func<Func<IReadOnlyList<MapIssue>>> Snapshot);

/// <summary>
/// Times the editor on a large map (ADR-278, <c>tools/perf-editor.ps1</c>): opens it, lets it settle, times idle frames, paints the terrain with
/// a stroke of real mouse events over the map view, times the frames until the Problems panel has checked the edit, and times the validation's
/// two parts on their own. Writes <see cref="EditorTimings"/> to the report file and quits.
/// </summary>
public partial class EditorPerfProbe : Node
{
    private const int StartFrames = 10;
    private const int IdleFrames = 120;
    private const int PaintFrames = 300;
    private const int RowFrames = 30;
    private const double SettleTimeoutMs = 60_000;

    private readonly List<double> _idle = [];
    private readonly List<double> _painting = [];
    private readonly List<double> _validating = [];
    private readonly Stopwatch _clock = new();
    private EditorPerfHost _host = null!;
    private string _map = "";
    private string _report = "";
    private Phase _phase;
    private int _frame;
    private ulong _lastTicks;
    private double _openStarted;
    private double _openMs;
    private double _readMs;
    private double _phaseStarted;
    private bool _checked;
    private Vector2 _pointer;
    private AdventureMap? _startMap;

    private enum Phase
    {
        Start,
        Opening,
        Settling,
        Idle,
        Painting,
        Validating,
        Done,
    }

    /// <summary>Times the editor on the map at <paramref name="map"/> and writes the report to <paramref name="report"/>.</summary>
    public void Bind(EditorPerfHost host, string map, string report)
    {
        _host = host;
        _map = map;
        _report = report;
        Engine.MaxFps = 0; // as fast as it goes: the frame times are the work's (vsync is off on the command line)
        ProcessPriority = int.MinValue; // first in the frame: a frame's time runs from one start of a frame to the next
        _clock.Start();
    }

    public override void _Process(double delta)
    {
        var ticks = Time.GetTicksUsec();
        var frameMs = _lastTicks == 0 ? 0 : (ticks - _lastTicks) / 1000.0;
        _lastTicks = ticks;
        _frame++;
        switch (_phase)
        {
            case Phase.Start when _frame >= StartFrames:
                _openStarted = _clock.Elapsed.TotalMilliseconds;
                _startMap = _host.Map();
                _phase = Phase.Opening;
                try
                {
                    _host.Open(_map);
                }
                catch (Exception e)
                {
                    Fail($"opening the map threw {e.GetType().Name}: {e.Message}");
                    return;
                }

                if (ReferenceEquals(_host.Map(), _startMap))
                {
                    Fail("the map did not open (see the Output panel's message in the log)");
                    return;
                }

                _readMs = _host.ReadMs();
                RenderingServer.FramePostDraw += Opened;
                break;
            case Phase.Opening when Now - _openStarted > SettleTimeoutMs:
                Fail("no frame was drawn after opening the map");
                break;
            case Phase.Settling when !_host.Problems().IsChecking || Now - _phaseStarted > SettleTimeoutMs:
                Enter(Phase.Idle);
                break;
            case Phase.Idle:
                _idle.Add(frameMs);
                if (_idle.Count >= IdleFrames)
                {
                    StartStroke();
                }

                break;
            case Phase.Painting:
                _painting.Add(frameMs);
                if (_painting.Count >= PaintFrames)
                {
                    EndStroke();
                }
                else
                {
                    MoveStroke(_painting.Count);
                }

                break;
            case Phase.Validating:
                _validating.Add(frameMs);
                if ((_checked && !_host.Problems().IsChecking) || Now - _phaseStarted > SettleTimeoutMs)
                {
                    Finish();
                }

                break;
        }
    }

    private double Now => _clock.Elapsed.TotalMilliseconds;

    /// <summary>The first frame that shows the opened map is drawn: the opening is over.</summary>
    private void Opened()
    {
        RenderingServer.FramePostDraw -= Opened;
        _openMs = Now - _openStarted;
        Enter(Phase.Settling);
    }

    private void Enter(Phase phase)
    {
        _phase = phase;
        _phaseStarted = Now;
    }

    /// <summary>The Terrain tool, and the left button pressed in the middle of the map view.</summary>
    private void StartStroke()
    {
        _host.UseTerrainBrush();
        _pointer = StrokePoint(0);
        Send(new InputEventMouseMotion { Position = _pointer });
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = _pointer });
        Enter(Phase.Painting);
    }

    /// <summary>The stroke moves on every frame, as a hand drags it.</summary>
    private void MoveStroke(int step)
    {
        var to = StrokePoint(step);
        Send(new InputEventMouseMotion { Position = to, Relative = to - _pointer, ButtonMask = MouseButtonMask.Left });
        _pointer = to;
    }

    private void EndStroke()
    {
        _host.Problems().Changed += Checked;
        Send(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = _pointer });
        Enter(Phase.Validating);
    }

    private void Checked()
    {
        if (!_host.Problems().IsChecking)
        {
            _checked = true;
        }
    }

    /// <summary>
    /// Where the stroke is on a frame: rows across the map view, back and forth, a row every <see cref="RowFrames"/> frames, so every frame
    /// paints ground the stroke has not painted yet.
    /// </summary>
    private Vector2 StrokePoint(int step)
    {
        var view = _host.MapView();
        var (row, along) = (step / RowFrames, (step % RowFrames) / (float)(RowFrames - 1));
        var across = row % 2 == 0 ? along : 1 - along;
        var rows = PaintFrames / RowFrames;
        return view.Position + (view.Size * new Vector2(0.1f + (0.8f * across), 0.1f + (0.8f * row / (rows - 1))));
    }

    /// <summary>An event at a point in UI points, sent as the window would (in its pixels).</summary>
    private void Send(InputEventMouse @event)
    {
        var window = GetViewport().GetFinalTransform();
        @event.Position = window * @event.Position;
        @event.GlobalPosition = @event.Position;
        if (@event is InputEventMouseMotion motion)
        {
            motion.Relative = window.BasisXform(motion.Relative);
        }

        Input.ParseInputEvent(@event);
    }

    /// <summary>A run that cannot be timed: the reason as the report, and the editor closed with a failure.</summary>
    private void Fail(string reason)
    {
        _phase = Phase.Done;
        RenderingServer.FramePostDraw -= Opened;
        var text = $"FAILED: {reason}";
        GD.PrintErr(text);
        try
        {
            File.WriteAllText(_report, text + "\n");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            GD.PrintErr($"Could not write {_report}: {e.Message}");
        }

        GetTree().Quit(4);
    }

    /// <summary>The validation's parts on their own, then the report written and the editor closed.</summary>
    private void Finish()
    {
        try
        {
            Report();
        }
        catch (Exception e)
        {
            Fail($"the report threw {e.GetType().Name}: {e.Message}");
        }
    }

    private void Report()
    {
        _phase = Phase.Done;
        _host.Problems().Changed -= Checked;
        var watch = Stopwatch.StartNew();
        var check = _host.Snapshot();
        var snapshotMs = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        check();
        var validateMs = watch.Elapsed.TotalMilliseconds;
        var map = _host.Map();
        var timings = new EditorTimings(
            $"{Path.GetFileName(_map)}, {map.Width} × {map.Height}, {map.Levels} level(s)", _openMs, _readMs,
            FrameStats.Of(_idle), FrameStats.Of(_painting), FrameStats.Of(_validating), snapshotMs, validateMs, _checked);
        var text = timings.Text();
        File.WriteAllText(_report, text + "\n");
        GD.Print(text);
        GetTree().Quit(timings.Misses().Count == 0 ? 0 : 3);
    }
}
