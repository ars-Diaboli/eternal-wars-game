namespace EternalWars.Core.Domain.Commands;

/// <summary>
/// A command given in a battle. Each side is commanded by its own player, whoever's turn it is on the map, so the session only checks that a battle is
/// being fought; the battle checks that the player commands the side to act.
/// </summary>
public interface IBattleCommand;
