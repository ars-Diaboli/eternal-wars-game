namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// The registry of the validator's checks, in the order their issues are listed: heroes, objects and their names, the ground, the Eyes of the Magi and border
/// posts, the players' starts and factions, the rules, bans, placeholders, map events, the Grail, the arena, the tournament and the start zones. A new check is one class
/// registered here (a test checks every check is registered once).
/// </summary>
internal static class MapChecks
{
    /// <summary>Every check, in the order they run.</summary>
    public static IReadOnlyList<IMapCheck> All { get; } =
    [
        new HeroCheck(),
        new ObjectCheck(),
        new NameCheck(),
        new GroundCheck(),
        new ColourCheck(),
        new StartCheck(),
        new FactionsCheck(),
        new GracePeriodCheck(),
        new HeroLimitCheck(),
        new RulesCheck(),
        new BansCheck(),
        new PlaceholderCheck(),
        new EventCheck(),
        new GrailCheck(),
        new ArenaCheck(),
        new TournamentCheck(),
        new StartZoneCheck(),
    ];
}
