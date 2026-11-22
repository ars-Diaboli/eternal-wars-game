using EternalWars.Godot.Shared.Battle;
using EternalWars.Godot.Shared.Cameras;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.Shared.Towns;
using EternalWars.Godot.UI.Battle;
using EternalWars.Godot.UI.Hud;
using EternalWars.Godot.UI.Setup;
using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// Which of the game app's screens is shown (ADR-222): the screens before a game on their layer (<see cref="SetupScreenHost"/>, ADR-101), the
/// F10 menu above everything and its button, and during a game the adventure map or the battle scene in its place. The app builds the screens
/// and the game's views and hands them over here; this class opens, closes and swaps them, and places the menu button.
/// </summary>
/// <remarks>
/// On the adventure map the menu is one of the HUD's action buttons (ADR-210), and the menu button is hidden. In a battle that is played it sits
/// on the status bar's right end (ADR-204); in the replay viewer's battles (beside its bar) and before a game it stands in the bottom right
/// corner. The setup screens hide it.
/// </remarks>
public sealed class GameScreens(Node owner, Func<bool> playing)
{
    private SetupScreenHost? _setup;
    private Button _menuButton = null!;
    private bool _inGame;
    private AdventureHud? _hud;
    private BattleView3D? _battle;
    private AutoCombatPanel? _autoCombat;
    private ChatPanel? _chat;

    /// <summary>The F10 menu (save, load, settings), above every screen, the battle scene too, so saving works in battle.</summary>
    public GameMenuPanel Menu { get; private set; } = null!;

    /// <summary>Whether a screen before a game is shown.</summary>
    public bool IsSetupShown => _setup is not null;

    /// <summary>
    /// Whether a screen is open over the game that holds it: the menu, the battle scene or the auto combat question. The computer players
    /// wait, and a turn played here does not end on the timer.
    /// </summary>
    public bool HoldTheGame => Menu.Visible || _battle?.IsOpen == true || _autoCombat?.Visible == true;

    /// <summary>The menu's layer and its button; the app binds the menu.</summary>
    public void Build()
    {
        var layer = new CanvasLayer { Name = "Menu", Layer = 5 };
        owner.AddChild(layer);
        Menu = new GameMenuPanel();
        layer.AddChild(Menu);
        _menuButton = new Button { Text = "Menu (F10)" }.WithIcon(UiIcon.Menu, UiIcons.StatusBarSize);
        _menuButton.Pressed += Menu.Toggle;
        layer.AddChild(_menuButton);
        PlaceMenuButton();
        owner.GetViewport().SizeChanged += PlaceMenuButton;
    }

    /// <summary>Puts a screen on the setup layer in place of the one shown; Escape does <paramref name="back"/> (none on the main menu).</summary>
    public void ShowSetup(Control screen, Action? back)
    {
        if (_setup is null)
        {
            _setup = new SetupScreenHost();
            owner.AddChild(_setup);
        }

        _setup.ShowScreen(screen, back);
        _menuButton.Visible = false;
    }

    /// <summary>Takes the setup layer down, for a game.</summary>
    public void CloseSetup()
    {
        _setup?.QueueFree();
        _setup = null;
        PlaceMenuButton();
    }

    /// <summary>Escape on a screen before a game, while the menu is closed: that screen's way back. True when it went back.</summary>
    public bool GoBack()
    {
        if (_setup?.Back is not { } back || Menu.Visible)
        {
            return false;
        }

        back();
        return true;
    }

    /// <summary>A game's views are being built.</summary>
    public void EnterGame() => _inGame = true;

    /// <summary>The game is taken down; its views go at the end of the frame.</summary>
    public void LeaveGame()
    {
        _inGame = false;
        PlaceMenuButton();
    }

    /// <summary>The adventure map's HUD of the game being built.</summary>
    public void ShowHud(AdventureHud hud)
    {
        _hud = hud;
        PlaceMenuButton();
    }

    /// <summary>
    /// The battle scene of the game being built: while it is open it replaces the adventure map, its HUD and its camera, and the chat of a LAN game
    /// (<paramref name="chat"/>) moves above the battle's log.
    /// </summary>
    public void ShowBattle(BattleView3D battle, AutoCombatPanel? autoCombat, AdventureWorldView world, StrategyCamera camera, ChatPanel? chat)
    {
        (_battle, _autoCombat, _chat) = (battle, autoCombat, chat);
        battle.Opened += () =>
        {
            world.Visible = false;
            _hud!.Visible = false;
            PlaceMenuButton();
            camera.ProcessMode = Node.ProcessModeEnum.Disabled;
            world.PathPreview.Clear();
            PlaceChat(battle: true);
        };
        battle.Closed += () =>
        {
            GameCursors.Show(Core.Domain.Cursors.GameCursor.Pointer); // the battle's last cursor goes; the map's comes with the next move
            world.Visible = true;
            _hud!.Visible = true;
            PlaceMenuButton();
            camera.ProcessMode = Node.ProcessModeEnum.Inherit;
            camera.MakeCurrent();
            PlaceChat(battle: false);
        };
    }

    /// <summary>
    /// The town screen's 3D scene of the game being built (ADR-297): while it is open the adventure world, its light, its camera and the HUD's map
    /// parts are put away, as for a battle; the town screen's bars and the status bar stay over it.
    /// </summary>
    public void ShowTown(TownScene3D town, AdventureWorldView world, StrategyCamera camera, DirectionalLight3D sun)
    {
        town.Opened += () =>
        {
            _hud!.MapPartsHidden = true;
            world.Visible = false;
            sun.Visible = false;
            camera.ProcessMode = Node.ProcessModeEnum.Disabled;
            world.PathPreview.Clear();
        };
        town.Closed += () =>
        {
            _hud!.MapPartsHidden = false;
            world.Visible = true;
            sun.Visible = true;
            camera.ProcessMode = Node.ProcessModeEnum.Inherit;
            camera.MakeCurrent();
        };
    }

    /// <summary>The chat at the bottom left of the map; over a battle, above the battle's log.</summary>
    private void PlaceChat(bool battle)
    {
        if (_chat is not null)
        {
            (_chat.OffsetTop, _chat.OffsetBottom) = battle ? (-430, -250) : (-190, -40);
        }
    }

    /// <summary>The menu button where the screen shown wants it (see the remarks), or hidden.</summary>
    public void PlaceMenuButton()
    {
        var screen = owner.GetViewport().GetVisibleRect().Size;
        var inGame = GodotObject.IsInstanceValid(_hud) && _inGame;
        var inBattle = inGame && GodotObject.IsInstanceValid(_battle) && _battle!.IsOpen;
        _menuButton.Visible = _setup is null && !(inGame && !inBattle);
        if (playing() && inBattle)
        {
            var slot = StatusBar.MenuSlot(screen);
            _menuButton.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            _menuButton.Position = slot.Position;
            _menuButton.Size = slot.Size;
            return;
        }

        _menuButton.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        (_menuButton.OffsetLeft, _menuButton.OffsetTop) = (-StatusBar.MenuButtonWidth - UiSpacing.ScreenEdge, -StatusBar.Height - UiSpacing.ScreenEdge);
        (_menuButton.OffsetRight, _menuButton.OffsetBottom) = (-UiSpacing.ScreenEdge, -UiSpacing.ScreenEdge);
    }
}
