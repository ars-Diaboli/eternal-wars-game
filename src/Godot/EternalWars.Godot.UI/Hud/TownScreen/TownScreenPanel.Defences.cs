using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>
/// The other windows (ADR-296): the town's defences (walls, towers, gate and moat at their level, the next level's gain, the garrison's strength,
/// the siege bonus of the unique buildings and the Grail building); the caravan (<see cref="CaravanSection"/>); the governor list from the
/// town's portrait, each hero with the discount it would give; the Undead Transformer (<see cref="TransformerSection"/>).
/// </summary>
public partial class TownScreenPanel
{
    private void FillFort(Town town, VBoxContainer body)
    {
        var state = State;
        var fort = town.Level(BuiltInBuildings.Fort);
        var catalog = TownRules.Buildings(state, town);
        body.AddHeading(fort == 0 ? "No fort: the town is not besieged, its garrison fights in the open" : BuildingName(town, BuiltInBuildings.Fort));
        foreach (var line in Defences(town, fort))
        {
            body.AddChild(new Label { Text = line, AutowrapMode = TextServer.AutowrapMode.WordSmart });
        }

        if (TownRules.NextLevel(state, town, BuiltInBuildings.Fort) is { } next)
        {
            body.AddChild(new HSeparator());
            body.AddHeading($"Next: {next.Name}");
            foreach (var gain in Defences(town, next.Level).Except(Defences(town, fort)))
            {
                body.AddChild(Note($"+ {gain}"));
            }

            if (TownRules.GrowthBonusPercent(next.Level) is var then && then > TownRules.GrowthBonusPercent(town))
            {
                body.AddChild(Note($"+ dwellings grow {then} % more a week (now {TownRules.GrowthBonusPercent(town)} %)"));
            }

            var refusal = TownRules.CheckBuild(state, town, BuiltInBuildings.Fort);
            var build = new Button { Text = $"Build {next.Name}", Disabled = refusal is not null, TooltipText = refusal ?? "" }
                .WithCost(TownRules.BuildCost(state, town, next), treasury: Treasury);
            build.Pressed += () => Run(new BuildCommand(Player, town.Id, BuiltInBuildings.Fort));
            body.AddChild(build);
        }
        else if (catalog.MaxLevel(BuiltInBuildings.Fort) > 0)
        {
            body.AddChild(Note("The fort is fully built.", UiTheme.TextComplete));
        }

        body.AddChild(new HSeparator());
        var strength = town.Garrison.Stacks.Sum(s => (long)s.Stack.Count * state.Creatures.Get(s.Stack.Creature).FightValue);
        body.AddChild(new Label
        {
            Text = $"Garrison strength: {strength}" + (town.GarrisonHero is { } inside ? $", led by {inside.Name} (level {inside.Level})" : ", without a hero"),
        });
        var siege = UniqueBuildings.SiegeOf(town);
        body.AddChild(new Label
        {
            Text = siege.IsNone ? "No building of the town adds to its defender in a siege." : $"A defending hero gets: {DescribeSiege(siege)}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
    }

    /// <summary>The fortifications of a fort level, as the siege builds them (ADR-099, ADR-106).</summary>
    private static IReadOnlyList<string> Defences(Town town, int fort)
    {
        if (fort <= 0)
        {
            return [];
        }

        var lines = new List<string>
        {
            $"Four wall sections of {SiegeRules.WallHealth(fort)} hit points each, and a gate of {SiegeRules.GateHealth(fort)}",
        };
        if (fort >= 2)
        {
            lines.Add($"The keep ({SiegeRules.TowerHealth} hit points) shoots every round, and a moat stops and hurts the attackers");
        }

        if (fort >= 3)
        {
            lines.Add("Two towers shoot as well");
        }

        if (fort >= 2)
        {
            lines.Add($"Each shot deals {SiegeRules.TowerBaseDamage} + {SiegeRules.TowerDamagePerBuilding} per building ({SiegeRules.TowerBaseDamage + (SiegeRules.TowerDamagePerBuilding * SiegeRules.Buildings(town))} now)");
        }

        return lines;
    }

    private static string DescribeSiege(SiegeBonus bonus)
    {
        var parts = new List<string>();
        void Add(int value, string name)
        {
            if (value != 0)
            {
                parts.Add($"{value:+0;−0} {name}");
            }
        }

        Add(bonus.Attack, "Attack");
        Add(bonus.Defense, "Defense");
        Add(bonus.SpellPower, "Power");
        Add(bonus.Knowledge, "Knowledge");
        Add(bonus.Morale, "morale");
        Add(bonus.Luck, "luck");
        Add(bonus.Initiative, "initiative for every stack");
        return string.Join(", ", parts);
    }

    private void FillCaravan(Town town, VBoxContainer body)
    {
        if (town.Level(BuiltInBuildings.Caravan) < 1)
        {
            body.AddChild(NotBuilt(town, BuiltInBuildings.Caravan));
            return;
        }

        body.AddChild(Note("Pick a garrison stack in the bottom bar to send it to another town."));
        _caravan.Show(town);
        Adopt(_caravan.Rows, body);
    }

    /// <summary>The governor list (ADR-063): the governor with Dismiss, then every own hero with Stewardship and the discounts it would give.</summary>
    private void FillGovernor(Town town, VBoxContainer body)
    {
        var state = State;
        var governor = StewardshipRules.GovernorOf(state, town);
        body.AddChild(new Label
        {
            Text = governor is null
                ? "No governor: Stewardship does not apply here."
                : $"{governor.Name} governs: buildings −{StewardshipRules.BuildingDiscountPercent(StewardshipRules.LevelIn(state, town))} %, creatures −{StewardshipRules.CreatureDiscountPercent(StewardshipRules.LevelIn(state, town))} %",
        });
        if (governor is not null)
        {
            var dismiss = new Button { Text = "Dismiss governor", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
            dismiss.Pressed += () => Run(new DismissGovernorCommand(Player, town.Id));
            body.AddChild(dismiss);
        }

        body.AddChild(new HSeparator());
        var candidates = state.AllHeroesOf(Player).Where(h => h.SkillLevelOf(BuiltInSkills.Stewardship) > SkillLevel.None && h != governor).OrderBy(h => h.Name, StringComparer.Ordinal).ToList();
        if (candidates.Count == 0)
        {
            body.AddChild(Note("None of your heroes has Stewardship."));
        }

        foreach (var hero in candidates)
        {
            var level = hero.SkillLevelOf(BuiltInSkills.Stewardship);
            var elsewhere = StewardshipRules.TownGovernedBy(state, hero);
            var row = new HBoxContainer();
            row.AddChild(new Label
            {
                Text = $"{hero.Name}: {level} Stewardship, buildings −{StewardshipRules.BuildingDiscountPercent(level)} %, creatures −{StewardshipRules.CreatureDiscountPercent(level)} %"
                       + (elsewhere is null ? "" : $" (governs {elsewhere.Name})"),
                CustomMinimumSize = new Vector2(640, 0),
            });
            var refusal = StewardshipRules.CheckAppoint(state, town, hero);
            var appoint = new Button { Text = $"Appoint {hero.Name}", Disabled = refusal is not null, TooltipText = refusal ?? "" };
            var id = hero.Id;
            appoint.Pressed += () => Run(new AppointGovernorCommand(Player, town.Id, id));
            row.AddChild(appoint);
            row.WithInfo(detailed => InfoSheets.Hero(_session.State, hero, _session.State.CurrentPlayer.Id, detailed));
            body.AddChild(row);
        }
    }

    private void FillTransformer(Town town, VBoxContainer body)
    {
        if (UndeadTransformer.CheckTown(town) is { } refusal)
        {
            body.AddChild(Note(refusal));
            return;
        }

        _transformer.Show(town);
        Adopt(_transformer.Row, body);
    }
}
