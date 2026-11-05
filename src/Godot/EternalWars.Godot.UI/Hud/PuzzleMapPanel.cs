using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The puzzle map (ADR-140, P): the ground around the buried Grail as the player's obelisks show it. Revealed pieces show the terrain (objects darker,
/// off the map black), the others stay parchment; once every obelisk is visited a red cross marks the spot. No coordinates are shown: the player
/// finds the place by its shape. Opened from the HUD (or P), closed with Escape, P or "Close"; it follows <see cref="ObeliskVisited"/> while open.
/// </summary>
public partial class PuzzleMapPanel : PanelContainer
{
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
        var close = new Button { Text = "Close" }.WithIcon(UiIcon.Close);
        close.Pressed += () => Visible = false;
        header.AddChild(close);
        _canvas.CustomMinimumSize = new Vector2(GrailRules.PuzzleSize * PuzzleCanvas.CellPixels, GrailRules.PuzzleSize * PuzzleCanvas.CellPixels);
        box.AddChild(_canvas);
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
            : $"Puzzle map: {puzzle.Revealed} of {puzzle.Pieces.Count} pieces, {puzzle.Visited} of {puzzle.Obelisks} obelisks visited."
              + (puzzle.Grail is null ? "" : " The cross marks the spot: dig there with a whole day's movement (G).");
        _canvas.Display(_session.State, puzzle);
    }
}
