using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Domain.Creatures.CapabilityKind;
using static EternalWars.Core.Domain.Creatures.CreatureSize;

namespace EternalWars.Core.Domain.Creatures;

/// <summary>
/// Creatures that ship with the game. <b>Draft rosters</b> for the vertical slice (Haven, Inferno) derived
/// from the reference project and reorganised into 9 lines over 5 tiers (2+2+2+2+1), every line with an
/// upgrade (ADR-030, ADR-037), and at most five capabilities each (ADR-042). Numbers are a first balance pass, to
/// be tuned with the simulator. Caster spell ids name spells the magic system will define.
/// Extra or overriding definitions load from JSON (<c>CreatureDefinitionSerializer</c>).
/// </summary>
public static class BuiltInCreatures
{
    // Haven ----------------------------------------------------------------------------------
    public const string Footman = "haven.footman";
    public const string Crusader = "haven.crusader";
    public const string Pikeman = "haven.pikeman";
    public const string Guardian = "haven.guardian";
    public const string Zealot = "haven.zealot";
    public const string Inquisitor = "haven.inquisitor";
    public const string Archer = "haven.archer";
    public const string Marksman = "haven.marksman";
    public const string Monk = "haven.monk";
    public const string Priest = "haven.priest";
    public const string Griffin = "haven.griffin";
    public const string RoyalGriffin = "haven.royal-griffin";
    public const string Lancer = "haven.lancer";
    public const string Champion = "haven.champion";
    public const string Paladin = "haven.paladin";
    public const string SacredWarrior = "haven.sacred-warrior";
    public const string RadiantSpirit = "haven.radiant-spirit";
    public const string Seraph = "haven.seraph";

    // Inferno --------------------------------------------------------------------------------
    public const string Imp = "inferno.imp";
    public const string Familiar = "inferno.familiar";
    public const string HornedDemon = "inferno.horned-demon";
    public const string HornedOverseer = "inferno.horned-overseer";
    public const string HellHound = "inferno.hell-hound";
    public const string Cerberus = "inferno.cerberus";
    public const string Tormentor = "inferno.tormentor";
    public const string Lacerator = "inferno.lacerator";
    public const string Succubus = "inferno.succubus";
    public const string SuccubusMistress = "inferno.succubus-mistress";
    public const string HellCharger = "inferno.hell-charger";
    public const string Nightmare = "inferno.nightmare";
    public const string PitFiend = "inferno.pit-fiend";
    public const string PitLord = "inferno.pit-lord";
    public const string Juggernaut = "inferno.juggernaut";
    public const string Ravager = "inferno.ravager";
    public const string Devil = "inferno.devil";
    public const string ArchDevil = "inferno.arch-devil";

    // Neutrals -------------------------------------------------------------------------------
    public const string Wolf = "neutral.wolf";
    public const string Bandit = "neutral.bandit";
    public const string Troll = "neutral.troll";
    public const string Wyvern = "neutral.wyvern";
    public const string RedDragon = "dragon-utopia.red-dragon";

