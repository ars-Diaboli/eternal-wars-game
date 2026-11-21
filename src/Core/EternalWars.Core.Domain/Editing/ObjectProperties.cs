using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// The fields the inspector edits over several objects of one kind at once (EDIT-1, ADR-255). Each is written through the editor
/// that owns it, keeping the object's own values of the fields that editor sets together with it. Fields that only make sense for
/// one object (a town's name and buildings, a box's contents, a garrison's slots) are not here: the inspector edits those for a
/// single object only.
/// </summary>
public static class ObjectProperties
{
    public static ObjectProperty<MonsterGuard, CreatureId> MonsterCreature { get; } =
        new("creature", m => m.Creature, (e, m, v) => e.Monsters.Set(m.Id, v, m.Count, m.Disposition, m.NeverFlees));

    /// <summary>A monster's number; null is rolled at game start.</summary>
    public static ObjectProperty<MonsterGuard, int?> MonsterCount { get; } =
        new("count", m => m.Count, (e, m, v) => e.Monsters.Set(m.Id, m.Creature, v, m.Disposition, m.NeverFlees));

    public static ObjectProperty<MonsterGuard, GuardDisposition> MonsterDisposition { get; } =
        new("disposition", m => m.Disposition, (e, m, v) => e.Monsters.Set(m.Id, m.Creature, m.Count, v, m.NeverFlees));

    public static ObjectProperty<MonsterGuard, bool> MonsterNeverFlees { get; } =
        new("never flees", m => m.NeverFlees, (e, m, v) => e.Monsters.Set(m.Id, m.Creature, m.Count, m.Disposition, v));

    public static ObjectProperty<MonsterGuard, GuardMode> MonsterGuardMode { get; } =
        new("guard mode", m => m.Mode, (e, m, v) => e.Monsters.SetGuardMode(m.Id, v));

    /// <summary>A random monster's tier (ADR-126): null is a fixed creature, 0 any tier.</summary>
    public static ObjectProperty<MonsterGuard, int?> MonsterRandomTier { get; } =
        new("random tier", m => m.RandomTier, (e, m, v) => e.Monsters.SetRandomTier(m.Id, v));

    public static ObjectProperty<Town, PlayerId?> TownOwner { get; } =
        new("owner", t => t.Owner, (e, t, v) => e.Towns.SetOwner(t.Id, v));

    public static ObjectProperty<Town, string> TownFaction { get; } =
        new("faction", t => t.TownType, (e, t, v) => e.Towns.SetType(t.Id, v));

    public static ObjectProperty<Town, bool> TownRandom { get; } =
        new("random faction", t => t.IsRandom, (e, t, v) => e.Towns.SetRandom(t.Id, v));

    public static ObjectProperty<Mine, PlayerId?> MineOwner { get; } =
        new("owner", m => m.Owner, (e, m, v) => e.Objects.SetMineOwner(m.Id, v));

    /// <summary>A mine's guard creature (null is unguarded); each guarded mine keeps its own count, a mine newly guarded rolls it.</summary>
    public static ObjectProperty<Mine, CreatureId?> MineGuardCreature { get; } =
        new("guard", m => m.Guard?.Creature, (e, m, v) => e.Objects.SetMineGuard(m.Id, v is { } creature ? new GuardStack(creature, m.Guard?.Count) : null));

    /// <summary>A guarded mine's guard count (null is rolled); an unguarded mine has none and is left as it is.</summary>
    public static ObjectProperty<Mine, int?> MineGuardCount { get; } =
        new("guard count", m => m.Guard?.Count, (e, m, v) => m.Guard is { } guard ? e.Objects.SetMineGuard(m.Id, guard with { Count = v }) : EditResult.Ok);

    public static ObjectProperty<ResourceProvider, PlayerId?> ProviderOwner { get; } =
        new("owner", p => p.Owner, (e, p, v) => e.Objects.SetProviderOwner(p.Id, v));

