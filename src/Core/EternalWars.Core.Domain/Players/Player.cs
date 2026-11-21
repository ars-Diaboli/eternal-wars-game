using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Domain.Players;

public sealed class Player(PlayerId id, string name, bool isHuman, TeamId? team = null)
{
    public PlayerId Id { get; } = id;

    public string Name { get; internal set; } = name;

    public bool IsHuman { get; internal set; } = isHuman;

    /// <summary>How many player colours there are (blue, red, yellow, green, orange, purple, teal, pink).</summary>
    public const int ColourCount = 8;

    /// <summary>The colours' names, by colour; a player's default name is its number's colour's.</summary>
    public static IReadOnlyList<string> ColourNames { get; } = ["Blue", "Red", "Yellow", "Green", "Orange", "Purple", "Teal", "Pink"];

    /// <summary>
    /// The colour the player is drawn in (ADR-225): 0 … <see cref="ColourCount"/> − 1, by default its number's. Chosen on the new-game screen
    /// (<see cref="Game.GameOptions.Colours"/>) and set when the game is created; no rule reads it. A player named after its number's colour takes
    /// the chosen colour's name.
    /// </summary>
    public int Colour { get; internal set; } = id.Value % ColourCount;

    /// <summary>Alliance; defaults to a team of its own (free-for-all).</summary>
    public TeamId Team { get; internal set; } = team ?? new TeamId(id.Value);

    public ResourceSet Resources { get; internal set; }

    /// <summary>The player's own hero limit (campaign-like maps, ADR-068); null = the map's <c>ScenarioRules.MaxHeroes</c>.</summary>
    public int? MaxHeroes { get; internal set; }

    /// <summary>
    /// The factions (town types) the player may take on a fixed map (ADR-257), in <c>BuiltInMapObjects.PlayableTowns</c>' order; null = any. The
    /// new-game screen offers only these, and a random choice draws among them (<see cref="Game.PlayerFactions"/>).
    /// </summary>
    public IReadOnlyList<string>? Factions { get; internal set; }

    private readonly SortedSet<Objects.ObjectColour> _keys = [];

    /// <summary>The keys a Keymaster's Tent gave the player (ADR-071); every hero of the player holds them.</summary>
    public IReadOnlySet<Objects.ObjectColour> Keys => _keys;

    internal bool AddKey(Objects.ObjectColour colour) => _keys.Add(colour);

    /// <summary>Auto combat against computer players and neutrals (ADR-090, CMB-10); a human player's setting, off by default.</summary>
    public Battles.AutoCombatMode AutoCombat { get; internal set; }

    /// <summary>Out of the game (ADR-057): its turns are skipped and it owns nothing any more.</summary>
    public bool IsDefeated { get; internal set; }

    /// <summary>The day the player lost its last town; the grace period (<c>ScenarioRules.GraceDays</c>) runs from it. Null while it owns a town.</summary>
    public Common.GameDate? TownlessSince { get; internal set; }

    /// <summary>
    /// The town type of the first town the player owned when the game started, or null when it started without one (a campaign-like map,
    /// ADR-075). The ranking's faction and its "campaign-like" check (ADR-101) read it.
    /// </summary>
    public string? StartTown { get; internal set; }

    /// <summary>Owned a town at some point; only such players have a grace period.</summary>
    internal bool HadTown { get; set; }

    /// <summary>Had a hero or a town when the game started; only such players can be eliminated or win.</summary>
    internal bool TookPart { get; set; }
}
