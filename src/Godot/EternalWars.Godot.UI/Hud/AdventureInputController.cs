using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Cursors;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Godot.Shared.Cameras;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The mouse and keys on the adventure map for the player whose turn it is, and the selected hero. Click an own hero to select it, click a tile
/// to preview the route (markers coloured by day), click the same tile again to go; an armed adventure spell casts on the next clicked cell.
/// Right click clears the preview (and the armed spell) and opens the information window of what is there (ADR-154). Keys: arrows/numpad
/// step, Tab next hero, T next town, H hero screen, K kingdom, Q quest log, P puzzle map, G dig, L switch level. Results, and the name of what
/// the pointer is over, go to the HUD's status line.
/// </summary>
public sealed class AdventureInputController(
    GameSession session, MapViewContext context, AdventureWorldView world, StrategyCamera camera, AdventureHud hud, InfoWindow info)
{
    /// <summary>The hero and the aimed adventure spell waiting for its cell (Dimension Door, Distant Vision, Eavesdrop, Town Portal II): the next map click casts it there.</summary>
    private (HeroId Hero, string Spell)? _aimedSpell;

    /// <summary>The cell last named on the status line.</summary>
    private MapPosition? _hovered;

    /// <summary>The cell and the state the cursor was last chosen for (ADR-204); cleared when a selection, a spell or a move may change it.</summary>
    private MapPosition? _cursorCell;

    /// <summary>The own hero the keys and clicks move; null for none.</summary>
    public HeroId? SelectedHero { get; private set; }

    /// <summary>Handles an unhandled input event on the map (no battle, menu or question in the way); true when it was the map's.</summary>
    public bool Handle(InputEvent @event)
    {
        if (hud.QuestScreen.Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Q })
        {
            hud.QuestScreen.Visible = false;
            camera.IgnoreHeldKeys(); // Q also turns the camera; this press only closes the log
            return true;
        }

        if (hud.PuzzleScreen.Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.P })
        {
            hud.PuzzleScreen.Visible = false;
            return true;
        }

        if (hud.IsScreenOpen)
        {
            return false; // a full-screen panel takes the input (Escape closes it)
        }

        if (@event is InputEventMouseMotion motion)
        {
            ShowHover(motion.Position);
            return false; // the camera and the HUD see the pointer too
        }

        if (session.State.PendingDecision is not null)
        {
            return false;
        }

        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                if (MapSpace.PickTile(camera, click.Position, session.State.Map, context.Level) is { } tile && session.State.Map.Contains(tile))
                {
                    ClickTile(tile);
                }

                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } rightClick:
                world.PathPreview.Clear();
                _aimedSpell = null;
                RefreshCursor();
                if (MapSpace.PickTile(camera, rightClick.Position, session.State.Map, context.Level) is { } pointed
                    && InfoSheets.MapCell(session.State, session.State.CurrentPlayer.Id, pointed, info.Detailed) is { } sheet)
                {
                    info.Open(sheet, rightClick.Position);
                }

                break;
            case InputEventKey { Pressed: true, Keycode: Key.Tab }:
                SelectNextHero();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.T }:
                hud.OpenNextTown();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.H }:
                hud.ShowHero(SelectedHero);
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.C } when context.PlaysHere(session.State.CurrentPlayer.Id):
                hud.OpenSpellBook(SelectedHero);
                break;
            case InputEventKey { Pressed: true, Keycode: Key.K } when context.PlaysHere(session.State.CurrentPlayer.Id):
                hud.KingdomScreen.Open(session.State.CurrentPlayer.Id);
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.Q } when context.PlaysHere(session.State.CurrentPlayer.Id):
                hud.QuestScreen.Open(session.State.CurrentPlayer.Id);
                camera.IgnoreHeldKeys(); // Q also turns the camera; this press only opens the log
                break;
            case InputEventKey { Pressed: true, Keycode: Key.L }:
                context.SetLevel((context.Level + 1) % session.State.Map.Levels);
                camera.FocusOn(MapSpace.ToWorld(new MapPosition(session.State.Map.Width / 2, session.State.Map.Height / 2, context.Level)));
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.P } when context.PlaysHere(session.State.CurrentPlayer.Id):
                hud.PuzzleScreen.Open(session.State.CurrentPlayer.Id);
                break;
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.G } when context.PlaysHere(session.State.CurrentPlayer.Id):
                Dig();
                break;
            case InputEventKey { Pressed: true } key when SelectedHero is { } hero && KeyboardDirections.ToDirection(key.Keycode) is { } direction:
                world.PathPreview.Clear();
                Report(session.Execute(new StepHeroCommand(session.State.CurrentPlayer.Id, hero, direction)));
                break;
            default:
                return false;
        }

        return true;
    }

    /// <summary>
    /// What the pointer is over goes to the status line (ADR-186): the name the information window would give, once per cell; the cursor says
    /// what a click would do there (ADR-204).
    /// </summary>
    private void ShowHover(Vector2 pointer)
    {
        var cell = MapSpace.PickTile(camera, pointer, session.State.Map, context.Level);
        context.Point(cell);
        if (cell != _cursorCell)
        {
            _cursorCell = cell;
            GameCursors.Show(CursorAt(cell));
        }

        if (cell == _hovered)
        {
            return;
        }

        _hovered = cell;
        hud.ShowHover(cell is { } pointed && InfoSheets.MapCell(session.State, session.State.CurrentPlayer.Id, pointed, detailed: false) is { } sheet
            ? sheet.Title
            : null);
    }

    /// <summary>The cursor over <paramref name="cell"/>: busy on a turn not played here, else what the chosen hero would do there.</summary>
    private GameCursor CursorAt(MapPosition? cell)
    {
        var player = session.State.CurrentPlayer.Id;
        if (!context.PlaysHere(player))
        {
            return GameCursor.Busy;
        }

        if (cell is not { } pointed)
        {
            return GameCursor.Pointer;
        }

        var hero = SelectedHero is { } chosen && session.State.HeroesOf(player).Any(h => h.Id == chosen) ? chosen : (HeroId?)null;
        var path = hero is { } id && _aimedSpell is null && session.State.Map.Contains(pointed) ? session.Movement.FindPath(id, pointed) : null;
        return AdventureCursors.For(session.State, player, hero, pointed, path, _aimedSpell is not null).Cursor;
    }

    /// <summary>The cursor is chosen again at the next pointer move (a selection, a spell or a move changed what a click does).</summary>
    private void RefreshCursor() => _cursorCell = null;

    /// <summary>Selects <paramref name="hero"/> (none: null) in the world and the hero list; <paramref name="focus"/> moves the camera to it.</summary>
    public void Select(HeroId? hero, bool focus)
    {
        SelectedHero = hero;
        RefreshCursor();
        world.Select(hero);
        world.PathPreview.Clear();
        hud.ShowSelected(hero);
        if (hero is { } id && focus)
        {
            var position = session.State.GetHero(id).Position;
            context.SetLevel(position.Level);
            camera.FocusOn(MapSpace.ToWorld(position));
        }
    }

    /// <summary>An adventure spell armed on the hero screen: the next map click casts it there.</summary>
    public void Aim(HeroId hero, string spell)
    {
        _aimedSpell = (hero, spell);
        RefreshCursor();
    }

    /// <summary>The selected hero digs where it stands (ADR-140): a whole day's movement, for the Grail.</summary>
    public void Dig()
    {
        if (SelectedHero is not { } hero)
        {
            hud.Status.Text = "Select a hero to dig.";
            return;
        }

        world.PathPreview.Clear();
        Report(session.Execute(new DigCommand(session.State.CurrentPlayer.Id, hero)));
    }

    private void ClickTile(MapPosition tile)
    {
        var state = session.State;
        var player = state.CurrentPlayer.Id;
        RefreshCursor();
        if (_aimedSpell is { } aimed)
        {
            _aimedSpell = null;
            world.PathPreview.Clear();
            Report(session.Execute(new CastAdventureSpellCommand(player, aimed.Hero, aimed.Spell, tile)));
            return;
        }

        if (state.HeroAt(tile) is { } clicked && clicked.Owner == player)
        {
            Select(clicked.Id, focus: false);
            return;
        }

        if (SelectedHero is not { } hero)
        {
            return;
        }

        // Second click on the previewed destination: go.
        if (world.PathPreview.Path is { } planned && planned.Hero == hero && planned.Destination == tile)
        {
            world.PathPreview.Clear();
            Report(session.Execute(new MoveHeroCommand(player, hero, tile)));
            return;
        }

        if (session.Movement.FindPath(hero, tile) is { } path)
        {
            world.PathPreview.Show(path, RouteEndMark(path));
            hud.Status.Text = MoveOutcomeText.Route(path);
        }
        else
        {
            world.PathPreview.Clear();
            hud.Status.Text = "No route there.";
        }
    }

    /// <summary>The mark over a route's end: what the hero does there, as the cursor says (none for a plain move).</summary>
    private Texture2D? RouteEndMark(HeroPath path) =>
        AdventureCursors.RouteEndOf(session.State, session.State.CurrentPlayer.Id, path) switch
        {
            RouteEnd.Attack => UiIcons.Get(UiIcon.RouteAttack, RouteEndMarkSize),
            RouteEnd.Visit => UiIcons.Get(UiIcon.RouteVisit, RouteEndMarkSize),
            RouteEnd.PickUp => UiIcons.Get(UiIcon.RoutePickUp, RouteEndMarkSize),
            RouteEnd.EnterTown => UiIcons.Get(UiIcon.RouteEnterTown, RouteEndMarkSize),
            RouteEnd.MeetHero => UiIcons.Get(UiIcon.RouteMeetHero, RouteEndMarkSize),
            _ => null,
        };

    /// <summary>The route end mark's texture size, in pixels.</summary>
    private const int RouteEndMarkSize = 96;

    private void SelectNextHero()
    {
        var heroes = session.State.HeroesOf(session.State.CurrentPlayer.Id).Select(h => h.Id).ToList();
        if (heroes.Count > 0)
        {
            var next = SelectedHero is { } current ? heroes[(heroes.IndexOf(current) + 1) % heroes.Count] : heroes[0];
            Select(next, focus: true);
        }
    }

    private void Report(CommandResult result)
    {
        RefreshCursor();
        hud.Status.Text = MoveOutcomeText.Of(result);
    }
}
