using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Network;

/// <summary>
/// The colours a LAN game's computer plays, for its <see cref="GameDirector"/> (ADR-222). The host plays every colour but the remote ones; the
/// computer players are its, the turn timer is its and runs commands for any colour (ADR-198), and a battle this computer does not show waits for
/// its players (ADR-195). A client plays its own colour only and shows the host's clock.
/// </summary>
public static class LanSeats
{
    /// <summary>The host's seats: the game pauses while it waits for a colour nobody plays.</summary>
    public static LocalSeats Of(GameHost host) => new()
    {
        PlaysElsewhere = host.IsRemote,
        IsPaused = () => host.IsPaused,
        Execute = host.ExecuteAsHost,
        ShareClock = host.ShowClock,
        ComputersWaitForBattles = true,
    };

    /// <summary>A client's seat: the game pauses when the host says so.</summary>
    public static LocalSeats Of(GameClient client) => new()
    {
        PlaysElsewhere = player => client.Seat != player,
        IsPaused = () => client.Paused is not null,
        SharedClock = () => client.Clock,
    };
}