    public static IReadOnlyList<CreatureDefinition> All { get; } =
    [
        // id, name, tier, size, attack, defense, damage, health, speed (cells), map speed, initiative
        Haven(Footman, "Footman", 1, Small, 6, 5, (4, 6), 14, 10, 3, 10, 60, 18) with { UpgradesTo = Crusader },
        Haven(Crusader, "Crusader", 1, Small, 7, 6, (5, 7), 16, 11, 4, 11, 80, 18) with
            { Capabilities = [new(DoubleStrike)] },
        Haven(Pikeman, "Pikeman", 1, Small, 6, 8, (3, 6), 18, 9, 4, 9, 70, 16) with { UpgradesTo = Guardian },
        Haven(Guardian, "Guardian", 1, Small, 7, 10, (4, 6), 20, 10, 4, 10, 90, 16) with
            { Capabilities = [new(FirstStrike), new(UnlimitedRetaliation)] },
        Haven(Zealot, "Zealot", 2, Small, 10, 9, (7, 11), 35, 11, 5, 11, 200, 9) with { UpgradesTo = Inquisitor },
        Haven(Inquisitor, "Inquisitor", 2, Small, 11, 10, (8, 12), 40, 12, 5, 12, 250, 9) with
            { Capabilities = [new(Relentless, 15), new(SpellImmunity, Parameter: SpellTags.Mind)] },
        Haven(Archer, "Archer", 2, Small, 8, 6, (3, 5), 12, 8, 4, 9, 150, 10) with { Shots = 12, UpgradesTo = Marksman, Capabilities = [new(MeleePenalty)] },
        Haven(Marksman, "Marksman", 2, Small, 9, 7, (4, 6), 14, 9, 5, 10, 200, 10) with
            { Shots = 16, Capabilities = [new(ShootsTwice), new(MeleePenalty)] },
        Haven(Monk, "Monk", 3, Small, 12, 9, (8, 12), 30, 8, 5, 9, 400, 5) with { Shots = 10, UpgradesTo = Priest, Capabilities = [new(MeleePenalty), new(RangedRetaliation)] },
        Haven(Priest, "Priest", 3, Small, 13, 11, (9, 13), 35, 9, 6, 11, 480, 5) with
            { Shots = 12, Capabilities = [new(RangedRetaliation), new(Caster, 1, "cure")] },
        Haven(Griffin, "Griffin", 3, Medium, 13, 12, (10, 14), 60, 18, 7, 12, 450, 5) with
            { UpgradesTo = RoyalGriffin, Tags = [CreatureTags.Beast], Capabilities = [new(Flying)] },
        Haven(RoyalGriffin, "Royal griffin", 3, Medium, 14, 13, (11, 15), 65, 22, 8, 13, 550, 5) with
            { Tags = [CreatureTags.Beast], Capabilities = [new(Flying), new(UnlimitedRetaliation)] },
        Haven(Lancer, "Lancer", 4, Medium, 16, 16, (16, 22), 100, 18, 7, 12, 1000, 3) with
            { UpgradesTo = Champion, Tags = [CreatureTags.Human, CreatureTags.Holy, CreatureTags.Mounted] },
        Haven(Champion, "Champion", 4, Medium, 18, 17, (18, 24), 110, 20, 9, 13, 1200, 3) with
            { Tags = [CreatureTags.Human, CreatureTags.Holy, CreatureTags.Mounted], Capabilities = [new(PackHunter, 10)] },
        Haven(Paladin, "Paladin", 4, Small, 16, 20, (12, 16), 90, 12, 5, 12, 1000, 3) with { UpgradesTo = SacredWarrior },
        Haven(SacredWarrior, "Sacred warrior", 4, Small, 17, 22, (14, 18), 100, 13, 6, 13, 1200, 3) with
            { Capabilities = [new(DoubleStrike), new(SpellImmunity, Parameter: SpellTags.Curse)] },
        Haven(RadiantSpirit, "Radiant spirit", 5, Large, 22, 22, (40, 50), 175, 22, 10, 15, 3000, 1) with
            {
                UpgradesTo = Seraph, Tags = [CreatureTags.Holy], Capabilities = [new(Flying), new(SpellImmunity, Parameter: SpellTags.Mind)],
                Cost = new ResourceSet(Gold: 3000, Crystal: 1),
            },
        Haven(Seraph, "Seraph", 5, Large, 26, 26, (45, 55), 220, 26, 11, 16, 4000, 1) with
            {
                Tags = [CreatureTags.Holy], Capabilities = [new(Flying), new(NoEnemyRetaliation), new(SpellImmunity, Parameter: SpellTags.Mind), new(Caster, 1, "resurrection")],
                Cost = new ResourceSet(Gold: 4000, Crystal: 2),
            },

        Inferno(Imp, "Imp", 1, Small, 4, 3, (2, 4), 8, 10, 5, 13, 50, 20) with
            { UpgradesTo = Familiar, Capabilities = [new(Flying)] },
        Inferno(Familiar, "Familiar", 1, Small, 5, 4, (3, 4), 9, 12, 6, 14, 65, 20) with
            { Capabilities = [new(Flying), new(Relentless, 10)] },
        Inferno(HornedDemon, "Horned demon", 1, Small, 7, 7, (4, 6), 16, 9, 4, 9, 70, 15) with { UpgradesTo = HornedOverseer },
        Inferno(HornedOverseer, "Horned overseer", 1, Small, 8, 9, (5, 7), 18, 10, 5, 10, 90, 15) with
            { Capabilities = [new(PackHunter, 10)] },
        Inferno(HellHound, "Hell hound", 2, Medium, 10, 7, (6, 10), 30, 16, 7, 13, 200, 8) with
            { UpgradesTo = Cerberus, Tags = [CreatureTags.Demon, CreatureTags.Beast], Capabilities = [new(PackHunter, 15)] },
        Inferno(Cerberus, "Cerberus", 2, Medium, 11, 8, (7, 11), 34, 18, 8, 14, 250, 8) with
            { Tags = [CreatureTags.Demon, CreatureTags.Beast], Capabilities = [new(Cleave), new(NoEnemyRetaliation)] },
        Inferno(Tormentor, "Tormentor", 2, Small, 10, 8, (6, 9), 28, 11, 5, 11, 180, 9) with
            { UpgradesTo = Lacerator, Capabilities = [new(Relentless, 15)] },
        Inferno(Lacerator, "Lacerator", 2, Small, 11, 9, (7, 10), 32, 12, 5, 12, 230, 9) with
            { Capabilities = [new(Relentless, 25), new(FirstStrike)] },
        Inferno(Succubus, "Succubus", 3, Small, 12, 9, (8, 12), 30, 10, 6, 11, 400, 5) with
            { Shots = 10, UpgradesTo = SuccubusMistress, Capabilities = [new(MeleePenalty)] },
        Inferno(SuccubusMistress, "Succubus mistress", 3, Small, 13, 10, (9, 13), 34, 11, 6, 12, 480, 5) with
            { Shots = 12, Capabilities = [new(RangedRetaliation), new(SpellImmunity, Parameter: SpellTags.Mind)] },
        Inferno(HellCharger, "Hell charger", 3, Medium, 13, 11, (10, 15), 60, 18, 8, 13, 450, 5) with
            { UpgradesTo = Nightmare, Tags = [CreatureTags.Demon, CreatureTags.Beast] },
        Inferno(Nightmare, "Nightmare", 3, Medium, 14, 12, (11, 16), 66, 21, 9, 14, 550, 5) with
            { Tags = [CreatureTags.Demon, CreatureTags.Beast], Capabilities = [new(Relentless, 15)] },
        Inferno(PitFiend, "Pit fiend", 4, Medium, 17, 16, (15, 22), 110, 12, 6, 11, 1000, 3) with { UpgradesTo = PitLord },
        Inferno(PitLord, "Pit lord", 4, Medium, 19, 17, (17, 24), 120, 13, 7, 12, 1250, 3) with
            { Capabilities = [new(MultiHead), new(Caster, 1, "fireball")] },
        Inferno(Juggernaut, "Juggernaut", 4, Large, 17, 20, (18, 26), 150, 10, 5, 9, 1100, 2) with
            { UpgradesTo = Ravager, Tags = [CreatureTags.Demon, CreatureTags.Beast] },
        Inferno(Ravager, "Ravager", 4, Large, 19, 22, (20, 28), 165, 11, 6, 10, 1350, 2) with
            { Tags = [CreatureTags.Demon, CreatureTags.Beast], Capabilities = [new(Breath), new(SpellImmunity, Parameter: SpellTags.Fire)] },
        Inferno(Devil, "Devil", 5, Large, 26, 24, (36, 60), 180, 20, 11, 15, 3500, 1) with
            {
                UpgradesTo = ArchDevil, Capabilities = [new(Flying), new(NoEnemyRetaliation), new(SpellImmunity, Parameter: SpellTags.Fire)],
                Cost = new ResourceSet(Gold: 3500, Mercury: 1),
            },
        Inferno(ArchDevil, "Arch devil", 5, Large, 28, 26, (40, 65), 210, 24, 11, 16, 4500, 1) with
            {
                Capabilities = [new(Flying), new(NoEnemyRetaliation), new(Relentless, 10), new(SpellImmunity, Parameter: SpellTags.Fire)],
                Cost = new ResourceSet(Gold: 4500, Mercury: 2),
            },

        Neutral(Wolf, "Wolf", 1, Small, 6, 4, (3, 5), 12, 14, 7, 13, 60, 12) with
            { Tags = [CreatureTags.Beast], Capabilities = [new(PackHunter, 20)] },
        Neutral(Bandit, "Bandit", 2, Small, 9, 7, (5, 9), 25, 11, 6, 11, 150, 8) with { Tags = [CreatureTags.Human] },
        Neutral(Troll, "Troll", 3, Medium, 14, 10, (10, 16), 55, 11, 6, 9, 500, 4) with
            { Capabilities = [new(UnlimitedRetaliation)] },
        Neutral(Wyvern, "Wyvern", 4, Large, 16, 15, (16, 24), 120, 18, 8, 12, 1100, 2) with
            { Tags = [CreatureTags.Beast], Capabilities = [new(Flying)] },
        new CreatureDefinition(RedDragon, "Red dragon", 5, Large)
        {
            Town = TownIds.DragonUtopia, Attack = 28, Defense = 26, MinDamage = 40, MaxDamage = 50, Health = 250,
            Speed = 20, MapSpeed = 10, Initiative = 14, WeeklyGrowth = 1,
            Cost = new ResourceSet(Gold: 4000, Sulfur: 2),
            Tags = [CreatureTags.Dragon], Capabilities = [new(Flying), new(Breath), new(SpellImmunity, Parameter: SpellTags.Fire)],
        },
    ];

