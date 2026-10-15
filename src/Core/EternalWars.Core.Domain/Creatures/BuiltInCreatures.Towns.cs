using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Towns;
using Cap = EternalWars.Core.Domain.Creatures.CreatureCapability;
using static EternalWars.Core.Domain.Creatures.CapabilityKind;
using static EternalWars.Core.Domain.Creatures.CreatureSize;
using static EternalWars.Core.Domain.Creatures.CreatureTags;

namespace EternalWars.Core.Domain.Creatures;

/// <summary>
/// Rosters of the other six playable towns plus the non-playable Conflux and Dragon Utopia creatures and extra
/// neutrals (ADR-044). Names come from the sandbox project (<c>reference/ew</c>) whose stats were placeholders;
/// these are a first balance pass on the same per-tier scale as Haven and Inferno.
/// </summary>
/// <remarks>
/// Scale of base forms per tier (upgrades about 10–20 % stronger, +25 % cost): T1 HP 6–20, damage 1–6,
/// gold 30–70; T2 HP 20–40, damage 4–11, gold 150–230; T3 HP 26–60, damage 7–16, gold 400–450; T4 HP 70–150,
/// damage 12–26, gold 1000–1150; T5 HP 160–240, damage 30–60, gold 2800–3500 plus a rare resource.
/// Columns of <see cref="C"/>: tier, size, attack, defense, damage, health, battle speed (cells), map speed,
/// initiative, gold, weekly growth.
/// </remarks>
public static partial class BuiltInCreatures
{
    private const string NeutralPrefix = "neutral";

    private static IEnumerable<CreatureDefinition> OtherTowns() =>
        [.. Sylvan(), .. Academy(), .. Necropolis(), .. Dungeon(), .. Stronghold(), .. SwampBestiary()];

    private static IEnumerable<CreatureDefinition> MoreNeutrals() => [.. Conflux(), .. DragonUtopia(), .. Wanderers()];

    private static IEnumerable<CreatureDefinition> Sylvan()
    {
        const string t = TownIds.Sylvan;
        string[] elf = [Elf];
        return
        [
            .. Line(
                C(t, "Pixie", 1, Small, 3, 2, (1, 3), 6, 12, 5, 14, 40, 20, new Cap(Flying)) with { Tags = [Fey] },
                C(t, "Sprite", 1, Small, 4, 3, (2, 3), 7, 14, 6, 15, 55, 20, new Cap(Flying), new Cap(NoEnemyRetaliation)) with { Tags = [Fey] }),
            .. Line(
                C(t, "Satyr", 1, Small, 6, 5, (3, 5), 14, 10, 4, 11, 60, 16) with { Tags = [Fey] },
                C(t, "Satyr Warrior", 1, Small, 7, 6, (4, 6), 16, 11, 5, 12, 80, 16, new Cap(Relentless, 10)) with { Tags = [Fey] }),
            .. Line(
                C(t, "Blade Dancer", 2, Small, 9, 6, (5, 8), 22, 12, 6, 13, 180, 8) with { Tags = elf },
                C(t, "War Dancer", 2, Small, 10, 7, (6, 9), 25, 13, 6, 14, 230, 8, new Cap(MultiHead)) with { Tags = elf }),
            .. Line(
                C(t, "Hunter", 2, Small, 9, 5, (3, 6), 12, 9, 5, 11, 170, 9, new Cap(MeleePenalty)) with { Shots = 14, Tags = elf },
                C(t, "Arcane Archer", 2, Small, 10, 6, (4, 7), 14, 10, 6, 12, 220, 9, new Cap(ShootsTwice), new Cap(MeleePenalty)) with { Shots = 16, Tags = elf }),
            .. Line(
                C(t, "Druid", 3, Small, 11, 8, (7, 10), 30, 8, 5, 10, 400, 5, new Cap(MeleePenalty), new Cap(Caster, 1, "lightning")) with { Shots = 10, Tags = elf },
                C(t, "Druid Elder", 3, Small, 12, 9, (8, 11), 34, 9, 5, 11, 480, 5, new Cap(RangedRetaliation), new Cap(Caster, 2, "lightning")) with { Shots = 12, Tags = elf }),
            .. Line(
                C(t, "Pegasus", 3, Medium, 12, 10, (9, 13), 50, 18, 8, 13, 420, 5, new Cap(Flying)) with { Tags = [Beast] },
                C(t, "Silver Pegasus", 3, Medium, 13, 12, (10, 14), 56, 22, 9, 14, 520, 5, new Cap(Flying), new Cap(SpellImmunity, Parameter: SpellTags.Mind)) with { Tags = [Beast] }),
            .. Line(
                C(t, "Qilin", 4, Medium, 15, 16, (14, 20), 95, 16, 7, 12, 1000, 3, new Cap(SpellImmunity, Parameter: SpellTags.Curse)) with { Tags = [Beast] },
                C(t, "Pristine Unicorn", 4, Medium, 17, 18, (15, 22), 105, 18, 8, 13, 1250, 3,
                    new Cap(SpellImmunity, Parameter: SpellTags.Curse), new Cap(SpellImmunity, Parameter: SpellTags.Mind)) with { Tags = [Beast] }),
            .. Line(
                C(t, "Treant", 4, Large, 13, 22, (12, 18), 150, 7, 3, 7, 1050, 2, new Cap(UnlimitedRetaliation)),
                C(t, "Ancient Treant", 4, Large, 15, 24, (13, 20), 170, 8, 4, 8, 1300, 2, new Cap(UnlimitedRetaliation), new Cap(FirstStrike))),
            .. Line(
                C(t, "Fire Bird", 5, Large, 22, 20, (30, 40), 175, 24, 11, 15, 3000, 1, new Cap(Flying), new Cap(Breath), new Cap(SpellImmunity, Parameter: SpellTags.Fire))
                    with { Cost = new ResourceSet(Gold: 3000, Mercury: 1) },
                C(t, "Phoenix", 5, Large, 26, 24, (35, 45), 220, 28, 11, 16, 4000, 1,
                    new Cap(Flying), new Cap(Breath), new Cap(SpellImmunity, Parameter: SpellTags.Fire), new Cap(Caster, 1, "rebirth"))
                    with { Cost = new ResourceSet(Gold: 4000, Mercury: 2) }),
        ];
    }

