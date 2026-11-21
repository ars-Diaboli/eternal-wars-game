using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Objects;
using Godot;

namespace EternalWars.Godot.UI.Editor.Tools;

/// <summary>
/// Places what is chosen in the object browser (EDIT-1, ADR-253), its top-left tile where clicked. Before the click a ghost shows the
/// cells it would take, red where it does not fit. A hero is placed for the player the tool's options name and opens in the inspector.
/// </summary>
public sealed class ObjectTool(EditorToolContext context) : EditorTool(context)
{
    private PlayerId _owner = context.Editor.Scenario.Players[0].Id;
    private MapPosition? _hovered;

    public override string Name => "Object";

    public override Key? Shortcut => Key.O;

    private BrowserEntry? Entry => Context.Choice.Entry;

    /// <summary>The player a hero is placed for: the picked one, or the first when that player has left the map.</summary>
    private PlayerId Owner => Editor.Scenario.HasPlayer(_owner) ? _owner : Editor.Scenario.Players[0].Id;

    public override void BuildOptions(ToolOptions options)
    {
        if (Entry is not { } entry)
        {
            options.Hint("Pick what to place in the browser below.");
            return;
        }

        if (entry.Placement == BrowserPlacement.Hero)
        {
            options.Choice("Owner", Editor.Scenario.Players.Select(p => (p.Name, p.Id)), Owner, p =>
            {
                _owner = p;
                ShowGhost(_hovered);
            });
        }

        options.Hint($"Places {entry.Name} where clicked: the ghost shows its tiles, red where it does not fit.");
    }

    public override void Hover(MapPosition? tile)
    {
        if (tile != _hovered)
        {
            ShowGhost(tile);
        }
    }

    public override void Press(MapPosition tile)
    {
        if (Entry is not { } entry)
        {
            Context.Say("Pick what to place in the browser first.");
            return;
        }

        if (Context.Report(Editor.Placing.Place(entry, tile, out var placed, Owner)))
        {
            Context.Choice.NotePlaced(entry);
            if (placed.Hero is { } hero)
            {
                Context.Inspect(hero);
            }
        }

        ShowGhost(tile);
    }

    public override void Drag(MapPosition tile) => Hover(tile);

    public override void Refresh() => ShowGhost(_hovered);

    public override void Cancel() => ShowGhost(null);

    private void ShowGhost(MapPosition? tile)
    {
        _hovered = tile;
        Context.Marks?.ShowMove(tile is { } at && Entry is { } entry ? Editor.Placing.Check(entry, at, Owner) : null);
    }
}

/// <summary>Places a monster guard.</summary>
public sealed class MonsterTool(EditorToolContext context) : EditorTool(context)
{
    private string _creature = MonsterGuard.DefaultCreature;

    /// <summary>Null: rolled from the tier at game start.</summary>
    private int? _count;
    private GuardDisposition _disposition = GuardDisposition.Aggressive;

    public override string Name => "Monster";

    public override void BuildOptions(ToolOptions options)
    {
        var creatures = Editor.Scenario.Creatures.All.OrderBy(c => c.Town ?? "~").ThenBy(c => c.Tier).ThenBy(c => c.Name, StringComparer.Ordinal)
            .Select(c => ($"{c.Name} (T{c.Tier})", c.Id.Value));
        options.Choice("Creature", creatures, _creature, c => _creature = c);
        var count = new SpinBox { MinValue = 0, MaxValue = 999, Value = _count ?? 0, TooltipText = "0 = rolled from the tier at game start" };
        count.ValueChanged += v => _count = v > 0 ? (int)v : null;
        options.Labelled("Count", count);
        options.Choice("Disposition", Enum.GetValues<GuardDisposition>().Select(d => (d.ToString(), d)), _disposition, d => _disposition = d);
    }

    public override void Press(MapPosition tile) =>
        Context.Report(Editor.Monsters.Place(tile, _creature, _count, _disposition, neverFlees: false, out _));
}

/// <summary>Places a hero of the chosen player and opens it in the inspector.</summary>
public sealed class HeroTool(EditorToolContext context) : EditorTool(context)
{
    private PlayerId _owner = context.Editor.Scenario.Players[0].Id;

    public override string Name => "Hero";

    public override void BuildOptions(ToolOptions options) =>
        options.Choice("Owner", Editor.Scenario.Players.Select(p => (p.Name, p.Id)), _owner, p => _owner = p);

    public override void Press(MapPosition tile)
    {
        if (Context.Report(Editor.Heroes.Place(_owner, tile, $"Hero {Editor.Scenario.NextHeroId().Value}", out var placed)))
        {
            Context.Inspect(placed);
        }
    }
}

/// <summary>Links gates: the first click picks a two-way gate or a one-way entrance, the second its partner or exit.</summary>
public sealed class LinkGatesTool(EditorToolContext context) : EditorTool(context)
{
    private MapObjectId? _from;

    public override string Name => "LinkGates";

    public override void BuildOptions(ToolOptions options) => options.Hint("Click a gate (or one-way entrance), then its partner (or exit).");

    public override void Cancel() => _from = null;

    public override void Press(MapPosition tile)
    {
        var clicked = Editor.Scenario.ObjectCellAt(tile)?.Object;
        if (_from is not { } from)
        {
            if (clicked is TwoWayGate or OneWayGateEntrance)
            {
                _from = clicked.Id;
                Context.Say($"Linking {clicked}: now click its partner (or exit).");
            }
            else
            {
                Context.Say("Click a two-way gate or a one-way entrance first.");
            }

            return;
        }

        _from = null;
        var source = Editor.Scenario.GetObject(from);
        Context.Report(clicked is null ? EditResult.InvalidTarget
            : source is OneWayGateEntrance ? Editor.Objects.SetGateExit(from, clicked.Id)
            : Editor.Objects.LinkGates(from, clicked.Id));
    }
}

/// <summary>Buries the Grail where clicked, or takes it away when its cell is clicked again (ADR-140).</summary>
public sealed class GrailTool(EditorToolContext context) : EditorTool(context)
{
    public override string Name => "Grail";

    public override void BuildOptions(ToolOptions options) => options.Hint(
        "Click open ground at least 10 cells from the edges to bury the Grail; click it again to take it away "
        + "(left unset, it is drawn at game start on maps with obelisks; at most 25 obelisks a map).");

    public override void Press(MapPosition tile) => Context.Report(Editor.Map.SetGrail(Editor.Scenario.Grail.Position == tile ? null : tile));
}
