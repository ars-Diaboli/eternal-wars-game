using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Magic.AdventureSpells;

/// <summary>
/// What one adventure spell does (ADR-120): whether it is aimed at a cell, its own conditions, its aim's prompt, and the cast itself. The checks
/// every spell shares (book, mana, movement points) and the payment are <see cref="AdventureMagic"/>'s; the handler for a spell is
/// <see cref="AdventureSpellHandlers.For"/>.
/// </summary>
internal interface IAdventureSpellHandler
{
    /// <summary>The spell id this handler casts.</summary>
    string Spell { get; }

    /// <summary>Whether the cast needs a cell picked on the map at the hero's rank.</summary>
    bool NeedsTarget(GameState state, Hero hero);

    /// <summary>
    /// Why the hero cannot cast the spell at <paramref name="rank"/> now (aimed at <paramref name="target"/>), or null; the aim is not checked when
    /// <paramref name="aimed"/> is false.
    /// </summary>
    string? Refusal(GameState state, Hero hero, int rank, MapPosition? target, bool aimed);

    /// <summary>What the status line asks for while the spell waits for its cell, or null for the default prompt.</summary>
    string? Aim(int rank);

    /// <summary>Does what the spell does, once it is paid for, publishing what happens, in order.</summary>
    void Cast(AdventureCast cast);
}