    private static IEnumerable<CreatureDefinition> Academy()
    {
        const string t = TownIds.Academy;
        string[] construct = [Construct];
        return
        [
            .. Line(
                C(t, "Gremlin", 1, Small, 4, 3, (1, 2), 6, 9, 4, 11, 30, 20),
                C(t, "Gremlin Master", 1, Small, 5, 4, (2, 3), 7, 10, 5, 12, 45, 20, new Cap(MeleePenalty)) with { Shots = 8 }),
            .. Line(
                C(t, "Defender", 1, Small, 5, 8, (2, 4), 16, 8, 3, 8, 60, 15, new Cap(FirstStrike)) with { Tags = [Human] },
                C(t, "Shield Guard", 1, Small, 6, 10, (3, 4), 18, 9, 4, 9, 80, 15, new Cap(FirstStrike), new Cap(UnlimitedRetaliation)) with { Tags = [Human] }),
            .. Line(
                C(t, "Gargoyle", 2, Small, 8, 9, (3, 6), 20, 14, 7, 12, 150, 9, new Cap(Flying)) with { Tags = construct },
                C(t, "Obsidian Gargoyle", 2, Small, 9, 11, (4, 7), 25, 16, 8, 13, 190, 9, new Cap(Flying), new Cap(SpellImmunity, Parameter: SpellTags.Fire)) with { Tags = construct }),
            .. Line(
                C(t, "Iron Golem", 2, Medium, 9, 11, (6, 9), 35, 8, 4, 8, 220, 7, new Cap(SpellImmunity, Parameter: SpellTags.Mind)) with { Tags = construct },
                C(t, "Steel Golem", 2, Medium, 10, 13, (7, 10), 42, 9, 4, 9, 280, 7,
                    new Cap(SpellImmunity, Parameter: SpellTags.Mind), new Cap(SpellImmunity, Parameter: SpellTags.Fire)) with { Tags = construct }),
            .. Line(
                C(t, "Snow Maiden", 3, Small, 11, 7, (7, 10), 26, 8, 5, 11, 420, 5, new Cap(MeleePenalty)) with { Shots = 10 },
                C(t, "Enchantress", 3, Small, 12, 8, (8, 11), 30, 9, 5, 12, 500, 5, new Cap(MeleePenalty), new Cap(Caster, 1, "frost-bolt")) with { Shots = 12 }),
            .. Line(
                C(t, "Thane", 3, Medium, 13, 12, (9, 14), 55, 12, 6, 11, 450, 5) with { Tags = [Giant] },
                C(t, "Thunder Thane", 3, Medium, 14, 13, (10, 15), 62, 14, 7, 12, 550, 5,
                    new Cap(Caster, 1, "lightning"), new Cap(SpellImmunity, Parameter: "lightning")) with { Tags = [Giant] }),
            .. Line(
                C(t, "Djinn", 4, Medium, 15, 14, (14, 20), 90, 18, 8, 13, 1050, 3, new Cap(Flying)) with { Tags = [Elemental] },
                C(t, "Djinn Sultan", 4, Medium, 17, 16, (15, 22), 100, 20, 9, 14, 1300, 3,
                    new Cap(Flying), new Cap(Caster, 1, "confusion"), new Cap(SpellImmunity, Parameter: SpellTags.Mind)) with { Tags = [Elemental] }),
            .. Line(
                C(t, "Naga", 4, Large, 17, 17, (18, 24), 120, 10, 5, 11, 1100, 2, new Cap(NoEnemyRetaliation)),
                C(t, "Naga Queen", 4, Large, 19, 19, (20, 26), 135, 11, 6, 12, 1350, 2, new Cap(NoEnemyRetaliation), new Cap(Relentless, 10))),
            .. Line(
                C(t, "Colossus", 5, Large, 24, 24, (40, 55), 220, 16, 9, 13, 3500, 1, new Cap(SpellImmunity, Parameter: SpellTags.Mind))
                    with { Tags = [Giant], Cost = new ResourceSet(Gold: 3500, Mercury: 1) },
                C(t, "Titan", 5, Large, 28, 27, (45, 60), 280, 18, 10, 14, 4500, 1, new Cap(SpellImmunity, Parameter: SpellTags.Mind), new Cap(RangedRetaliation))
                    with { Shots = 20, Tags = [Giant], Cost = new ResourceSet(Gold: 4500, Mercury: 2) }),
        ];
    }

