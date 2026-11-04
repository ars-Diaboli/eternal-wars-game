using System.Globalization;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Visibility;

namespace EternalWars.Core.Domain.Magic;

/// <summary>
/// A creature stack as a player is shown it: <paramref name="Count"/> is null when Veil hides it (then only <paramref name="Amount"/>, a word
/// such as "a few" or "a horde", is known).
/// </summary>
public sealed record ShownStack(string Creature, int? Count, string Amount);

/// <summary>
/// A hero or a neutral stack as an information spell shows it: a hero (<paramref name="Hero"/>, its <paramref name="Owner"/>, its worn
/// artifacts unless Veil II hides them) or a map monster (<paramref name="Monster"/>). <paramref name="Veiled"/>: the counts are hidden.
/// </summary>
public sealed record ShownArmy(
    MapPosition Position, string Name, HeroId? Hero, PlayerId? Owner, MapObjectId? Monster, IReadOnlyList<ShownStack> Stacks,
    IReadOnlyList<string> Artifacts, bool Veiled, bool ArtifactsHidden);

/// <summary>What Detect Magic found in a map object: its name, where it is, and one line per thing it holds.</summary>
public sealed record DetectedObject(MapObjectId Object, string Name, MapPosition Position, IReadOnlyList<string> Contents);

/// <summary>What Ledger of Ages tells: next week's event and the enemy heroes it reads (in sight; at rank II also those inside towns).</summary>
public sealed record LedgerReport(Turns.WeekEvent NextWeek, IReadOnlyList<ShownArmy> Heroes);

/// <summary>
/// An enemy town as Eavesdrop shows it, read-only: its buildings (name and level), the creatures waiting in its dwellings, the garrison, the
/// hero inside and the hero at the gate.
/// </summary>
public sealed record TownIntel(
    MapObjectId Town, string Name, string TownType, PlayerId? Owner, IReadOnlyList<string> Buildings, IReadOnlyList<ShownStack> Available,
    IReadOnlyList<ShownStack> Garrison, ShownArmy? GarrisonHero, ShownArmy? VisitingHero);

/// <summary>
/// The queries behind the information spells (Distant Vision, Detect Magic, Grave Whisper III, Ledger of Ages, Eavesdrop) and how a player is
/// shown another player's army (Veil). Pure functions of the state.
/// </summary>
public static class AdventureIntel
{
    /// <summary>
    /// The word for a stack's size when its count is hidden *(proposal: HoMM3's scale)*: a few 1–4, several 5–9, a pack 10–19, lots 20–49,
    /// a horde 50–99, a throng 100–249, a swarm 250–499, zounds 500–999, a legion 1000+.
    /// </summary>
    public static string AmountWord(int count) => count switch
    {
        < 5 => "a few",
        < 10 => "several",
        < 20 => "a pack",
        < 50 => "lots",
        < 100 => "a horde",
        < 250 => "a throng",
        < 500 => "a swarm",
        < 1000 => "zounds",
        _ => "a legion",
    };

    /// <summary>The stacks of an army as shown: exact, or with the counts hidden.</summary>
    public static IReadOnlyList<ShownStack> Stacks(IEnumerable<CreatureStack> stacks, bool hidden) =>
        [.. stacks.Select(s => new ShownStack(s.Creature, hidden ? null : s.Count, AmountWord(s.Count)))];

    /// <summary>
    /// How <paramref name="viewer"/> is shown <paramref name="hero"/>'s army (the one query every screen showing another player's hero uses):
    /// the counts are hidden from other players while it is under Veil, and its artifacts too from Veil II on.
    /// </summary>
    public static ShownArmy ShowHero(GameState state, Hero hero, PlayerId viewer)
    {
        var veil = hero.Owner == viewer ? 0 : AdventureMagic.VeilRank(hero);
        var artifactsHidden = veil >= 2;
        return new ShownArmy(
            hero.Position, hero.Name, hero.Id, hero.Owner, null, Stacks(hero.Army.Stacks.Select(s => s.Stack), veil >= 1),
            artifactsHidden ? [] : [.. hero.Artifacts.AllWorn.Select(w => w.Id)], veil >= 1, artifactsHidden);
    }

