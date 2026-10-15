using Godot;

namespace EW.Scripts.Interface;

public partial class InGameMenu : Control
{
    [Export(PropertyHint.File, "*.tscn")]
    public string MainMenuScenePath { get; set; } = "res://Scenes/MainMenu.tscn";

    [Export] public ColorRect Backdrop { get; set; }
    [Export] public Button LoadGameButton { get; set; }
    [Export] public Button SaveGameButton { get; set; }
    [Export] public Button MainMenuButton { get; set; }
    [Export] public Button ExitGameButton { get; set; }
    [Export] public Button ReturnToGameButton { get; set; }
    [Export] public LoadGameWindow LoadGameWindow { get; set; }

    private PanelContainer _menuPanel;

    public override void _Ready()
    {
        ResolveReferences();

        if (LoadGameButton != null)
        {
            LoadGameButton.Pressed += OnLoadGamePressed;
        }

        if (SaveGameButton != null)
        {
            SaveGameButton.Pressed += OnSaveGamePressed;
        }

        if (MainMenuButton != null)
        {
            MainMenuButton.Pressed += OnMainMenuPressed;
        }

        if (ExitGameButton != null)
        {
            ExitGameButton.Pressed += OnExitGamePressed;
        }

        if (ReturnToGameButton != null)
        {
            ReturnToGameButton.Pressed += OnReturnToGamePressed;
        }

        if (LoadGameWindow != null)
        {
            LoadGameWindow.BackRequested += OnLoadGameBackRequested;
        }

        CloseMenu();
    }

    public override void _ExitTree()
    {
        if (LoadGameButton != null)
        {
            LoadGameButton.Pressed -= OnLoadGamePressed;
        }

        if (SaveGameButton != null)
        {
            SaveGameButton.Pressed -= OnSaveGamePressed;
        }

        if (MainMenuButton != null)
        {
            MainMenuButton.Pressed -= OnMainMenuPressed;
        }

        if (ExitGameButton != null)
        {
            ExitGameButton.Pressed -= OnExitGamePressed;
        }

        if (ReturnToGameButton != null)
        {
            ReturnToGameButton.Pressed -= OnReturnToGamePressed;
        }

        if (LoadGameWindow != null)
        {
            LoadGameWindow.BackRequested -= OnLoadGameBackRequested;
        }
    }

    public void OpenMenu()
    {
        if (Backdrop == null)
        {
            ResolveReferences();
        }

        if (Backdrop != null)
        {
            Backdrop.Visible = true;
        }
    }

    private void OnReturnToGamePressed()
    {
        CloseMenu();
    }

    private void OnLoadGamePressed()
    {
        if (_menuPanel != null)
        {
            _menuPanel.Visible = false;
        }

        LoadGameWindow?.OpenWindow();
    }

    private void OnSaveGamePressed()
    {
        TurnManager turnManager = ResolveTurnManager();
        if (turnManager == null)
        {
            GD.PushWarning($"{Name}: Cannot save because no TurnManager was found.");
            return;
        }

        bool wasBackdropVisible = Backdrop?.Visible == true;
        if (Backdrop != null)
        {
            Backdrop.Visible = false;
        }

        string savePath = turnManager.SaveCurrentGame();
        if (!string.IsNullOrEmpty(savePath))
        {
            LoadGameWindow?.RefreshSaveList();
        }

        if (Backdrop != null)
        {
            Backdrop.Visible = wasBackdropVisible;
        }
    }

    private void OnLoadGameBackRequested()
    {
        if (_menuPanel != null)
        {
            _menuPanel.Visible = true;
        }
    }

    private void OnMainMenuPressed()
    {
        if (!string.IsNullOrWhiteSpace(MainMenuScenePath))
        {
            GetTree().ChangeSceneToFile(MainMenuScenePath);
        }
    }

    private void OnExitGamePressed()
    {
        GetTree().Quit();
    }

    private void CloseMenu()
    {
        if (Backdrop == null)
        {
            ResolveReferences();
        }

        if (Backdrop != null)
        {
            Backdrop.Visible = false;
        }

        LoadGameWindow?.CloseWindow();
        if (_menuPanel != null)
        {
            _menuPanel.Visible = true;
        }
    }

    private void ResolveReferences()
    {
        Backdrop ??= GetNodeOrNull<ColorRect>("Backdrop");
        _menuPanel ??= GetNodeOrNull<PanelContainer>("Backdrop/CenterContainer/PanelContainer");
        LoadGameButton ??= GetNodeOrNull<Button>("Backdrop/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/LoadGameButton");
        SaveGameButton ??= GetNodeOrNull<Button>("Backdrop/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/SaveGameButton");
        MainMenuButton ??= GetNodeOrNull<Button>("Backdrop/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/MainMenuButton");
        ExitGameButton ??= GetNodeOrNull<Button>("Backdrop/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/ExitGameButton");
        ReturnToGameButton ??= GetNodeOrNull<Button>("Backdrop/CenterContainer/PanelContainer/MarginContainer/VBoxContainer/ReturnToGameButton");
        LoadGameWindow ??= GetNodeOrNull<LoadGameWindow>("Backdrop/LoadGameWindow");
    }

    private TurnManager ResolveTurnManager()
    {
        Node sceneRoot = GetTree().CurrentScene ?? GetParent();
        return sceneRoot?.GetNodeOrNull<TurnManager>("TurnManager");
    }
}
