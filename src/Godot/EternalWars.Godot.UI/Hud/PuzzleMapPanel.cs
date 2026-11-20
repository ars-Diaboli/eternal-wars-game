using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The puzzle map (ADR-140, P): the ground around the buried Grail as the player's obelisks show it. Revealed pieces show the terrain (objects darker,
/// off the map black), the others show the player's faction picture in a wooden frame (parchment for a faction without one); once every
/// obelisk is visited a red cross marks the spot. No coordinates are shown: the player finds the place by its shape. Opened from the HUD (or P), closed with Escape, P or "Close"; it follows <see cref="ObeliskVisited"/> while open.
/// </summary>
public partial class PuzzleMapPanel : PanelContainer
{
    /// <summary>The puzzle's side on screen, in pixels.</summary>
    private const int CanvasPixels = (int)(GrailRules.PuzzleSize * PuzzleCanvas.CellPixels);

    /// <summary>The wooden frame's border on screen; the frame's art is scaled so its wood and torn parchment edge (about 12 %) fit it.</summary>
    private const int FrameBorder = 48;

    private const int FramePixels = FrameBorder * 1254 / 150;

    private const string Folder = "res://shared/textures/ui/puzzle/";

    /// <summary>The faction pictures by town type, scaled to the puzzle once (null: none drawn, parchment stays).</summary>
    private static readonly Dictionary<string, Texture2D?> Pictures = [];

    private readonly Label _title = new();
    private readonly PuzzleCanvas _canvas = new();
    private GameSession _session = null!;
    private PlayerId _player;

    public void Bind(GameSession session)
    {
        _session = session;
        this.CenterAsScreen();
        var box = new VBoxContainer();
        AddChild(box);
        var header = new HBoxContainer();
        box.AddChild(header);
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(_title);
        header.AddCloseButton(() => Visible = false);
        _canvas.CustomMinimumSize = new Vector2(CanvasPixels, CanvasPixels);
        var frame = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter }; // the title can be wider; the puzzle stays square
        if (UiIcons.LoadScaled($"{Folder}puzzle_frame.png", FramePixels) is { } wood)
        {
            frame.AddThemeStyleboxOverride("panel", new StyleBoxTexture
            {
                Texture = wood,
                TextureMarginLeft = FrameBorder, TextureMarginTop = FrameBorder, TextureMarginRight = FrameBorder, TextureMarginBottom = FrameBorder,
                ContentMarginLeft = FrameBorder, ContentMarginTop = FrameBorder, ContentMarginRight = FrameBorder, ContentMarginBottom = FrameBorder,
            });
        }

        frame.AddChild(_canvas);
        box.AddChild(frame);
        Visible = false;

        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Visible = false);
        this.SubscribeWhileInTree<ObeliskVisited>(session.Events, _ =>
        {
            if (Visible)
            {
                Fill();
            }
        });
    }

    public void Open(PlayerId player)
    {
        _player = player;
        Fill();
        Visible = true;
    }

    /// <summary>Opens the puzzle for the player, or closes it when it is open (the P key).</summary>
    public void Toggle(PlayerId player)
    {
        if (Visible)
        {
            Visible = false;
        }
        else
        {
            Open(player);
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Visible = false;
            GetViewport().SetInputAsHandled();
        }
    }

    private void Fill()
    {
        var puzzle = GrailRules.Puzzle(_session.State, _player);
        _title.Text = puzzle is null ? "Puzzle map: no Grail lies on this map."
            : _session.State.Grail.IsFound ? $"Puzzle map: the Grail has been dug up. Obelisks visited: {puzzle.Visited} of {puzzle.Obelisks}."
            : puzzle.Obelisks == 0 ? "Puzzle map: this map has no obelisks; the Grail is found only by digging at random."
            : $"Puzzle map ({(puzzle.Origin.Level == 0 ? "surface" : "underground")}): {puzzle.Revealed} of {puzzle.Pieces.Count} pieces, {puzzle.Visited} of {puzzle.Obelisks} obelisks visited."
              + (puzzle.Grail is null ? "" : " The cross marks the spot: dig there with a whole day's movement (G).");
        _canvas.Display(_session.State, puzzle, Picture(_session.State.GetPlayer(_player).StartTown));
    }

    /// <summary>The puzzle picture of <paramref name="townType"/> (<c>puzzle_&lt;town&gt;.png</c>, docs/content/prompts/puzzle-map-prompts.md), or null.</summary>
    private static Texture2D? Picture(string? townType)
    {
        if (townType is null)
        {
            return null;
        }

        if (!Pictures.TryGetValue(townType, out var picture))
        {
            Pictures[townType] = picture = UiIcons.LoadScaled($"{Folder}puzzle_{townType}.png", CanvasPixels);
        }

        return picture;
    }
}
