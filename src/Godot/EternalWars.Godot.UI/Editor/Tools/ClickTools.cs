using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Objects;
using Godot;

namespace EternalWars.Godot.UI.Editor.Tools;

/// <summary>Places a map object of the chosen definition.</summary>
public sealed class ObjectTool(EditorToolContext context) : EditorTool(context)
{
    private string _definition = BuiltInMapObjects.TreasureChest;

    public override string Name => "Object";

    public override void BuildOptions(ToolOptions options)
    {
        var definitions = Editor.Catalog.All.Where(d => d.Kind != MapObjectKind.Monster).OrderBy(d => d.Name, StringComparer.Ordinal);
        options.Choice("Object", definitions.Select(d => (d.Name, d.Id)), _definition, id => _definition = id);
    }

    public override void Press(MapPosition tile) => Context.Report(Editor.Objects.Place(_definition, tile, out _));
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
