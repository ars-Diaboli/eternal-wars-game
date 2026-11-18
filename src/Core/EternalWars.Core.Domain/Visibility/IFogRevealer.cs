using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Visibility;

/// <summary>
/// Reveals terrain for good on a rule's request (ADR-214): an observatory, a cartographer's map, View Terrain, a map event. The rule calls it
/// first and then publishes its own past-tense event (<see cref="Events.SurroundingsRevealed"/>, <see cref="Events.AreaRevealed"/>), so the event
/// reports a change already made instead of asking for one. <see cref="VisibilityService"/> implements it.
/// </summary>
public interface IFogRevealer
{
    /// <summary>
    /// Explores the cells within <paramref name="radius"/> of <paramref name="center"/> for <paramref name="player"/> and refreshes what it
    /// remembers of the objects there; publishes <see cref="Events.TilesRevealed"/> and <see cref="Events.KnownObjectsChanged"/> for what changed.
    /// </summary>
    void RevealArea(PlayerId player, MapPosition center, int radius);
}
