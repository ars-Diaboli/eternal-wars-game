using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>
/// Recruitment (ADR-296): a <see cref="CreatureCard"/> per line (unbuilt lines greyed, "build it in the Hall"), the recruit dialog with its
/// destination, and Recruit all with its summary before paying. The dwelling window shows one line's card large with its building, the
/// recruit buttons and Upgrade the dwelling.
/// </summary>
public partial class TownScreenPanel
{
    private void FillRecruitment(Town town, VBoxContainer body)
    {
        var state = State;
        var top = new HBoxContainer();
        var plan = TownPlans.Recruit(state, town);
        var all = new Button
        {
            Text = "Recruit all",
            Disabled = plan.IsEmpty,
            TooltipText = plan.IsEmpty ? "Nothing waits that you can pay for and find room for." : $"The most of every line, top tier first: {ResourceIcons.Describe(plan.Cost)}",
        };
        all.Pressed += () => RecruitAll(town);
        top.AddChild(all);
        top.AddChild(ResourceIcons.Balance(Treasury.Gold));
        body.AddChild(top);

        var flow = new HFlowContainer();
        body.AddChild(flow);
        foreach (var line in TownRules.Lines(state, town))
        {
            var column = new VBoxContainer();
            var card = new CreatureCard(state, line, town, Treasury);
            var level = town.DwellingLevel(line.Id);
            column.AddChild(card);
            if (level == 0)
            {
                card.Modulate = new Color(1, 1, 1, 0.55f);
                column.AddChild(Note("Not built: build it in the Hall."));
            }
            else
            {
                column.AddChild(RecruitButton(town, line, () => card.Upgraded && level >= 2 ? card.Shown : line, $"Recruit {line.Name}"));
            }

            flow.AddChild(column);
        }
    }

    /// <summary>The dwelling window: the line's card, its building, recruiting base or upgraded, and the dwelling's next level.</summary>
    private void FillDwelling(Town town, VBoxContainer body)
    {
        var state = State;
        if (_dwelling is not { } id || TownRules.Lines(state, town).FirstOrDefault(l => l.Id == id) is not { } line)
        {
            body.AddChild(Note("This town has no such dwelling."));
            return;
        }

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UiSpacing.Gap * 2);
        var card = new CreatureCard(state, line, town, Treasury) { CustomMinimumSize = new Vector2(CreatureCard.Width + 80, 0) };
        row.AddChild(card);
        var side = new VBoxContainer();
        var level = town.DwellingLevel(line.Id);
        var status = BuildingTree.DwellingStatus(state, town, line);
        var next = TownRules.NextDwellingLevel(state, town, line);
        var refusal = TownRules.CheckDwelling(state, town, line.Id);
        var tile = new BuildingTile($"{line.Name} dwelling", next is null ? level : level + 1, line.UpgradesTo is null ? 1 : 2, status,
            next is null ? null : TownRules.BuildCost(state, town, next), Treasury, TileTooltip(status, refusal));
        tile.WithInfo(detailed => InfoSheets.Dwelling(_session.State, town, line, detailed));
        side.AddChild(tile);
        if (level > 0)
        {
            side.AddChild(RecruitButton(town, line, () => line, $"Recruit {line.Name}"));
            if (level >= 2 && line.UpgradesTo is { } up)
            {
                var upgraded = state.Creatures.Get(up);
                side.AddChild(RecruitButton(town, line, () => upgraded, $"Recruit {upgraded.Name}"));
            }
        }

        if (next is not null)
        {
            var build = new Button
            {
                Text = level == 0 ? "Build the dwelling" : "Upgrade the dwelling",
                Disabled = refusal is not null,
                TooltipText = refusal ?? "",
            }.WithCost(TownRules.BuildCost(state, town, next), treasury: Treasury);
            build.Pressed += () => Run(new BuildDwellingCommand(Player, town.Id, line.Id));
            side.AddChild(build);
        }

        row.AddChild(side);
        body.AddChild(row);
    }

    /// <summary>A line's Recruit button: greyed while nothing waits; it opens the recruit dialog for the creature <paramref name="creature"/> gives.</summary>
    private Button RecruitButton(Town town, CreatureDefinition line, Func<CreatureDefinition> creature, string text)
    {
        var waiting = town.AvailableOf(line.Id);
        var hero = TownRules.VisitingHero(State, town);
        var room = town.Garrison.CanAccept(creature().Id) || hero?.Army.CanAccept(creature().Id) == true;
        var button = new Button
        {
            Text = text,
            Disabled = waiting == 0 || !room,
            TooltipText = waiting == 0 ? "Nobody waits: more come on Monday." : !room ? "Neither the garrison nor a visiting hero has room for them." : $"{waiting} waiting",
        };
        button.Pressed += () => Recruit(town, line, creature());
        return button;
    }

    /// <summary>
    /// The recruit dialog (<see cref="SliderDialog"/>): how many, and into the garrison or the visiting hero, preselected to where the creature
    /// fits (the garrison first).
    /// </summary>
    private void Recruit(Town town, CreatureDefinition line, CreatureDefinition creature)
    {
        var state = State;
        var available = town.AvailableOf(line.Id);
        if (available == 0)
        {
            return;
        }

        var cost = TownRules.CreatureCost(state, town, creature);
        var hero = TownRules.VisitingHero(state, town);
        var garrisonFits = town.Garrison.CanAccept(creature.Id);
        var heroFits = hero?.Army.CanAccept(creature.Id) == true;
        var request = new SliderRequest(
            $"Recruit {creature.Name}", 1, available, Math.Max(1, TownRules.MaxAffordable(Treasury, cost, available)), "Recruit", cost, Treasury, n => $"{n} of {available}")
        {
            ChoiceCaption = "Into",
            Choices =
            [
                new SliderChoice("Garrison", garrisonFits, garrisonFits ? "" : "The garrison has no room for them."),
                new SliderChoice(hero is null ? "Visiting hero" : hero.Name, heroFits, hero is null ? "No hero is visiting." : heroFits ? "" : $"{hero.Name} has no room for them."),
            ],
            Choice = garrisonFits || !heroFits ? 0 : 1,
        };
        SliderDialog.Open(this, request, (count, into) =>
            Run(new RecruitCommand(Player, town.Id, creature.Id, count, into == 1 ? hero?.Id : null)));
    }

    /// <summary>Recruit all (ADR-296): the plan's lines and total, then <see cref="RecruitManyCommand"/>.</summary>
    private void RecruitAll(Town town)
    {
        var state = State;
        var plan = TownPlans.Recruit(state, town);
        if (plan.IsEmpty)
        {
            _status.Text = "Nothing waits that you can pay for and find room for.";
            return;
        }

        var hero = TownRules.VisitingHero(state, town);
        var body = new VBoxContainer();
        foreach (var order in plan.Orders)
        {
            var into = order.Hero is null ? "the garrison" : hero?.Name ?? "the visiting hero";
            body.AddChild(new Label { Text = $"{order.Count} {state.Creatures.Get(order.Creature).Name} into {into}" });
        }

        var total = new HBoxContainer();
        total.AddChild(new Label { Text = "Total", ThemeTypeVariation = UiTheme.QuietLabel });
        total.AddChild(new CostLabel(plan.Cost, Treasury));
        body.AddChild(total);
        ConfirmDialog.Open(this, "Recruit all", body, "Recruit", () => Run(new RecruitManyCommand(Player, town.Id, plan.Orders)));
    }
}
