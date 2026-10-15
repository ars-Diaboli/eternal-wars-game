using System.Linq;
using EW.Scripts.Messaging;
using EW.Scripts.WorldMap;
using Godot;

namespace EW.Scripts.Players;

public partial class GamePlayer : Node,
    IHandleEvent<WorldMapResourceUiRefreshRequested>,
    IHandleEvent<TurnStart>,
    IHandleEvent<CapturableOwnershipChanged>,
    IHandleEvent<ResourceIncomeEvent>, 
    IHandleEvent<ResourceSpendRequestEvent>
{
    [Export(PropertyHint.Range, "0,9,1")] public int PlayerNumber { get; set; } = 1;
    [Export] public string DisplayName { get; set; } = "Player";
    [Export] public PlayerControlType ControlType { get; set; } = PlayerControlType.Human;
    [Export] public bool IsEnvironment { get; set; }
    [Export] public Color PlayerColor { get; set; } = new Color(DataTypes.Enums.PlayerColor.Red.Value);
    [Export] public Godot.Collections.Array<Heroes.Hero> Heroes { get; set; } = new();
    [Export] public AiBrain AiBrain { get; set; }

    public ResourceEconomy Economy { get; } = new();
    private readonly System.Collections.Generic.List<Town> _playerTowns = new();
    private ItemList _heroList;
    private ItemList _townList;
    private Label _selectedStatsLabel;

    public bool IsHuman => ControlType == PlayerControlType.Human;
    public bool IsAi => ControlType == PlayerControlType.Ai;

    public bool OwnsHero(Heroes.Hero hero)
    {
        return hero != null && Heroes.Contains(hero);
    }

    public override void _Ready()
    {
        ResolveHudReferences();

        if (_townList != null)
        {
            _townList.ItemSelected += OnTownListItemSelected;
        }

        RefreshHud();
    }

    public override void _ExitTree()
    {
        if (_townList != null)
        {
            _townList.ItemSelected -= OnTownListItemSelected;
        }
    }

    public void Handle(WorldMapResourceUiRefreshRequested eventData)
    {
        RefreshHud();
    }

    public void Handle(TurnStart eventData)
    {
        RefreshHud();
    }

    public void Handle(CapturableOwnershipChanged eventData)
    {
        RefreshHud();
    }

    public void Handle(ResourceIncomeEvent eventData)
    {
        if (eventData.Player == this)
        {
            Economy.Add(eventData.ResourceType, eventData.Amount);
            GD.Print($"{eventData.Player} economy: +{eventData.Amount} {eventData.ResourceType}. Total {Economy.Resources}");
        }
    }

    public void Handle(ResourceSpendRequestEvent eventData)
    {
        if (eventData.Player == this)
        {
            eventData.Succeeded = Economy.TrySpend(eventData.Costs);
        }
    }

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName;
    }

    private void RefreshHud()
    {
        if (!IsObservedPlayer())
        {
            return;
        }

        ResolveHudReferences();
        UpdateHeroList();
        UpdateTownList();
        UpdateSelectedStatsLabel();
    }

    private void UpdateHeroList()
    {
        if (_heroList == null)
        {
            return;
        }

        _heroList.Clear();

        if (Heroes == null || Heroes.Count == 0)
        {
            _heroList.AddItem("No heroes");
            return;
        }

        foreach (Heroes.Hero hero in Heroes)
        {
            if (!IsInstanceValid(hero))
            {
                continue;
            }

            _heroList.AddItem($"{hero.Name} ({hero.CurrentMovementPoints}/{hero.MaxMovementPoints})");
        }

        if (_heroList.ItemCount == 0)
        {
            _heroList.AddItem("No heroes");
        }
    }

    private void UpdateTownList()
    {
        if (_townList == null)
        {
            return;
        }

        _townList.Clear();
        _playerTowns.Clear();

        Node sceneRoot = GetTree().CurrentScene ?? GetParent();
        if (sceneRoot == null)
        {
            _townList.AddItem("No towns");
            return;
        }

        var playerTowns = sceneRoot
            .GetChildren()
            .OfType<Town>()
            .Where(town => IsInstanceValid(town) && ReferenceEquals(town.OwnerPlayer, this))
            .ToList();

        if (playerTowns.Count == 0)
        {
            _townList.AddItem("No towns");
            return;
        }

        foreach (Town town in playerTowns)
        {
            Vector2I gridPosition = town.GridPosition;
            _playerTowns.Add(town);
            _townList.AddItem($"{town.Name} ({gridPosition.X}, {gridPosition.Y})");
        }
    }

    private void UpdateSelectedStatsLabel()
    {
        if (_selectedStatsLabel == null)
        {
            return;
        }

        _selectedStatsLabel.Text = "Selected Stats\n-";
    }

    private void ResolveHudReferences()
    {
        Node sceneRoot = GetTree().CurrentScene ?? GetParent();
        _heroList ??= sceneRoot?.GetNodeOrNull<ItemList>("CanvasLayer/HudPanel/MarginContainer/HudStack/HeroPanel/MarginContainer/VBoxContainer/HeroList");
        _townList ??= sceneRoot?.GetNodeOrNull<ItemList>("CanvasLayer/HudPanel/MarginContainer/HudStack/TownPanel/MarginContainer/VBoxContainer/TownList");
        _selectedStatsLabel ??= sceneRoot?.GetNodeOrNull<Label>("CanvasLayer/HudPanel/MarginContainer/HudStack/SelectedStatsPanel/MarginContainer/SelectedStatsLabel");
    }

    private void OnTownListItemSelected(long index)
    {
        if (!IsObservedPlayer() || index < 0 || index >= _playerTowns.Count)
        {
            return;
        }

        GetTree().CurrentScene?.GetNodeOrNull<WorldMapController>("WorldMapController")?.OpenTown(_playerTowns[(int)index]);
    }

    private bool IsObservedPlayer()
    {
        TurnManager turnManager = ResolveTurnManager();
        if (turnManager == null)
        {
            return false;
        }

        GamePlayer observedPlayer = turnManager.LocalPlayer ?? turnManager.CurrentPlayer;
        return observedPlayer != null && ReferenceEquals(observedPlayer, this);
    }

    private TurnManager ResolveTurnManager()
    {
        Node sceneRoot = GetTree().CurrentScene ?? GetParent();
        return sceneRoot?.GetNodeOrNull<TurnManager>("TurnManager");
    }
}
