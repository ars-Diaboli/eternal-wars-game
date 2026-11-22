using System.Globalization;
using System.Text.RegularExpressions;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.UI.Widgets;

/// <summary>
/// A creature line as a card (ADR-294): the portrait, the name with a base / upgraded toggle (when the line has an upgrade), the numbers with
/// their icons, the abilities as badges whose tooltips say what they do, and, in a town, the creatures waiting, the growth a week and the cost of
/// one (<see cref="CostLabel"/>, the lack marked). The toggle only changes what the card shows; recruiting is the window's.
/// </summary>
public partial class CreatureCard : PanelContainer
{
    /// <summary>A card's width in the recruitment window.</summary>
    public const int Width = 260;

    private const int PortraitSize = 96;

    private readonly GameState _state;
    private readonly CreatureDefinition _line;
    private readonly Town? _town;
    private readonly ResourceSet? _treasury;
    private readonly VBoxContainer _body = new();

    /// <param name="state">The game, for the creature table and the town's rules.</param>
    /// <param name="line">The line's base creature.</param>
    /// <param name="town">The town the card is in (waiting, growth, cost after the governor), or null for a neutral card.</param>
    /// <param name="treasury">The player's resources, to mark what the cost lacks.</param>
    public CreatureCard(GameState state, CreatureDefinition line, Town? town = null, ResourceSet? treasury = null)
    {
        (_state, _line, _town, _treasury) = (state, line, town, treasury);
        ThemeTypeVariation = UiTheme.TilePanel;
        CustomMinimumSize = new Vector2(Width, 0);
        AddChild(_body);
        ShowCreature(upgraded: false);
    }

    /// <summary>Whether the card shows the upgraded creature.</summary>
    public bool Upgraded { get; private set; }

    /// <summary>The creature the card shows now.</summary>
    public CreatureDefinition Shown => Upgraded && _line.UpgradesTo is { } up ? _state.Creatures.Get(up) : _line;

    private void ShowCreature(bool upgraded)
    {
        Upgraded = upgraded && _line.UpgradesTo is not null;
        _body.ClearChildren();
        var creature = Shown;

        var top = new HBoxContainer();
        var portrait = new TextureRect
        {
            Texture = CreatureIcons.Get(creature.Id.Value, PortraitSize),
            CustomMinimumSize = new Vector2(PortraitSize, PortraitSize),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        };
        top.AddChild(portrait);
        var title = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddChild(new Label { Text = creature.Name, ThemeTypeVariation = UiTheme.HeadingLabel, AutowrapMode = TextServer.AutowrapMode.WordSmart });
        title.AddChild(new Label { Text = $"Tier {N(creature.Tier)}", ThemeTypeVariation = UiTheme.QuietLabel });
        if (_line.UpgradesTo is not null)
        {
            var toggle = new HBoxContainer();
            var basic = new Button { Text = "Base", ToggleMode = true, ButtonPressed = !Upgraded, TooltipText = _line.Name };
            var upgradedButton = new Button { Text = "Upgraded", ToggleMode = true, ButtonPressed = Upgraded, TooltipText = _state.Creatures.Get(_line.UpgradesTo.Value).Name };
            basic.Pressed += () => ShowCreature(false);
            upgradedButton.Pressed += () => ShowCreature(true);
            toggle.AddChild(basic);
            toggle.AddChild(upgradedButton);
            title.AddChild(toggle);
        }

        top.AddChild(title);
        _body.AddChild(top);

        var stats = new GridContainer { Columns = 4 };
        AddStat(stats, HeroStatIcon.Attack, "Attack", N(creature.Attack));
        AddStat(stats, HeroStatIcon.Defense, "Defense", N(creature.Defense));
        AddStat(stats, null, "Damage", $"{N(creature.MinDamage)}–{N(creature.MaxDamage)}");
        AddStat(stats, null, "Health", N(creature.Health));
        AddStat(stats, null, "Speed", N(creature.Speed));
        AddStat(stats, null, "Initiative", N(creature.Initiative));
        if (creature.IsRanged)
        {
            AddStat(stats, null, "Shots", N(creature.Shots));
        }

        _body.AddChild(stats);
        if (creature.Capabilities.Count > 0)
        {
            var abilities = new HFlowContainer();
            foreach (var capability in creature.Capabilities)
            {
                var badge = new PanelContainer { ThemeTypeVariation = UiTheme.TilePanel, TooltipText = InfoSheets.Capability(capability, _state.Spells), MouseFilter = MouseFilterEnum.Pass };
                badge.AddChild(new Label { Text = Words(capability.Kind.ToString()), MouseFilter = MouseFilterEnum.Ignore });
                abilities.AddChild(badge);
            }

            _body.AddChild(abilities);
        }

        if (_town is { } town)
        {
            _body.AddChild(new HSeparator());
            _body.AddChild(new Label
            {
                Text = $"{N(town.AvailableOf(_line.Id))} waiting · +{N(TownRules.WeeklyGrowth(_state, town, _line))} a week",
            });
            var cost = new HBoxContainer();
            cost.AddChild(new Label { Text = "Each", ThemeTypeVariation = UiTheme.QuietLabel });
            cost.AddChild(new CostLabel(TownRules.CreatureCost(_state, town, creature), _treasury));
            _body.AddChild(cost);
        }
        else
        {
            _body.AddChild(new CostLabel(creature.Cost, _treasury, " each"));
        }
    }

    private static void AddStat(GridContainer grid, HeroStatIcon? icon, string name, string value)
    {
        if (icon is { } stat && HeroIcons.Stat(stat, 20) is { } texture)
        {
            grid.AddChild(new TextureRect { Texture = texture, TooltipText = name, StretchMode = TextureRect.StretchModeEnum.KeepCentered, MouseFilter = MouseFilterEnum.Pass });
        }
        else
        {
            grid.AddChild(new Label { Text = name, ThemeTypeVariation = UiTheme.QuietLabel });
        }

        grid.AddChild(new Label { Text = value });
    }

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>"NoEnemyRetaliation" as "No enemy retaliation".</summary>
    private static string Words(string kind)
    {
        var words = Regex.Replace(kind, "(?<=[a-z])(?=[A-Z])", " ").ToLowerInvariant();
        return char.ToUpperInvariant(words[0]) + words[1..];
    }
}