    private static CreatureDefinition Haven(
        string id, string name, int tier, CreatureSize size, int attack, int defense, (int Min, int Max) damage,
        int health, int speed, int mapSpeed, int initiative, int gold, int growth) =>
        Create(TownIds.Haven, id, name, tier, size, attack, defense, damage, health, speed, mapSpeed, initiative, gold, growth)
            with { Tags = [CreatureTags.Human, CreatureTags.Holy] };

    private static CreatureDefinition Inferno(
        string id, string name, int tier, CreatureSize size, int attack, int defense, (int Min, int Max) damage,
        int health, int speed, int mapSpeed, int initiative, int gold, int growth) =>
        Create(TownIds.Inferno, id, name, tier, size, attack, defense, damage, health, speed, mapSpeed, initiative, gold, growth)
            with { Tags = [CreatureTags.Demon] };

    private static CreatureDefinition Neutral(
        string id, string name, int tier, CreatureSize size, int attack, int defense, (int Min, int Max) damage,
        int health, int speed, int mapSpeed, int initiative, int gold, int growth) =>
        Create(null, id, name, tier, size, attack, defense, damage, health, speed, mapSpeed, initiative, gold, growth);

    private static CreatureDefinition Create(
        string? town, string id, string name, int tier, CreatureSize size, int attack, int defense, (int Min, int Max) damage,
        int health, int speed, int mapSpeed, int initiative, int gold, int growth) =>
        new(id, name, tier, size)
        {
            Town = town,
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
        };
}
