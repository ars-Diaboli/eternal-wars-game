using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>
/// The building tree (ADR-114): one column per requirement depth (what needs nothing on the left), a button per building with its next level
/// and cost; the tooltip lists the cost, each requirement (✓ met, ✗ missing), what the level pays or grows, and why it cannot be built.
/// Built-out buildings are green, those waiting on others grey, the rest show whether they wait for tomorrow or for resources.
/// </summary>
internal sealed class BuildingTreeSection(TownPanel panel) : TownSection(panel)
{
    public Label BuiltToday { get; } = new();

    public HBoxContainer Buildings { get; } = new();

    protected override Node Anchor => Buildings;

    public override void Show(Town town)
    {
        var state = State;
        Buildings.ClearChildren();
        BuiltToday.Text = TownRules.HasBuiltToday(state, town)
            ? $"Built today: {town.LastBuiltName ?? "a building"}. The next building can go up tomorrow."
            : "Nothing built today yet.";
        var step = 0;
        foreach (var column in BuildingTree.For(state, town).GroupBy(n => n.Depth))
        {
            step++;
            var box = new VBoxContainer { CustomMinimumSize = new Vector2(UiTheme.BuildingColumnWidth, 0) };
            box.AddChild(new Label { Text = column.Key == 0 ? "Needs nothing" : $"Then ({step})", Modulate = UiTheme.TextQuiet });
            Buildings.AddChild(box);
            foreach (var node in column)
            {
                var building = node.Building;
                var button = new Button
                {
                    Text = node.Next is null ? $"{node.Name} ✓" : $"{node.Name} ({node.Level + 1}/{node.MaxLevel})",
                    Disabled = node.Status != BuildingStatus.Available,
                    TooltipText = Tooltip(node),
                    Modulate = node.Status switch
                    {
                        BuildingStatus.Complete => UiTheme.TextComplete,
                        BuildingStatus.NeedsBuildings => UiTheme.TextBlocked,
                        BuildingStatus.CannotAfford => UiTheme.TextUnaffordable,
                        _ => Colors.White,
                    },
                };
                if (node.Next is not null)
                {
                    button.WithCost(node.Cost!.Value);
                }

                button.Pressed += () => Run(new BuildCommand(state.CurrentPlayer.Id, town.Id, building));
                button.WithInfo(detailed => InfoSheets.Building(Session.State, town, building, detailed));
                box.AddChild(button);
            }
        }
    }

    /// <summary>A dwelling counts as a requirement (<c>dwelling-3</c>), and the resources decide what can be afforded.</summary>
    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) =>
        refresh.On<DwellingBuilt>(events)
            .On<ResourcesChanged>(events);

    private static string Tooltip(BuildingNode node)
    {
        if (node.Next is not { } next)
        {
            return $"{node.Name}: fully built.";
        }

        var lines = new List<string> { $"{next.Name} (level {next.Level} of {node.MaxLevel})", $"Cost: {Cost(node.Cost!.Value)}" };
        lines.AddRange(node.Requires.Select(r => $"{(r.Met ? "✓" : "✗")} needs {r.Name}"));
        if (!next.Income.IsEmpty)
        {
            lines.Add($"Pays {Cost(next.Income)} a day");
        }

        if (next.Growth is { } growth)
        {
            lines.Add($"Every tier {growth.Tier} dwelling grows {growth.Amount} more a week");
        }

        if (BuiltInBuildings.SchoolOf(next.Building) is { } school)
        {
            lines.Add($"Adds one {school} spell per mage guild level");
        }

        lines.Add(node.Status switch
        {
            BuildingStatus.Available => "Click to build.",
            BuildingStatus.BuiltToday => "Built something today: tomorrow.",
            _ => node.Refusal ?? "",
        });
        return string.Join("\n", lines);
    }

    private static string Cost(ResourceSet cost) => ResourceIcons.Describe(cost);
}
