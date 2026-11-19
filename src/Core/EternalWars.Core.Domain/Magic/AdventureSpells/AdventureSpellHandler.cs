using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Magic.AdventureSpells;

/// <summary>The defaults of a <see cref="IAdventureSpellHandler"/>: no cell to pick, no conditions of its own, the default prompt.</summary>
internal abstract class AdventureSpellHandler : IAdventureSpellHandler
{
    public abstract Common.SpellId Spell { get; }

    public virtual bool NeedsTarget(GameState state, Hero hero) => false;

    public virtual string? Refusal(GameState state, Hero hero, int rank, MapPosition? target, bool aimed) => null;

    public virtual string? Aim(int rank) => null;

    public abstract void Cast(AdventureCast cast);
}
