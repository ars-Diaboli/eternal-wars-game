using System.Globalization;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Turns;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Setup;

/// <summary>A map the player can start: a name and a factory building its initial state for the chosen options.</summary>
public sealed record ScenarioChoice(string Name, Func<GameOptions, GameState> Create);

/// <summary>
/// A start slot of a random map template: its player's name, whether a human may (and by default does) take it, its default team and whether its
/// player picks the start town's tier-1 dwelling (the start zone gives none).
/// </summary>
public sealed record RandomMapSlot(string Name, bool HumanAllowed, bool HumanByDefault, int Team, bool PicksTier1Dwelling = true);

/// <summary>
/// One player of a random map as set up: the slot, human or computer, the town (null = random) and hero (null = random), the team (null = the
/// template's, when locked) and the start town's tier-1 dwelling (a base creature id; null = random).
/// </summary>
public sealed record RandomMapPlayer(int Slot, bool IsHuman, string? Town, string? Hero, int? Team, string? Tier1Dwelling = null);

/// <summary>
/// What the screen asks the generator for: the map size, the players (the first slots, in order), the game options (their seed is the map's) and
/// the hero limit (null = the template's, ADR-068).
/// </summary>
public sealed record RandomMapRequest(int Size, IReadOnlyList<RandomMapPlayer> Players, GameOptions Options, int? MaxHeroes = null);

/// <summary>A generated game and the generator's problems when every attempt had some (the player may still play it).</summary>
public sealed record RandomMapResult(GameState State, IReadOnlyList<string> Problems);

/// <summary>
/// A random map template the player can start (ADR-086): its sizes, the player count range, the start slots, a factory that generates the game
/// and its hero limit. Refused setups (too many humans, a hero picked twice, ...) throw <see cref="ArgumentException"/>, which the screen shows.
/// </summary>
public sealed record RandomMapChoice(
    string Name,
    string Description,
    IReadOnlyList<int> Sizes,
    int MinPlayers,
    bool TeamsLocked,
    IReadOnlyList<RandomMapSlot> Slots,
    Func<RandomMapRequest, RandomMapResult> Create,
    int? MinTeams = null,
    int? MaxTeams = null,
    int MaxHeroes = ScenarioRules.HeroLimit);

/// <summary>
/// New-game setup: a map or a random map template, fog mode and refog margin, complexity, turn timer, seed. For a map: its players, with teams unless the map
/// locks them (ADR-032); on an arena map a line on the mode and, per player, Human or Computer (ADR-159). For a template (ADR-086): the size, the number of players, the hero limit (the template's by default, ADR-068) and for each
/// player human or computer, town, hero, the start town's tier-1 dwelling (a human player with a picked town, when the start zone gives none; RMG-7)
/// and team (unless the template locks them); the map is generated on "Start" and, if it still has problems, shown so the player can start it anyway or
/// change something. Raises <see cref="Started"/> with the ready state; the app then builds the session and the world.
/// </summary>
public partial class NewGameScreen : PanelContainer
{
    private const string Random = "Random";

