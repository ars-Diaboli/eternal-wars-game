using System;
using Godot;

namespace EW.Scripts.TechTree;

public partial class TownTechTree : Control
{
    [Export] public Godot.Collections.Array<TownTechTreeTheme> TechTreeThemes { get; set; } = new();
    [Export] public Label TechTreeTitleLabel { get; set; }
    [Export] public TextureRect TechTreeBackgroundRect { get; set; }
    [Export] public Label TechTreePlaceholderLabel { get; set; }
    [Export] public Button BackButton { get; set; }
    [Export] public Color BuiltMarkerColor { get; set; } = new(0.89f, 0.82f, 0.48f);
    [Export] public Color MissingResourcesMarkerColor { get; set; } = new(0.78f, 0.22f, 0.22f);
    [Export] public Color AvailableMarkerColor { get; set; } = new(0.24f, 0.72f, 0.34f);
    [Export] public Color DailyLimitMarkerColor { get; set; } = new(0.45f, 0.45f, 0.45f);
    [Export] public Button BuildTownHallButton { get; set; }
    [Export] public ColorRect BuildTownHallMarker { get; set; }
    [Export] public Button BuildFortButton { get; set; }
    [Export] public ColorRect BuildFortMarker { get; set; }
    [Export] public Button BuildBlacksmithButton { get; set; }
    [Export] public ColorRect BuildBlacksmithMarker { get; set; }
    [Export] public Button BuildMarketplaceButton { get; set; }
    [Export] public ColorRect BuildMarketplaceMarker { get; set; }
    [Export] public Button BuildMagesGuildButton { get; set; }
    [Export] public ColorRect BuildMagesGuildMarker { get; set; }
    [Export] public Button BuildTavernButton { get; set; }
    [Export] public ColorRect BuildTavernMarker { get; set; }
    [Export] public VBoxContainer DwellingListContainer { get; set; }
    [Export] public Label BuildingStatusLabel { get; set; }

    public event Action BackRequested;

    private readonly TownTechTreeModel _model = new();

    public override void _Ready()
    {
        ResolveReferences();
        _model.TechTreeThemes = TechTreeThemes;
        _model.TurnManager = ResolveTurnManager();

        if (BackButton != null)
        {
            BackButton.Pressed += OnBackPressed;
        }

        BindCommonBuildingButtons();
        Visible = false;
    }

    public override void _ExitTree()
    {
        if (BackButton != null)
        {
            BackButton.Pressed -= OnBackPressed;
        }

        UnbindCommonBuildingButtons();
    }

    public void ShowForTown(Town town)
    {
        ResolveReferences();
        _model.TechTreeThemes = TechTreeThemes;
        _model.TurnManager = ResolveTurnManager();
        _model.ShowTown(town);
        Visible = true;
        UpdateDisplay();
    }

    public void HideTree()
    {
        _model.HideTown();
        Visible = false;
    }

    private void OnBackPressed()
    {
        BackRequested?.Invoke();
    }

    private void UpdateDisplay()
    {
        TechTreeTitleLabel?.SetDeferred(Label.PropertyName.Text, _model.GetTitle());

        if (TechTreeBackgroundRect != null)
        {
            TechTreeBackgroundRect.Texture = _model.GetBackgroundTexture();
        }

        if (TechTreePlaceholderLabel != null)
        {
            TechTreePlaceholderLabel.Text = _model.GetPlaceholderText();
            TechTreePlaceholderLabel.Visible = _model.ShouldShowPlaceholder();
        }

        var commonStates = _model.GetCommonBuildingStates();
        ApplyBuildingState(BuildTownHallButton, BuildTownHallMarker, FindState(commonStates, TownBuildingType.TownHall));
        ApplyBuildingState(BuildFortButton, BuildFortMarker, FindState(commonStates, TownBuildingType.Fort));
        ApplyBuildingState(BuildBlacksmithButton, BuildBlacksmithMarker, FindState(commonStates, TownBuildingType.Blacksmith));
        ApplyBuildingState(BuildMarketplaceButton, BuildMarketplaceMarker, FindState(commonStates, TownBuildingType.Marketplace));
        ApplyBuildingState(BuildMagesGuildButton, BuildMagesGuildMarker, FindState(commonStates, TownBuildingType.MagesGuild));
        ApplyBuildingState(BuildTavernButton, BuildTavernMarker, FindState(commonStates, TownBuildingType.Tavern));

        RebuildDwellingRows();

        if (BuildingStatusLabel != null)
        {
            BuildingStatusLabel.Text = _model.GetStatusText();
        }
    }