    private static IEnumerable<CreatureDefinition> Necropolis()
    {
        const string t = TownIds.Necropolis;
        string[] undead = [Undead];
        CreatureCapability mindless = new(SpellImmunity, Parameter: SpellTags.Mind);
        return
        [
            .. Line(
                C(t, "Skeleton", 1, Small, 5, 4, (1, 3), 6, 9, 4, 10, 45, 20, mindless) with { Tags = undead },
                C(t, "Skeleton Warrior", 1, Small, 6, 6, (2, 3), 8, 10, 5, 11, 60, 20, mindless, new Cap(UnlimitedRetaliation)) with { Tags = undead }),
            .. Line(
                C(t, "Plague Bearer", 1, Small, 4, 6, (2, 4), 18, 7, 3, 7, 55, 15, mindless, new Cap(Relentless, 10)) with { Tags = undead },
                C(t, "Flesh Golem", 1, Small, 6, 8, (3, 5), 26, 8, 3, 8, 75, 15, mindless, new Cap(UnlimitedRetaliation)) with { Tags = [Undead, Construct] }),
            .. Line(
                C(t, "Ghoul", 2, Small, 9, 7, (5, 8), 24, 11, 5, 11, 170, 9, mindless, new Cap(Relentless, 15)) with { Tags = undead },
                C(t, "Alghoul", 2, Small, 10, 8, (6, 9), 28, 12, 6, 12, 215, 9, mindless, new Cap(Relentless, 20)) with { Tags = undead }),
            .. Line(
                C(t, "Death Weaver", 2, Medium, 8, 8, (5, 7), 26, 14, 6, 12, 190, 8, new Cap(Flying), new Cap(PackHunter, 10)) with { Tags = [Undead, Beast] },
                C(t, "Fate Weaver", 2, Medium, 9, 9, (6, 8), 30, 16, 7, 13, 240, 8, new Cap(Flying), new Cap(PackHunter, 15), mindless) with { Tags = [Undead, Beast] }),
            .. Line(
                C(t, "Vampire", 3, Small, 12, 10, (6, 10), 32, 14, 6, 12, 450, 5, new Cap(Flying), new Cap(NoEnemyRetaliation)) with { Tags = undead },
                C(t, "Vampire Lord", 3, Small, 13, 12, (7, 12), 40, 16, 7, 13, 550, 5, new Cap(Flying), new Cap(NoEnemyRetaliation), mindless) with { Tags = undead }),
            .. Line(
                C(t, "Wight", 3, Small, 11, 9, (7, 11), 30, 9, 5, 10, 420, 5, new Cap(MeleePenalty), mindless) with { Shots = 10, Tags = undead },
                C(t, "Wraith", 3, Small, 12, 10, (8, 12), 34, 10, 5, 11, 500, 5, new Cap(MeleePenalty), new Cap(RangedRetaliation), mindless) with { Shots = 12, Tags = undead }),
            .. Line(
                C(t, "Dark Knight", 4, Medium, 17, 16, (15, 22), 100, 16, 7, 12, 1100, 3, new Cap(Relentless, 10)) with { Tags = [Undead, Mounted] },
                C(t, "Death Knight", 4, Medium, 19, 18, (17, 25), 115, 18, 8, 13, 1350, 3, new Cap(Relentless, 15), mindless) with { Tags = [Undead, Mounted] }),
            .. Line(
                C(t, "Lich", 4, Small, 15, 14, (12, 18), 70, 8, 5, 10, 1000, 3, new Cap(MeleePenalty), new Cap(Caster, 1, "death-cloud"), mindless)
                    with { Shots = 12, Tags = undead },
                C(t, "Arch Lich", 4, Small, 17, 16, (14, 20), 80, 9, 6, 11, 1250, 3,
                    new Cap(MeleePenalty), new Cap(Caster, 1, "death-cloud"), mindless, new Cap(RangedRetaliation)) with { Shots = 16, Tags = undead }),
            .. Line(
                C(t, "Bone Dragon", 5, Large, 22, 20, (30, 45), 160, 18, 9, 13, 2800, 1, new Cap(Flying), mindless, new Cap(Relentless, 10))
                    with { Tags = [Undead, Dragon], Cost = new ResourceSet(Gold: 2800, Mercury: 1) },
                C(t, "Ghost Dragon", 5, Large, 25, 23, (35, 50), 200, 22, 10, 14, 3800, 1, new Cap(Flying), mindless, new Cap(Breath))
                    with { Tags = [Undead, Dragon], Cost = new ResourceSet(Gold: 3800, Mercury: 2) }),
        ];
    }

