using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Scenarios;
using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// What the new-map wizard ends with (ADR-259): a blank map of <see cref="Settings"/>, or, with a <see cref="Template"/> (an index into the
/// templates it was given), the Generate tab set to that template at <see cref="Size"/> for the first <see cref="Settings"/> players.
/// </summary>
public sealed record NewMapRequest(NewMapSettings Settings, int? Template = null, int Size = 0);

/// <summary>
/// The new-map wizard (ADR-259), File → New map or Ctrl+N, in three steps: the size (a preset or any size from 16 to 256 a side), the levels and
/// the ground; the players and their kind; a template or none. It ends on a blank map or in the Generate tab, set to the template at its size
/// nearest to the one chosen and to the players (the template then decides the ground and the levels). Everything is picked from buttons.
/// Cancel or Escape closes it.
/// </summary>
public partial class NewMapWizard : Control
{
    private const int Other = -1;
    private static readonly string[] StepTitles = ["Size and ground", "Players", "Template"];

    private readonly Label _title = new();
    private readonly List<VBoxContainer> _steps = [];
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(620, 0) };
    private readonly Button _back = new() { Text = "Back", CustomMinimumSize = new Vector2(110, 0) };
    private readonly Button _next = new() { Text = "Next", CustomMinimumSize = new Vector2(110, 0) };
    private readonly Button _finish = new() { CustomMinimumSize = new Vector2(150, 0) };
    private readonly SpinBox _width = new() { MinValue = MapSizes.MinDimension, MaxValue = MapSizes.MaxDimension, Prefix = "width", CustomMinimumSize = new Vector2(120, 0) };
    private readonly SpinBox _height = new() { MinValue = MapSizes.MinDimension, MaxValue = MapSizes.MaxDimension, Prefix = "height", CustomMinimumSize = new Vector2(120, 0) };
    private readonly HBoxContainer _otherSize = new();
    private readonly SpinBox _players = new() { MinValue = 1, MaxValue = GameState.MaxPlayers, Step = 1 };
    private readonly VBoxContainer _kinds = new();
    private readonly Label _templateNote = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(620, 0) };
    private readonly List<bool> _humans = [];
    private IReadOnlyList<GeneratorTemplate> _templates = [];
    private Action<NewMapRequest> _done = _ => { };
    private int _step;
    private int _size;
    private bool _underground;
    private TerrainType _terrain = TerrainType.Grass;
    private int? _template;

    /// <summary>The step shown, from 0.</summary>
    public int Step => _step;

    /// <summary>Fills the wizard (it is in the tree): <paramref name="size"/> is the size it starts at (the preferences' default).</summary>
    public void Bind(int size, IReadOnlyList<GeneratorTemplate> templates, Action<NewMapRequest> done)
    {
        _templates = templates;
        _done = done;
        var defaults = NewMapSettings.Default(size);
        _size = MapSizes.Standard.Contains(size) ? size : Other;
        (_width.Value, _height.Value) = (defaults.Width, defaults.Height);
        _humans.AddRange(defaults.Humans);

        var rows = EditorOverlay.Build(this);
        rows.AddChild(new Label { Text = "New map", HorizontalAlignment = HorizontalAlignment.Center });
        rows.AddChild(_title);
        _steps.Add(SizeStep());
        _steps.Add(PlayersStep());
        _steps.Add(TemplateStep());
        foreach (var step in _steps)
        {
            rows.AddChild(step);
        }

        rows.AddChild(_status);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        rows.AddChild(buttons);
        _back.Pressed += () => Show(_step - 1);
        _next.Pressed += () => Show(_step + 1);
        _finish.Pressed += Finish;
        var cancel = new Button { Text = "Cancel", CustomMinimumSize = new Vector2(110, 0) };
        cancel.Pressed += () => EditorOverlay.Close(this);
        buttons.AddChild(_back);
        buttons.AddChild(_next);
        buttons.AddChild(_finish);
        buttons.AddChild(cancel);
        Show(0);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            EditorOverlay.Close(this);
        }
    }

    /// <summary>The settings chosen so far.</summary>
    public NewMapSettings Settings => new(
        _size == Other ? (int)_width.Value : _size,
        _size == Other ? (int)_height.Value : _size,
        _underground ? 2 : 1,
        _terrain,
        [.. _humans]);

    private VBoxContainer SizeStep()
    {
        var step = new VBoxContainer();
        step.AddChild(new Label { Text = "Size" });
        var sizes = new HFlowContainer();
        step.AddChild(sizes);
        var group = new ButtonGroup();
        foreach (var size in MapSizes.Standard)
        {
            sizes.AddChild(Choice($"{size} × {size}", group, _size == size, () => PickSize(size)));
        }

        sizes.AddChild(Choice("Other size", group, _size == Other, () => PickSize(Other)));
        _otherSize.AddChild(_width);
        _otherSize.AddChild(_height);
        _otherSize.AddChild(new Label { Text = $"({MapSizes.MinDimension} to {MapSizes.MaxDimension} tiles a side)" });
        _otherSize.Visible = _size == Other;
        step.AddChild(_otherSize);

        step.AddChild(new Label { Text = "Levels" });
        var levels = new HBoxContainer();
        step.AddChild(levels);
        var levelGroup = new ButtonGroup();
        levels.AddChild(Choice("The surface", levelGroup, !_underground, () => _underground = false));
        levels.AddChild(Choice("The surface and the underground", levelGroup, _underground, () => _underground = true));

        step.AddChild(new Label { Text = "Ground" });
        var terrains = new HFlowContainer();
        step.AddChild(terrains);
        var terrainGroup = new ButtonGroup();
        foreach (var terrain in NewMapSettings.Terrains)
        {
            terrains.AddChild(Choice(Words(terrain.ToString()), terrainGroup, terrain == _terrain, () => _terrain = terrain));
        }

        return step;
    }

    private void PickSize(int size)
    {
        _size = size;
        _otherSize.Visible = size == Other;
    }

    private VBoxContainer PlayersStep()
    {
        var step = new VBoxContainer();
        var count = new HBoxContainer();
        step.AddChild(count);
        count.AddChild(new Label { Text = "Players", CustomMinimumSize = new Vector2(100, 0) });
        _players.Value = _humans.Count;
        _players.ValueChanged += value => SetPlayers((int)value);
        count.AddChild(_players);
        step.AddChild(new Label { Text = "Each player is a person or the computer by default; the new-game screen may change it." });
        step.AddChild(_kinds);
        ShowKinds();
        return step;
    }

    private void SetPlayers(int count)
    {
        while (_humans.Count < count)
        {
            _humans.Add(true);
        }

        _humans.RemoveRange(count, _humans.Count - count);
        ShowKinds();
    }

    private void ShowKinds()
    {
        foreach (var child in _kinds.GetChildren())
        {
            _kinds.RemoveChild(child);
            child.QueueFree();
        }

        for (var slot = 0; slot < _humans.Count; slot++)
        {
            var index = slot;
            var row = new HBoxContainer();
            _kinds.AddChild(row);
            row.AddChild(new Label { Text = Player.ColourNames[slot], CustomMinimumSize = new Vector2(100, 0) });
            var group = new ButtonGroup();
            row.AddChild(Choice("Person", group, _humans[slot], () => _humans[index] = true));
            row.AddChild(Choice("Computer", group, !_humans[slot], () => _humans[index] = false));
        }
    }

    private VBoxContainer TemplateStep()
    {
        var step = new VBoxContainer();
        step.AddChild(new Label { Text = "Start blank, or generate the map from a template:" });
        var list = new HFlowContainer();
        step.AddChild(list);
        var group = new ButtonGroup();
        list.AddChild(Choice("None: a blank map", group, true, () => PickTemplate(null)));
        for (var i = 0; i < _templates.Count; i++)
        {
            var index = i;
            var button = Choice(_templates[i].Name, group, false, () => PickTemplate(index));
            button.TooltipText = _templates[i].Description;
            list.AddChild(button);
        }

        step.AddChild(_templateNote);
        return step;
    }

    private void PickTemplate(int? template)
    {
        _template = template;
        ShowFinish();
    }

    /// <summary>The template's size for the chosen one and the players it can take.</summary>
    private (int Size, int Players) Fitted(GeneratorTemplate template)
    {
        var settings = Settings;
        return (MapSizes.Nearest(template.Sizes, Math.Max(settings.Width, settings.Height)),
            Math.Clamp(settings.Humans.Count, template.MinPlayers, template.SlotNames.Count));
    }

    private void ShowFinish()
    {
        if (_template is { } index)
        {
            var template = _templates[index];
            var (size, players) = Fitted(template);
            _templateNote.Text = $"{template.Description}\nYou finish in the Generate tab: {template.Name} at {size} × {size} for {players} " +
                $"player{(players == 1 ? "" : "s")}; the template decides the ground and the levels. Press Generate there.";
            _finish.Text = "Go to Generate";
        }
        else
        {
            _templateNote.Text = "The map opens blank: the ground in one terrain, a hero for each player.";
            _finish.Text = "Create map";
        }
    }

    private void Show(int step)
    {
        if (step == 1 && Settings.Problem() is { } problem)
        {
            _status.Text = problem;
            return;
        }

        _step = Math.Clamp(step, 0, _steps.Count - 1);
        _status.Text = "";
        for (var i = 0; i < _steps.Count; i++)
        {
            _steps[i].Visible = i == _step;
        }

        _title.Text = $"Step {_step + 1} of {_steps.Count}: {StepTitles[_step]}";
        _back.Disabled = _step == 0;
        _next.Disabled = _step == _steps.Count - 1;
        ShowFinish();
    }

    private void Finish()
    {
        var settings = Settings;
        if (settings.Problem() is { } problem)
        {
            _status.Text = problem;
            return;
        }

        NewMapRequest request;
        if (_template is { } index)
        {
            // The players the note shows: the chosen ones, cut to the template's slots or filled up to its fewest with people.
            var (size, players) = Fitted(_templates[index]);
            bool[] humans = [.. settings.Humans.Take(players), .. Enumerable.Repeat(true, Math.Max(0, players - settings.Humans.Count))];
            request = new NewMapRequest(settings with { Humans = humans }, index, size);
        }
        else
        {
            request = new NewMapRequest(settings);
        }

        EditorOverlay.Close(this);
        _done(request);
    }

    private static Button Choice(string text, ButtonGroup group, bool pressed, Action picked)
    {
        var button = new Button { Text = text, ToggleMode = true, ButtonGroup = group, ButtonPressed = pressed };
        button.Toggled += on =>
        {
            if (on)
            {
                picked();
            }
        };
        return button;
    }

    /// <summary>"ForestFloor" as "Forest floor".</summary>
    private static string Words(string name) =>
        string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? $" {char.ToLowerInvariant(c)}" : c.ToString()));
}
