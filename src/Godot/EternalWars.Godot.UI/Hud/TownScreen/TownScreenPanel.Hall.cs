using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>
/// The Hall (ADR-296): the buildings in columns by kind (Core, Military, Magic, Dwellings, Unique; the <c>kind</c> column of
/// <c>buildings.csv</c>) with their requirement links drawn between the tiles; hovering a tile lights its chain of requirements and dims the
/// rest. The dwellings stand as the 7-of-9 block, a row per tier. A click selects a tile for the detail card: the cost with what is lacking,
/// the requirements as ✓ / ✗ buttons that select them, what the level gives, the refusal, and Build; a dwelling that would close others
/// draws them crossed through, with the warning on the card. "Show in town" closes the Hall and shows a ghost of the selected level on its plot of the
/// 3D scene (ADR-304).
/// </summary>
public partial class TownScreenPanel
{
    private static readonly Vector2 HallTileSize = new(140, 150);

    /// <summary>The selected tile of the Hall: a building's id, or a dwelling's plot id (<see cref="TownOverview.DwellingPlot"/>).</summary>
    private string? _hallSelected;

    private void FillHall(Town town, VBoxContainer body, VBoxContainer footer)
    {
        var state = State;
        var catalog = TownRules.Buildings(state, town);
        var nodes = BuildingTree.For(state, town).ToDictionary(n => n.Building.Value);
        var tiles = new Dictionary<string, BuildingTile>(StringComparer.Ordinal);
        var requires = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var links = new HallLinks();
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", UiSpacing.Gap * 2);
        var choice = _hallSelected is { } chosen && TownRules.Lines(state, town).FirstOrDefault(l => TownOverview.DwellingPlot(l.Id) == chosen) is { } line
            ? BuildingTree.DwellingChoices(state, town).FirstOrDefault(c => c.Line == line)
            : null;
        var closing = choice?.ClosedLines.Select(l => TownOverview.DwellingPlot(l.Id)).ToHashSet(StringComparer.Ordinal) ?? [];

        BuildingTile Tile(BuildingNode node)
        {
            var tile = new BuildingTile(node.Name, node.Next is null ? node.Level : node.Level + 1, node.MaxLevel, node.Status, node.Cost, Treasury, TileTooltip(node.Status, node.Refusal))
            {
                CustomMinimumSize = HallTileSize,
            };
            var id = node.Building;
            tile.WithInfo(detailed => InfoSheets.Building(_session.State, town, id, detailed));
            return Track(tile, id.Value);
        }

        BuildingTile Track(BuildingTile tile, string key)
        {
            tiles[key] = tile;
            tile.Pressed += () =>
            {
                _hallSelected = key;
                FillWindow(town);
            };
            tile.MouseEntered += () => Light(key);
            tile.MouseExited += () => Light(null);
            if (key == _hallSelected)
            {
                tile.ToggleMode = true;
                tile.ButtonPressed = true;
            }

            return tile;
        }

        foreach (var kind in new[] { BuildingKind.Core, BuildingKind.Military, BuildingKind.Magic, BuildingKind.Dwelling, BuildingKind.Unique })
        {
            var column = new VBoxContainer();
            column.AddHeading(KindName(kind));
            var buildings = catalog.TownBuildings.Where(b => catalog.KindOf(b) == kind && nodes.ContainsKey(b.Value)).ToList();
            if (kind == BuildingKind.Dwelling)
            {
                var growth = new HBoxContainer();
                foreach (var building in buildings)
                {
                    growth.AddChild(Tile(nodes[building.Value]));
                }

                column.AddChild(growth);
                column.AddChild(DwellingBlock(town, Track, closing));
            }
            else
            {
                foreach (var building in buildings)
                {
                    column.AddChild(Tile(nodes[building.Value]));
                }
            }

            columns.AddChild(column);
        }

        var stack = new MarginContainer();
        stack.AddChild(links);
        stack.AddChild(columns);
        body.AddChild(stack);

        // The links: each tile's next (or last) level's requirements, from the required tile to the tile.
        foreach (var (key, _) in tiles)
        {
            requires[key] = Requirements(town, key).SelectMany(r => KeysOf(town, r.Building.Value)).Where(tiles.ContainsKey).Distinct().ToList();
        }

        links.Links = [.. requires.SelectMany(r => r.Value.Select(from => (tiles[from], tiles[r.Key])))];

        void Light(string? key)
        {
            var chain = new HashSet<string>(StringComparer.Ordinal);
            void Walk(string k)
            {
                if (chain.Add(k))
                {
                    requires[k].ForEach(Walk);
                }
            }

            if (key is not null)
            {
                Walk(key);
            }

            foreach (var (k, tile) in tiles)
            {
                tile.Modulate = key is null || chain.Contains(k) ? closing.Contains(k) ? ClosingTint : Colors.White : new Color(1, 1, 1, 0.4f);
            }

            links.Lit = key is null ? null : chain;
            links.QueueRedraw();
        }

        foreach (var key in closing.Where(tiles.ContainsKey))
        {
            CrossOut(tiles[key]);
        }

        // The card stays in sight below the tiles, which scroll.
        footer.AddChild(new HSeparator());
        footer.AddChild(DetailCard(town, nodes, choice));
    }