    private static IEnumerable<CreatureDefinition> Dungeon()
    {
        const string t = TownIds.Dungeon;
        return
        [
            .. Line(
                C(t, "Troglodyte", 1, Small, 5, 4, (2, 3), 7, 9, 4, 10, 50, 18, new Cap(SpellImmunity, Parameter: "blind")),
                C(t, "Deep Troglodyte", 1, Small, 6, 5, (2, 4), 8, 10, 5, 11, 65, 18, new Cap(SpellImmunity, Parameter: "blind"), new Cap(PackHunter, 10))),
            .. Line(
                C(t, "Scout", 1, Small, 6, 3, (3, 5), 10, 12, 6, 14, 60, 15, new Cap(Relentless, 10)) with { Tags = [Elf] },
                C(t, "Stalker", 1, Small, 7, 4, (3, 6), 12, 13, 7, 15, 80, 15, new Cap(Relentless, 15), new Cap(FirstStrike)) with { Tags = [Elf] }),
            .. Line(
                C(t, "Medusa", 2, Small, 9, 8, (4, 7), 22, 8, 5, 10, 200, 8) with { Shots = 8, Tags = [Reptile] },
                C(t, "Putrid Medusa", 2, Small, 10, 10, (5, 8), 26, 9, 5, 11, 250, 8, new Cap(RangedRetaliation)) with { Shots = 10, Tags = [Reptile] }),
            .. Line(
                C(t, "Minotaur", 2, Medium, 11, 9, (7, 11), 40, 11, 6, 11, 230, 7, new Cap(Relentless, 10)),
                C(t, "Minotaur Guard", 2, Medium, 12, 11, (8, 12), 46, 12, 6, 12, 290, 7, new Cap(Relentless, 10), new Cap(FirstStrike))),
            .. Line(
                C(t, "Dark Raider", 3, Medium, 12, 10, (9, 14), 52, 18, 8, 13, 450, 5, new Cap(PackHunter, 10)) with { Tags = [Elf, Mounted] },
                C(t, "Grim Raider", 3, Medium, 13, 11, (10, 15), 58, 20, 9, 14, 560, 5, new Cap(PackHunter, 15), new Cap(Relentless, 10)) with { Tags = [Elf, Mounted] }),
            .. Line(
                C(t, "Shadow Mistress", 3, Small, 11, 8, (7, 11), 28, 9, 5, 11, 430, 5, new Cap(MeleePenalty), new Cap(Caster, 1, "blind")) with { Shots = 10, Tags = [Elf] },
                C(t, "Shadow Matriarch", 3, Small, 12, 9, (8, 12), 32, 10, 6, 12, 520, 5, new Cap(MeleePenalty), new Cap(Caster, 1, "blind"), new Cap(RangedRetaliation))
                    with { Shots = 12, Tags = [Elf] }),
            .. Line(
                C(t, "Manticore", 4, Large, 16, 14, (15, 22), 100, 16, 8, 12, 1100, 3, new Cap(Flying)) with { Tags = [Beast] },
                C(t, "Scorpicore", 4, Large, 18, 16, (17, 24), 115, 18, 9, 13, 1350, 3, new Cap(Flying), new Cap(Relentless, 15)) with { Tags = [Beast] }),
            .. Line(
                C(t, "Spectator", 4, Medium, 16, 15, (14, 20), 80, 7, 4, 10, 1050, 3, new Cap(SpellImmunity, Parameter: SpellTags.Mind)) with { Shots = 12 },
                C(t, "Beholder", 4, Medium, 18, 17, (16, 22), 90, 8, 5, 11, 1300, 3, new Cap(SpellImmunity, Parameter: SpellTags.Mind), new Cap(RangedRetaliation))
                    with { Shots = 16 }),
            .. Line(
                C(t, "Shadow Horror", 5, Large, 24, 22, (35, 55), 200, 22, 11, 15, 3300, 1, new Cap(Flying), new Cap(Breath))
                    with { Tags = [Dragon], Cost = new ResourceSet(Gold: 3300, Sulfur: 1) },
                C(t, "Shadow Reaper", 5, Large, 28, 25, (40, 60), 240, 24, 11, 16, 4300, 1, new Cap(Flying), new Cap(Breath), new Cap(SpellImmunity, Parameter: SpellTags.Mind))
                    with { Tags = [Dragon], Cost = new ResourceSet(Gold: 4300, Sulfur: 2) }),
        ];
    }

