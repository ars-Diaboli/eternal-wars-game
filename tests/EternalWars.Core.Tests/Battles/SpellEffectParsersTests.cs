using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Magic.Parsing;

namespace EternalWars.Core.Tests.Battles;

/// <summary>
/// Every spell effect kind has its one head (<see cref="SpellEffectHeads"/>) and every further word at most one general parser
/// (<see cref="SpellEffectWords"/>): <see cref="SpellEffect.Parse"/> looks both up, so a kind left out fails here, not when the table loads.
/// </summary>
public sealed class SpellEffectParsersTests
{
    public static TheoryData<SpellEffectKind> Kinds() => [.. Enum.GetValues<SpellEffectKind>()];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryKind_HasExactlyOneHead(SpellEffectKind kind)
    {
        Assert.Single(SpellEffectHeads.All, h => h.Kind == kind);
        Assert.Equal(kind, SpellEffectHeads.For(kind).Kind);
    }

    [Fact]
    public void TheHeads_HoldNothingElse() =>
        Assert.Equal(Enum.GetValues<SpellEffectKind>(), SpellEffectHeads.All.Select(h => h.Kind));

    [Fact]
    public void AWord_HasAtMostOneParserPerKind_AndItsGeneralOneLast()
    {
        foreach (var word in SpellEffectWords.All.GroupBy(w => w.Word))
        {
            Assert.True(word.Count(w => w.Only is null) <= 1, $"'{word.Key}' has two general parsers");
            Assert.Equal(word.Select(w => w.Only).Distinct().Count(), word.Count());
            Assert.True(word.SkipWhile(w => w.Only is not null).All(w => w.Only is null), $"'{word.Key}' has a kind's parser after its general one");
        }
    }

    [Theory]
    [InlineData("heal 40 cure", "Heal", 40)]
    [InlineData("dispel own heal 30", "Dispel", 30)]
    public void AKindBoundWord_ReadsForItsKind(string text, string kind, int amount)
    {
        var effect = SpellEffect.Parse(text);
        Assert.Equal(Enum.Parse<SpellEffectKind>(kind), effect.Kind);
        Assert.Equal(amount, effect.Amount);
    }

    [Fact]
    public void AKindBoundWord_ElsewhereMeansItsGeneralParser() =>
        Assert.Equal(15, SpellEffect.Parse("zone area 2 heal 15 rounds 2").Zone!.Heal);

    [Theory]
    [InlineData("damage 30 cure", "'damage 30 cure' ends too early")]
    [InlineData("damage 30 cure 3", "'cure' is not something a spell changes")]
    public void AnUnknownWord_IsReadAsAChange(string text, string message) =>
        Assert.Equal(message, Assert.Throws<FormatException>(() => SpellEffect.Parse(text)).Message);
}
