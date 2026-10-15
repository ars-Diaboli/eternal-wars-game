using EW.Engine;
using EW.Scripts.DataTypes.Enums;
using Godot;

namespace EW.Scripts.DataTypes;

public partial class AcademyFactionUnits : Node
{
    #region Tier1
    
    public static UnitData Gremlin = new UnitData
    {
        UnitName = "Gremlin", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier1,
        MinDamage = 50, MaxDamage = 50,
        Attack = 22, Defense = 22,
        Speed = 25, Initiative = 15,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 150,
    };    
    public static UnitData GremlinMaster = new UnitData
    {
        UnitName = "Gremlin Master", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier1,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = false, IsRanged = true,
        Shots = 0, Experience = 200,
    };
        
    public static UnitData Gargoyle = new UnitData
    {
        UnitName = "Gargoyle", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier1,
        MinDamage = 50, MaxDamage = 50,
        Attack = 22, Defense = 22,
        Speed = 25, Initiative = 15,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 150,
    };    
    public static UnitData ObsidianGargoyle = new UnitData
    {
        UnitName = "Obsidian Gargoyle", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier1,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 200,
    };
            
    public static UnitData Defender = new UnitData
    {
        UnitName = "Defender", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier1,
        MinDamage = 50, MaxDamage = 50,
        Attack = 22, Defense = 22,
        Speed = 25, Initiative = 15,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 150,
    };    
    public static UnitData ShieldGuard = new UnitData
    {
        UnitName = "Shield Guard", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier1,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    
    #endregion Tier1

    #region Tier2
        
    public static UnitData IronGolem = new UnitData
    {
        UnitName = "Iron Golem", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier2,
        MinDamage = 50, MaxDamage = 50,
        Attack = 22, Defense = 22,
        Speed = 25, Initiative = 15,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 150,
    };    
    public static UnitData AnimatedArmor = new UnitData
    {
        UnitName = "Animated Armor", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier2,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    
    public static UnitData Sorcerer = new UnitData
    {
        UnitName = "Iron Golem", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier2,
        MinDamage = 50, MaxDamage = 50,
        Attack = 22, Defense = 22,
        Speed = 25, Initiative = 15,
        IsFlying = false, IsRanged = true,
        Shots = 12, Experience = 150,
    };    
    public static UnitData Mage = new UnitData
    {
        UnitName = "Steel Golem", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier2,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = false, IsRanged = true,
        Shots = 20, Experience = 200,
    };
    
    public static UnitData SnowMaiden = new UnitData
    {
        UnitName = "Snow Maiden", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier2,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = false, IsRanged = true,
        Shots = 20, Experience = 200,
    };
    public static UnitData Enchantress = new UnitData
    {
        UnitName = "Enchantress", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier2,
        MinDamage = 50, MaxDamage = 50,
        Attack = 22, Defense = 22,
        Speed = 25, Initiative = 15,
        IsFlying = false, IsRanged = true,
        Shots = 12, Experience = 150,
    };    
    
    #endregion Tier2

    #region Tier3

    public static UnitData Djinn = new UnitData
    {
        UnitName = "Djinn", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier3,
        MinDamage = 50, MaxDamage = 50,
        Attack = 22, Defense = 22,
        Speed = 25, Initiative = 15,
        IsFlying = true, IsRanged = false,
        Shots = 12, Experience = 150,
    };    
    public static UnitData DjinnSultan = new UnitData
    {
        UnitName = "Djinn Sultan", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier3,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = true, IsRanged = false,
        Shots = 20, Experience = 200,
    };
    
    public static UnitData Naga = new UnitData
    {
        UnitName = "Naga", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier3,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = false, IsRanged = false,
        Shots = 20, Experience = 200,
    };
    public static UnitData NagaQueen = new UnitData
    {
        UnitName = "Naga Queen", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier3,
        MinDamage = 50, MaxDamage = 50,
        Attack = 22, Defense = 22,
        Speed = 25, Initiative = 15,
        IsFlying = false, IsRanged = false,
        Shots = 12, Experience = 150,
    };    
    
    public static UnitData Thane = new UnitData
    {
        UnitName = "Thane", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier3,
        MinDamage = 50, MaxDamage = 50,
        Attack = 22, Defense = 22,
        Speed = 25, Initiative = 15,
        IsFlying = false, IsRanged = false,
        Shots = 12, Experience = 150,
    };    
    public static UnitData ThunderThane = new UnitData
    {
        UnitName = "Thunder Thane", MaxHealth = 10,
        Faction = Faction.Academy, Tier = UnitTier.Tier3,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = false, IsRanged = false,
        Shots = 20, Experience = 200,
    };
    
    #endregion Tier3
    
    public static UnitData Colossus = new UnitData
    {
        UnitName = "Colossus", MaxHealth = 175,
        Faction = Faction.Academy, Tier = UnitTier.Tier4,
        MinDamage = 50, MaxDamage = 50,
        Attack = 22, Defense = 22,
        Speed = 25, Initiative = 15,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 150,
    };    
    public static UnitData Titan = new UnitData
    {
        UnitName = "Titan", MaxHealth = 250,
        Faction = Faction.Academy, Tier = UnitTier.Tier4,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = false, IsRanged = true,
        Shots = 0, Experience = 200,
    };
}