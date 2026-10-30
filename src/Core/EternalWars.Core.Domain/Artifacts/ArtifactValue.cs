namespace EternalWars.Core.Domain.Artifacts;

/// <summary>
/// What an artifact is worth, as one number (ADR-061): its effects weighed (Attack and Defense most, battle speed, morale
/// and luck high, income by gold). The AI uses it to choose and equip artifacts, and loot that does not fit in a backpack
/// is chosen by it: the most valuable come first. Ties go to the rarer tier, then to the id.
/// </summary>
public static class ArtifactValue
{
    public static double Of(ArtifactDefinition artifact) => artifact.Effects.Sum(e => e.Value * e.Kind switch
    {
        ArtifactEffectKind.Attack or ArtifactEffectKind.Defense => 10.0,
        ArtifactEffectKind.Morale or ArtifactEffectKind.Luck => 8.0,
        ArtifactEffectKind.BattleSpeed => 15.0,
        ArtifactEffectKind.SpellPower or ArtifactEffectKind.Knowledge => 2.0,
        ArtifactEffectKind.Movement => 1.5,
        ArtifactEffectKind.MovementFlat => 0.02,
        ArtifactEffectKind.MeleeDamage or ArtifactEffectKind.RangedDamage => 1.0,
        ArtifactEffectKind.DamageTaken => 3.0,
        ArtifactEffectKind.RangedDamageTaken => 1.5,
        ArtifactEffectKind.Income => e.Target == "gold" ? 0.1 : 5.0,
        ArtifactEffectKind.NativeTerrain => 3.0,
        ArtifactEffectKind.TerrainPenalty => 0.2,
        ArtifactEffectKind.Skill => 6.0,
        ArtifactEffectKind.AllSkills => 15.0,
        ArtifactEffectKind.Sight => 3.0,
        ArtifactEffectKind.ManaADay => 3.0,
        ArtifactEffectKind.ManaMax => 0.3,
        ArtifactEffectKind.CreatureHealth => 2.0,
        ArtifactEffectKind.Necromancy => 1.5,
        ArtifactEffectKind.Growth => 2.0,
        ArtifactEffectKind.Immune => 12.0,
        ArtifactEffectKind.ElementPower => 0.4,
        ArtifactEffectKind.Spells => 20.0,
        _ => 0.0,
    });

    /// <summary>Artifact ids best first: by value, then rarer tier, then id. Unknown ids come last.</summary>
    public static IEnumerable<string> BestFirst(ArtifactCatalog catalog, IEnumerable<string> ids) =>
        ids.OrderByDescending(id => catalog.TryGet(id, out var a) ? Of(a) : double.MinValue)
            .ThenByDescending(id => catalog.TryGet(id, out var a) ? (int)a.Tier : -1)
            .ThenBy(id => id, StringComparer.Ordinal);
}
