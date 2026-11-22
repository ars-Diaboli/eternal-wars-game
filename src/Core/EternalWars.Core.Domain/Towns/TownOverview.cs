using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Towns;

/// <summary>Where a weekly growth comes from, in the order the formula adds them (<see cref="TownRules.WeeklyGrowth(GameState, Town, CreatureDefinition, int)"/>).</summary>
public enum GrowthSource
{
    /// <summary>The creature's own weekly growth.</summary>
    Base,

    /// <summary>The Citadel's or the Castle's percent (ADR-066).</summary>
    Fort,

    /// <summary>The owner's heroes' growth artifacts (ADR-111).</summary>
    Artifacts,

    /// <summary>The tier's growth building (ADR-071).</summary>
    GrowthBuilding,

    /// <summary>The Grail building's percent (ADR-130).</summary>
    Grail,
}

/// <summary>
/// One part of a line's weekly growth; the parts of a line add up to its <see cref="LineGrowth.Week"/>. Next week's event (ADR-073) is drawn on
/// Monday, so it is not part of it: a week of the line's creature doubles it, a plague or starvation takes it away.
/// </summary>
public sealed record GrowthPart(GrowthSource Source, int Amount);

/// <summary>A dwelling line of the town: its dwelling level (0 not built), the creatures waiting, and what next Monday adds, part by part.</summary>
public sealed record LineGrowth(CreatureDefinition Line, int DwellingLevel, int Waiting, int Week, IReadOnlyList<GrowthPart> Parts);

/// <summary>How a plot of the town scene looks (TOWN-15, ADR-291).</summary>
public enum PlotState
{
    /// <summary>Unused land: not built, whether it can still be built or not yet.</summary>
    NotBuilt,

    /// <summary>Built today: scaffolding, then the model.</summary>
    BuiltToday,

    /// <summary>The model for its level.</summary>
    Built,

    /// <summary>A dwelling line the 7-of-9 rule has closed for good (ADR-030): unused land as well.</summary>
    Closed,

    /// <summary>A closed dwelling line while the town has the Capitol: homes of the town's citizens (ADR-291).</summary>
    CitizensHomes,
}

/// <summary>
/// One plot of the town: a building (<see cref="Line"/> null; <see cref="Id"/> is the building's id) or a dwelling line (<see cref="Id"/> is
/// <c>dwelling-&lt;line&gt;</c>), its level and its state. <see cref="Buildable"/>: unused land the town can still build on, now or once it has
/// the buildings and the resources (not a closed line, not the Grail building without the Grail); the 3D scene lets the pointer pick such a
/// plot as it does a building (ADR-304).
/// </summary>
public sealed record PlotInfo(string Id, Common.BuildingId Building, Common.CreatureId? Line, string Name, int Level, PlotState State, bool Buildable = false);

