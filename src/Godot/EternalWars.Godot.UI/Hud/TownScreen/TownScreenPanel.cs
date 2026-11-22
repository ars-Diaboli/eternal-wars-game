using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Towns;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>The town screen's windows (ADR-294, ADR-296): one per hotkey, then those opened from a building or the bottom bar.</summary>
public enum TownWindow
{
    Hall,
    Recruitment,
    Guild,
    Tavern,
    Marketplace,
    Blacksmith,
    Fort,
    Caravan,

    /// <summary>One dwelling: its large card and recruit dialog, and the upgrade (a dwelling of the building strip).</summary>
    Dwelling,

    /// <summary>The governor list (the town's portrait).</summary>
    Governor,

    /// <summary>The Undead Transformer.</summary>
    Transformer,
}

/// <summary>
/// The town screen (the town screen plan, ADR-246, ADR-294, ADR-296). The top bar: crest, name (a click renames the town), ◄ ► through the
/// player's towns, the faction, town hall and governor, the quick buttons, ×. The main area: the town's 3D scene (<see cref="Scene"/>, ADR-297)
/// where its town type has a layout, else the faction's key painting; a strip of the town's buildings and the notices card (built today,
/// caravans, the discount). A click on a building of the scene opens its window, as its strip button does; a right click its information. The bottom bar: the town's crest
/// with its governor, the garrison and visiting hero as <see cref="ArmyBar"/>s, Split, Upgrade all, Swap and Dismiss, the income, the week's
/// growth (its parts on hover) and the date. One window at a time over the dimmed main area (the windows are this class's other parts); Escape
/// closes the window, a second Escape leaves. The keys are <see cref="TownScreenKeys"/>'.
/// </summary>
public partial class TownScreenPanel : Control, ITownScreen, ITownSectionHost
{
    private const string PaintingFolder = "res://shared/textures/ui/backgrounds/towns/";

    private readonly TownArmiesSection _armies;
    private readonly TextureRect _crest = new() { StretchMode = TextureRect.StretchModeEnum.KeepCentered, CustomMinimumSize = new Vector2(Heraldry.TitleSize, Heraldry.TitleSize) };
    private readonly Button _title = new() { ThemeTypeVariation = UiTheme.TitleLabel, Flat = true, TooltipText = "Rename the town" };
    private readonly Label _facts = new() { ThemeTypeVariation = UiTheme.QuietLabel, VerticalAlignment = VerticalAlignment.Center };
    private readonly ColorRect _backdrop = new() { Color = UiTheme.Background, MouseFilter = MouseFilterEnum.Ignore };