    private static IEnumerable<CreatureDefinition> Stronghold()
    {
        const string t = TownIds.Stronghold;
        string[] orc = [Orc];
        return
        [
            .. Line(
                C(t, "Goblin", 1, Small, 4, 2, (1, 3), 6, 11, 5, 12, 35, 22, new Cap(PackHunter, 10)) with { Tags = orc },
                C(t, "Hobgoblin", 1, Small, 5, 3, (2, 3), 7, 12, 6, 13, 50, 22, new Cap(PackHunter, 15)) with { Tags = orc }),
            .. Line(
                C(t, "Harpy", 1, Small, 6, 4, (2, 4), 12, 14, 6, 14, 65, 14, new Cap(Flying)) with { Tags = [Beast] },
                C(t, "Fury", 1, Small, 7, 5, (3, 5), 14, 16, 7, 15, 85, 14, new Cap(Flying), new Cap(NoEnemyRetaliation)) with { Tags = [Beast] }),
            .. Line(
                C(t, "Orc", 2, Small, 9, 6, (5, 8), 24, 10, 5, 11, 160, 9) with { Tags = orc },
                C(t, "Orc Berserker", 2, Small, 11, 5, (6, 10), 26, 11, 5, 12, 200, 9, new Cap(Relentless, 20)) with { Tags = orc }),
            .. Line(
                C(t, "Centaur", 2, Medium, 10, 8, (6, 9), 30, 16, 8, 13, 200, 8) with { Tags = [Beast] },
                C(t, "Centaur Warchief", 2, Medium, 11, 9, (7, 10), 34, 18, 8, 14, 250, 8, new Cap(PackHunter, 10)) with { Tags = [Beast] }),
            .. Line(
                C(t, "Ogre", 3, Medium, 13, 10, (10, 15), 60, 8, 4, 9, 420, 5) with { Tags = [Giant] },
                C(t, "Ogre Mage", 3, Medium, 14, 11, (11, 16), 66, 9, 5, 10, 520, 5, new Cap(Caster, 1, "bloodlust")) with { Tags = [Giant] }),
            .. Line(
                C(t, "Roc", 3, Large, 13, 11, (11, 15), 60, 20, 9, 13, 450, 5, new Cap(Flying)) with { Tags = [Beast] },
                C(t, "Thunderbird", 3, Large, 14, 12, (12, 17), 66, 22, 10, 14, 550, 5, new Cap(Flying), new Cap(Caster, 1, "lightning")) with { Tags = [Beast] }),
            .. Line(
                C(t, "Cyclops", 4, Large, 17, 14, (16, 22), 110, 10, 5, 11, 1100, 3) with { Shots = 12, Tags = [Giant] },
                C(t, "Cyclops King", 4, Large, 19, 16, (18, 25), 125, 11, 6, 12, 1350, 3, new Cap(RangedRetaliation)) with { Shots = 16, Tags = [Giant] }),
            .. Line(
                C(t, "Mammoth", 4, Large, 16, 19, (18, 26), 150, 12, 6, 9, 1150, 2, new Cap(PackHunter, 10)) with { Tags = [Beast] },
                C(t, "War Mammoth", 4, Large, 18, 21, (20, 28), 165, 13, 6, 10, 1400, 2, new Cap(PackHunter, 10), new Cap(Cleave)) with { Tags = [Beast] }),
            .. Line(
                C(t, "Behemoth", 5, Large, 25, 20, (40, 60), 240, 16, 9, 13, 3300, 1) with { Tags = [Beast], Cost = new ResourceSet(Gold: 3300, Crystal: 1) },
                C(t, "Ancient Behemoth", 5, Large, 28, 23, (45, 70), 280, 18, 10, 14, 4300, 1, new Cap(Relentless, 15))
                    with { Tags = [Beast], Cost = new ResourceSet(Gold: 4300, Crystal: 2) }),
        ];
    }