/// <summary>
/// The town screen's read model (ADR-296): the daily income, each line's growth with its parts, what was built today, the governor and its
/// discounts, and each plot's state. Computed from <see cref="TownRules"/>, so the screen, the AI and the simulator agree.
/// </summary>
public sealed record TownOverview(
    ResourceSet Income,
    IReadOnlyList<LineGrowth> Lines,
    string? BuiltToday,
    Hero? Governor,
    int BuildingDiscountPercent,
    int CreatureDiscountPercent,
    IReadOnlyList<PlotInfo> Plots)
{
    /// <summary>The plot id of a dwelling line.</summary>
    public static string DwellingPlot(Common.CreatureId line) => $"dwelling-{line.Value}";

    /// <summary>
    /// The name of a plot's marker in a town layout scene (<c>scenes/towns/&lt;town&gt;.tscn</c>, ADR-297): <c>plot_</c> and the plot's id with its
    /// dots as underscores (a node's name has no dots), so <c>plot_town-hall</c> or <c>plot_dwelling-haven_footman</c>. The 3D town scene finds its
    /// plots by these names.
    /// </summary>
    public static string PlotNode(string plotId) => PlotNodePrefix + plotId.Replace('.', '_');

    /// <summary>What every plot marker's name starts with.</summary>
    public const string PlotNodePrefix = "plot_";

    /// <summary>The town as the screen shows it today.</summary>
    public static TownOverview Of(GameState state, Town town)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(town);
        var artifacts = TownRules.ArtifactGrowthPercent(state, town);
        var lines = TownRules.Lines(state, town).Select(line => Growth(state, town, line, artifacts)).ToList();
        var level = StewardshipRules.LevelIn(state, town);
        var builtToday = town.LastBuilt == state.Date ? town.LastBuiltName : null;
        return new TownOverview(
            TownRules.DailyIncome(state, town), lines, builtToday, StewardshipRules.GovernorOf(state, town),
            StewardshipRules.BuildingDiscountPercent(level), StewardshipRules.CreatureDiscountPercent(level), PlotsOf(state, town));
    }

    /// <summary>
    /// A line's growth next Monday, part by part: the parts are the steps of <see cref="TownRules.WeeklyGrowth(GameState, Town, CreatureDefinition, int)"/>
    /// with the same rounding, so they add up to what the dwelling will get before next week's event, which is not known yet. Parts that add nothing
    /// are left out; an unbuilt line grows nothing.
    /// </summary>
    public static LineGrowth Growth(GameState state, Town town, CreatureDefinition line, int artifactPercent)
    {
        var dwelling = town.DwellingLevel(line.Id);
        var parts = new List<GrowthPart>();
        if (dwelling > 0)
        {
            var fortPercent = TownRules.GrowthBonusPercent(town);
            var fort = line.WeeklyGrowth * fortPercent / 100;
            var withArtifacts = line.WeeklyGrowth * (fortPercent + artifactPercent) / 100;
            var building = TownRules.GrowthBuildingBonus(state.Buildings, town, line.Tier);
            var normal = TownRules.WeeklyGrowth(state, town, line, artifactPercent);
            var beforeGrail = line.WeeklyGrowth + withArtifacts + building;
            parts.Add(new GrowthPart(GrowthSource.Base, line.WeeklyGrowth));
            parts.Add(new GrowthPart(GrowthSource.Fort, fort));
            parts.Add(new GrowthPart(GrowthSource.Artifacts, withArtifacts - fort));
            parts.Add(new GrowthPart(GrowthSource.GrowthBuilding, building));
            parts.Add(new GrowthPart(GrowthSource.Grail, normal - beforeGrail));
            parts.RemoveAll(p => p.Amount == 0 && p.Source != GrowthSource.Base);
        }

        return new LineGrowth(line, dwelling, town.AvailableOf(line.Id), parts.Sum(p => p.Amount), parts);
    }

    /// <summary>Every building of the town type and every dwelling line, with its state (TOWN-15, ADR-291).</summary>
    public static IReadOnlyList<PlotInfo> PlotsOf(GameState state, Town town)
    {
        var catalog = TownRules.Buildings(state, town);
        var today = town.LastBuilt == state.Date ? town.LastBuiltPlot : null;
        var capitol = town.Level(BuiltInBuildings.TownHall) >= BuiltInBuildings.CapitolLevel;
        var plots = new List<PlotInfo>();
        var refused = BuildingTree.For(state, town).Where(n => n.Status == BuildingStatus.Refused).Select(n => n.Building).ToHashSet();
        foreach (var building in catalog.TownBuildings)
        {
            var level = Math.Min(town.Level(building), catalog.MaxLevel(building));
            var look = level == 0 ? PlotState.NotBuilt : today == building.Value ? PlotState.BuiltToday : PlotState.Built;
            plots.Add(new PlotInfo(
                building.Value, building, null, catalog.Get(building, Math.Max(level, 1)).Name, level, look, look == PlotState.NotBuilt && !refused.Contains(building)));
        }

        foreach (var line in TownRules.Lines(state, town))
        {
            var id = DwellingPlot(line.Id);
            var level = town.DwellingLevel(line.Id);
            var closed = level == 0 && TownRules.CheckDwellingChoice(state, town, line.Id, 1) is not null;
            var plot = level > 0 ? today == id ? PlotState.BuiltToday : PlotState.Built
                : closed ? capitol ? PlotState.CitizensHomes : PlotState.Closed
                : PlotState.NotBuilt;
            plots.Add(new PlotInfo(id, BuiltInBuildings.Dwelling(line.Tier), line.Id, $"{line.Name} dwelling", level, plot, plot == PlotState.NotBuilt));
        }

        return plots;
    }
}