    private readonly OptionButton _maps = new();
    private readonly Label _description = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(520, 0) };
    private readonly OptionButton _size = new();
    private readonly SpinBox _playerCount = new() { MinValue = 1, MaxValue = 8, Step = 1 };
    private readonly SpinBox _maxHeroes = new() { MinValue = 1, MaxValue = ScenarioRules.HeroLimit, Step = 1 };
    private readonly OptionButton _fog = new();
    private readonly OptionButton _complexity = new();
    private readonly OptionButton _timer = new();
    private readonly SpinBox _margin = new() { MinValue = 0, MaxValue = 6, Value = GameOptions.Default.RefogMargin };
    private readonly LineEdit _seed = new() { CustomMinimumSize = new Vector2(220, 0) };
    private readonly GridContainer _players = new() { Columns = 3 };
    private readonly Button _start = new Button { Text = "Start" }.WithIcon(UiIcon.Confirm);
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(520, 0) };
    private readonly Label _arena = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(520, 0), Visible = false };
    private readonly Dictionary<PlayerId, SpinBox> _teams = [];
    private readonly Dictionary<PlayerId, OptionButton> _controllers = [];
    private readonly Dictionary<int, OptionButton> _startTowns = [];
    private readonly List<RandomPlayerRow> _randomPlayers = [];
    private readonly List<Control> _randomRows = [];
    private IReadOnlyList<ScenarioChoice> _choices = [];
    private IReadOnlyList<RandomMapChoice> _templates = [];
    private Func<string, IReadOnlyList<HeroDefinition>> _heroesOf = _ => [];
    private Func<string, IReadOnlyList<CreatureDefinition>> _tier1Of = _ => [];
    private RandomMapResult? _generated;
    private bool _busy;

    public event Action<GameState>? Started;

    /// <summary>The player asked to go back to the main menu (Back, or Escape, which the app maps).</summary>
    public event Action? BackRequested;

    /// <summary>
    /// Binds the maps, the random map templates, the heroes a player of a town can pick and the tier-1 dwellings its start town can begin with
    /// (for the templates' hero and dwelling choices).
    /// </summary>
    public void Bind(
        IReadOnlyList<ScenarioChoice> choices,
        IReadOnlyList<RandomMapChoice>? templates = null,
        Func<string, IReadOnlyList<HeroDefinition>>? heroesOf = null,
        Func<string, IReadOnlyList<CreatureDefinition>>? tier1Of = null)
    {
        _choices = choices;
        _templates = templates ?? [];
        _heroesOf = heroesOf ?? (_ => []);
        _tier1Of = tier1Of ?? (_ => []);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
        AddChild(box);
        box.AddChild(new Label { Text = "New game" });

        foreach (var choice in choices)
        {
            _maps.AddItem(choice.Name);
        }

        foreach (var template in _templates)
        {
            _maps.AddItem($"Random map: {template.Name}");
        }

        _maps.ItemSelected += _ => ShowPlayers();
        Row(box, "Map", _maps);
        box.AddChild(_arena);
        box.AddChild(_description);
        _randomRows.Add(_description);
        _randomRows.Add(Row(box, "Size", _size));
        _size.ItemSelected += _ => Changed();
        _randomRows.Add(Row(box, "Players", _playerCount));
        _playerCount.ValueChanged += _ => ShowRandomPlayers();
        _maxHeroes.TooltipText = "The most heroes a player may have on the map at once; the template sets the default.";
        var heroLimitRow = Row(box, "Hero limit", _maxHeroes);
        heroLimitRow.AddChild(new Label { Text = "heroes per player" });
        _randomRows.Add(heroLimitRow);
        _maxHeroes.ValueChanged += _ => Changed();

        _fog.AddItem("Permanent reveal (HoMM3)", (int)FogMode.PermanentReveal);
        _fog.AddItem("Shroud + refog for units", (int)FogMode.ShroudWithRefog);
        _fog.ItemSelected += _ => _margin.Editable = SelectedFog == FogMode.ShroudWithRefog;
        Row(box, "Fog of war", _fog);
        _margin.Editable = false;
        Row(box, "Refog margin", _margin);

        _complexity.AddItem("Map default", -1);
        foreach (var level in Enum.GetValues<Complexity>())
        {
            _complexity.AddItem($"{level} ({StartingResources.For(level).Gold} gold)", (int)level);
        }

        // The turn timer (ADR-091) shares the complexity's row: off, or 1 to 30 minutes a human player's turn.
        var complexityRow = Row(box, "Complexity", _complexity);
        _timer.AddItem("Off", 0);
        for (var minutes = 1; minutes <= TurnTimer.MaxMinutes; minutes++)
        {
            _timer.AddItem($"{minutes} min", minutes);
        }

        complexityRow.AddChild(new Label { Text = "  Turn timer" });
        complexityRow.AddChild(_timer);

        _seed.Text = ((ulong)System.Random.Shared.NextInt64()).ToString(CultureInfo.InvariantCulture);
        _seed.TextChanged += _ => Changed();
        Row(box, "Seed", _seed);

        box.AddChild(_players);
        _start.Pressed += Start;
        var buttons = new HBoxContainer();
        _start.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        buttons.AddChild(_start);
        var back = new Button { Text = "Back (Esc)" }.WithIcon(UiIcon.ArrowLeft);
        back.Pressed += () => BackRequested?.Invoke();
        buttons.AddChild(back);
        box.AddChild(buttons);
        _start.CallDeferred(Control.MethodName.GrabFocus); // Enter starts (and the smoke scripts need no coordinates)
        box.AddChild(_status);
        ShowPlayers();
    }

    /// <summary>What an arena map is, under the map's name (ADR-097).</summary>
    public const string ArenaLine =
        "Arena: day 1 is preparation (heroes move nearly without limit, towns build as much as they can pay). At the start of day 2 each side's "
        + "strongest hero meets at the combat place, and the winner of that one battle wins the game. Not ranked.";

    private FogMode SelectedFog => (FogMode)_fog.GetSelectedId();

    private ScenarioChoice? SelectedMap => _maps.Selected >= 0 && _maps.Selected < _choices.Count ? _choices[_maps.Selected] : null;

    private RandomMapChoice? SelectedTemplate =>
        _maps.Selected - _choices.Count is var index && index >= 0 && index < _templates.Count ? _templates[index] : null;

    private static HBoxContainer Row(Container parent, string label, Control control)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(140, 0) });
        row.AddChild(control);
        parent.AddChild(row);
        return row;
    }

    private void ClearPlayers()
    {
        foreach (var child in _players.GetChildren())
        {
            _players.RemoveChild(child);
            child.QueueFree();
        }

        _teams.Clear();
        _controllers.Clear();
        _startTowns.Clear();
        _randomPlayers.Clear();
    }

    /// <summary>Shows the selected map's player slots (team boxes disabled when the map locks its teams), or the selected template's setup.</summary>
    private void ShowPlayers()
    {
        _status.Text = "";
        _arena.Visible = false;
        var template = SelectedTemplate;
        foreach (var row in _randomRows)
        {
            row.Visible = template is not null;
        }

        if (template is not null)
        {
            _description.Text = template.Description;
            _size.Clear();
            foreach (var size in template.Sizes)
            {
                _size.AddItem($"{size}×{size}", size);
            }

            _size.Select(template.Sizes.Count / 2);
            _playerCount.MinValue = template.MinPlayers;
            _playerCount.MaxValue = template.Slots.Count;
            _playerCount.SetValueNoSignal(template.Slots.Count);
            _maxHeroes.SetValueNoSignal(template.MaxHeroes);
            ShowRandomPlayers();
            return;
        }

        Changed();
        ClearPlayers();
        _players.Columns = 4;
        if (SelectedMap is not { } map)
        {
            return;
        }

        GameState preview;
        try
        {
            preview = map.Create(GameOptions.Default);
        }
        catch (Exception e) when (e is FormatException or IOException)
        {
            _status.Text = $"Cannot load {map.Name}: {e.Message}";
            return;
        }

        // An arena map (ADR-097, ADR-159): its mode said in one line, and each player played by a person (hot-seat) or the computer.
        _arena.Visible = preview.Rules.Arena;
        _arena.Text = ArenaLine;
        _players.AddChild(new Label { Text = "Player" });
        _players.AddChild(new Label { Text = "Controlled by" });
        _players.AddChild(new Label { Text = (preview.TeamsLocked ? "Team (locked by the map)" : "Team") + TeamCounts(preview.MinTeams, preview.MaxTeams) });
        _players.AddChild(new Label { Text = "Town" });
        foreach (var player in preview.Players)
        {
            _players.AddChild(PlayerName(player.Name, player.Id));
            if (preview.Rules.Arena)
            {
                var controller = new OptionButton();
                controller.AddItem("Human", 1);
                controller.AddItem("Computer", 0);
                controller.Select(controller.GetItemIndex(player.IsHuman ? 1 : 0));
                _players.AddChild(controller);
                _controllers[player.Id] = controller;
            }
            else
            {
                _players.AddChild(new Label { Text = player.IsHuman ? "Human" : "Computer" });
            }
            var team = new SpinBox { MinValue = 0, MaxValue = GameState.MaxPlayers - 1, Value = player.Team.Value, Editable = !preview.TeamsLocked };
            _players.AddChild(team);
            _teams[player.Id] = team;
            _players.AddChild(TownChoice(preview, player.Id));
        }
    }

    /// <summary>
    /// The player's faction (ADR-126): a choice when the map leaves it open (a random town of the player's, or a hero placeholder without a fixed
    /// town), else the type of its first town, or a dash.
    /// </summary>
    private Control TownChoice(GameState preview, PlayerId player)
    {
        var towns = preview.ObjectsOf<Town>().Where(t => t.Owner == player).OrderBy(t => t.Id.Value).ToList();
        var open = towns.Any(t => t.IsRandom) || (towns.Count == 0 && preview.HeroesOf(player).Any(h => h.IsPlaceholder));
        if (!open)
        {
            return new Label { Text = towns.FirstOrDefault()?.TownType ?? "—" };
        }

        var choice = new OptionButton { TooltipText = "The faction of this player's random towns and hero placeholders" };
        choice.AddItem(Random);
        foreach (var town in BuiltInMapObjects.PlayableTowns)
        {
            choice.AddItem(town);
        }

        _startTowns[player.Value] = choice;
        return choice;
    }

    /// <summary>The factions chosen for the map's players (ADR-126); null when nobody chose one.</summary>
    private IReadOnlyDictionary<int, string>? ChosenTowns()
    {
        var chosen = _startTowns.Where(c => c.Value.Selected > 0).ToDictionary(c => c.Key, c => BuiltInMapObjects.PlayableTowns[c.Value.Selected - 1]);
        return chosen.Count == 0 ? null : chosen;
    }

    /// <summary>The team counts a map or template allows (ADR-117), as a label suffix: " — 2 teams", " — 2 to 4 teams"; empty without a limit.</summary>
    private static string TeamCounts(int? min, int? max) => (min, max) switch
    {
        (null, null) => "",
        ({ } a, { } b) when a == b => $" — {a} teams",
        ({ } a, null) => $" — at least {a} teams",
        (null, { } b) => $" — at most {b} teams",
        ({ } a, { } b) => $" — {a} to {b} teams",
    };

    private static Label PlayerName(string name, PlayerId id)
    {
        var label = new Label { Text = name };
        label.AddThemeColorOverride("font_color", Palette.ForPlayer(id).Lightened(0.3f));
        return label;
    }

    /// <summary>The template's first slots, one row each: human or computer, town, hero (of that town), tier-1 dwelling (of that town), team.</summary>
    private void ShowRandomPlayers()
    {
        Changed();
        ClearPlayers();
        if (SelectedTemplate is not { } template)
        {
            return;
        }

        _players.Columns = 6;
        _players.AddChild(new Label { Text = "Player" });
        _players.AddChild(new Label { Text = "Controlled by" });
        _players.AddChild(new Label { Text = "Town" });
        _players.AddChild(new Label { Text = "Hero" });
        _players.AddChild(new Label { Text = "Tier-1 dwelling" });
        _players.AddChild(new Label { Text = (template.TeamsLocked ? "Team (locked)" : "Team") + TeamCounts(template.MinTeams, template.MaxTeams) });
        for (var i = 0; i < (int)_playerCount.Value; i++)
        {
            var slot = template.Slots[i];
            var row = new RandomPlayerRow(i, slot, template.TeamsLocked);
            _players.AddChild(PlayerName(slot.Name, new PlayerId(i)));
            _players.AddChild(row.Control);
            _players.AddChild(row.Town);
            _players.AddChild(row.Hero);
            _players.AddChild(row.Dwelling);
            _players.AddChild(row.Team);
            row.Control.ItemSelected += _ =>
            {
                row.ShowDwellings(_tier1Of);
                Changed();
            };
            row.Town.ItemSelected += _ =>
            {
                row.ShowHeroes(_heroesOf);
                row.ShowDwellings(_tier1Of);
                Changed();
            };
            row.Hero.ItemSelected += _ => Changed();
            row.Dwelling.ItemSelected += _ => Changed();
            row.Team.ValueChanged += _ => Changed();
            _randomPlayers.Add(row);
        }
    }

    /// <summary>A setting changed: a map generated with the old ones (shown for its problems) is dropped.</summary>
    private void Changed()
    {
        _generated = null;
        _start.Text = "Start";
    }

    private void Start()
    {
        if (_busy)
        {
            return;
        }

        if (!ulong.TryParse(_seed.Text, CultureInfo.InvariantCulture, out var seed))
        {
            _status.Text = "Enter a numeric seed.";
            return;
        }

        var options = new GameOptions(
            SelectedFog, (int)_margin.Value, seed, _complexity.GetSelectedId() < 0 ? null : (Complexity)_complexity.GetSelectedId(),
            _timer.GetSelectedId() > 0 ? _timer.GetSelectedId() : null);
        if (SelectedTemplate is { } template)
        {
            StartRandom(template, options);
            return;
        }

        if (SelectedMap is not { } map)
        {
            _status.Text = "Choose a map.";
            return;
        }

        try
        {
            var state = map.Create(options with { StartTowns = ChosenTowns() });
            if (!state.TeamsLocked)
            {
                state.AssignTeams(_teams.ToDictionary(t => t.Key, t => new TeamId((int)t.Value.Value)));
            }

            if (_controllers.Count > 0)
            {
                state.AssignControllers(_controllers.ToDictionary(c => c.Key, c => c.Value.GetSelectedId() == 1));
            }

            Started?.Invoke(state);
        }
        catch (Exception e) when (e is FormatException or IOException or InvalidOperationException)
        {
            _status.Text = e.Message;
        }
    }

    /// <summary>
    /// Generates the map, a frame after showing "Generating…" (it takes a moment on large maps). A map without problems starts at once; one with
    /// problems is shown, and "Start anyway" starts it.
    /// </summary>
    private async void StartRandom(RandomMapChoice template, GameOptions options)
    {
        if (_generated is { } shown)
        {
            Started?.Invoke(shown.State);
            return;
        }

        _busy = true;
        _status.Text = "Generating the map…";
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        try
        {
            var maxHeroes = (int)_maxHeroes.Value;
            var request = new RandomMapRequest(
                _size.GetSelectedId(), _randomPlayers.Select(p => p.Player).ToList(), options, maxHeroes == template.MaxHeroes ? null : maxHeroes);
            var result = template.Create(request);
            if (result.Problems.Count == 0)
            {
                Started?.Invoke(result.State);
                return;
            }

            _generated = result;
            _start.Text = "Start anyway";
            _status.Text = $"Every try of this seed left problems; the best map has: {string.Join(" ", result.Problems)} Start it anyway, or change the seed.";
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            _status.Text = e.Message;
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>The controls of one player of a random map.</summary>
    private sealed class RandomPlayerRow
    {
        private readonly int _slot;
        private readonly bool _teamsLocked;
        private readonly bool _picksDwelling;
        private IReadOnlyList<HeroDefinition> _heroes = [];
        private IReadOnlyList<CreatureDefinition> _dwellings = [];

        public RandomPlayerRow(int slot, RandomMapSlot setup, bool teamsLocked)
        {
            _slot = slot;
            _teamsLocked = teamsLocked;
            _picksDwelling = setup.PicksTier1Dwelling;
            Control.AddItem("Human");
            Control.AddItem("Computer");
            Control.SetItemDisabled(0, !setup.HumanAllowed);
            Control.Select(setup.HumanByDefault ? 0 : 1);
            if (!setup.HumanAllowed)
            {
                Control.TooltipText = "This start is for computer players only.";
            }

            Town.AddItem(Random);
            foreach (var town in BuiltInMapObjects.PlayableTowns)
            {
                Town.AddItem(town);
            }

            Hero.AddItem(Random);
            Hero.Disabled = true;
            Dwelling.AddItem(Random);
            Dwelling.Disabled = true;
            Dwelling.TooltipText = DwellingTip();
            Team.Value = setup.Team;
            Team.Editable = !teamsLocked;
        }

        public OptionButton Control { get; } = new();

        public OptionButton Town { get; } = new();

        public OptionButton Hero { get; } = new() { CustomMinimumSize = new Vector2(150, 0) };

        public OptionButton Dwelling { get; } = new() { CustomMinimumSize = new Vector2(120, 0) };

        public SpinBox Team { get; } = new() { MinValue = 0, MaxValue = GameState.MaxPlayers - 1, Step = 1 };

        private string? SelectedTown => Town.Selected <= 0 ? null : BuiltInMapObjects.PlayableTowns[Town.Selected - 1];

        public RandomMapPlayer Player => new(
            _slot,
            Control.Selected == 0,
            SelectedTown,
            Hero.Selected <= 0 || Hero.Selected > _heroes.Count ? null : _heroes[Hero.Selected - 1].Id,
            _teamsLocked ? null : (int)Team.Value,
            Dwelling.Selected <= 0 || Dwelling.Selected > _dwellings.Count ? null : _dwellings[Dwelling.Selected - 1].Id);

        private bool IsHuman => Control.Selected == 0;

        /// <summary>
        /// The dwelling choice (RMG-7): "Random" and the town's tier-1 dwellings, for a human player with a picked town whose start zone gives
        /// no tier-1 dwelling of its own; otherwise only "Random" (a computer player's is always drawn).
        /// </summary>
        public void ShowDwellings(Func<string, IReadOnlyList<CreatureDefinition>> tier1Of)
        {
            Dwelling.Clear();
            Dwelling.AddItem(Random);
            _dwellings = _picksDwelling && IsHuman && SelectedTown is { } town ? tier1Of(town) : [];
            foreach (var line in _dwellings)
            {
                Dwelling.AddItem(line.Name);
            }

            Dwelling.Disabled = _dwellings.Count == 0;
            Dwelling.TooltipText = DwellingTip();
            Dwelling.Select(0);
        }

        private string DwellingTip() =>
            !_picksDwelling ? "This start town already has its tier-1 dwelling."
            : !IsHuman ? "A computer player's tier-1 dwelling is drawn at random."
            : SelectedTown is null ? "Pick a town to choose its tier-1 dwelling."
            : "The tier-1 dwelling the start town begins with.";

        /// <summary>The hero choice follows the town: "Random" and the town's heroes; a random town leaves only a random hero.</summary>
        public void ShowHeroes(Func<string, IReadOnlyList<HeroDefinition>> heroesOf)
        {
            Hero.Clear();
            Hero.AddItem(Random);
            _heroes = SelectedTown is { } town ? heroesOf(town) : [];
            foreach (var hero in _heroes)
            {
                Hero.AddItem($"{hero.Name} ({hero.Class.ToString().ToLowerInvariant()})");
            }

            Hero.Disabled = _heroes.Count == 0;
            Hero.Select(0);
        }
    }
}