    private static IEnumerable<CreatureDefinition> SwampBestiary()
    {
        const string t = TownIds.SwampBestiary;
        string[] reptile = [Reptile];
        return
        [
            .. Line(
                C(t, "Lizardman", 1, Small, 5, 5, (2, 4), 12, 10, 5, 10, 55, 16) with { Tags = reptile },
                C(t, "Lizard Warrior", 1, Small, 6, 6, (3, 5), 14, 11, 5, 11, 75, 16, new Cap(FirstStrike)) with { Tags = reptile }),
            .. Line(
                C(t, "Kappa", 1, Small, 6, 4, (2, 4), 10, 12, 6, 13, 60, 15, new Cap(Relentless, 10)) with { Tags = [Fey] },
                C(t, "Kappa Shaman", 1, Small, 7, 5, (3, 4), 12, 13, 6, 14, 80, 15, new Cap(MeleePenalty)) with { Shots = 8, Tags = [Fey] }),
            .. Line(
                C(t, "Jungle Horror", 2, Medium, 10, 8, (6, 10), 34, 12, 6, 11, 200, 7, new Cap(PackHunter, 15)) with { Tags = [Beast] },
                C(t, "Jungle Terror", 2, Medium, 11, 9, (7, 11), 38, 13, 6, 12, 250, 7, new Cap(PackHunter, 20)) with { Tags = [Beast] }),
            .. Line(
                C(t, "Mantis", 2, Small, 10, 6, (5, 8), 20, 13, 6, 14, 180, 9, new Cap(FirstStrike)) with { Tags = [Beast] },
                C(t, "Dragon Mantis", 2, Small, 11, 7, (6, 9), 22, 16, 7, 15, 225, 9, new Cap(Flying), new Cap(FirstStrike)) with { Tags = [Beast] }),
            .. Line(
                C(t, "Basilisk", 3, Medium, 12, 12, (8, 13), 50, 9, 5, 10, 420, 5) with { Tags = reptile },
                C(t, "Greater Basilisk", 3, Medium, 13, 14, (9, 14), 56, 10, 5, 11, 520, 5, new Cap(Relentless, 10)) with { Tags = reptile }),
            .. Line(
                C(t, "Owlbear", 3, Medium, 13, 10, (10, 14), 55, 11, 6, 11, 430, 5, new Cap(Relentless, 10)) with { Tags = [Beast] },
                C(t, "Elder Owlbear", 3, Medium, 14, 11, (11, 15), 62, 12, 6, 12, 530, 5, new Cap(DoubleStrike)) with { Tags = [Beast] }),
            .. Line(
                C(t, "Gorgon", 4, Large, 16, 17, (14, 20), 120, 9, 5, 9, 1050, 3) with { Tags = [Beast] },
                C(t, "Mighty Gorgon", 4, Large, 18, 19, (15, 22), 135, 10, 5, 10, 1300, 3, new Cap(Breath)) with { Tags = [Beast] }),
            .. Line(
                C(t, "Haspid", 4, Medium, 17, 14, (15, 21), 95, 15, 7, 13, 1100, 3, new Cap(Relentless, 15)) with { Tags = reptile },
                C(t, "Royal Haspid", 4, Medium, 19, 16, (17, 23), 108, 17, 8, 14, 1350, 3, new Cap(Relentless, 15), new Cap(NoEnemyRetaliation)) with { Tags = reptile }),
            .. Line(
                C(t, "Hydra", 5, Large, 23, 22, (30, 45), 220, 12, 7, 12, 3200, 1, new Cap(MultiHead), new Cap(NoEnemyRetaliation))
                    with { Tags = reptile, Cost = new ResourceSet(Gold: 3200, Sulfur: 1) },
                C(t, "Chaos Hydra", 5, Large, 26, 25, (35, 50), 260, 14, 8, 13, 4200, 1, new Cap(MultiHead), new Cap(NoEnemyRetaliation), new Cap(SpellImmunity, Parameter: "poison"))
                    with { Tags = reptile, Cost = new ResourceSet(Gold: 4200, Sulfur: 2) }),
        ];
    }

