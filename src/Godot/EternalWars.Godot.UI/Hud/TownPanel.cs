using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Basic town screen (ADR-046) for the current player's town: build the next level of a building, build or upgrade
/// dwellings, recruit, and manage the garrison and the visiting hero (click a stack, then a slot; "Upgrade" upgrades
/// the picked stack"), and appoint or dismiss the governor (ADR-063). Every action is a town command; refusals show
/// below. Opened with <see cref="Open"/>. With <see cref="FullScreen"/> (the game app) it fills its parent and scrolls;
/// Escape or "Close" shut it.
/// </summary>
public partial class TownPanel : PanelContainer
{
    private readonly Label _title = new();
    private readonly HFlowContainer _buildings = new();
    private readonly GridContainer _dwellings = new() { Columns = 4 };
    private readonly VBoxContainer _armies = new();
    private readonly HBoxContainer _blacksmith = new();
    private readonly HBoxContainer _tavern = new();
    private readonly HBoxContainer _governor = new();
    private readonly CheckBox _split = new() { Text = "Split (move half)" };
    private readonly CheckBox _toHero = new() { Text = "Recruit to visiting hero" };
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600, 0) };
    private GameSession _session = null!;
    private MapObjectId? _town;
    private (TownArmy Army, int Slot)? _picked;

    /// <summary>Fill the parent and scroll (the game app's town screen) instead of shrinking to the content. Set before <see cref="Bind"/>.</summary>
    public bool FullScreen { get; set; }

    public event Action? Closed;

    public void Bind(GameSession session)
    {
        _session = session;
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (FullScreen)
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
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
        header.AddChild(_title);
        var close = new Button { Text = "Close" };
        close.Pressed += Close;
        header.AddChild(close);

        box.AddChild(new Label { Text = "Buildings (one a day)" });
        box.AddChild(_buildings);
        box.AddChild(new Label { Text = "Dwellings (7 of 9, every tier)" });
        box.AddChild(_dwellings);
        box.AddChild(new Label { Text = "Governor (Stewardship)" });
        box.AddChild(_governor);
        box.AddChild(_armies);
        box.AddChild(_blacksmith);
        box.AddChild(_tavern);

        var options = new HBoxContainer();
        box.AddChild(options);
        options.AddChild(_split);
        options.AddChild(_toHero);
        var upgrade = new Button { Text = "Upgrade picked stack" };
        upgrade.Pressed += UpgradePicked;
        options.AddChild(upgrade);
        box.AddChild(_status);
        Visible = false;

        this.SubscribeWhileInTree<TownBuilt>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<DwellingBuilt>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<CreaturesRecruited>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<StackUpgraded>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<GarrisonChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArmyChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ResourcesChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TownCreaturesGrew>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TownOwnerChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroMoved>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<DecisionRequested>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<WarMachineBought>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroHired>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<GovernorChanged>(session.Events, _ => Refresh());
    }

    /// <summary>Shows a town of the player whose turn it is.</summary>
    public void Open(MapObjectId town)
    {
        _town = town;
        _picked = null;
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
        var state = _session.State;
        _title.Text = $"{town.Name} ({town.TownType}), {state.CurrentPlayer.Name}   income {Cost(TownRules.DailyIncome(state, town))} a day";

        Clear(_buildings);
        foreach (var building in state.Buildings.TownBuildings)
        {
            var next = TownRules.NextLevel(state, town, building);
            var refusal = TownRules.CheckBuild(state, town, building);
            var level = town.Level(building);
            var button = new Button
            {
                Text = next is null ? $"{state.Buildings.Get(building, level).Name} (max)" : $"{next.Name}\n{Cost(TownRules.BuildCost(state, town, next))}",
                Disabled = refusal is not null,
                TooltipText = refusal ?? $"Build {next!.Name}",
            };
            button.Pressed += () => Run(new BuildCommand(state.CurrentPlayer.Id, town.Id, building));
            _buildings.AddChild(button);
        }

        Clear(_dwellings);
        var resources = state.CurrentPlayer.Resources;
        foreach (var line in TownRules.Lines(state, town))
        {
            var level = town.DwellingLevel(line.Id);
            var available = town.AvailableOf(line.Id);
            _dwellings.AddChild(new Label
            {
                Text = $"T{line.Tier} {line.Name}: {(level switch { 0 => "not built", 1 => "built", _ => "upgraded" })}, {available} waiting",
                CustomMinimumSize = new Vector2(240, 0),
            });

            var next = TownRules.NextDwellingLevel(state, town, line);
            var refusal = TownRules.CheckDwelling(state, town, line.Id);
            var build = new Button
            {
                Text = next is null ? "—" : $"{(next.Level == 1 ? "Build" : "Upgrade")} {Cost(TownRules.BuildCost(state, town, next))}",
                Disabled = refusal is not null,
                TooltipText = refusal ?? "",
            };
            build.Pressed += () => Run(new BuildDwellingCommand(state.CurrentPlayer.Id, town.Id, line.Id));
            _dwellings.AddChild(build);

            foreach (var creature in new[] { line, line.UpgradesTo is { } up ? state.Creatures.Get(up) : null })
            {
                var canRecruit = creature is not null && level >= (creature == line ? 1 : 2) && available > 0;
                var count = canRecruit ? TownRules.MaxAffordable(resources, TownRules.CreatureCost(state, town, creature!), available) : 0;
                var recruit = new Button
                {
                    Text = creature is null ? "—" : $"Recruit {count} {creature.Name}\n{Cost(TownRules.CreatureCost(state, town, creature))} each",
                    Disabled = count == 0,
                };
                var id = creature?.Id;
                recruit.Pressed += () => Recruit(town, id!, count);
                _dwellings.AddChild(recruit);
            }
        }

        ShowGovernor(state, town);
        Clear(_armies);
        AddArmyRow(town, TownArmy.Garrison, "Garrison", town.Garrison);
        ShowTavern(state, town);
        Clear(_blacksmith);
        if (TownRules.VisitingHero(state, town) is { } hero)
        {
            AddArmyRow(town, TownArmy.VisitingHero, hero.Name, hero.Army);

            // The blacksmith sells the war machines the visiting hero lacks (ADR-056).
            _blacksmith.AddChild(new Label { Text = $"Blacksmith (war machines of {hero.Name})", CustomMinimumSize = new Vector2(240, 0) });
            foreach (var slot in Enum.GetValues<WarMachineSlot>())
            {
                var name = TownRules.WarMachineName(state, hero, slot);
                var refusal = TownRules.CheckBuyWarMachine(state, town, slot);
                var buy = new Button
                {
                    Text = hero.WarMachines.Contains(slot) ? $"{name} (owned)" : $"Buy {name}\n{Cost(WarMachines.Cost(slot))}",
                    Disabled = refusal is not null,
                    TooltipText = refusal ?? "",
                };
                buy.Pressed += () => Run(new BuyWarMachineCommand(state.CurrentPlayer.Id, town.Id, slot));
                _blacksmith.AddChild(buy);
            }
        }
    }

    /// <summary>The governor and what Stewardship does here (ADR-063); appoint the visiting hero, or dismiss the governor.</summary>
    private void ShowGovernor(GameState state, Town town)
    {
        Clear(_governor);
        var player = state.CurrentPlayer.Id;
        var governor = StewardshipRules.GovernorOf(state, town);
        var level = StewardshipRules.LevelIn(state, town);
        _governor.AddChild(new Label
        {
            Text = governor is null
                ? "No governor: Stewardship does not apply."
                : $"{governor.Name}: buildings -{StewardshipRules.BuildingDiscountPercent(level)} %, creatures -{StewardshipRules.CreatureDiscountPercent(level)} %",
            CustomMinimumSize = new Vector2(360, 0),
        });
        if (governor is not null)
        {
            var dismiss = new Button { Text = "Dismiss governor" };
            dismiss.Pressed += () => Run(new DismissGovernorCommand(player, town.Id));
            _governor.AddChild(dismiss);
        }

        if (TownRules.VisitingHero(state, town) is { } visitor && visitor != governor && StewardshipRules.CheckAppoint(state, town, visitor) is null)
        {
            var appoint = new Button { Text = $"Appoint {visitor.Name}" };
            appoint.Pressed += () => Run(new AppointGovernorCommand(player, town.Id, visitor.Id));
            _governor.AddChild(appoint);
        }
    }

    /// <summary>The tavern's offers (ADR-060): who can be hired for gold at the entrance, by the tavern's level.</summary>
    private void ShowTavern(GameState state, Town town)
    {
        Clear(_tavern);
        var level = town.Level(BuiltInBuildings.Tavern);
        if (level < 1)
        {
            return;
        }

        _tavern.AddChild(new Label { Text = $"Tavern {level}", CustomMinimumSize = new Vector2(110, 0) });
        foreach (var offer in TavernRules.Offers(state, state.CurrentPlayer.Id, town))
        {
            var refusal = TavernRules.CheckHire(state, town, offer.Id);
            var hire = new Button
            {
                Text = $"Hire {offer.Name}{System.Environment.NewLine}{offer.Class}, {offer.Town}{System.Environment.NewLine}{Cost(TavernRules.HeroCost)}",
                Disabled = refusal is not null,
                TooltipText = refusal ?? "",
            };
            hire.Pressed += () => Run(new HireHeroCommand(state.CurrentPlayer.Id, town.Id, offer.Id));
            _tavern.AddChild(hire);
        }
    }

    private void AddArmyRow(Town town, TownArmy which, string name, Army army)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = name, CustomMinimumSize = new Vector2(110, 0) });
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            var stack = army[slot];
            var target = (which, slot);
            var button = new Button
            {
                Text = stack is null ? "—" : $"{stack.Count}\n{stack.Creature.Split('.')[^1]}",
                CustomMinimumSize = new Vector2(86, 48),
                ToggleMode = true,
                ButtonPressed = _picked == target,
            };
            button.Pressed += () => Click(town, target, stack is not null);
            row.AddChild(button);
        }

        _armies.AddChild(row);
    }

    private void Click(Town town, (TownArmy Army, int Slot) target, bool occupied)
    {
        if (_picked is not { } from)
        {
            _picked = occupied ? target : null;
            Refresh();
            return;
        }

        _picked = null;
        if (from != target)
        {
            var source = from.Army == TownArmy.Garrison ? town.Garrison : TownRules.VisitingHero(_session.State, town)!.Army;
            var stack = source[from.Slot]!;
            int? count = _split.ButtonPressed && stack.Count > 1 ? stack.Count / 2 : null;
            Run(new MoveTownStackCommand(_session.State.CurrentPlayer.Id, town.Id, from.Army, from.Slot, target.Army, target.Slot, count));
        }

        Refresh();
    }

    private void UpgradePicked()
    {
        if (Current is not { } town || _picked is not { } picked)
        {
            _status.Text = "Pick a stack first.";
            return;
        }

        _picked = null;
        Run(new UpgradeStackCommand(_session.State.CurrentPlayer.Id, town.Id, picked.Army, picked.Slot));
    }

    private void Recruit(Town town, string creature, int count)
    {
        var hero = _toHero.ButtonPressed ? TownRules.VisitingHero(_session.State, town)?.Id : null;
        Run(new RecruitCommand(_session.State.CurrentPlayer.Id, town.Id, creature, count, hero));
    }

    private void Run(GameCommand command)
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

    private static void Clear(Node node)
    {
        // Freed at the end of the frame, as in ExchangePanel: the button whose signal is running stays valid.
        foreach (var child in node.GetChildren())
        {
            child.QueueFree();
        }
    }

    private static string Cost(ResourceSet cost)
    {
        var parts = Enum.GetValues<ResourceType>().Where(t => cost[t] != 0).Select(t => $"{cost[t]} {t.ToString().ToLowerInvariant()}").ToList();
        return parts.Count == 0 ? "free" : string.Join(", ", parts);
    }
}
