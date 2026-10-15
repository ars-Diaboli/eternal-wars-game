using EW.Scripts.DataTypes.Enums;
using EW.Scripts.Messaging;
using EW.Scripts.TechTree;
using Godot;

namespace EW.Scripts.Interface;

public partial class TownScreen : Control
{
    [Export] public Label TownNameLabel { get; set; }
    [Export] public TextureRect TownImageRect { get; set; }
    [Export] public Button TechTreeButton { get; set; }
    [Export] public Button CloseButton { get; set; }
    [Export] public Control TownOverviewPanel { get; set; }
    [Export] public TownTechTree TechTree { get; set; }

    public Town CurrentTown { get; private set; }

    public override void _Ready()
    {
        ResolveReferences();

        if (TechTreeButton != null)
        {
            TechTreeButton.Pressed += OnTechTreePressed;
        }
        if (CloseButton != null)
        {
            CloseButton.Pressed += OnClosePressed;
        }
        if (TechTree != null)
        {
            TechTree.BackRequested += OnBackRequested;
        }

        Visible = false;
    }

    public override void _ExitTree()
    {
        if (TechTreeButton != null)
        {
            TechTreeButton.Pressed -= OnTechTreePressed;
        }
        if (CloseButton != null)
        {
            CloseButton.Pressed -= OnClosePressed;
        }
        if (TechTree != null)
        {
            TechTree.BackRequested -= OnBackRequested;
        }
    }

    public void ShowTown(Town town)
    {
        ResolveReferences();

        CurrentTown = town;
        if (TownNameLabel != null)
        {
            TownNameLabel.Text = town != null ? town.Name : "Town";
        }

        if (TechTreeButton != null)
        {
            TechTreeButton.Disabled = town == null || town.Faction == Faction.Neutral;
        }

        ShowTownOverview();
        Visible = true;
        Hub.Instance?.Publish(new TownScreenVisibilityChanged { IsOpen = true });
    }

    public void HideTown()
    {
        CurrentTown = null;
        ShowTownOverview();
        Visible = false;
        Hub.Instance?.Publish(new TownScreenVisibilityChanged { IsOpen = false });
    }

    private void OnClosePressed()
    {
        HideTown();
    }

    private void OnTechTreePressed()
    {
        ShowTechTree();
    }

    private void OnBackRequested()
    {
        ShowTownOverview();
    }

    private void ShowTownOverview()
    {
        if (TownOverviewPanel != null)
        {
            TownOverviewPanel.Visible = true;
        }

        if (TechTree != null)
        {
            TechTree.HideTree();
        }
    }

    private void ShowTechTree()
    {
        if (CurrentTown == null)
        {
            return;
        }

        if (TownOverviewPanel != null)
        {
            TownOverviewPanel.Visible = false;
        }

        if (TechTree != null)
        {
            TechTree.ShowForTown(CurrentTown);
        }
    }

    private void ResolveReferences()
    {
        TownNameLabel ??= GetNodeOrNull<Label>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/HeaderLabel");
        TownImageRect ??= GetNodeOrNull<TextureRect>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/TownOverviewPanel/ImagePanel/CenterContainer/TownImage");
        TechTreeButton ??= GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/BottomBar/HBoxContainer/TechTreeButton");
        CloseButton ??= GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/BottomBar/HBoxContainer/CloseButton");
        TownOverviewPanel ??= GetNodeOrNull<Control>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/TownOverviewPanel");
        TechTree ??= GetNodeOrNull<TownTechTree>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/TownTechTree");
    }
}