    /// <summary>Non-playable elementals (map guardians, campaign); most have no upgrade (ADR-030).</summary>
    private static IEnumerable<CreatureDefinition> Conflux()
    {
        const string t = TownIds.Conflux;
        string[] elemental = [Elemental];
        CreatureCapability mindless = new(SpellImmunity, Parameter: SpellTags.Mind);
        return new[]
        {
            C(t, "Wisp", 1, Small, 5, 4, (2, 4), 8, 14, 7, 15, 70, 15, new Cap(Flying), mindless),
            C(t, "Earth Elemental", 3, Medium, 12, 14, (8, 12), 60, 8, 4, 8, 400, 5, mindless),
            C(t, "Air Elemental", 3, Medium, 12, 10, (8, 11), 45, 18, 9, 14, 400, 5, new Cap(Flying), mindless),
            C(t, "Fire Elemental", 3, Medium, 13, 10, (9, 13), 45, 14, 7, 12, 400, 5, mindless, new Cap(SpellImmunity, Parameter: SpellTags.Fire)),
            C(t, "Water Elemental", 3, Medium, 12, 12, (8, 12), 50, 12, 6, 11, 400, 5, mindless),
            C(t, "Magma Elemental", 4, Medium, 16, 18, (14, 20), 100, 9, 5, 9, 1000, 3, mindless, new Cap(SpellImmunity, Parameter: SpellTags.Fire)),
            C(t, "Storm Elemental", 4, Medium, 16, 14, (13, 19), 85, 20, 9, 14, 1000, 3, new Cap(Flying), mindless, new Cap(Caster, 1, "lightning")),
            C(t, "Flame Elemental", 4, Medium, 17, 14, (15, 21), 85, 16, 8, 13, 1000, 3, mindless, new Cap(SpellImmunity, Parameter: SpellTags.Fire), new Cap(Relentless, 10)),
            C(t, "Ice Elemental", 4, Medium, 15, 17, (13, 19), 95, 12, 6, 11, 1000, 3, mindless, new Cap(RangedRetaliation)) with { Shots = 10 },
            C(t, "Energy Elemental", 5, Large, 24, 20, (35, 50), 190, 22, 10, 15, 3200, 1, new Cap(Flying), mindless, new Cap(SpellImmunity, Parameter: "lightning")),
            C(t, "Magic Elemental", 5, Large, 25, 24, (35, 50), 210, 18, 10, 14, 3500, 1, mindless, new Cap(SpellImmunity, Parameter: SpellTags.All), new Cap(MultiHead)),
        }.Select(c => c with { Tags = elemental });
    }

