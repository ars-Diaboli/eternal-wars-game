namespace EternalWars.Core.Domain.Creatures;

/// <summary>
/// Creatures that ship with the game. The roster and every stat live in the creature table
/// <c>src/Core/data/creatures.csv</c> (<see cref="CreatureTable"/>, ADR-044), embedded in this
/// assembly as the default (ADR-071); the Godot apps read an edited copy only when given <c>--creatures=</c>.
/// Eight playable towns with 9 lines over 5 tiers (2+2+2+2+1), every line with an upgrade (ADR-030, ADR-037), at most five
/// capabilities each (ADR-042); Conflux, Dragon Utopia and neutrals besides. Caster spell ids name spells the magic system
/// will define. The constants below are the ids code and tests refer to.
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
    public const string DireWolf = "neutral.dire-wolf";
    public const string Bandit = "neutral.bandit";
    public const string Troll = "neutral.troll";

    // Dragon Utopia --------------------------------------------------------------------------
    public const string Wyvern = "dragon-utopia.wyvern";
    public const string RedDragon = "dragon-utopia.red-dragon";

    /// <summary>Name of the embedded creature table in this assembly.</summary>
    public const string ResourceName = "EternalWars.Creatures.csv";

    /// <summary>Every built-in creature, in table order.</summary>
    public static IReadOnlyList<CreatureDefinition> All { get; } = CreatureTable.Parse(EmbeddedTable());

    /// <summary>Text of the embedded creature table (what the build shipped).</summary>
    public static string EmbeddedTable()
    {
        using var stream = typeof(BuiltInCreatures).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"The creature table '{ResourceName}' is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
