namespace EW.Scripts.Interface;
using EW.Scripts;
using Godot;
using System.Collections.Generic;

public partial class MainMenu : Control
{
    private static readonly Vector2I DefaultWindowSize = new(1920, 1080);

    [Export(PropertyHint.File, "*.tscn")] 
    public string OverworldScenePath { get; set; } = "res://Scenes/world_map.tscn";

    private VBoxContainer _mainMenuContainer;
    private PanelContainer _settingsWindow;
    private LoadGameWindow _loadGameWindow;
    private ScenarioSelectionWindow _scenarioSelectionWindow;
    
    private Button _newCampaignButton;
    private Button _customScenarioButton;
    private Button _loadGameButton;
    private Button _settingsButton;
    private Button _exitButton;
    private Button _closeSettingsButton;
    
    private OptionButton _resolutionDropdown;
    private OptionButton _hudSideDropdown;

    // A structured list of standard desktop target aspect resolutions
    private readonly List<Vector2I> _resolutions =
    [
        new(1280, 720),
        new(1600, 900),
        new(1920, 1080),
        new(2560, 1440),
        new(3840, 2160)
    ];

    public override void _Ready()
    {
        _mainMenuContainer = GetNode<VBoxContainer>("MarginContainer/VBoxContainer");
        _settingsWindow = GetNode<PanelContainer>("SettingsWindow");
        _loadGameWindow = GetNodeOrNull<LoadGameWindow>("LoadGameWindow");
        _scenarioSelectionWindow = GetNodeOrNull<ScenarioSelectionWindow>("ScenarioSelectionWindow");
        ApplySavedSettings();

        InitMainMenu();

        InitializeSettingsWindow();

        InitializeResolutionDropdown();
        InitializeHudSideDropdown();

        if (_loadGameWindow != null)
        {
            _loadGameWindow.BackRequested += OnLoadGameBackPressed;
        }

        if (_scenarioSelectionWindow != null)
        {
            _scenarioSelectionWindow.BackRequested += OnScenarioSelectionBackPressed;
        }
    }

    public override void _ExitTree()
    {
        if (_loadGameWindow != null)
        {
            _loadGameWindow.BackRequested -= OnLoadGameBackPressed;
        }

        if (_scenarioSelectionWindow != null)
        {
            _scenarioSelectionWindow.BackRequested -= OnScenarioSelectionBackPressed;
        }

        if (_loadGameButton != null)
        {
            _loadGameButton.Pressed -= OnLoadGamePressed;
        }

        if (_customScenarioButton != null)
        {
            _customScenarioButton.Pressed -= OnCustomScenarioPressed;
        }

        if (_newCampaignButton != null)
        {
            _newCampaignButton.Pressed -= OnNewCampaignPressed;
        }

        if (_settingsButton != null)
        {
            _settingsButton.Pressed -= OnSettingsPressed;
        }

        if (_exitButton != null)
        {
            _exitButton.Pressed -= OnExitPressed;
        }

        if (_resolutionDropdown != null)
        {
            _resolutionDropdown.ItemSelected -= OnResolutionSelected;
        }

        if (_hudSideDropdown != null)
        {
            _hudSideDropdown.ItemSelected -= OnHudSideSelected;
        }

        if (_closeSettingsButton != null)
        {
            _closeSettingsButton.Pressed -= OnSettingsBackPressed;
        }
    }

    private static void SetWindowToFullHd()
    {
        Vector2I fullHD = new Vector2I(1920, 1080);
        ApplyWindowResolution(fullHD);
    }

    private void InitializeSettingsWindow()
    {
        _resolutionDropdown = GetNode<OptionButton>("SettingsWindow/PanelContainer/MarginContainer/VBoxContainer/HBoxContainer/ResolutionOptionButton");
        _resolutionDropdown.ItemSelected += OnResolutionSelected;
        _hudSideDropdown = GetNode<OptionButton>("SettingsWindow/PanelContainer/MarginContainer/VBoxContainer/HudSideRow/HudSideOptionButton");
        _hudSideDropdown.ItemSelected += OnHudSideSelected;
        _closeSettingsButton = GetNode<Button>("SettingsWindow/PanelContainer/MarginContainer/BackButton");
        _closeSettingsButton.Pressed += OnSettingsBackPressed;
    }

