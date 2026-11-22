using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.UI.Widgets;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>
/// A town screen: the game's <see cref="TownScreenPanel"/> (ADR-246, ADR-296), or the 2D harness's <see cref="TownPanel"/>, kept as a tool (TOWN-16).
/// </summary>
public interface ITownScreen
{
    /// <summary>Whether the screen is in sight.</summary>
    bool Visible { get; }

    /// <summary>The town the screen shows, if it is open.</summary>
    MapObjectId? CurrentTown { get; }

    /// <summary>Builds the screen for <paramref name="session"/>; once, when the HUD is built.</summary>
    void Bind(GameSession session);

    /// <summary>Shows a town of the player whose turn it is.</summary>
    void Open(MapObjectId town);

    void Close();

    event Action? Closed;

    /// <summary>The player asked to trade from this town's marketplace (ADR-067).</summary>
    event Action? TradeRequested;

    /// <summary>The player opened the Thieves' Guild from this town's tavern (ADR-079).</summary>
    event Action? ThievesGuildRequested;

    /// <summary>The player opened the tavern screen (ADR-114).</summary>
    event Action<MapObjectId>? TavernRequested;
}

/// <summary>What the town screen's sections (<see cref="TownSection"/>) need from the screen that holds them.</summary>
internal interface ITownSectionHost
{
    GameSession Session { get; }

    /// <summary>The town the sections show: the open town while the screen is in sight, or null.</summary>
    Town? Shown { get; }

    /// <summary>The army bars' group: what their drags do, and the stack picked to move or to send.</summary>
    ArmyBarGroup Armies { get; }

    /// <summary>Someone opens a tavern screen.</summary>
    bool HasTavernScreen { get; }

    void OpenTavern(MapObjectId town);

    /// <summary>Runs a command; a refusal shows in the screen's status line.</summary>
    void Run(GameCommand command);

    /// <summary>Runs the commands in order, stopping at the first refused one.</summary>
    void RunAll(IReadOnlyList<GameCommand> commands);
}
