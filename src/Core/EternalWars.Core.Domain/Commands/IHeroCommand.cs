using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Commands;

/// <summary>
/// A command a hero on the map carries out in its owner's turn: the session refuses it when <see cref="Hero"/> is no hero on the map
/// ("Unknown hero …") or another player's ("… does not belong to …") before the rules see it.
/// </summary>
public interface IHeroCommand
{
    /// <summary>The acting hero.</summary>
    HeroId Hero { get; }
}