    /// <summary>A neutral map stack as shown: always exact.</summary>
    public static ShownArmy ShowMonster(GameState state, MonsterGuard monster) =>
        new(monster.Position, state.Creatures.TryGet(monster.Creature, out var creature) ? creature.Name : monster.Creature, null, null, monster.Id,
            monster.Count is { } count ? [new ShownStack(monster.Creature, count, AmountWord(count))] : [], [], false, false);

    /// <summary>
    /// Distant Vision: the other players' heroes and the neutral stacks within <paramref name="radius"/> of <paramref name="center"/>, on tiles
    /// the viewer has explored, or anywhere with <paramref name="underFog"/> (rank III); heroes first, by id, then the stacks by id.
    /// </summary>
    public static IReadOnlyList<ShownArmy> ArmiesAround(GameState state, PlayerId viewer, MapPosition center, int radius, bool underFog)
    {
        bool Seen(MapPosition position) => FogOfWar.IsWithinRadius(center, position, radius) && (underFog || state.Fog.IsExplored(viewer, position));
        return
        [
            .. state.Heroes.Where(h => h.Owner != viewer && Seen(h.Position)).OrderBy(h => h.Id.Value).Select(h => ShowHero(state, h, viewer)),
            .. MonstersAround(state, viewer, center, radius, underFog),
        ];
    }

    /// <summary>The neutral map stacks within the radius (Grave Whisper III shows their numbers), by id.</summary>
    public static IReadOnlyList<ShownArmy> MonstersAround(GameState state, PlayerId viewer, MapPosition center, int radius, bool underFog = true) =>
    [
        .. state.ObjectsOf<MonsterGuard>()
            .Where(m => FogOfWar.IsWithinRadius(center, m.Position, radius) && (underFog || state.Fog.IsExplored(viewer, m.Position)))
            .OrderBy(m => m.Id.Value).Select(m => ShowMonster(state, m)),
    ];

    /// <summary>
    /// Ledger of Ages: next week's event and the enemy heroes the viewer sees on the map; with <paramref name="inTowns"/> (rank II) also the
    /// enemy heroes inside towns whose entrance it has explored.
    /// </summary>
    public static LedgerReport Ledger(GameState state, PlayerId viewer, bool inTowns)
    {
        var heroes = state.Heroes.Where(h => state.AreEnemies(viewer, h.Owner) && state.CanSee(viewer, h)).OrderBy(h => h.Id.Value).ToList();
        if (inTowns)
        {
            heroes.AddRange(state.ObjectsOf<Town>()
                .Where(t => t.GarrisonHero is { } inside && state.AreEnemies(viewer, inside.Owner) && state.Fog.IsExplored(viewer, t.VisitPosition))
                .OrderBy(t => t.Id.Value).Select(t => t.GarrisonHero!));
        }

        return new LedgerReport(state.NextWeekEvent, [.. heroes.Select(h => ShowHero(state, h, viewer))]);
    }

    /// <summary>Eavesdrop: what the town's screen would show, read-only.</summary>
    public static TownIntel Town(GameState state, Town town, PlayerId viewer)
    {
        var catalog = state.Buildings.For(town.TownType);
        var buildings = town.Buildings.Where(b => b.Value > 0).OrderBy(b => b.Key, StringComparer.Ordinal)
            .Select(b => catalog.MaxLevel(b.Key) >= b.Value ? $"{catalog.Get(b.Key, b.Value).Name} ({b.Value})" : $"{b.Key} ({b.Value})")
            .ToList();
        var available = town.Available.Where(a => a.Value > 0).OrderBy(a => a.Key, StringComparer.Ordinal)
            .Select(a => new ShownStack(a.Key, a.Value, AmountWord(a.Value))).ToList();
        var veiledInside = town.GarrisonHero is { } inside && inside.Owner != viewer && AdventureMagic.VeilRank(inside) >= 1;
        return new TownIntel(
            town.Id, town.Name, town.TownType, town.Owner, buildings, available, Stacks(town.Garrison.Stacks.Select(s => s.Stack), veiledInside),
            town.GarrisonHero is { } garrisonHero ? ShowHero(state, garrisonHero, viewer) : null,
            state.HeroAt(town.VisitPosition) is { } visitor && visitor.Owner == town.Owner ? ShowHero(state, visitor, viewer) : null);
    }

