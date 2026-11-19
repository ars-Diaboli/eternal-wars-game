using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Magic.AdventureSpells;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// Every adventure spell of the catalog has its one handler (<see cref="AdventureSpellHandlers"/>): the checks, the cast and the aim's prompt
/// dispatch through it, so a spell left out fails here, not on the map.
/// </summary>
public sealed class AdventureSpellHandlersTests
{
    public static TheoryData<string> AdventureSpells() => [.. SpellCatalog.Default.All.Where(s => s.IsAdventure).Select(s => s.Id.Value)];

    [Theory]
    [MemberData(nameof(AdventureSpells))]
    public void EveryAdventureSpell_HasExactlyOneHandler(string spell)
    {
        Assert.Single(AdventureSpellHandlers.All, h => h.Spell == spell);
        Assert.Equal(spell, AdventureSpellHandlers.For(spell).Spell);
    }

    [Fact]
    public void TheRegistry_HoldsNothingElse_AndIsWhatIsBuilt()
    {
        Assert.Equal(
            SpellCatalog.Default.All.Where(s => s.IsAdventure).Select(s => s.Id).Order(),
            AdventureSpellHandlers.All.Select(h => h.Spell).Order());
        Assert.Equal(AdventureMagic.Built.Order(), AdventureSpellHandlers.All.Select(h => h.Spell).Order());
    }

    [Fact]
    public void ABattleSpell_HasNoHandler() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AdventureSpellHandlers.For("light.bless"));
}