    private void BindCommonBuildingButtons()
    {
        if (BuildTownHallButton != null) BuildTownHallButton.Pressed += OnBuildTownHallPressed;
        if (BuildFortButton != null) BuildFortButton.Pressed += OnBuildFortPressed;
        if (BuildBlacksmithButton != null) BuildBlacksmithButton.Pressed += OnBuildBlacksmithPressed;
        if (BuildMarketplaceButton != null) BuildMarketplaceButton.Pressed += OnBuildMarketplacePressed;
        if (BuildMagesGuildButton != null) BuildMagesGuildButton.Pressed += OnBuildMagesGuildPressed;
        if (BuildTavernButton != null) BuildTavernButton.Pressed += OnBuildTavernPressed;
    }

    private void UnbindCommonBuildingButtons()
    {
        if (BuildTownHallButton != null) BuildTownHallButton.Pressed -= OnBuildTownHallPressed;
        if (BuildFortButton != null) BuildFortButton.Pressed -= OnBuildFortPressed;
        if (BuildBlacksmithButton != null) BuildBlacksmithButton.Pressed -= OnBuildBlacksmithPressed;
        if (BuildMarketplaceButton != null) BuildMarketplaceButton.Pressed -= OnBuildMarketplacePressed;
        if (BuildMagesGuildButton != null) BuildMagesGuildButton.Pressed -= OnBuildMagesGuildPressed;
        if (BuildTavernButton != null) BuildTavernButton.Pressed -= OnBuildTavernPressed;
    }

    private void OnBuildTownHallPressed() => BuildAndRefresh(TownBuildingType.TownHall);
    private void OnBuildFortPressed() => BuildAndRefresh(TownBuildingType.Fort);
    private void OnBuildBlacksmithPressed() => BuildAndRefresh(TownBuildingType.Blacksmith);
    private void OnBuildMarketplacePressed() => BuildAndRefresh(TownBuildingType.Marketplace);
    private void OnBuildMagesGuildPressed() => BuildAndRefresh(TownBuildingType.MagesGuild);
    private void OnBuildTavernPressed() => BuildAndRefresh(TownBuildingType.Tavern);

    private void BuildAndRefresh(TownBuildingType buildingType)
    {
        _model.TurnManager = ResolveTurnManager();
        _model.TryBuild(buildingType);
        UpdateDisplay();
    }

    private void RebuildDwellingRows()
    {
        if (DwellingListContainer == null)
        {
            return;
        }

        foreach (Node child in DwellingListContainer.GetChildren())
        {
            child.QueueFree();
        }

        foreach (TownTechTreeBuildingState state in _model.GetDwellingStates())
        {
            HBoxContainer row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            ColorRect marker = new ColorRect();
            marker.CustomMinimumSize = new Vector2(14, 14);
            row.AddChild(marker);

            Button button = new Button();
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.Pressed += () => BuildAndRefresh(state.BuildingType);
            row.AddChild(button);

            DwellingListContainer.AddChild(row);
            ApplyBuildingState(button, marker, state);
        }
    }

    private void ApplyBuildingState(Button button, ColorRect marker, TownTechTreeBuildingState state)
    {
        if (button != null)
        {
            button.Visible = state?.Visible == true;
            if (state != null)
            {
                button.Text = state.ButtonText;
                button.TooltipText = state.TooltipText;
                button.Disabled = state.Disabled;
            }
        }

        if (marker != null)
        {
            marker.Visible = state?.Visible == true && state.MarkerState != TownTechTreeMarkerState.Hidden;
            if (state != null)
            {
                marker.Color = ResolveMarkerColor(state.MarkerState);
            }
        }
    }

