using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>The town screen's windows, one per hotkey (ADR-294).</summary>
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
}

/// <summary>
/// The new town screen (the town screen plan, ADR-246, ADR-294), opened instead of <see cref="TownPanel"/> with <c>--town-screen=new</c> until
/// the plan's step 2 ends. Built in steps: this is step 0's shell with step 1's widgets in it. The top bar (crest, name, ◄ ►, the facts, the
/// quick buttons, ×), the main area (the faction's key painting as a stand-in for the 3D scene, with the notices card), the bottom bar (the
/// town's crest, the garrison and visiting hero as <see cref="ArmyBar"/>s with Swap, the income and the date) and a window host: one window
/// at a time over the dimmed main area; Escape closes the window, a second Escape leaves. The Hall shows <see cref="BuildingTile"/>s, the
/// recruitment window <see cref="CreatureCard"/>s with the recruit dialog; the other windows are titled and empty until step 2. The keys are
/// <see cref="TownScreenKeys"/>'.
/// </summary>
public partial class TownScreenPanel : Control, ITownScreen, ITownSectionHost
{
    private const string PaintingFolder = "res://shared/textures/ui/backgrounds/towns/";

    private readonly TownArmiesSection _armies;
    private readonly TextureRect _crest = new() { StretchMode = TextureRect.StretchModeEnum.KeepCentered, CustomMinimumSize = new Vector2(Heraldry.TitleSize, Heraldry.TitleSize) };
    private readonly Label _title = new() { ThemeTypeVariation = UiTheme.TitleLabel, VerticalAlignment = VerticalAlignment.Center };
    private readonly Label _facts = new() { ThemeTypeVariation = UiTheme.QuietLabel, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextureRect _painting = new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        MouseFilter = MouseFilterEnum.Ignore,
    };
    private readonly TextureRect _portrait = new() { StretchMode = TextureRect.StretchModeEnum.KeepCentered, CustomMinimumSize = new Vector2(96, 96) };
    private readonly Label _notice = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(260, 0) };
    private readonly HBoxContainer _income = new();
    private readonly Label _date = new() { ThemeTypeVariation = UiTheme.QuietLabel };
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(300, 0) };
    private readonly Button _swap = new() { Text = "Swap" };
    private readonly ColorRect _dim = new() { Color = new Color(0, 0, 0, 0.5f), MouseFilter = MouseFilterEnum.Stop, Visible = false };
    private readonly CenterContainer _windowHost = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly Dictionary<TownWindow, Button> _quick = [];
    private GameSession _session = null!;
    private MapObjectId? _town;
    private WindowFrame? _window;

    public TownScreenPanel()
    {
        Armies = TownArmiesSection.Group(this);
        _armies = new TownArmiesSection(this);
    }

    public event Action? Closed;

    public event Action? TradeRequested;

    public event Action? ThievesGuildRequested;

    public event Action<MapObjectId>? TavernRequested;

    /// <summary>H: the visiting hero's screen.</summary>
    public event Action<HeroId>? HeroRequested;

    /// <summary>◄ ► or [ ]: another own town; the adventure HUD centres the map on it and opens it. Without a listener the screen opens it itself.</summary>
    public event Action<MapObjectId>? SwitchRequested;

    /// <summary>Whether another screen covers this one (the hero screen, today's marketplace or tavern): its keys then stay off.</summary>
    public Func<bool>? IsCovered { get; set; }

    /// <summary>The window open over the main area, if any.</summary>
    public TownWindow? OpenWindow { get; private set; }

    public MapObjectId? CurrentTown => Visible ? _town : null;

    public GameSession Session => _session;

    public Town? Shown => Visible && _session.State.PendingDecision is null ? Current : null;

    public ArmyBarGroup Armies { get; }

    public bool HasTavernScreen => TavernRequested is not null;

    private Town? Current =>
        _town is { } id && _session.State.FindObject(id) is Town town && town.Owner == _session.State.CurrentPlayer.Id ? town : null;

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
        Visible = false;

        new RefreshScheduler(this, Refresh)
            .On<TownOwnerChanged>(session.Events)
            .On<DecisionRequested>(session.Events)
            .On<DecisionResolved>(session.Events)
            .On<TurnStarted>(session.Events)
            .On<TownBuilt>(session.Events)
            .On<DwellingBuilt>(session.Events)
            .On<TownBuildingDowngraded>(session.Events)
            .On<GovernorChanged>(session.Events)
            .On<ResourcesChanged>(session.Events)
            .On<CreaturesRecruited>(session.Events)
            .On<TownCreaturesGrew>(session.Events)
            .On<HeroMoved>(session.Events)
            .On<HeroEnteredGarrison>(session.Events)
            .On<HeroLeftGarrison>(session.Events)
            .On<HeroHired>(session.Events)
            .On<GarrisonChanged>(session.Events) // the Swap button's state
            .On<ArmyChanged>(session.Events);
        _armies.Watch(session.Events);
    }

    public void Open(MapObjectId town)
    {
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

    public void OpenTavern(MapObjectId town) => TavernRequested?.Invoke(town);

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
        if (OpenWindow == window)
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
        _window = new WindowFrame(Title(window), UiFrames.Accent(town.TownType)) { CustomMinimumSize = new Vector2(980, 560) };
        _window.CloseRequested += CloseWindow;
        _windowHost.AddChild(_window);
        _dim.Visible = true;
        FillWindow(town);
    }

    public void CloseWindow()
    {
        OpenWindow = null;
        _window?.QueueFree();
        _window = null;
        _dim.Visible = false;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || Current is not { } town || SliderDialog.Current is not null || IsCovered?.Invoke() == true)
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

        foreach (var window in Enum.GetValues<TownWindow>())
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
            Run(new SwapTownHeroesCommand(_session.State.CurrentPlayer.Id, town.Id));
        }
        else if (TownScreenKeys.Pressed(@event, TownKey.Hero))
        {
            if (TownRules.VisitingHero(_session.State, town) is { } hero)
            {
                HeroRequested?.Invoke(hero.Id);
            }
        }
        else if (TownScreenKeys.Pressed(@event, TownKey.UpgradeAll))
        {
            _status.Text = "Upgrade all comes with the plan's step 2: upgrade a stack with its arrow badge.";
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

    private static string Title(TownWindow window) => TownScreenKeys.All[KeyOf(window)].Label;

    private Control TopBar()
    {
        var bar = new PanelContainer { ThemeTypeVariation = UiTheme.BarPanel };
        var row = new HBoxContainer();
        bar.AddChild(row);
        row.AddChild(_crest);
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
        foreach (var window in Enum.GetValues<TownWindow>())
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
        area.AddChild(new ColorRect { Color = UiTheme.Background, MouseFilter = MouseFilterEnum.Ignore });
        area.GetChild<ColorRect>(0).SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        area.AddChild(_painting);
        _painting.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // The notices card in the top-right corner (built today).
        var card = new PanelContainer { ThemeTypeVariation = UiTheme.CardPanel };
        card.AddChild(_notice);
        area.AddChild(card);
        card.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
        card.GrowHorizontal = GrowDirection.Begin;
        card.OffsetRight = -UiSpacing.ScreenEdge * 2;
        card.OffsetTop = UiSpacing.ScreenEdge * 2;

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
        row.AddChild(_portrait);
        row.AddChild(new VSeparator());
        row.AddChild(_armies.Rows);
        var buttons = new VBoxContainer();
        _swap.SetMeta(UiIcons.StatusLabelMeta, TownScreenKeys.Hint(TownKey.SwapHeroes));
        _swap.Pressed += () =>
        {
            if (Current is { } town)
            {
                Run(new SwapTownHeroesCommand(_session.State.CurrentPlayer.Id, town.Id));
            }
        };
        buttons.AddChild(_swap);
        row.AddChild(buttons);
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        var info = new VBoxContainer();
        info.AddChild(_income);
        info.AddChild(_date);
        info.AddChild(_status);
        row.AddChild(info);
        return bar;
    }

    private void Refresh()
    {
        if (Current is not { } town || _session.State.PendingDecision is not null)
        {
            Visible = false;
            CloseWindow();
            return;
        }

        var state = _session.State;
        Visible = true;
        _crest.Texture = Heraldry.Crest(town.TownType, Heraldry.TitleSize);
        _portrait.Texture = Heraldry.Crest(town.TownType, 96);
        _title.Text = town.Name;
        var hall = BuildingTree.For(state, town).FirstOrDefault(n => n.Building == BuiltInBuildings.TownHall);
        var governor = StewardshipRules.GovernorOf(state, town);
        _facts.Text = $"{Faction(town.TownType)} · {(hall is null ? "" : TownRules.Buildings(state, town).Get(BuiltInBuildings.TownHall, Math.Max(1, hall.Level)).Name)}"
                      + (governor is null ? "" : $" · governed by {governor.Name}");
        _painting.Texture = Painting(town.TownType);
        _notice.Text = TownRules.HasBuiltToday(state, town)
            ? $"Built today: {town.LastBuiltName ?? "a building"}. The next building goes up tomorrow."
            : "Nothing built today yet.";
        _income.ClearChildren();
        _income.AddChild(new Label { Text = "Income", ThemeTypeVariation = UiTheme.QuietLabel });
        _income.AddChild(ResourceIcons.CostRow(TownRules.DailyIncome(state, town), " a day"));
        _date.Text = CalendarLabel.Describe(state);
        var swap = TownRules.CheckSwapHeroes(state, town);
        _swap.Disabled = swap is not null;
        _swap.TooltipText = swap ?? "The garrison hero and the visiting hero change places, each with its own troops.";
        _armies.Show(town);
        if (_window is not null)
        {
            FillWindow(town);
        }
    }

    private void Switch(Town? from, int step)
    {
        if (from is null)
        {
            return;
        }

        var towns = TownListPanel.TownsOf(_session.State, _session.State.CurrentPlayer.Id);
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

        _window.Content.ClearChildren();
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _window.Content.AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(body);
        switch (window)
        {
            case TownWindow.Hall:
                FillHall(town, body);
                break;
            case TownWindow.Recruitment:
                FillRecruitment(town, body);
                break;
            case TownWindow.Marketplace when town.Level(BuiltInBuildings.Marketplace) > 0:
                Placeholder(body, window);
                body.AddChild(Action("Trade at today's marketplace", () => TradeRequested?.Invoke()));
                break;
            case TownWindow.Tavern when town.Level(BuiltInBuildings.Tavern) > 0:
                Placeholder(body, window);
                if (HasTavernScreen)
                {
                    body.AddChild(Action("Open today's tavern", () => OpenTavern(town.Id)));
                }

                body.AddChild(Action("Thieves' guild", () => ThievesGuildRequested?.Invoke()));
                break;
            default:
                Placeholder(body, window);
                break;
        }

        FocusOrder.Chain(FocusOrder.Focusable(_window));
    }

    private static void Placeholder(VBoxContainer body, TownWindow window) =>
        body.AddChild(new Label
        {
            Text = $"The {Title(window).ToLowerInvariant()} window comes with the town screen plan's step 2. Until then, today's town screen does it.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ThemeTypeVariation = UiTheme.QuietLabel,
        });

    private Button Action(string text, Action pressed)
    {
        var button = new Button { Text = text, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        button.Pressed += () =>
        {
            CloseWindow();
            pressed();
        };
        return button;
    }

    /// <summary>The Hall: every building and dwelling as a <see cref="BuildingTile"/>; a tile that can be built builds on a click.</summary>
    private void FillHall(Town town, VBoxContainer body)
    {
        var state = _session.State;
        var player = state.CurrentPlayer.Id;
        var treasury = state.CurrentPlayer.Resources;
        body.AddHeading("Buildings");
        var buildings = new HFlowContainer();
        body.AddChild(buildings);
        foreach (var node in BuildingTree.For(state, town))
        {
            var tooltip = node.Status switch
            {
                BuildingStatus.Available => "Click to build.",
                BuildingStatus.Complete => "Fully built.",
                BuildingStatus.BuiltToday => "Built something today: tomorrow.",
                _ => node.Refusal ?? "",
            };
            var tile = new BuildingTile(node.Name, node.Next is null ? node.Level : node.Level + 1, node.MaxLevel, node.Status, node.Cost, treasury, tooltip);
            var building = node.Building;
            tile.Pressed += () => Run(new BuildCommand(player, town.Id, building));
            tile.WithInfo(detailed => InfoSheets.Building(_session.State, town, building, detailed));
            buildings.AddChild(tile);
        }

        body.AddHeading("Dwellings (7 of 9, every tier)");
        var dwellings = new HFlowContainer();
        body.AddChild(dwellings);
        foreach (var line in TownRules.Lines(state, town))
        {
            var status = BuildingTree.DwellingStatus(state, town, line);
            var next = TownRules.NextDwellingLevel(state, town, line);
            var refusal = TownRules.CheckDwelling(state, town, line.Id);
            var tooltip = status switch
            {
                BuildingStatus.Available => "Click to build.",
                BuildingStatus.Complete => "Fully built.",
                BuildingStatus.Closed => $"Closed for good: {refusal}",
                _ => refusal ?? "",
            };
            var level = town.DwellingLevel(line.Id);
            var tile = new BuildingTile(
                $"T{line.Tier} {line.Name}", next is null ? level : level + 1, 2, status, next is null ? null : TownRules.BuildCost(state, town, next), treasury, tooltip);
            var id = line.Id;
            var dwellingLine = line;
            tile.Pressed += () => Run(new BuildDwellingCommand(player, town.Id, id));
            tile.WithInfo(detailed => InfoSheets.Dwelling(_session.State, town, dwellingLine, detailed));
            dwellings.AddChild(tile);
        }
    }

    /// <summary>Recruitment: a <see cref="CreatureCard"/> per line; a built line's Recruit opens the recruit dialog.</summary>
    private void FillRecruitment(Town town, VBoxContainer body)
    {
        var state = _session.State;
        var flow = new HFlowContainer();
        body.AddChild(flow);
        foreach (var line in TownRules.Lines(state, town))
        {
            var column = new VBoxContainer();
            var card = new CreatureCard(state, line, town, state.CurrentPlayer.Resources);
            var level = town.DwellingLevel(line.Id);
            column.AddChild(card);
            if (level == 0)
            {
                card.Modulate = new Color(1, 1, 1, 0.55f);
                column.AddChild(new Label { Text = "Not built: build it in the Hall.", ThemeTypeVariation = UiTheme.QuietLabel });
            }
            else
            {
                var recruit = new Button { Text = $"Recruit {line.Name}" };
                recruit.Pressed += () => Recruit(town, line, card.Upgraded && level >= 2 ? card.Shown : line);
                recruit.Disabled = town.AvailableOf(line.Id) == 0;
                column.AddChild(recruit);
            }

            flow.AddChild(column);
        }
    }

    /// <summary>The recruit dialog (<see cref="SliderDialog"/>): into the garrison, or the visiting hero when the garrison has no room.</summary>
    private void Recruit(Town town, Core.Domain.Creatures.CreatureDefinition line, Core.Domain.Creatures.CreatureDefinition creature)
    {
        var state = _session.State;
        var available = town.AvailableOf(line.Id);
        if (available == 0)
        {
            return;
        }

        var cost = TownRules.CreatureCost(state, town, creature);
        var treasury = state.CurrentPlayer.Resources;
        var request = new SliderRequest(
            $"Recruit {creature.Name}", 1, available, Math.Max(1, TownRules.MaxAffordable(treasury, cost, available)), "Recruit", cost, treasury, n => $"{n} of {available}");
        SliderDialog.Open(this, request, count =>
        {
            var hero = town.Garrison.CanAccept(creature.Id) ? null : TownRules.VisitingHero(state, town)?.Id;
            Run(new RecruitCommand(state.CurrentPlayer.Id, town.Id, creature.Id, count, hero));
        });
    }

    private static Texture2D? Painting(string townType)
    {
        var path = $"{PaintingFolder}key_{townType}.jpg";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    private static string Faction(string townType) =>
        string.Join(' ', townType.Split('-').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
