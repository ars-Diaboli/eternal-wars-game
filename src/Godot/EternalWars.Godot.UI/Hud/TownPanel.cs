using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.UI.Hud.TownScreen;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Basic town screen (ADR-046) for the current player's town: build the next level of a building, build or upgrade
/// dwellings, recruit, and manage the garrison and the visiting hero (click a stack, then a slot; "Upgrade" upgrades
/// the picked stack"), turn stacks into undead at a Necropolis's Undead Transformer (ADR-137), and appoint or dismiss the governor (ADR-063). Every action is a town command; refusals show
/// below. Opened with <see cref="Open"/>. With <see cref="FullScreen"/> (the game app) it fills its parent and scrolls;
/// Escape or "Close" shut it. A right click on a building, a dwelling, a recruit button or a stack opens its information window (ADR-154).
/// The screen is made of <see cref="TownSection"/>s (ADR-223), each rebuilt alone on its own events; the screen rebuilds them all when it opens
/// and on the events that change what it shows at all or what every section reads (the owner, a decision, the turn, a building, the governor).
/// </summary>
public partial class TownPanel : PanelContainer
{
    private readonly TownHeaderSection _header;
    private readonly BuildingTreeSection _buildings;
    private readonly DwellingsSection _dwellings;
    private readonly GovernorSection _governor;
    private readonly TownArmiesSection _armies;
    private readonly CaravanSection _caravan;
    private readonly TransformerSection _transformer;
    private readonly BlacksmithSection _blacksmith;
    private readonly TavernSection _tavern;
    private readonly TownSection[] _sections;
    private readonly CheckBox _split = new() { Text = "Split (move half)" };
    private readonly CheckBox _toHero = new() { Text = "Recruit to visiting hero" };
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(UiTheme.ScreenTextWidth, 0) };
    private GameSession _session = null!;
    private MapObjectId? _town;

    public TownPanel()
    {
        _header = new TownHeaderSection(this);
        _buildings = new BuildingTreeSection(this);
        _dwellings = new DwellingsSection(this);
        _governor = new GovernorSection(this);
        _armies = new TownArmiesSection(this);
        _caravan = new CaravanSection(this);
        _transformer = new TransformerSection(this);
        _blacksmith = new BlacksmithSection(this);
        _tavern = new TavernSection(this);
        _sections = [_header, _buildings, _dwellings, _governor, _armies, _caravan, _tavern, _blacksmith, _transformer];
    }

    /// <summary>Fill the parent and scroll (the game app's town screen) instead of shrinking to the content. Set before <see cref="Bind"/>.</summary>
    public bool FullScreen { get; set; }

    public event Action? Closed;

    /// <summary>The player asked to trade from this town's marketplace (ADR-067).</summary>
    public event Action? TradeRequested;

    /// <summary>The player opened the Thieves' Guild from this town's tavern (ADR-079).</summary>
    public event Action? ThievesGuildRequested;

    /// <summary>The player opened the tavern screen (ADR-114); when nobody listens, the town screen only has its hire buttons.</summary>
    public event Action<MapObjectId>? TavernRequested;

    public void Bind(GameSession session)
    {
        _session = session;
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (FullScreen)
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            OffsetBottom = -StatusBar.Height; // the status bar stays in sight below (ADR-204)
            this.UseOpaqueBackground();
            var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            AddChild(scroll);
            scroll.AddChild(box);
        }
        else
        {
            AddChild(box);
        }

        var header = new HBoxContainer();
        box.AddChild(header);
        header.AddChild(_header.Crest);
        header.AddChild(_header.Title);
        header.AddChild(_header.Income);
        _header.Trade.Pressed += () => TradeRequested?.Invoke();
        header.AddChild(_header.Trade);
        _header.Guild.Pressed += () => ThievesGuildRequested?.Invoke();
        header.AddChild(_header.Guild);
        header.AddCloseButton(Close);

        box.AddHeading("Buildings (one a day; hover for cost and requirements)");
        box.AddChild(_buildings.BuiltToday);
        box.AddChild(_buildings.Buildings);
        box.AddHeading("Dwellings (7 of 9, every tier)");
        box.AddChild(_dwellings.Grid);
        box.AddHeading("Governor (Stewardship)");
        box.AddChild(_governor.Row);
        box.AddChild(_armies.Rows);
        box.AddChild(_caravan.Rows);
        box.AddChild(_transformer.Row);
        box.AddChild(_blacksmith.Row);
        box.AddChild(_tavern.Row);

        var options = new HBoxContainer();
        box.AddChild(options);
        options.AddChild(_split);
        options.AddChild(_toHero);
        var upgrade = new Button { Text = "Upgrade picked stack" };
        upgrade.Pressed += UpgradePicked;
        options.AddChild(upgrade);
        box.AddChild(_status);
        Visible = false;

        // What the screen shows at all (the owner, a decision over it, the turn), and what every section reads (the buildings, the governor's discount).
        new RefreshScheduler(this, Refresh)
            .On<TownOwnerChanged>(session.Events)
            .On<DecisionRequested>(session.Events)
            .On<DecisionResolved>(session.Events) // back after a level-up on the visit (the Elder Grove)
            .On<TurnStarted>(session.Events)
            .On<TownBuilt>(session.Events)
            .On<TownBuildingDowngraded>(session.Events)
            .On<GovernorChanged>(session.Events);
        foreach (var section in _sections)
        {
            section.Watch(session.Events);
        }
    }

    /// <summary>Shows a town of the player whose turn it is.</summary>
    public void Open(MapObjectId town)
    {
        _town = town;
        Picked = null;
        _transformer.Reset();
        _status.Text = "";
        Refresh();
    }

    public void Close()
    {
        var wasOpen = _town is not null;
        _town = null;
        Visible = false;
        if (wasOpen)
        {
            Closed?.Invoke();
        }
    }

    /// <summary>The town the screen shows, if it is open.</summary>
    public MapObjectId? CurrentTown => Visible ? _town : null;

    internal GameSession Session => _session;

    /// <summary>The town the sections show: the open town while the screen is in sight.</summary>
    internal Town? Shown => Visible && _session.State.PendingDecision is null ? Current : null;

    /// <summary>The stack picked to move (the army rows) or to send (the caravan).</summary>
    internal (TownArmy Army, int Slot)? Picked { get; set; }

    /// <summary>"Split (move half)" is ticked.</summary>
    internal bool Split => _split.ButtonPressed;

    /// <summary>"Recruit to visiting hero" is ticked.</summary>
    internal bool RecruitToHero => _toHero.ButtonPressed;

    /// <summary>Someone opens a tavern screen (<see cref="TavernRequested"/>).</summary>
    internal bool HasTavernScreen => TavernRequested is not null;

    internal void OpenTavern(MapObjectId town) => TavernRequested?.Invoke(town);

    private Town? Current =>
        _town is { } id && _session.State.FindObject(id) is Town town && town.Owner == _session.State.CurrentPlayer.Id ? town : null;

    private void Refresh()
    {
        if (Current is not { } town || _session.State.PendingDecision is not null)
        {
            Visible = false;
            return;
        }

        Visible = true;
        foreach (var section in _sections)
        {
            section.Show(town);
        }
    }

    /// <summary>A stack's slot clicked: picks it, or moves the picked stack there (half with "Split").</summary>
    internal void ClickSlot(Town town, (TownArmy Army, int Slot) target, bool occupied)
    {
        if (Picked is not { } from)
        {
            Picked = occupied ? target : null;
            RefreshPicking();
            return;
        }

        Picked = null;
        if (from != target)
        {
            var source = from.Army == TownArmy.Garrison ? town.Garrison : TownRules.VisitingHero(_session.State, town)!.Army;
            var stack = source[from.Slot]!;
            int? count = _split.ButtonPressed && stack.Count > 1 ? stack.Count / 2 : null;
            Run(new MoveTownStackCommand(_session.State.CurrentPlayer.Id, town.Id, from.Army, from.Slot, target.Army, target.Slot, count));
        }

        RefreshPicking();
    }

    /// <summary>The sections that show the picked stack.</summary>
    private void RefreshPicking()
    {
        _armies.Refresh();
        _caravan.Refresh();
    }

    private void UpgradePicked()
    {
        if (Current is not { } town || Picked is not { } picked)
        {
            _status.Text = "Pick a stack first.";
            return;
        }

        Picked = null;
        Run(new UpgradeStackCommand(_session.State.CurrentPlayer.Id, town.Id, picked.Army, picked.Slot));
    }

    internal void Run(GameCommand command)
    {
        var result = _session.Execute(command);
        _status.Text = result.IsAccepted ? "" : $"Refused: {result.Error}";
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }
}