    private static readonly Color ClosingTint = new(1f, 0.55f, 0.5f, 0.85f);

    /// <summary>The 7-of-9 block: a column per tier with its two dwellings one above the other (the tier-5 line alone).</summary>
    private Control DwellingBlock(Town town, Func<BuildingTile, string, BuildingTile> track, HashSet<string> closing)
    {
        var state = State;
        var tiers = TownRules.Lines(state, town).GroupBy(l => l.Tier).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
        var block = new GridContainer { Columns = Math.Max(1, tiers.Count) };
        foreach (var tier in tiers)
        {
            block.AddChild(new Label { Text = $"Tier {tier[0].Tier}", HorizontalAlignment = HorizontalAlignment.Center, ThemeTypeVariation = UiTheme.QuietLabel });
        }

        for (var i = 0; i < 2; i++)
        {
            foreach (var lines in tiers)
            {
                if (i >= lines.Count)
                {
                    block.AddChild(new Control());
                    continue;
                }

                var line = lines[i];
                var status = BuildingTree.DwellingStatus(state, town, line);
                var next = TownRules.NextDwellingLevel(state, town, line);
                var level = town.DwellingLevel(line.Id);
                var key = TownOverview.DwellingPlot(line.Id);
                var tooltip = TileTooltip(status, TownRules.CheckDwelling(state, town, line.Id));
                if (closing.Contains(key))
                {
                    tooltip += "\nBuilding the selected dwelling closes this line for good.";
                }

                var tile = new BuildingTile(line.Name, next is null ? level : level + 1, line.UpgradesTo is null ? 1 : 2, status,
                    next is null ? null : TownRules.BuildCost(state, town, next), Treasury, tooltip) { CustomMinimumSize = HallTileSize };
                var dwelling = line;
                tile.WithInfo(detailed => InfoSheets.Dwelling(_session.State, town, dwelling, detailed));
                block.AddChild(track(tile, key));
            }
        }

        return block;
    }

