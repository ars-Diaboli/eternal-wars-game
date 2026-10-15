using System.Collections.Generic;
using Godot;

namespace EW.Scripts.Interface;

public partial class ScenarioSelectionWindow : Control
{
    [Signal] public delegate void BackRequestedEventHandler();

    [Export] public Control SelectionPanel { get; set; }
    [Export] public Control ConfigurationPanel { get; set; }
    [Export] public ItemList ScenarioList { get; set; }
    [Export] public Button NextButton { get; set; }
    [Export] public Button BackButton { get; set; }
    [Export] public Label ConfigurationScenarioLabel { get; set; }
    [Export] public Button BeginButton { get; set; }
    [Export] public Button ConfigurationBackButton { get; set; }

    private readonly List<string> _scenarioPaths = new();
    private readonly List<string> _scenarioNames = new();
    private int _selectedIndex = -1;

    public override void _Ready()
    {
        ResolveReferences();

        if (ScenarioList != null)
        {
            ScenarioList.ItemSelected += OnScenarioSelected;
        }

        if (NextButton != null)
        {
            NextButton.Pressed += OnNextPressed;
        }

        if (BackButton != null)
        {
            BackButton.Pressed += OnBackPressed;
        }

        if (BeginButton != null)
        {
            BeginButton.Pressed += OnBeginPressed;
        }

        if (ConfigurationBackButton != null)
        {
            ConfigurationBackButton.Pressed += OnConfigurationBackPressed;
        }

        Visible = false;
        RefreshScenarioList();
    }

    public override void _ExitTree()
    {
        if (ScenarioList != null)
        {
            ScenarioList.ItemSelected -= OnScenarioSelected;
        }

        if (NextButton != null)
        {
            NextButton.Pressed -= OnNextPressed;
        }

        if (BackButton != null)
        {
            BackButton.Pressed -= OnBackPressed;
        }

        if (BeginButton != null)
        {
            BeginButton.Pressed -= OnBeginPressed;
        }

        if (ConfigurationBackButton != null)
        {
            ConfigurationBackButton.Pressed -= OnConfigurationBackPressed;
        }
    }

    public void OpenWindow()
    {
        RefreshScenarioList();
        ShowSelectionPanel();
        Visible = true;
    }

    public void CloseWindow()
    {
        ShowSelectionPanel();
        Visible = false;
    }

    private void RefreshScenarioList()
    {
        ResolveReferences();
        _scenarioPaths.Clear();
        _scenarioNames.Clear();
        _selectedIndex = -1;

        if (ScenarioList == null)
        {
            return;
        }

        ScenarioList.Clear();

        foreach ((string displayName, string scenePath) in ResolveAvailableScenarios())
        {
            _scenarioNames.Add(displayName);
            _scenarioPaths.Add(scenePath);
            ScenarioList.AddItem(displayName);
        }

        if (_scenarioPaths.Count == 0)
        {
            ScenarioList.AddItem("No scenarios available");
        }

        UpdateNextButtonState();
    }

    private void OnScenarioSelected(long index)
    {
        _selectedIndex = (int)index;
        UpdateNextButtonState();
    }

    private void OnNextPressed()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _scenarioPaths.Count)
        {
            return;
        }

        if (ConfigurationScenarioLabel != null)
        {
            ConfigurationScenarioLabel.Text = _scenarioNames[_selectedIndex];
        }

        ShowConfigurationPanel();
    }

    private void OnBeginPressed()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _scenarioPaths.Count)
        {
            return;
        }

        GetTree().ChangeSceneToFile(_scenarioPaths[_selectedIndex]);
    }

    private void OnBackPressed()
    {
        CloseWindow();
        EmitSignal(SignalName.BackRequested);
    }

    private void OnConfigurationBackPressed()
    {
        ShowSelectionPanel();
    }

    private void UpdateNextButtonState()
    {
        if (NextButton != null)
        {
            NextButton.Disabled = _selectedIndex < 0 || _selectedIndex >= _scenarioPaths.Count;
        }
    }

    private static IEnumerable<(string DisplayName, string ScenePath)> ResolveAvailableScenarios()
    {
        yield return ("World Map", "res://Scenes/world_map.tscn");
        yield return ("Adventure Map", "res://Scenes/adventure_map.tscn");
    }

    private void ShowSelectionPanel()
    {
        if (SelectionPanel != null)
        {
            SelectionPanel.Visible = true;
        }

        if (ConfigurationPanel != null)
        {
            ConfigurationPanel.Visible = false;
        }
    }

    private void ShowConfigurationPanel()
    {
        if (SelectionPanel != null)
        {
            SelectionPanel.Visible = false;
        }

        if (ConfigurationPanel != null)
        {
            ConfigurationPanel.Visible = true;
        }
    }

    private void ResolveReferences()
    {
        SelectionPanel ??= GetNodeOrNull<Control>("CenterContainer/PanelContainer/MarginContainer/SelectionPanel");
        ConfigurationPanel ??= GetNodeOrNull<Control>("CenterContainer/PanelContainer/MarginContainer/ConfigurationPanel");
        ScenarioList ??= GetNodeOrNull<ItemList>("CenterContainer/PanelContainer/MarginContainer/SelectionPanel/VBoxContainer/ScenarioList");
        NextButton ??= GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/SelectionPanel/VBoxContainer/BottomBar/NextButton");
        BackButton ??= GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/SelectionPanel/VBoxContainer/BottomBar/BackButton");
        ConfigurationScenarioLabel ??= GetNodeOrNull<Label>("CenterContainer/PanelContainer/MarginContainer/ConfigurationPanel/VBoxContainer/ScenarioNameLabel");
        BeginButton ??= GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/ConfigurationPanel/VBoxContainer/BottomBar/BeginButton");
        ConfigurationBackButton ??= GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/ConfigurationPanel/VBoxContainer/BottomBar/BackButton");
    }
}
