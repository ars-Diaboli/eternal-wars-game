using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The quest log (EVT-5, Q): every quest the player's heroes were shown (<see cref="MapEventState.QuestLog"/>), open ones first, each with its quest
/// object, where it stands, the stage and the stage's proposal; "Show" moves the camera there. Opened from the HUD (or Q), closed with Escape,
/// Q or "Close"; it follows <see cref="QuestLogChanged"/> while open.
/// </summary>
public partial class QuestLogPanel : PanelContainer
{
    private readonly Label _title = new();
    private readonly VBoxContainer _list = new() { CustomMinimumSize = new Vector2(620, 0) };
    private GameSession _session = null!;
    private PlayerId _player;

    /// <summary>"Show" on an entry: the quest object's position (its entrance).</summary>
    public event Action<MapPosition>? ShowRequested;

    public void Bind(GameSession session)
    {
        _session = session;
        this.CenterAsScreen();
        var box = new VBoxContainer();
        AddChild(box);
        var header = new HBoxContainer();
        box.AddChild(header);
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(_title);
        var close = new Button { Text = "Close" };
        close.Pressed += () => Visible = false;
        header.AddChild(close);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(640, 420), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        box.AddChild(scroll);
        scroll.AddChild(_list);
        Visible = false;

        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Visible = false);
        this.SubscribeWhileInTree<QuestLogChanged>(session.Events, e =>
        {
            if (Visible && e.Player == _player)
            {
                Fill();
            }
        });
    }

    public void Open(PlayerId player)
    {
        _player = player;
        Fill();
        Visible = true;
    }

    /// <summary>Opens the log for the player, or closes it when it is open (the Q key).</summary>
    public void Toggle(PlayerId player)
    {
        if (Visible)
        {
            Visible = false;
        }
        else
        {
            Open(player);
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Visible = false;
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>The lines of an entry: the quest object and where it stands, the stage or "done", and the stage's text.</summary>
    public static (string Heading, string Text) Describe(GameState state, QuestLogEntry entry)
    {
        var site = state.FindObject(entry.Site);
        var name = site?.Definition.Name ?? "Quest";
        var where = site is null ? "" : $" at ({site.VisitPosition.X}, {site.VisitPosition.Y}{(state.Map.Levels > 1 ? $", level {site.VisitPosition.Level + 1}" : "")})";
        var stages = state.MapEvents.Set.Find(entry.Quest)?.Stages.Count ?? 0;
        var status = entry.Done ? "done" : stages > 1 ? $"stage {entry.Stage + 1} of {stages}" : "open";
        return ($"{name}{where}: {status}", state.MapEvents.QuestText(entry));
    }

    private void Fill()
    {
        foreach (var child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }

        var state = _session.State;
        var log = state.MapEvents.QuestLog(_player).OrderBy(e => e.Done).ToList();
        _title.Text = $"Quests of {state.GetPlayer(_player).Name}: {log.Count(e => !e.Done)} open, {log.Count(e => e.Done)} done";
        if (log.Count == 0)
        {
            _list.AddChild(new Label { Text = "No quests yet. Seer's Huts, Quest Guards and Quest Gates tell theirs when a hero visits." });
            return;
        }

        foreach (var entry in log)
        {
            var (heading, text) = Describe(state, entry);
            var row = new HBoxContainer();
            _list.AddChild(row);
            var lines = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(lines);
            lines.AddChild(new Label { Text = heading, Modulate = entry.Done ? new Color(0.6f, 0.6f, 0.6f) : Colors.White });
            lines.AddChild(new Label
            {
                Text = text,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(520, 0),
                Modulate = entry.Done ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.9f, 0.85f, 0.7f),
            });
            if (state.FindObject(entry.Site) is { } site)
            {
                var show = new Button { Text = "Show", SizeFlagsVertical = SizeFlags.ShrinkBegin };
                var position = site.VisitPosition;
                show.Pressed += () =>
                {
                    Visible = false;
                    ShowRequested?.Invoke(position);
                };
                row.AddChild(show);
            }

            _list.AddChild(new HSeparator());
        }
    }
}
