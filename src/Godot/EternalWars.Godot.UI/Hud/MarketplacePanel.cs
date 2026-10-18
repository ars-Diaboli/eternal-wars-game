using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Trading gold against resources (ADR-067). Opened for the player's marketplace towns (a HUD button or the town screen), or for a map
/// marketplace an own hero stands on, where the rate is fixed. Shows the price of one unit of each resource, buying and selling the
/// amount in the spin box with no cap. Closes with Escape or "Close"; a map marketplace also closes when the hero leaves it.
/// </summary>
public partial class MarketplacePanel : PanelContainer
{
    private readonly Label _title = new();
    private readonly Label _info = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 0) };
    private readonly GridContainer _rows = new() { Columns = 5 };
    private readonly SpinBox _amount = new() { MinValue = 1, MaxValue = 1_000_000, Value = 1, Step = 1, Rounded = true };
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 0) };
    private GameSession _session = null!;
    private bool _isOpen;
    private MapObjectId? _mapMarketplace;

    public event Action? Closed;

    public bool IsOpen => Visible && _isOpen;

    public void Bind(GameSession session)
    {
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        var box = new VBoxContainer();
        AddChild(box);
        var header = new HBoxContainer();
        box.AddChild(header);
        header.AddChild(_title);
        var close = new Button { Text = "Close" };
        close.Pressed += Close;
        header.AddChild(close);
        box.AddChild(_info);
        var amount = new HBoxContainer();
        box.AddChild(amount);
        amount.AddChild(new Label { Text = "Amount:" });
        amount.AddChild(_amount);
        box.AddChild(_rows);
        box.AddChild(_status);
        Visible = false;

        this.SubscribeWhileInTree<ResourcesChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroMoved>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TownBuilt>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TownOwnerChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<GovernorChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<DecisionRequested>(session.Events, _ => Refresh());
    }

    /// <summary>The player has a town with a marketplace, so the HUD may offer the trading screen.</summary>
    public static bool CanOpenFromTowns(GameState state) => MarketplaceRules.CanTrade(state, state.CurrentPlayer.Id);

    /// <summary>Opens trading with the player's marketplace towns, or with the map marketplace given.</summary>
    public void Open(MapObjectId? mapMarketplace = null)
    {
        _isOpen = true;
        _mapMarketplace = mapMarketplace;
        _status.Text = "";
        Refresh();
    }

    public void Close()
    {
        var wasOpen = _isOpen;
        _isOpen = false;
        _mapMarketplace = null;
        Visible = false;
        if (wasOpen)
        {
            Closed?.Invoke();
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Refresh()
    {
        if (!_isOpen)
        {
            return;
        }

        var state = _session.State;
        var player = state.CurrentPlayer;
        if (state.PendingDecision is not null || !player.IsHuman || !CanTrade(state, player.Id))
        {
            Close();
            return;
        }

        Visible = true;
        _title.Text = _mapMarketplace is null ? "Marketplace" : "Map marketplace";
        _info.Text = _mapMarketplace is null
            ? $"Your marketplace towns set the rates (more towns, better rates; a governed town counts 1.5). {player.Name} has {player.Resources.Gold} gold."
            : $"A fixed rate, as if you had {MarketplaceRules.MapMarketplaceTowns} marketplace towns. {player.Name} has {player.Resources.Gold} gold.";

        foreach (var child in _rows.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var text in new[] { "Resource", "You have", "Buy 1 for", "", "Sell 1 for" })
        {
            _rows.AddChild(new Label { Text = text });
        }

        foreach (var resource in Enum.GetValues<ResourceType>().Where(MarketplaceRules.IsTradable))
        {
            var rate = _mapMarketplace is not null ? MarketplaceRules.MapRate(resource) : MarketplaceRules.TownRate(state, player.Id, resource)!.Value;
            _rows.AddChild(new Label { Text = resource.ToString() });
            _rows.AddChild(new Label { Text = player.Resources[resource].ToString() });
            _rows.AddChild(new Label { Text = $"{rate.BuyPrice} gold" });
            _rows.AddChild(TradeButton("Buy", resource, TradeDirection.Buy));
            var sell = TradeButton($"Sell ({rate.SellPrice} gold)", resource, TradeDirection.Sell);
            _rows.AddChild(sell);
        }
    }

    private bool CanTrade(GameState state, PlayerId player) =>
        _mapMarketplace is { } id
            ? state.FindObject(id) is Marketplace market && state.HeroAt(market.VisitPosition) is { } hero && hero.Owner == player
            : MarketplaceRules.CanTrade(state, player);

    private Button TradeButton(string text, ResourceType resource, TradeDirection direction)
    {
        var button = new Button { Text = text };
        button.Pressed += () =>
        {
            var amount = (int)_amount.Value;
            var result = _session.Execute(new TradeResourceCommand(_session.State.CurrentPlayer.Id, resource, direction, amount, _mapMarketplace));
            _status.Text = result.IsAccepted ? $"{(direction == TradeDirection.Buy ? "Bought" : "Sold")} {amount} {resource}." : $"Refused: {result.Error}";
        };
        return button;
    }
}
