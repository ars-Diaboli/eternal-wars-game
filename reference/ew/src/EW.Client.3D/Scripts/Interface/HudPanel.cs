using Godot;

namespace EW.Scripts.Interface;

public partial class HudPanel : PanelContainer
{
    private const float PanelMargin = 12.0f;
    private const float PanelWidth = 236.0f;
    private const float PanelHeight = 540.0f;

    [Export] public Button MenuButton { get; set; }
    [Export] public Button EndTurnButton { get; set; }

    private InGameMenu _inGameMenu;
    private TurnManager _turnManager;

    public override void _Ready()
    {
        ResolveReferences();
        ApplySavedLayout();

        if (MenuButton != null)
        {
            MenuButton.Pressed += OnMenuPressed;
        }

        if (EndTurnButton != null)
        {
            EndTurnButton.Pressed += OnEndTurnPressed;
        }
    }

    public override void _ExitTree()
    {
        if (MenuButton != null)
        {
            MenuButton.Pressed -= OnMenuPressed;
        }

        if (EndTurnButton != null)
        {
            EndTurnButton.Pressed -= OnEndTurnPressed;
        }
    }

    private void OnMenuPressed()
    {
        ResolveReferences();
        _inGameMenu?.OpenMenu();
    }

    private void OnEndTurnPressed()
    {
        ResolveReferences();
        if (_turnManager?.CurrentPlayer != null && _turnManager.CurrentPlayer.IsHuman)
        {
            _turnManager.EndCurrentTurn();
        }
    }

    private void ResolveReferences()
    {
        MenuButton ??= GetNodeOrNull<Button>("MarginContainer/HudStack/BottomControls/MenuButton");
        EndTurnButton ??= GetNodeOrNull<Button>("MarginContainer/HudStack/BottomControls/EndTurnButton");

        Node sceneRoot = GetTree().CurrentScene ?? GetParent();
        _turnManager = sceneRoot?.GetNodeOrNull<TurnManager>("TurnManager");

        CanvasLayer canvasLayer = GetParentOrNull<CanvasLayer>();
        _inGameMenu = canvasLayer?.GetNodeOrNull<InGameMenu>("InGameMenu")
            ?? sceneRoot?.GetNodeOrNull<InGameMenu>("CanvasLayer/InGameMenu");
    }

    private void ApplySavedLayout()
    {
        HudPanelSide hudPanelSide = GameSettingsStore.LoadHudPanelSide();
        SetAnchorsPreset(hudPanelSide == HudPanelSide.Right ? LayoutPreset.TopRight : LayoutPreset.TopLeft);
        OffsetTop = PanelMargin;
        OffsetBottom = PanelMargin + PanelHeight;

        if (hudPanelSide == HudPanelSide.Right)
        {
            OffsetLeft = -PanelWidth - PanelMargin;
            OffsetRight = -PanelMargin;
            return;
        }

        OffsetLeft = PanelMargin;
        OffsetRight = PanelMargin + PanelWidth;
    }
}