    private Color ResolveMarkerColor(TownTechTreeMarkerState markerState)
    {
        return markerState switch
        {
            TownTechTreeMarkerState.Built => BuiltMarkerColor,
            TownTechTreeMarkerState.Available => AvailableMarkerColor,
            TownTechTreeMarkerState.DailyLimit => DailyLimitMarkerColor,
            TownTechTreeMarkerState.MissingResources => MissingResourcesMarkerColor,
            _ => Colors.Transparent
        };
    }

    private static TownTechTreeBuildingState FindState(System.Collections.Generic.IReadOnlyList<TownTechTreeBuildingState> states, TownBuildingType buildingType)
    {
        foreach (TownTechTreeBuildingState state in states)
        {
            if (state.BuildingType == buildingType)
            {
                return state;
            }
        }

        return null;
    }

    private void ResolveReferences()
    {
        TechTreeTitleLabel ??= GetNodeOrNull<Label>("VBoxContainer/HeaderLabel");
        TechTreeBackgroundRect ??= GetNodeOrNull<TextureRect>("VBoxContainer/BackgroundPanel/CenterContainer/TechTreeBackground");
        TechTreePlaceholderLabel ??= GetNodeOrNull<Label>("VBoxContainer/BackgroundPanel/CenterContainer/TechTreeBackground/PlaceholderLabel");
        BackButton ??= GetNodeOrNull<Button>("VBoxContainer/BottomBar/HBoxContainer/BackButton");
        BuildTownHallButton ??= GetNodeOrNull<Button>("VBoxContainer/BuildingsPanel/VBoxContainer/TownHallRow/BuildTownHallButton");
        BuildTownHallMarker ??= GetNodeOrNull<ColorRect>("VBoxContainer/BuildingsPanel/VBoxContainer/TownHallRow/Marker");
        BuildFortButton ??= GetNodeOrNull<Button>("VBoxContainer/BuildingsPanel/VBoxContainer/FortRow/BuildFortButton");
        BuildFortMarker ??= GetNodeOrNull<ColorRect>("VBoxContainer/BuildingsPanel/VBoxContainer/FortRow/Marker");
        BuildBlacksmithButton ??= GetNodeOrNull<Button>("VBoxContainer/BuildingsPanel/VBoxContainer/BlacksmithRow/BuildBlacksmithButton");
        BuildBlacksmithMarker ??= GetNodeOrNull<ColorRect>("VBoxContainer/BuildingsPanel/VBoxContainer/BlacksmithRow/Marker");
        BuildMarketplaceButton ??= GetNodeOrNull<Button>("VBoxContainer/BuildingsPanel/VBoxContainer/MarketplaceRow/BuildMarketplaceButton");
        BuildMarketplaceMarker ??= GetNodeOrNull<ColorRect>("VBoxContainer/BuildingsPanel/VBoxContainer/MarketplaceRow/Marker");
        BuildMagesGuildButton ??= GetNodeOrNull<Button>("VBoxContainer/BuildingsPanel/VBoxContainer/MagesGuildRow/BuildMagesGuildButton");
        BuildMagesGuildMarker ??= GetNodeOrNull<ColorRect>("VBoxContainer/BuildingsPanel/VBoxContainer/MagesGuildRow/Marker");
        BuildTavernButton ??= GetNodeOrNull<Button>("VBoxContainer/BuildingsPanel/VBoxContainer/TavernRow/BuildTavernButton");
        BuildTavernMarker ??= GetNodeOrNull<ColorRect>("VBoxContainer/BuildingsPanel/VBoxContainer/TavernRow/Marker");
        DwellingListContainer ??= GetNodeOrNull<VBoxContainer>("VBoxContainer/DwellingsPanel/VBoxContainer");
        BuildingStatusLabel ??= GetNodeOrNull<Label>("VBoxContainer/StatusLabel");
    }

    private TurnManager ResolveTurnManager()
    {
        return GetTree().CurrentScene?.GetNodeOrNull<TurnManager>("TurnManager");
    }
}
