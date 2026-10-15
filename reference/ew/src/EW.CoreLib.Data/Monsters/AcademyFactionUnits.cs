using EW.CoreLib.Types;

namespace EW.CoreLib.Data.Monsters;

public partial class AcademyFactionUnits
{
    #region Tier1
    
    public static UnitData Gremlin = new UnitData
    {
        UnitName = "Gremlin", MaxHealth = 8,
        Faction = Faction.Academy, Tier = UnitTier.Tier1,
        MinDamage = 1, MaxDamage = 3,
        Attack = 6, Defense = 6,
        Speed = 6, Initiative = 3,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 10,
    };    
    public static UnitData GremlinMaster = new UnitData
    {
        UnitName = "Gremlin Master", MaxHealth = 9,
        Faction = Faction.Academy, Tier = UnitTier.Tier1,
        MinDamage = 1, MaxDamage = 3,
        Attack = 6, Defense = 6,
        Speed = 8, Initiative = 4,
        IsFlying = false, IsRanged = true,
        Shots = 12, Experience = 12,
    };
            
    public static UnitData Defender = new UnitData
    {
        UnitName = "Defender", MaxHealth = 14,
        Faction = Faction.Academy, Tier = UnitTier.Tier1,
        MinDamage = 2, MaxDamage = 3,
        Attack = 5, Defense = 7,
        Speed = 6, Initiative = 4,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 11,
    };    
    public static UnitData ShieldGuard = new UnitData
    {
        UnitName = "Shield Guard", MaxHealth = 14,
        Faction = Faction.Academy, Tier = UnitTier.Tier1,
        MinDamage = 2, MaxDamage = 3,
        Attack = 5, Defense = 9,
        Speed = 8, Initiative = 5,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 13,
    };
    
    #endregion Tier1

    #region Tier2
                
    public static UnitData Gargoyle = new UnitData
    {
        UnitName = "Gargoyle", MaxHealth = 20,
        Faction = Faction.Academy, Tier = UnitTier.Tier2,
        MinDamage = 3, MaxDamage = 5,
        Attack = 10, Defense = 12,
        Speed = 12, Initiative = 5,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 150,
    };    
    public static UnitData ObsidianGargoyle = new UnitData
    {
        UnitName = "Obsidian Gargoyle", MaxHealth = 25,
        Faction = Faction.Academy, Tier = UnitTier.Tier2,
        MinDamage = 4, MaxDamage = 5,
        Attack = 12, Defense = 13,
        Speed = 16, Initiative = 6,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    
    public static UnitData IronGolem = new UnitData
    {
        UnitName = "Iron Golem", MaxHealth = 30,
        Faction = Faction.Academy, Tier = UnitTier.Tier2,
        MinDamage = 4, MaxDamage = 6,
        Attack = 12, Defense = 15,
        Speed = 10, Initiative = 5,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 150,
    };    
    public static UnitData AnimatedArmor = new UnitData
    {
        UnitName = "Animated Armor", MaxHealth = 35,
        Faction = Faction.Academy, Tier = UnitTier.Tier2,
        MinDamage = 5, MaxDamage = 7,
        Attack = 14, Defense = 15,
        Speed = 11, Initiative = 7,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    
    #endregion Tier2

    #region Tier3
        
    public static UnitData Sorcerer = new UnitData
    {
        UnitName = "Iron Golem", MaxHealth = 30,
        Faction = Faction.Academy, Tier = UnitTier.Tier2,
        MinDamage = 10, MaxDamage = 14,
        Attack = 18, Defense = 15,
        Speed = 6, Initiative = 6,
        IsFlying = false, IsRanged = true,
        Shots = 12, Experience = 150,
    };    
    public static UnitData Mage = new UnitData
    {
        UnitName = "Steel Golem", MaxHealth = 40,
        Faction = Faction.Academy, Tier = UnitTier.Tier2,
        MinDamage = 10, MaxDamage = 16,
        Attack = 20, Defense = 16,
        Speed = 9, Initiative = 6,
        IsFlying = false, IsRanged = true,
        Shots = 20, Experience = 200,
    };
    
    public static UnitData SnowMaiden = new UnitData
    {
        UnitName = "Snow Maiden", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier3,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = false, IsRanged = true,
        Shots = 20, Experience = 200,
    };
    public static UnitData Enchantress = new UnitData
    {
        UnitName = "Enchantress", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier3,
        MinDamage = 50, MaxDamage = 50,
        Attack = 22, Defense = 22,
        Speed = 25, Initiative = 15,
        IsFlying = false, IsRanged = true,
        Shots = 12, Experience = 150,
    };    
    
    #endregion Tier3
    
    #region Tier4
    
    public static UnitData Djinn = new UnitData
    {
        UnitName = "Djinn", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier4,
        MinDamage = 50, MaxDamage = 50,
        Attack = 22, Defense = 22,
        Speed = 25, Initiative = 15,
        IsFlying = true, IsRanged = false,
        Shots = 12, Experience = 150,
    };    
    public static UnitData DjinnSultan = new UnitData
    {
        UnitName = "Djinn Sultan", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier4,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = true, IsRanged = false,
        Shots = 20, Experience = 200,
    };
    
    public static UnitData Naga = new UnitData
    {
        UnitName = "Naga", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier4,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = false, IsRanged = false,
        Shots = 20, Experience = 200,
    };
    public static UnitData NagaQueen = new UnitData
    {
        UnitName = "Naga Queen", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier4,
        MinDamage = 50, MaxDamage = 50,
        Attack = 22, Defense = 22,
        Speed = 25, Initiative = 15,
        IsFlying = false, IsRanged = false,
        Shots = 12, Experience = 150,
    };    
    
    #endregion Tier4

    public static UnitData Colossus = new UnitData
    {
        UnitName = "Colossus", MaxHealth = 200,
        Faction = Faction.Academy, Tier = UnitTier.Tier5,
        MinDamage = 40, MaxDamage = 50,
        Attack = 22, Defense = 22,
        Speed = 25, Initiative = 15,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 150,
    };    
    public static UnitData Titan = new UnitData
    {
        UnitName = "Titan", MaxHealth = 300,
        Faction = Faction.Academy, Tier = UnitTier.Tier5,
        MinDamage = 40, MaxDamage = 50,
        Attack = 32, Defense = 36,
        Speed = 20, Initiative = 16,
        IsFlying = false, IsRanged = true,
        Shots = 16, Experience = 200,
    };
    
    // public static UnitData Thane = new UnitData
    // {
    //     UnitName = "Thane", MaxHealth = 10,
    //     Faction = Faction.Academy, Tier = UnitTier.Tier3,
    //     MinDamage = 50, MaxDamage = 50,
    //     Attack = 22, Defense = 22,
    //     Speed = 25, Initiative = 15,
    //     IsFlying = false, IsRanged = false,
    //     Shots = 12, Experience = 150,
    // };    
    // public static UnitData ThunderThane = new UnitData
    // {
    //     UnitName = "Thunder Thane", MaxHealth = 10,
    //     Faction = Faction.Academy, Tier = UnitTier.Tier3,
    //     MinDamage = 50, MaxDamage = 50,
    //     Attack = 30, Defense = 30,
    //     Speed = 30, Initiative = 16,
    //     IsFlying = false, IsRanged = false,
    //     Shots = 20, Experience = 200,
    // };
}