    /// <summary>A crossed-through tile: a line the selected dwelling would close.</summary>
    private static void CrossOut(BuildingTile tile)
    {
        tile.Modulate = ClosingTint;
        var cross = new Label
        {
            Text = "╳",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        cross.AddThemeFontSizeOverride("font_size", 96);
        cross.AddThemeColorOverride("font_color", new Color(0.9f, 0.2f, 0.2f, 0.8f));
        tile.AddChild(cross);
        cross.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    /// <summary>The detail card of the selected tile: the next level, its cost, requirements, what it gives, the refusal and Build.</summary>
    private Control DetailCard(Town town, IReadOnlyDictionary<string, BuildingNode> nodes, DwellingChoice? choice)
    {
        var state = State;
        var card = new HBoxContainer();
        card.AddThemeConstantOverride("separation", UiSpacing.Gap * 2);
        if (_hallSelected is null)
        {
            card.AddChild(Note("Select a building to see what it costs, needs and gives. Right-click any tile for its information."));
            return card;
        }

        var line = TownRules.Lines(state, town).FirstOrDefault(l => TownOverview.DwellingPlot(l.Id) == _hallSelected);
        BuildingNode? node = line is null ? nodes.GetValueOrDefault(_hallSelected) : null;
        if (line is null && node is null)
        {
            _hallSelected = null;
            return DetailCard(town, nodes, null);
        }

        var next = line is null ? node!.Next : TownRules.NextDwellingLevel(state, town, line);
        var refusal = line is null ? node!.Refusal : TownRules.CheckDwelling(state, town, line.Id);
        var sheet = line is null ? InfoSheets.Building(state, town, node!.Building, false) : InfoSheets.Dwelling(state, town, line, false);
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = line is null
            ? next is null ? $"{node!.Name}: fully built" : $"{next.Name} ({node!.Name.Split(' ')[0]} {next.Level} of {node.MaxLevel})"
            : next is null ? $"{line.Name} dwelling: fully built" : next.Level == 1 ? $"{line.Name} dwelling" : $"{line.Name} dwelling upgrade";
        text.AddChild(new Label { Text = title, ThemeTypeVariation = UiTheme.HeadingLabel });
        if (next is not null)
        {
            var cost = new HBoxContainer();
            cost.AddChild(new Label { Text = "Cost", ThemeTypeVariation = UiTheme.QuietLabel });
            var price = new CostLabel(TownRules.BuildCost(state, town, next), Treasury);
            cost.AddChild(price);
            if (!price.Lacking.IsEmpty)
            {
                cost.AddChild(Note($"(you lack {ResourceIcons.Describe(price.Lacking)})", UiTheme.TextRefusal));
            }

            text.AddChild(cost);
            if (next.Requires.Count > 0)
            {
                var needs = new HFlowContainer();
                needs.AddChild(new Label { Text = "Needs", ThemeTypeVariation = UiTheme.QuietLabel });
                foreach (var requirement in next.Requires)
                {
                    var met = TownRules.Level(state, town, requirement.Building) >= requirement.Level;
                    var target = KeysOf(town, requirement.Building.Value).FirstOrDefault();
                    var name = BuiltInBuildings.DwellingTier(requirement.Building) is { } tier
                        ? $"a tier {tier} dwelling{(requirement.Level > 1 ? " upgraded" : "")}"
                        : TownRules.Buildings(state, town).Get(requirement.Building, requirement.Level).Name;
                    var link = new Button { Text = $"{(met ? "✓" : "✗")} {name}", Flat = true, Disabled = target is null };
                    link.AddThemeColorOverride("font_color", met ? UiTheme.TextComplete : UiTheme.TextRefusal);
                    link.Pressed += () =>
                    {
                        _hallSelected = target;
                        FillWindow(town);
                    };
                    needs.AddChild(link);
                }

                text.AddChild(needs);
            }
        }

        foreach (var info in sheet.Lines.Take(4))
        {
            text.AddChild(Note(info));
        }

        if (choice is { ClosedLines.Count: > 0 })
        {
            text.AddChild(Note($"Building it closes for good: {string.Join(", ", choice.ClosedLines.Select(l => l.Name))} (7 of 9 dwellings).", UiTheme.TextCaution));
        }

        if (refusal is not null && next is not null)
        {
            text.AddChild(Note(refusal, UiTheme.TextRefusal));
        }

        card.AddChild(text);
        var plot = line is null ? node!.Building.Value : TownOverview.DwellingPlot(line.Id);
        if (next is not null && _scene is { IsOpen: true } scene
            && scene.Plots.FirstOrDefault(p => p.Id == plot) is { } shown && (shown.Buildable || shown.State is PlotState.Built or PlotState.BuiltToday))
        {
            // "Show in town" (ADR-304): the Hall closes and a ghost of the next level stands on its plot (not on land that can never take it).
            var level = next.Level;
            var show = new Button { Text = "Show in town", TooltipText = "Close the Hall and see where it would stand; Escape or a click puts it away" };
            show.Pressed += () =>
            {
                CloseWindow();
                if (scene.ShowGhost(plot, level))
                {
                    _status.Text = $"{title}: where it would stand. Escape or a click puts it away.";
                }
            };
            card.AddChild(show);
        }

        if (next is not null)
        {
            var build = new Button { Text = next.Level > 1 && line is not null ? "Upgrade" : "Build", Disabled = refusal is not null, TooltipText = refusal ?? "" };
            var player = Player;
            build.Pressed += () => Run(line is null ? new BuildCommand(player, town.Id, node!.Building) : new BuildDwellingCommand(player, town.Id, line.Id));
            card.AddChild(build);
        }

        return card;
    }

    /// <summary>The requirements a tile's links show: its next level's, or the last level's once fully built.</summary>
    private IReadOnlyList<BuildingRequirement> Requirements(Town town, string key)
    {
        var state = State;
        if (TownRules.Lines(state, town).FirstOrDefault(l => TownOverview.DwellingPlot(l.Id) == key) is { } line)
        {
            var building = BuiltInBuildings.Dwelling(line.Tier);
            var level = TownRules.NextDwellingLevel(state, town, line)?.Level ?? Math.Max(1, town.DwellingLevel(line.Id));
            return TownRules.Buildings(state, town).Get(building, level).Requires;
        }

        var catalog = TownRules.Buildings(state, town);
        var id = new Core.Domain.Common.BuildingId(key);
        return TownRules.NextLevel(state, town, id)?.Requires ?? catalog.Get(id, Math.Clamp(town.Level(id), 1, catalog.MaxLevel(id))).Requires;
    }

    /// <summary>The Hall's tile keys a requirement points to: the building, or every line of a dwelling requirement's tier.</summary>
    private IEnumerable<string> KeysOf(Town town, string building) =>
        BuiltInBuildings.DwellingTier(building) is { } tier
            ? TownRules.Lines(State, town).Where(l => l.Tier == tier).Select(l => TownOverview.DwellingPlot(l.Id))
            : [building];

    private static string TileTooltip(BuildingStatus status, string? refusal) => status switch
    {
        BuildingStatus.Available => "Select it, then Build.",
        BuildingStatus.Complete => "Fully built.",
        BuildingStatus.BuiltToday => "Built something today: tomorrow.",
        BuildingStatus.Closed => $"Closed for good: {refusal}",
        _ => refusal ?? "",
    };

    private static string KindName(BuildingKind kind) => kind switch
    {
        BuildingKind.Core => "Core",
        BuildingKind.Military => "Military",
        BuildingKind.Magic => "Magic",
        BuildingKind.Dwelling => $"Dwellings ({TownRules.MaxDwellings} of 9)",
        _ => "Unique",
    };
}

/// <summary>The Hall's requirement links (ADR-296): a line from each required tile to the tile needing it, drawn behind the tiles; a lit chain in bright bronze.</summary>
internal sealed partial class HallLinks : Control
{
    public HallLinks() => MouseFilter = MouseFilterEnum.Ignore;

    private int _settling;
    private IReadOnlyList<(Control From, Control To)> _links = [];

    /// <summary>The links; set once the tiles are built, which settle over the next frames.</summary>
    public IReadOnlyList<(Control From, Control To)> Links
    {
        get => _links;
        set
        {
            _links = value;
            _settling = 10;
        }
    }

    /// <summary>The tiles of the hovered chain, or null when nothing is hovered.</summary>
    public HashSet<string>? Lit { get; set; }

    public override void _Process(double delta)
    {
        if (_settling > 0)
        {
            _settling--;
            QueueRedraw(); // the tiles settle over the frames after the window is built
        }
    }

    public override void _Draw()
    {
        var origin = GetGlobalRect().Position;
        foreach (var (from, to) in Links)
        {
            if (!IsInstanceValid(from) || !IsInstanceValid(to))
            {
                continue;
            }

            var a = from.GetGlobalRect().GetCenter() - origin;
            var b = to.GetGlobalRect().GetCenter() - origin;
            var lit = Lit is not null && from.Modulate.A > 0.5f && to.Modulate.A > 0.5f;
            DrawLine(a, b, lit ? UiTheme.BorderBright : new Color(UiTheme.BorderDim, 0.6f), lit ? 3 : 2, antialiased: true);
        }
    }
}