    /// <summary>
    /// Detect Magic: every map object within <paramref name="radius"/> of <paramref name="center"/> (by its entrance) that holds something, by
    /// id, with its contents; <paramref name="bankRewards"/> (rank III) adds what creature banks pay.
    /// </summary>
    public static IReadOnlyList<DetectedObject> ObjectsAround(GameState state, MapPosition center, int radius, bool bankRewards) =>
    [
        .. state.Objects.Where(o => FogOfWar.IsWithinRadius(center, o.VisitPosition, radius)).OrderBy(o => o.Id.Value)
            .Select(o => (Object: o, Contents: Contents(state, o, bankRewards)))
            .Where(o => o.Contents.Count > 0)
            .Select(o => new DetectedObject(o.Object.Id, o.Object.Definition.Name, o.Object.VisitPosition, o.Contents)),
    ];

    /// <summary>What a map object holds, one line per thing; empty for objects that hold nothing worth showing (gates, signs, scenery …).</summary>
    public static IReadOnlyList<string> Contents(GameState state, MapObject mapObject, bool bankRewards)
    {
        string Creature(string id) => state.Creatures.TryGet(id, out var creature) ? creature.Name : id;
        string Artifact(string id) => state.Artifacts.TryGet(id, out var artifact) ? artifact.Name : id;
        string Stack(CreatureStack s) => string.Create(CultureInfo.InvariantCulture, $"{s.Count} {Creature(s.Creature)}");
        string Guard(IEnumerable<CreatureStack> stacks) => $"guard: {string.Join(", ", stacks.Select(Stack))}";
        var lines = new List<string>();
        switch (mapObject)
        {
            case TreasureChest chest:
                lines.Add(chest.FixedValues is { } values
                    ? string.Create(CultureInfo.InvariantCulture, $"{values.Gold} gold or {values.Experience} experience")
                    : "gold or experience, or a Minor artifact (rolled when opened)");
                break;
            case ArtifactPickup pickup when pickup.Artifact is { } artifact:
                lines.Add(Artifact(artifact));
                break;
            case ResourcePile pile when pile.Amount is { } amount:
                lines.Add(string.Create(CultureInfo.InvariantCulture, $"{amount} {pile.Resource.ToString().ToLowerInvariant()}"));
                break;
            case CreatureBank bank when bank.IsGuarded:
                lines.Add(Guard(bank.Guard));
                if (bankRewards)
                {
                    lines.Add($"reward: {Resources(bank.Reward)}"
                        + string.Concat(new[] { bank.RewardArtifact }.OfType<Artifacts.ArtifactTier>().Concat(bank.MoreArtifacts).Select(t => $", a {t} artifact")));
                }

                break;
            case CreatureBank:
                lines.Add("plundered");
                break;
            case PandorasBox box:
                if (box.IsGuarded)
                {
                    lines.Add(Guard(box.Guard));
                }

                lines.AddRange(BoxLines(box.Contents, Artifact, Stack));
                break;
            case Shrine shrine when shrine.Spell is { } spell:
                lines.Add($"teaches {(state.Spells.TryGet(spell, out var definition) ? definition.Name : spell)}");
                break;
            case WitchHut hut when hut.Skills.Count > 0:
                lines.Add($"teaches {string.Join(" or ", hut.Skills.Select(s => BuiltInSkills.Contains(s) ? BuiltInSkills.Get(s).Name : s))}");
                break;
            case Prison prison when prison.Hero is { } prisoner:
                lines.Add($"{(state.HeroRoster.Contains(prisoner) ? state.HeroRoster.Get(prisoner).Name : prisoner)}, level {prison.Level ?? 1}");
                break;
            case Mine { Guard: { } guard }:
                lines.Add($"guard: {(guard.Count is { } count ? $"{count} " : "")}{Creature(guard.Creature)}");
                break;
            case CreatureDwelling dwelling:
                if (dwelling.Guard is { } dwellingGuard)
                {
                    lines.Add($"guard: {(dwellingGuard.Count is { } guarding ? $"{guarding} " : "")}{Creature(dwellingGuard.Creature)}");
                }

                lines.Add(string.Create(CultureInfo.InvariantCulture, $"{dwelling.Available ?? 0} {Creature(dwelling.Creature)} to recruit"));
                break;
            case MapGarrison garrison when !garrison.Troops.IsEmpty:
                lines.Add($"troops: {string.Join(", ", garrison.Troops.Stacks.Select(s => Stack(s.Stack)))}");
                break;
        }

        return lines;
    }