    /// <summary>Non-playable dragons; Red dragon and Wyvern are in the slice list. Black is about twice any other dragon.</summary>
    private static IEnumerable<CreatureDefinition> DragonUtopia()
    {
        const string t = TownIds.DragonUtopia;
        string[] dragon = [Dragon];
        return new[]
        {
            C(t, "Dragonyte", 4, Large, 17, 17, (16, 24), 130, 12, 6, 11, 1200, 2, new Cap(Breath)),
            C(t, "Faerie Dragon", 5, Medium, 20, 20, (20, 30), 130, 24, 11, 16, 2500, 1, new Cap(Flying), new Cap(Caster, 2, "lightning")),
            C(t, "Green Dragon", 5, Large, 26, 24, (38, 48), 240, 18, 10, 14, 3800, 1, new Cap(Flying), new Cap(Breath), new Cap(SpellImmunity, Parameter: "poison")),
            C(t, "Azure Dragon", 5, Large, 27, 26, (40, 50), 260, 20, 10, 14, 4200, 1, new Cap(Flying), new Cap(Breath), new Cap(SpellImmunity, Parameter: "ice")),
            C(t, "Gold Dragon", 5, Large, 28, 27, (40, 52), 270, 22, 11, 15, 4500, 1, new Cap(Flying), new Cap(Breath), new Cap(SpellImmunity, Parameter: "lightning")),
            C(t, "Black Dragon", 5, Large, 36, 34, (60, 80), 500, 24, 11, 15, 9000, 1,
                new Cap(Flying), new Cap(Breath), new Cap(SpellImmunity, Parameter: SpellTags.All)),
        }.Select(c => c with { Tags = dragon, Cost = c.Cost with { Sulfur = c.Tier == 5 ? 2 : 1 } });
    }

    /// <summary>Neutrals from the sandbox's extra lines: the wolf upgrade, undead spirits, a construct.</summary>
    private static IEnumerable<CreatureDefinition> Wanderers() =>
    [
        C(NeutralPrefix, "Dire wolf", 1, Small, 7, 5, (4, 6), 14, 15, 7, 14, 80, 12, new Cap(PackHunter, 25)) with { Tags = [Beast] },
        .. Line(
            C(NeutralPrefix, "Specter", 2, Small, 9, 7, (4, 7), 20, 14, 7, 13, 190, 8, new Cap(Flying), new Cap(SpellImmunity, Parameter: SpellTags.Mind)) with { Tags = [Undead] },
            C(NeutralPrefix, "Banshee", 2, Small, 10, 8, (5, 8), 23, 16, 7, 14, 240, 8,
                new Cap(Flying), new Cap(SpellImmunity, Parameter: SpellTags.Mind), new Cap(NoEnemyRetaliation)) with { Tags = [Undead] }),
        C(NeutralPrefix, "Animated Armor", 2, Small, 8, 12, (4, 7), 30, 8, 4, 8, 200, 7, new Cap(SpellImmunity, Parameter: SpellTags.Mind), new Cap(UnlimitedRetaliation))
            with { Tags = [Construct] },
    ];

    /// <summary>A base creature and its upgrade, linked.</summary>
    private static CreatureDefinition[] Line(CreatureDefinition basic, CreatureDefinition upgrade) => [basic with { UpgradesTo = upgrade.Id }, upgrade];

    /// <summary>
    /// A creature of <paramref name="town"/> (a <c>TownIds</c> value, or the neutral prefix) with the id
    /// <c>town.kebab-name</c>.
    /// </summary>
    private static CreatureDefinition C(
        string town, string name, int tier, CreatureSize size, int attack, int defense, (int Min, int Max) damage,
        int health, int speed, int mapSpeed, int initiative, int gold, int growth, params CreatureCapability[] capabilities) =>
        new($"{town}.{Kebab(name)}", name, tier, size)
        {
            Town = town == NeutralPrefix ? null : town,
            Attack = attack,
            Defense = defense,
            MinDamage = damage.Min,
            MaxDamage = damage.Max,
            Health = health,
            Speed = speed,
            MapSpeed = mapSpeed,
            Initiative = initiative,
            WeeklyGrowth = growth,
            Cost = new ResourceSet(Gold: gold),
            Capabilities = capabilities,
        };

    private static string Kebab(string name) => name.ToLowerInvariant().Replace(' ', '-');
}