    /// <summary>The main area's input for the 3D scene: the pointer's hover and clicks (it lets them by while the painting shows).</summary>
    private readonly Control _stage = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly TextureRect _painting = new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        MouseFilter = MouseFilterEnum.Ignore,
    };
    private readonly TextureRect _portrait = new() { StretchMode = TextureRect.StretchModeEnum.KeepCentered, CustomMinimumSize = new Vector2(96, 96) };
    private readonly Button _governor = new() { TooltipText = "The governor: a hero with Stewardship makes building and recruiting cheaper here" };
    private readonly VBoxContainer _notices = new() { CustomMinimumSize = new Vector2(280, 0) };
    private readonly HFlowContainer _strip = new() { Alignment = FlowContainer.AlignmentMode.Center };
    private readonly HBoxContainer _income = new();
    private readonly Label _growth = new() { MouseFilter = MouseFilterEnum.Pass };
    private readonly Label _date = new() { ThemeTypeVariation = UiTheme.QuietLabel };
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(300, 0) };
    private readonly Button _split = new() { Text = "Split" };
    private readonly Button _upgradeAll = new() { Text = "Upgrade all" };
    private readonly Button _swap = new() { Text = "Swap" };
    private readonly Button _dismiss = new() { Text = "Dismiss" };
    private readonly ColorRect _dim = new() { Color = new Color(0, 0, 0, 0.5f), MouseFilter = MouseFilterEnum.Stop, Visible = false };
    private readonly CenterContainer _windowHost = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly Dictionary<TownWindow, Button> _quick = [];
    private readonly Control _parked = new() { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
    private readonly CaravanSection _caravan;
    private readonly TransformerSection _transformer;
    private GameSession _session = null!;
    private MapObjectId? _town;
    private WindowFrame? _window;
    private CreatureId? _dwelling;
    private TownScene3D? _scene;

    public TownScreenPanel()
    {
        Armies = TownArmiesSection.Group(this);
        _armies = new TownArmiesSection(this);
        _caravan = new CaravanSection(this);
        _transformer = new TransformerSection(this);
        Armies.PickChanged += ShowArmyButtons;
    }

    public event Action? Closed;

    /// <summary>The marketplace is a window of this screen: it never asks for another.</summary>
    event Action? ITownScreen.TradeRequested { add { } remove { } }

    public event Action? ThievesGuildRequested;

    /// <summary>The tavern is a window of this screen: it never asks for another.</summary>
    event Action<MapObjectId>? ITownScreen.TavernRequested { add { } remove { } }

    /// <summary>H: the visiting hero's screen.</summary>
    public event Action<HeroId>? HeroRequested;

    /// <summary>◄ ► or [ ]: another own town; the adventure HUD centres the map on it and opens it. Without a listener the screen opens it itself.</summary>
    public event Action<MapObjectId>? SwitchRequested;

    /// <summary>The name and state of the scene's building under the pointer, or null when it left them: the app shows it on the status bar.</summary>
    public event Action<string?>? Hovered;

    /// <summary>
    /// The town's 3D scene behind the bars (ADR-297), set by the game app once; without it (the harness) or for a town type without a layout the
    /// main area is the key painting.
    /// </summary>
    public TownScene3D? Scene
    {
        get => _scene;
        set
        {
            _scene = value;
            if (value is not null)
            {
                value.BuildingHovered += ShowHovered;
                value.BuildingPicked += OpenPlot;
            }
        }
    }

    /// <summary>Whether another screen covers this one (the hero screen, the Thieves' Guild): its keys then stay off.</summary>
    public Func<bool>? IsCovered { get; set; }

    /// <summary>The window open over the main area, if any.</summary>
    public TownWindow? OpenWindow { get; private set; }

    public MapObjectId? CurrentTown => Visible ? _town : null;

    public GameSession Session => _session;

    public Town? Shown => Visible && _session.State.PendingDecision is null ? Current : null;

    public ArmyBarGroup Armies { get; }

    public bool HasTavernScreen => false; // the tavern is a window of this screen

    private Town? Current =>
        _town is { } id && _session.State.FindObject(id) is Town town && town.Owner == _session.State.CurrentPlayer.Id ? town : null;

    private GameState State => _session.State;

    private PlayerId Player => _session.State.CurrentPlayer.Id;

    private ResourceSet Treasury => _session.State.CurrentPlayer.Resources;

    public void Bind(GameSession session)
    {
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        OffsetBottom = -StatusBar.Height; // the status bar stays in sight below (ADR-204)
        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 0);
        AddChild(page);
        page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        page.AddChild(TopBar());
        page.AddChild(MainArea());
        page.AddChild(BottomBar());
        AddChild(_parked);
        _parked.AddChild(_caravan.Rows);
        _parked.AddChild(_transformer.Row);
        Visible = false;
        VisibilityChanged += () =>
        {
            if (!Visible)
            {
                _scene?.Close();
            }
        };

        new RefreshScheduler(this, Refresh)
            .On<TownOwnerChanged>(session.Events)
            .On<DecisionRequested>(session.Events)
            .On<DecisionResolved>(session.Events)
            .On<TurnStarted>(session.Events)
            .On<TownBuilt>(session.Events)
            .On<DwellingBuilt>(session.Events)
            .On<TownBuildingDowngraded>(session.Events)
            .On<TownRenamed>(session.Events)
            .On<GovernorChanged>(session.Events)
            .On<ResourcesChanged>(session.Events)
            .On<CreaturesRecruited>(session.Events)
            .On<CreaturesRecruitedMany>(session.Events)
            .On<StackUpgraded>(session.Events)
            .On<StackDismissed>(session.Events)
            .On<CreaturesTransformed>(session.Events)
            .On<TownCreaturesGrew>(session.Events)
            .On<HeroMoved>(session.Events)
            .On<HeroEnteredGarrison>(session.Events)
            .On<HeroLeftGarrison>(session.Events)
            .On<HeroHired>(session.Events)
            .On<WarMachineBought>(session.Events)
            .On<SpellBookBought>(session.Events)
            .On<CaravanDispatched>(session.Events)
            .On<CaravanArrived>(session.Events)
            .On<CaravanReturned>(session.Events)
            .On<CaravanLost>(session.Events)
            .On<ProviderStockChanged>(session.Events)
            .On<DwellingCreaturesGrew>(session.Events)
            .On<SpellsLearned>(session.Events)
            .On<WarcriesLearned>(session.Events)
            .On<GarrisonChanged>(session.Events)
            .On<ArmyChanged>(session.Events);
        _armies.Watch(session.Events);
        this.SubscribeWhileInTree<SpellsLearned>(session.Events, e =>
        {
            if (e.Town == _town)
            {
                _learnedHere.UnionWith(e.Spells); // the guild's "learned now" marks
            }
        });
    }

    public void Open(MapObjectId town)
    {
        if (_town != town)
        {
            _transformer.Reset();
            _learnedHere.Clear();
            _hallSelected = null;
        }

        _town = town;
        Armies.Picked = null;
        _status.Text = "";
        CloseWindow();
        Refresh();
    }

    public void Close()
    {
        var wasOpen = _town is not null;
        _town = null;
        CloseWindow();
        Visible = false;
        if (wasOpen)
        {
            Closed?.Invoke();
        }
    }

    public void OpenTavern(MapObjectId town)
    {
        if (_town != town)
        {
            Open(town);
        }

        ShowWindow(TownWindow.Tavern);
    }

    public void Run(GameCommand command) => RunAll([command]);

    public void RunAll(IReadOnlyList<GameCommand> commands)
    {
        _status.Text = "";
        foreach (var command in commands)
        {
            var result = _session.Execute(command);
            if (!result.IsAccepted)
            {
                _status.Text = $"Refused: {result.Error}";
                return;
            }
        }
    }

    /// <summary>Opens <paramref name="window"/> over the main area, closing the one open; the same window again closes it.</summary>
    public void ShowWindow(TownWindow window)
    {
        if (OpenWindow == window && window != TownWindow.Dwelling)
        {
            CloseWindow();
            return;
        }

        CloseWindow();
        if (Current is not { } town)
        {
            return;
        }

        OpenWindow = window;
        // As large as the main area allows, the painting showing around it.
        var room = _windowHost.Size - new Vector2(UiSpacing.ScreenEdge * 8, UiSpacing.ScreenEdge * 4);
        _window = new WindowFrame(Title(town, window), UiFrames.Accent(town.TownType)) { CustomMinimumSize = room.Max(new Vector2(1040, 560)) };
        _window.CloseRequested += CloseWindow;
        _windowHost.AddChild(_window);
        _dim.Visible = true;
        FillWindow(town);
    }

    /// <summary>Opens the dwelling window of <paramref name="line"/> (a dwelling of the building strip).</summary>
    public void ShowDwelling(CreatureId line)
    {
        _dwelling = line;
        ShowWindow(TownWindow.Dwelling);
    }

    public void CloseWindow()
    {
        OpenWindow = null;
        if (_window is not null)
        {
            Release(_window.Content);
            _window.QueueFree();
        }

        _window = null;
        _dim.Visible = false;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || Current is not { } town || ConfirmDialog.AnyOpen || IsCovered?.Invoke() == true)
        {
            return;
        }

        if (TownScreenKeys.Pressed(@event, TownKey.Back))
        {
            if (OpenWindow is not null)
            {
                CloseWindow();
            }
            else
            {
                Close();
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        foreach (var window in QuickWindows)
        {
            if (TownScreenKeys.Pressed(@event, KeyOf(window)))
            {
                ShowWindow(window);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        var handled = true;
        if (TownScreenKeys.Pressed(@event, TownKey.PreviousTown))
        {
            Switch(town, -1);
        }
        else if (TownScreenKeys.Pressed(@event, TownKey.NextTown))
        {
            Switch(town, 1);
        }
        else if (TownScreenKeys.Pressed(@event, TownKey.SwapHeroes))
        {
            Run(new SwapTownHeroesCommand(Player, town.Id));
        }
        else if (TownScreenKeys.Pressed(@event, TownKey.Hero))
        {
            if (TownRules.VisitingHero(State, town) is { } hero)
            {
                HeroRequested?.Invoke(hero.Id);
            }
        }
        else if (TownScreenKeys.Pressed(@event, TownKey.UpgradeAll))
        {
            UpgradeAll(town);
        }
        else
        {
            handled = false;
        }

        if (handled)
        {
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>The windows with a quick button and a hotkey.</summary>
    private static readonly TownWindow[] QuickWindows =
        [TownWindow.Hall, TownWindow.Recruitment, TownWindow.Guild, TownWindow.Tavern, TownWindow.Marketplace, TownWindow.Blacksmith, TownWindow.Fort, TownWindow.Caravan];

    private static TownKey KeyOf(TownWindow window) => window switch
    {
        TownWindow.Hall => TownKey.Hall,
        TownWindow.Recruitment => TownKey.Recruitment,
        TownWindow.Guild => TownKey.Guild,
        TownWindow.Tavern => TownKey.Tavern,
        TownWindow.Marketplace => TownKey.Marketplace,
        TownWindow.Blacksmith => TownKey.Blacksmith,
        TownWindow.Fort => TownKey.Fort,
        _ => TownKey.Caravan,
    };

    private string Title(Town town, TownWindow window) => window switch
    {
        TownWindow.Dwelling when _dwelling is { } line && State.Creatures.TryGet(line, out var creature) => $"{creature.Name} dwelling",
        TownWindow.Guild when town.Level(BuiltInBuildings.WarLodge) > 0 => BuildingName(town, BuiltInBuildings.WarLodge),
        TownWindow.Governor => "Governor",
        TownWindow.Transformer => BuildingName(town, UndeadTransformer.Building),
        _ => TownScreenKeys.All[KeyOf(window)].Label,
    };

    private string BuildingName(Town town, BuildingId building) =>
        TownRules.Buildings(State, town).Contains(building)
            ? TownRules.Buildings(State, town).Get(building, Math.Clamp(town.Level(building), 1, TownRules.Buildings(State, town).MaxLevel(building))).Name
            : building.Value;

    private Control TopBar()
    {
        var bar = new PanelContainer { ThemeTypeVariation = UiTheme.BarPanel };
        var row = new HBoxContainer();
        bar.AddChild(row);
        row.AddChild(_crest);
        _title.Pressed += Rename;
        row.AddChild(_title);
        var previous = new Button { Text = "Previous town" }.WithIcon(UiIcon.ArrowLeft);
        previous.SetMeta(UiIcons.StatusLabelMeta, TownScreenKeys.Hint(TownKey.PreviousTown));
        previous.Pressed += () => Switch(Current, -1);
        var next = new Button { Text = "Next town" }.WithIcon(UiIcon.ArrowRight);
        next.SetMeta(UiIcons.StatusLabelMeta, TownScreenKeys.Hint(TownKey.NextTown));
        next.Pressed += () => Switch(Current, 1);
        row.AddChild(previous);
        row.AddChild(next);
        row.AddChild(_facts);
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        foreach (var window in QuickWindows)
        {
            var key = KeyOf(window);
            var button = new Button { Text = $"{TownScreenKeys.All[key].Label}" };
            button.SetMeta(UiIcons.StatusLabelMeta, TownScreenKeys.Hint(key));
            button.TooltipText = TownScreenKeys.Hint(key);
            button.Pressed += () => ShowWindow(window);
            _quick[window] = button;
            row.AddChild(button);
        }

        row.AddCloseButton(Close);
        return bar;
    }

    private Control MainArea()
    {
        var area = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        area.AddChild(_backdrop);
        _backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        area.AddChild(_painting);
        _painting.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // The 3D scene shows through where the backdrop and the painting are hidden; the stage hands it the pointer.
        area.AddChild(_stage);
        _stage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _stage.GuiInput += OnStageInput;
        _stage.MouseExited += () => _scene?.Hover(null);

        // The notices card in the top-right corner: built today, caravans, the governor's discount.
        var card = new PanelContainer { ThemeTypeVariation = UiTheme.CardPanel };
        card.AddChild(_notices);
        area.AddChild(card);
        card.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
        card.GrowHorizontal = GrowDirection.Begin;
        card.OffsetRight = -UiSpacing.ScreenEdge * 2;
        card.OffsetTop = UiSpacing.ScreenEdge * 2;

        // The town's buildings as a strip along the bottom of the main area (the keyboard's and the controller's way in until the ring of plots).
        var strip = new PanelContainer { ThemeTypeVariation = UiTheme.CardPanel };
        strip.AddChild(_strip);
        area.AddChild(strip);
        strip.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        strip.GrowVertical = GrowDirection.Begin;
        strip.OffsetLeft = UiSpacing.ScreenEdge * 2;
        strip.OffsetRight = -UiSpacing.ScreenEdge * 2;
        strip.OffsetBottom = -UiSpacing.ScreenEdge;

        area.AddChild(_dim);
        _dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _dim.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                CloseWindow(); // a click beside the window closes it
            }
        };
        area.AddChild(_windowHost);
        _windowHost.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return area;
    }

    private Control BottomBar()
    {
        var bar = new PanelContainer { ThemeTypeVariation = UiTheme.BarPanel };
        var row = new HBoxContainer();
        bar.AddChild(row);
        var portrait = new VBoxContainer();
        portrait.AddChild(_portrait);
        _governor.Pressed += () => ShowWindow(TownWindow.Governor);
        portrait.AddChild(_governor);
        row.AddChild(portrait);
        row.AddChild(new VSeparator());
        row.AddChild(_armies.Rows);
        var buttons = new GridContainer { Columns = 2 };
        _split.TooltipText = "Pick a stack, then split it into an empty slot of its row";
        _split.Pressed += Split;
        _upgradeAll.SetMeta(UiIcons.StatusLabelMeta, TownScreenKeys.Hint(TownKey.UpgradeAll));
        _upgradeAll.Pressed += () =>
        {
            if (Current is { } town)
            {
                UpgradeAll(town);
            }
        };
        _swap.SetMeta(UiIcons.StatusLabelMeta, TownScreenKeys.Hint(TownKey.SwapHeroes));
        _swap.Pressed += () =>
        {
            if (Current is { } town)
            {
                Run(new SwapTownHeroesCommand(Player, town.Id));
            }
        };
        _dismiss.TooltipText = "Pick a stack, then dismiss it for good";
        _dismiss.Pressed += Dismiss;
        buttons.AddChild(_split);
        buttons.AddChild(_upgradeAll);
        buttons.AddChild(_swap);
        buttons.AddChild(_dismiss);
        row.AddChild(buttons);
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        var info = new VBoxContainer();
        info.AddChild(_income);
        info.AddChild(_growth);
        info.AddChild(_date);
        info.AddChild(_status);
        row.AddChild(info);
        return bar;
    }

    private void Refresh()
    {
        if (Current is not { } town || State.PendingDecision is not null)
        {
            Visible = false;
            CloseWindow();
            return;
        }

        var state = State;
        Visible = true;
        var overview = TownOverview.Of(state, town);
        _crest.Texture = Heraldry.Crest(town.TownType, Heraldry.TitleSize);
        _portrait.Texture = Heraldry.Crest(town.TownType, 96);
        _title.Text = town.Name;
        _facts.Text = $"{Faction(town.TownType)} · {BuildingName(town, BuiltInBuildings.TownHall)}"
                      + (overview.Governor is { } governor ? $" · governed by {governor.Name}" : "");
        _governor.Text = overview.Governor is { } who ? $"Governor: {who.Name}" : "No governor";
        ShowScene(town);
        ShowNotices(town, overview);
        ShowStrip(town);
        _income.ClearChildren();
        _income.AddChild(new Label { Text = "Income", ThemeTypeVariation = UiTheme.QuietLabel });
        _income.AddChild(ResourceIcons.CostRow(overview.Income, " a day"));
        ShowGrowth(overview);
        _date.Text = CalendarLabel.Describe(state);
        var swap = TownRules.CheckSwapHeroes(state, town);
        _swap.Disabled = swap is not null;
        _swap.TooltipText = swap ?? "The garrison hero and the visiting hero change places, each with its own troops.";
        var upgrades = TownPlans.Upgrade(state, town);
        _upgradeAll.Disabled = upgrades.IsEmpty;
        _upgradeAll.TooltipText = upgrades.IsEmpty
            ? "Nothing here can be upgraded that you can pay for."
            : $"Upgrade {upgrades.Orders.Count} stack(s) for {ResourceIcons.Describe(upgrades.Cost)}";
        ShowArmyButtons();
        _armies.Show(town);
        if (_window is not null)
        {
            FillWindow(town);
        }
    }

    /// <summary>The 3D scene where the town type has a layout (the backdrop and the painting hidden so it shows through), else the painting.</summary>
    private void ShowScene(Town town)
    {
        var inScene = _scene?.Open(town.Id) == true;
        _backdrop.Visible = !inScene;
        _painting.Visible = !inScene;
        _painting.Texture = inScene ? null : Painting(town.TownType);
        _stage.MouseFilter = inScene ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        if (!inScene)
        {
            ShowHovered(null);
        }
    }

    private void OnStageInput(InputEvent @event)
    {
        if (_scene is not { IsOpen: true } scene)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseMotion motion:
                scene.Hover(motion.GlobalPosition);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                if (scene.Pick(click.GlobalPosition))
                {
                    _stage.AcceptEvent();
                }

                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } info:
                scene.Hover(info.GlobalPosition);
                if (Current is { } town && scene.Hovered is { } plot)
                {
                    InfoWindow.Current?.Open(PlotSheet(town, plot, InfoWindow.Current.Detailed), info.GlobalPosition);
                    _stage.AcceptEvent();
                }

                break;
        }
    }

    /// <summary>The scene's building under the pointer: the hand cursor, its name as the tooltip and on the status bar.</summary>
    private void ShowHovered(PlotInfo? plot)
    {
        _stage.MouseDefaultCursorShape = plot is null ? CursorShape.Arrow : CursorShape.PointingHand;
        _stage.TooltipText = plot?.Name ?? "";
        Hovered?.Invoke(plot is null ? null : HoverText(plot));
    }

    private static string HoverText(PlotInfo plot) =>
        plot.Name + (plot.State == PlotState.BuiltToday ? " · built today" : "");

    /// <summary>A building of the scene was clicked: its window, a dwelling's window, or its information, as its strip button does.</summary>
    private void OpenPlot(PlotInfo plot)
    {
        if (Current is not { } town || ConfirmDialog.AnyOpen)
        {
            return;
        }

        if (plot.Line is { } line)
        {
            ShowDwelling(line);
        }
        else if (WindowOf(plot.Building) is { } window)
        {
            ShowWindow(window);
        }
        else
        {
            InfoWindow.Current?.Open(PlotSheet(town, plot, InfoWindow.Current.Detailed), GetViewport().GetMousePosition());
        }
    }

    private InfoSheet PlotSheet(Town town, PlotInfo plot, bool detailed) =>
        plot.Line is { } line
            ? InfoSheets.Dwelling(State, town, State.Creatures.Get(line), detailed)
            : InfoSheets.Building(State, town, plot.Building, detailed);

    private void ShowNotices(Town town, TownOverview overview)
    {
        _notices.ClearChildren();
        _notices.AddChild(new Label
        {
            Text = overview.BuiltToday is { } built ? $"Built today: {built}. The next building goes up tomorrow." : "Nothing built today yet.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var coming = CaravanRules.Queue(State, town);
        if (coming.Count > 0)
        {
            _notices.AddChild(new Label { Text = $"Caravans coming: {coming.Count}", ThemeTypeVariation = UiTheme.QuietLabel });
        }

        if (overview.Governor is { } governor)
        {
            _notices.AddChild(new Label
            {
                Text = $"{governor.Name}: buildings −{overview.BuildingDiscountPercent} %, creatures −{overview.CreatureDiscountPercent} %",
                ThemeTypeVariation = UiTheme.QuietLabel,
            });
        }
    }

    /// <summary>Next Monday's growth with each line's parts in the tooltip (base, fort, artifacts, growth building, Grail; next week's event is not known yet).</summary>
    private void ShowGrowth(TownOverview overview)
    {
        var built = overview.Lines.Where(l => l.DwellingLevel > 0).ToList();
        _growth.Text = $"Growth: +{built.Sum(l => l.Week)} a week ▸";
        _growth.TooltipText = built.Count == 0
            ? "No dwelling built yet."
            : string.Join('\n', built.Select(l => $"{l.Line.Name}: +{l.Week} ({string.Join(", ", l.Parts.Select(p => $"{GrowthName(p.Source)} {p.Amount:+0;−0;0}"))})"))
              + "\nBefore next week's event, drawn on Monday.";
    }

    private static string GrowthName(GrowthSource source) => source switch
    {
        GrowthSource.Base => "base",
        GrowthSource.Fort => "fort",
        GrowthSource.Artifacts => "artifacts",
        GrowthSource.GrowthBuilding => "growth building",
        _ => "Grail",
    };

    /// <summary>The building strip: each built building that opens a window, each built dwelling, and the buildings that show their information.</summary>
    private void ShowStrip(Town town)
    {
        var state = State;
        _strip.ClearChildren();
        var catalog = TownRules.Buildings(state, town);
        foreach (var building in catalog.TownBuildings.Where(b => town.Level(b) > 0))
        {
            var window = WindowOf(building);
            var button = new Button { Text = BuildingName(town, building) };
            var id = building;
            if (building.Value == BuiltInBuildings.Tavern && TavernRules.Offers(state, Player, town).FirstOrDefault() is { } nextHire)
            {
                button.TooltipText = $"Next hire: {nextHire.Name}";
            }

            button.WithInfo(detailed => InfoSheets.Building(_session.State, town, id, detailed));
            if (window is { } opens)
            {
                button.Pressed += () => ShowWindow(opens);
            }
            else
            {
                button.Pressed += () => InfoWindow.Current?.Open(InfoSheets.Building(_session.State, town, id, InfoWindow.Current.Detailed), button.GlobalPosition + new Vector2(0, -8));
            }

            _strip.AddChild(button);
        }

        foreach (var line in TownRules.Lines(state, town).Where(l => town.DwellingLevel(l.Id) > 0))
        {
            var dwelling = line;
            var button = new Button { Text = $"{line.Name} dwelling" };
            button.WithInfo(detailed => InfoSheets.Dwelling(_session.State, town, dwelling, detailed));
            button.Pressed += () => ShowDwelling(dwelling.Id);
            _strip.AddChild(button);
        }
    }

    /// <summary>The window a built building opens from the strip, or null for one that shows its information.</summary>
    private static TownWindow? WindowOf(BuildingId building) => building.Value switch
    {
        BuiltInBuildings.TownHall => TownWindow.Hall,
        BuiltInBuildings.Fort => TownWindow.Fort,
        BuiltInBuildings.Tavern => TownWindow.Tavern,
        BuiltInBuildings.Marketplace => TownWindow.Marketplace,
        BuiltInBuildings.Blacksmith => TownWindow.Blacksmith,
        BuiltInBuildings.MageGuild or BuiltInBuildings.WarLodge => TownWindow.Guild,
        BuiltInBuildings.Caravan => TownWindow.Caravan,
        UndeadTransformer.Building => TownWindow.Transformer,
        _ => null,
    };

    /// <summary>The picked stack's Split and Dismiss.</summary>
    private void ShowArmyButtons()
    {
        var picked = PickedStack();
        _split.Disabled = picked is not { Count: > 1 };
        _dismiss.Disabled = picked is null;
    }

    /// <summary>The picked stack of the garrison or the visiting hero, with where it is.</summary>
    private (TownArmy Army, int Slot, Core.Domain.Armies.CreatureStack Stack, int Count)? PickedStack()
    {
        if (Current is not { } town || Armies.Picked is not { Army: TownArmy army, Slot: var slot })
        {
            return null;
        }

        var troops = army == TownArmy.Garrison ? town.Garrison : TownRules.VisitingHero(State, town)?.Army;
        return troops?[slot] is { } stack ? (army, slot, stack, stack.Count) : null;
    }

    /// <summary>Split: how many of the picked stack go to the first empty slot of its row.</summary>
    private void Split()
    {
        if (Current is not { } town || PickedStack() is not { } picked)
        {
            _status.Text = "Pick a stack to split first.";
            return;
        }

        var troops = picked.Army == TownArmy.Garrison ? town.Garrison : TownRules.VisitingHero(State, town)!.Army;
        var empty = Enumerable.Range(0, Core.Domain.Armies.Army.SlotCount).FirstOrDefault(s => troops[s] is null, -1);
        if (empty < 0)
        {
            _status.Text = "The row has no empty slot to split into.";
            return;
        }

        var name = State.Creatures.Get(picked.Stack.Creature).Name;
        SliderDialog.Open(this, new SliderRequest($"Split {name}", 1, picked.Count - 1, picked.Count / 2, "Split", Describe: n => $"{n} of {picked.Count}"), count =>
        {
            Armies.Picked = null;
            Run(new MoveTownStackCommand(Player, town.Id, picked.Army, picked.Slot, picked.Army, empty, count));
        });
    }

    /// <summary>Dismiss: the picked stack leaves for good, after a question (ADR-296).</summary>
    private void Dismiss()
    {
        if (Current is not { } town || PickedStack() is not { } picked)
        {
            _status.Text = "Pick a stack to dismiss first.";
            return;
        }

        var name = State.Creatures.Get(picked.Stack.Creature).Name;
        var hero = picked.Army == TownArmy.VisitingHero ? TownRules.VisitingHero(State, town) : null;
        var refusal = hero is null
            ? Core.Domain.Armies.StackDismissal.Check(State, Player, town, picked.Slot)
            : Core.Domain.Armies.StackDismissal.Check(State, Player, hero, picked.Slot);
        if (refusal is not null)
        {
            _status.Text = $"Refused: {refusal}";
            return;
        }

        ConfirmDialog.Open(this, "Dismiss", ConfirmDialog.Lines($"Dismiss {picked.Count} {name} for good? Nothing is paid back."), "Dismiss", () =>
        {
            Armies.Picked = null;
            Run(hero is null ? new DismissStackCommand(Player, picked.Slot, Town: town.Id) : new DismissStackCommand(Player, picked.Slot, Hero: hero.Id));
        });
    }

    /// <summary>Upgrade all (U): what it upgrades and the total, then <see cref="UpgradeAllCommand"/> (ADR-296).</summary>
    private void UpgradeAll(Town town)
    {
        var plan = TownPlans.Upgrade(State, town);
        if (plan.IsEmpty)
        {
            _status.Text = "Nothing here can be upgraded that you can pay for.";
            return;
        }

        var body = new VBoxContainer();
        foreach (var order in plan.Orders)
        {
            body.AddChild(new Label { Text = $"{order.Count} {State.Creatures.Get(order.From).Name} → {State.Creatures.Get(order.To).Name}" });
        }

        var total = new HBoxContainer();
        total.AddChild(new Label { Text = "Total", ThemeTypeVariation = UiTheme.QuietLabel });
        total.AddChild(new CostLabel(plan.Cost, Treasury));
        body.AddChild(total);
        ConfirmDialog.Open(this, "Upgrade all", body, "Upgrade", () => Run(new UpgradeAllCommand(Player, town.Id)));
    }

    /// <summary>The name in the top bar renames the town (ADR-296).</summary>
    private void Rename()
    {
        if (Current is not { } town)
        {
            return;
        }

        var name = new LineEdit { Text = town.Name, MaxLength = TownRules.MaxNameLength, CustomMinimumSize = new Vector2(360, 0), SelectAllOnFocus = true };
        name.TextSubmitted += _ => ConfirmDialog.Current?.Confirm();
        ConfirmDialog.Open(this, $"Rename {town.Name}", name, "Rename", () => Run(new RenameTownCommand(Player, town.Id, name.Text)));
        name.CallDeferred(Control.MethodName.GrabFocus); // after the dialog's own, so typing goes into the field
    }

    private void Switch(Town? from, int step)
    {
        if (from is null)
        {
            return;
        }

        var towns = TownListPanel.TownsOf(State, Player);
        var index = towns.ToList().FindIndex(t => t.Id == from.Id);
        if (towns.Count < 2 || index < 0)
        {
            return;
        }

        var target = towns[(index + step + towns.Count) % towns.Count].Id;
        if (SwitchRequested is not null)
        {
            SwitchRequested(target);
        }
        else
        {
            Open(target);
        }
    }

    /// <summary>Fills the open window for <paramref name="town"/>: rebuilt whole when anything it shows changes.</summary>
    private void FillWindow(Town town)
    {
        if (_window is null || OpenWindow is not { } window)
        {
            return;
        }

        Release(_window.Content);
        _window.Content.ClearChildren();
        _window.Title = Title(town, window);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _window.Content.AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(body);
        switch (window)
        {
            case TownWindow.Hall:
                FillHall(town, body, _window.Content);
                break;
            case TownWindow.Recruitment:
                FillRecruitment(town, body);
                break;
            case TownWindow.Dwelling:
                FillDwelling(town, body);
                break;
            case TownWindow.Guild:
                FillGuild(town, body);
                break;
            case TownWindow.Tavern:
                FillTavern(town, body);
                break;
            case TownWindow.Marketplace:
                FillMarketplace(town, body);
                break;
            case TownWindow.Blacksmith:
                FillBlacksmith(town, body);
                break;
            case TownWindow.Fort:
                FillFort(town, body);
                break;
            case TownWindow.Caravan:
                FillCaravan(town, body);
                break;
            case TownWindow.Governor:
                FillGovernor(town, body);
                break;
            case TownWindow.Transformer:
                FillTransformer(town, body);
                break;
        }

        FocusOrder.Chain(FocusOrder.Focusable(_window));
    }

    /// <summary>Parks the kept sections' nodes out of a window's content before it is freed, so they live on for the next window.</summary>
    private void Release(Node content)
    {
        foreach (var kept in new Control[] { _caravan.Rows, _transformer.Row })
        {
            if (kept.GetParent() is { } parent && (parent == content || content.IsAncestorOf(kept)))
            {
                kept.Reparent(_parked, keepGlobalTransform: false);
            }
        }
    }

    /// <summary>Moves a kept section's node from where it is parked into a window.</summary>
    private static void Adopt(Control kept, Container into)
    {
        if (kept.GetParent() is { } parent)
        {
            parent.RemoveChild(kept);
        }

        into.AddChild(kept);
    }

    /// <summary>A line of quiet text, wrapped.</summary>
    private static Label Note(string text, Color? colour = null)
    {
        var label = new Label
        {
            Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, ThemeTypeVariation = UiTheme.QuietLabel, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(200, 0),
        };
        if (colour is { } c)
        {
            label.AddThemeColorOverride("font_color", c);
        }

        return label;
    }

    /// <summary>A window's text for a building the town has not built yet.</summary>
    private Label NotBuilt(Town town, BuildingId building) =>
        Note($"{town.Name} has no {TownRules.Buildings(State, town).Get(building, 1).Name.ToLowerInvariant()} yet: build it in the Hall.");

    private static Texture2D? Painting(string townType)
    {
        var path = $"{PaintingFolder}key_{townType}.jpg";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    private static string Faction(string townType) =>
        string.Join(' ', townType.Split('-').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