    private static IEnumerable<string> BoxLines(BoxContents contents, Func<string, string> artifact, Func<CreatureStack, string> stack)
    {
        if (contents.IsEmpty)
        {
            yield return "nothing";
            yield break;
        }

        if (!contents.Resources.IsEmpty)
        {
            yield return Resources(contents.Resources);
        }

        foreach (var (name, value) in new[]
                 {
                     ("experience", contents.Experience), ("mana", contents.Mana), ("morale", contents.Morale), ("luck", contents.Luck),
                     ("Attack", contents.Attack), ("Defense", contents.Defense), ("Spell Power", contents.SpellPower), ("Knowledge", contents.Knowledge),
                 }.Where(v => v.Item2 != 0))
        {
            yield return string.Create(CultureInfo.InvariantCulture, $"+{value} {name}");
        }

        foreach (var (skill, level) in contents.Skills.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            yield return $"{(BuiltInSkills.Contains(skill) ? BuiltInSkills.Get(skill).Name : skill)} ({level})";
        }

        foreach (var id in contents.Artifacts)
        {
            yield return artifact(id);
        }

        if (contents.Creatures.Count > 0)
        {
            yield return $"creatures: {string.Join(", ", contents.Creatures.Select(stack))}";
        }
    }

    /// <summary>A resource set as "1500 gold, 5 ore"; "nothing" when empty.</summary>
    public static string Resources(ResourceSet resources)
    {
        var parts = Enum.GetValues<ResourceType>().Where(r => resources[r] != 0)
            .Select(r => string.Create(CultureInfo.InvariantCulture, $"{resources[r]} {r.ToString().ToLowerInvariant()}")).ToList();
        return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
    }

    /// <summary>The town a cell belongs to: any cell of its footprint (its entrance too).</summary>
    public static Town? TownAt(GameState state, MapPosition position) =>
        state.ObjectCellAt(position)?.Object as Town ?? state.ObjectsOf<Town>().FirstOrDefault(t => t.VisitPosition == position);

    /// <summary>Whether a cell of the town (or its entrance) is in the player's sight.</summary>
    public static bool InSight(GameState state, PlayerId player, Town town) =>
        state.Fog.IsInSight(player, town.VisitPosition) || town.OccupiedCells().Any(c => state.Fog.IsInSight(player, c.Position));

    /// <summary>Whether a cell of the town (or its entrance) has been explored by the player.</summary>
    public static bool Explored(GameState state, PlayerId player, Town town) =>
        state.Fog.IsExplored(player, town.VisitPosition) || town.OccupiedCells().Any(c => state.Fog.IsExplored(player, c.Position));
}