    public static ObjectProperty<MapGarrison, PlayerId?> GarrisonOwner { get; } =
        new("owner", g => g.Owner, (e, g, v) => e.Objects.SetGarrisonOwner(g.Id, v));

    public static ObjectProperty<CreatureDwelling, CreatureId> DwellingCreature { get; } =
        new("creature", d => d.Creature, (e, d, v) => e.Objects.SetDwelling(d.Id, v, d.Owner, d.Available));

    public static ObjectProperty<CreatureDwelling, PlayerId?> DwellingOwner { get; } =
        new("owner", d => d.Owner, (e, d, v) => e.Objects.SetDwelling(d.Id, d.Creature, v, d.Available));

    /// <summary>The creatures waiting in a dwelling; null is one week's growth.</summary>
    public static ObjectProperty<CreatureDwelling, int?> DwellingAvailable { get; } =
        new("creatures waiting", d => d.Available, (e, d, v) => e.Objects.SetDwelling(d.Id, d.Creature, d.Owner, v));

    /// <summary>A prison's hero; null is drawn at start.</summary>
    public static ObjectProperty<Prison, string?> PrisonHero { get; } =
        new("prisoner", p => p.Hero, (e, p, v) => e.Contents.SetPrison(p.Id, v, p.Level));

    /// <summary>A prisoner's level; null is rolled.</summary>
    public static ObjectProperty<Prison, int?> PrisonLevel { get; } =
        new("level", p => p.Level, (e, p, v) => e.Contents.SetPrison(p.Id, p.Hero, v));

    /// <summary>An artifact pickup's artifact; null is one of its tier drawn at start.</summary>
    public static ObjectProperty<ArtifactPickup, ArtifactId?> PickupArtifact { get; } =
        new("artifact", p => p.Artifact, (e, p, v) => e.Contents.SetArtifactPickup(p.Id, v));

    /// <summary>A shrine's spell; null is drawn at start.</summary>
    public static ObjectProperty<Shrine, SpellId?> ShrineSpell { get; } =
        new("spell", s => s.Spell, (e, s, v) => e.Contents.SetShrineSpell(s.Id, v));

    /// <summary>A chest's fixed values; null is rolled.</summary>
    public static ObjectProperty<TreasureChest, ChestValues?> ChestValues { get; } =
        new("values", c => c.FixedValues, (e, c, v) => e.Contents.SetChestValues(c.Id, v));

    public static ObjectProperty<SignPost, string> SignMessage { get; } =
        new("message", s => s.Message, (e, s, v) => e.Contents.SetSignMessage(s.Id, v));

    /// <summary>A sphinx's riddle; null is drawn from the riddle table.</summary>
    public static ObjectProperty<Sphinx, Riddle?> SphinxRiddle { get; } =
        new("riddle", s => s.Riddle, (e, s, v) => e.Contents.SetSphinxRiddle(s.Id, v));

    /// <summary>A witch hut's two skills; none are rolled at start.</summary>
    public static ObjectProperty<WitchHut, IReadOnlyList<string>> WitchHutSkills { get; } =
        new("skills", w => w.Skills, (e, w, v) => e.Contents.SetWitchHutSkills(w.Id, v), SameItems.Instance);

    public static ObjectProperty<QuestSite, string?> QuestSiteQuest { get; } =
        new("quest", q => q.Quest, (e, q, v) => e.Contents.SetQuestSiteQuest(q.Id, v));

    /// <summary>The hero event a Pandora's box runs.</summary>
    public static ObjectProperty<PandorasBox, string?> BoxEvent { get; } =
        new("event", b => b.Event, (e, b, v) => e.Contents.SetPandorasBoxEvent(b.Id, v));

    private sealed class SameItems : IEqualityComparer<IReadOnlyList<string>>
    {
        public static SameItems Instance { get; } = new();

        public bool Equals(IReadOnlyList<string>? x, IReadOnlyList<string>? y) => x is null ? y is null : y is not null && x.SequenceEqual(y);

        public int GetHashCode(IReadOnlyList<string> obj) => obj.Count;
    }
}
