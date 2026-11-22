using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>
/// The dwellings (7 of 9, every tier): per line its state, waiting creatures and growth (a warning when building it closes others), the
/// Build / Upgrade button, and recruit buttons for the base and the upgraded creature.
/// </summary>
internal sealed class DwellingsSection(ITownSectionHost panel) : TownSection(panel)
{
    public GridContainer Grid { get; } = new() { Columns = 4 };

    protected override Node Anchor => Grid;

    public override void Show(Town town)
    {
        var state = State;
        Grid.ClearChildren();
        var resources = state.CurrentPlayer.Resources;
        var choices = BuildingTree.DwellingChoices(state, town).ToDictionary(c => c.Line.Id);
        foreach (var line in TownRules.Lines(state, town))
        {
            var level = town.DwellingLevel(line.Id);
            var available = town.AvailableOf(line.Id);
            var choice = choices.GetValueOrDefault(line.Id);
            var warning = choice is { ClosedLines.Count: > 0 } ? $"  ⚠ closes {string.Join(", ", choice.ClosedLines.Select(l => $"T{l.Tier} {l.Name}"))}" : "";
            var dwellingLine = line;
            Grid.AddChild(new Label
            {
                Text = $"T{line.Tier} {line.Name}: {(level switch { 0 => "not built", 1 => "built", _ => "upgraded" })}, {available} waiting, +{TownRules.WeeklyGrowth(state, town, line)}/week{warning}",
                CustomMinimumSize = new Vector2(UiTheme.TownLineWidth, 0),
                Modulate = warning.Length > 0 ? UiTheme.TextCaution : Colors.White,
            }.WithInfo(detailed => InfoSheets.Dwelling(Session.State, town, dwellingLine, detailed)));

            var next = TownRules.NextDwellingLevel(state, town, line);
            var refusal = TownRules.CheckDwelling(state, town, line.Id);
            var build = new Button
            {
                Text = next is null ? "—" : next.Level == 1 ? "Build" : "Upgrade",
                Disabled = refusal is not null,
                TooltipText = Tooltip(state, town, next, refusal, choice),
            };
            if (next is not null)
            {
                build.WithCost(TownRules.BuildCost(state, town, next), treasury: resources);
            }

            build.Pressed += () => Run(new BuildDwellingCommand(state.CurrentPlayer.Id, town.Id, line.Id));
            Grid.AddChild(build);

            foreach (var creature in new[] { line, line.UpgradesTo is { } up ? state.Creatures.Get(up) : null })
            {
                var canRecruit = creature is not null && level >= (creature == line ? 1 : 2) && available > 0;
                var count = canRecruit ? TownRules.MaxAffordable(resources, TownRules.CreatureCost(state, town, creature!), available) : 0;
                var recruit = new Button
                {
                    Text = creature is null ? "—" : $"Recruit {count} {creature.Name}",
                    Disabled = count == 0,
                };
                if (creature is not null)
                {
                    recruit.WithCost(TownRules.CreatureCost(state, town, creature), " each", resources);
                }

                if (creature is not null)
                {
                    var id = creature.Id;
                    recruit.Pressed += () => Recruit(town, creature, available);
                    recruit.WithInfo(detailed => InfoSheets.Creature(Session.State, id, detailed));
                }

                Grid.AddChild(recruit);
            }
        }
    }

    /// <summary>What a dwelling has waiting and grows, what is built, and what the player can pay for.</summary>
    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) =>
        refresh.On<DwellingBuilt>(events)
            .On<CreaturesRecruited>(events)
            .On<CreaturesRecruitedMany>(events)
            .On<TownCreaturesGrew>(events)
            .On<ResourcesChanged>(events)
            .On<DwellingOwnerChanged>(events);

    /// <summary>
    /// Asks how many to recruit (<see cref="SliderDialog"/>, ADR-294: the cost of one, the total and the treasury after paying), then recruits them
    /// into the garrison, or into the visiting hero when the garrison has no room for them.
    /// </summary>
    private void Recruit(Town town, Core.Domain.Creatures.CreatureDefinition creature, int available)
    {
        var cost = TownRules.CreatureCost(State, town, creature);
        var treasury = State.CurrentPlayer.Resources;
        var request = new SliderRequest(
            $"Recruit {creature.Name}", 1, available, TownRules.MaxAffordable(treasury, cost, available), "Recruit", cost, treasury, n => $"{n} of {available}");
        SliderDialog.Open(Grid, request, count =>
        {
            var hero = town.Garrison.CanAccept(creature.Id) ? null : TownRules.VisitingHero(State, town)?.Id;
            Run(new RecruitCommand(Player, town.Id, creature.Id, count, hero));
        });
    }

    private static string Tooltip(GameState state, Town town, BuildingLevel? next, string? refusal, DwellingChoice? choice)
    {
        if (next is null)
        {
            return "Fully built.";
        }

        var lines = new List<string> { $"Cost: {ResourceIcons.Describe(TownRules.BuildCost(state, town, next))}" };
        if (choice is { TiersLeftOut.Count: > 0 })
        {
            lines.Add($"Would leave no room for tier {string.Join(", ", choice.TiersLeftOut)} (7 of 9 dwellings, every tier).");
        }

        if (choice is { ClosedLines.Count: > 0 })
        {
            lines.Add($"Building it means you can no longer build: {string.Join(", ", choice.ClosedLines.Select(l => $"T{l.Tier} {l.Name}"))}.");
        }

        lines.Add(refusal ?? "Click to build.");
        return string.Join("\n", lines);
    }
}