    private void InitMainMenu()
    {
        _newCampaignButton = GetNode<Button>("MarginContainer/VBoxContainer/NewCampaignButton");
        _newCampaignButton.Pressed += OnNewCampaignPressed;

        _customScenarioButton = GetNode<Button>("MarginContainer/VBoxContainer/CustomScenarioButton");
        _customScenarioButton.Pressed += OnCustomScenarioPressed;

        _loadGameButton = GetNode<Button>("MarginContainer/VBoxContainer/LoadGameButton");
        _loadGameButton.Pressed += OnLoadGamePressed;

        _settingsButton = GetNode<Button>("MarginContainer/VBoxContainer/SettingsButton");
        _settingsButton.Pressed += OnSettingsPressed;

        _exitButton = GetNode<Button>("MarginContainer/VBoxContainer/ExitButton");
        _exitButton.Pressed += OnExitPressed;
    }

    private void InitializeResolutionDropdown()
    {
        _resolutionDropdown.Clear();
        Vector2I currentWindowSize = DisplayServer.WindowGetSize();
        int defaultSelectionIndex = 2; // Default fallback to 1080p if matching fails

        for (int i = 0; i < _resolutions.Count; i++)
        {
            Vector2I res = _resolutions[i];
            _resolutionDropdown.AddItem($"{res.X} x {res.Y}");

            // If this menu entry matches the player's current native monitor screen setup, snap to highlight it
            if (res.X == currentWindowSize.X && res.Y == currentWindowSize.Y)
            {
                defaultSelectionIndex = i;
            }
        }

        _resolutionDropdown.Select(defaultSelectionIndex);
    }

    private void InitializeHudSideDropdown()
    {
        if (_hudSideDropdown == null)
        {
            return;
        }

        _hudSideDropdown.Clear();
        _hudSideDropdown.AddItem("Left");
        _hudSideDropdown.AddItem("Right");
        _hudSideDropdown.Select((int)GameSettingsStore.LoadHudPanelSide());
    }

    private void OnSettingsPressed()
    {
        _mainMenuContainer.Visible = false;
        _settingsWindow.Visible = true;
    }

    private void OnSettingsBackPressed()
    {
        _settingsWindow.Visible = false;
        _mainMenuContainer.Visible = true;
    }

    private void OnResolutionSelected(long index)
    {
        Vector2I targetResolution = _resolutions[(int)index];
        GD.Print($"Settings: Adjusting application window scaling resolution to {targetResolution.X}x{targetResolution.Y}");

        ApplyWindowResolution(targetResolution);

        Error saveError = GameSettingsStore.SaveWindowSize(targetResolution);
        if (saveError != Error.Ok)
        {
            GD.PushWarning($"{Name}: Failed to save settings. Error: {saveError}");
        }
    }

    private void OnHudSideSelected(long index)
    {
        HudPanelSide hudPanelSide = (HudPanelSide)(int)index;
        Error saveError = GameSettingsStore.SaveHudPanelSide(hudPanelSide);
        if (saveError != Error.Ok)
        {
            GD.PushWarning($"{Name}: Failed to save HUD side setting. Error: {saveError}");
        }
    }

    private void OnNewCampaignPressed()
    {
        GetTree().ChangeSceneToFile(OverworldScenePath);
    }

    private void OnLoadGamePressed()
    {
        _mainMenuContainer.Visible = false;
        _settingsWindow.Visible = false;
        _scenarioSelectionWindow?.CloseWindow();
        _loadGameWindow?.OpenWindow();
    }

    private void OnLoadGameBackPressed()
    {
        _mainMenuContainer.Visible = true;
        _settingsWindow.Visible = false;
    }

    private void OnCustomScenarioPressed()
    {
        _mainMenuContainer.Visible = false;
        _settingsWindow.Visible = false;
        _loadGameWindow?.CloseWindow();
        _scenarioSelectionWindow?.OpenWindow();
    }

    private void OnScenarioSelectionBackPressed()
    {
        _mainMenuContainer.Visible = true;
        _settingsWindow.Visible = false;
    }

    private void OnExitPressed()
    {
        GetTree().Quit();
    }

    private void ApplySavedSettings()
    {
        Vector2I savedWindowSize = GameSettingsStore.LoadWindowSize(DefaultWindowSize);
        ApplyWindowResolution(savedWindowSize);
    }

    private static void ApplyWindowResolution(Vector2I targetResolution)
    {
        DisplayServer.WindowSetSize(targetResolution);
        Vector2I screenSize = DisplayServer.ScreenGetSize();
        Vector2I newPosition = (screenSize - targetResolution) / 2;
        DisplayServer.WindowSetPosition(newPosition);
    }
}
