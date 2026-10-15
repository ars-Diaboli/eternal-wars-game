using EW.CoreLib.Types;

namespace EW.CoreLib.Data.Monsters;

public class CastleFactionUnits
{
    #region Tier1
    
    public static UnitData Footman = new UnitData
    {
        UnitName = "Footman", MaxHealth = 14,
        Faction = Faction.Temple, Tier = UnitTier.Tier1,
        MinDamage = 5, MaxDamage = 7,
        Attack = 6, Defense = 5,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 20,
    };    
    public static UnitData Crusader = new UnitData
    {
        UnitName = "Crusader", MaxHealth = 14,
        Faction = Faction.Temple, Tier = UnitTier.Tier1,
        MinDamage = 5, MaxDamage = 7,
        Attack = 6, Defense = 5,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 20,
    };    

    public static UnitData Pikeman = new UnitData
    {
        UnitName = "Pikeman", MaxHealth = 20,
        Faction = Faction.Temple, Tier = UnitTier.Tier1,
        MinDamage = 5, MaxDamage = 10,
        Attack = 8, Defense = 8,
        Speed = 12, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 20,
    };    
    public static UnitData Guardian = new UnitData
    {
        UnitName = "Guardian", MaxHealth = 20,
        Faction = Faction.Temple, Tier = UnitTier.Tier1,
        MinDamage = 5, MaxDamage = 10,
        Attack = 8, Defense = 10,
        Speed = 12, Initiative = 11,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 20,
    };   
    
    #endregion Tier1

    #region Tier2

    public static UnitData Zealot = new UnitData
    {
        UnitName = "Zealot", MaxHealth = 40,
        Faction = Faction.Temple, Tier = UnitTier.Tier2,
        MinDamage = 7, MaxDamage = 13,
        Attack = 12, Defense = 12,
        Speed = 11, Initiative = 11,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 50,
    };    
    public static UnitData Inquisitor = new UnitData
    {
        UnitName = "Inquisitor", MaxHealth = 45,
        Faction = Faction.Temple, Tier = UnitTier.Tier2,
        MinDamage = 11, MaxDamage = 13,
        Attack = 13, Defense = 12,
        Speed = 13, Initiative = 11,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 50,
    };    
        
    public static UnitData Archer = new UnitData
    {
        UnitName = "Archer", MaxHealth = 12,
        Faction = Faction.Temple, Tier = UnitTier.Tier1,
        MinDamage = 4, MaxDamage = 6,
        Attack = 8, Defense = 6,
        Speed = 7, Initiative = 8,
        IsFlying = false, IsRanged = true,
        Shots = 12, Experience = 20,
    };  
    public static UnitData Marksman = new UnitData
    {
        UnitName = "Marksman", MaxHealth = 15,
        Faction = Faction.Temple, Tier = UnitTier.Tier1,
        MinDamage = 4, MaxDamage = 8,
        Attack = 9, Defense = 6,
        Speed = 8, Initiative = 9,
        IsFlying = false, IsRanged = true,
        Shots = 12, Experience = 20,
    };    
    
    #endregion Tier2
    
    #region Tier3
        
    public static UnitData Monk = new UnitData
    {
        UnitName = "Monk", MaxHealth = 30,
        Faction = Faction.Temple, Tier = UnitTier.Tier3,
        MinDamage = 8, MaxDamage = 12,
        Attack = 13, Defense = 9,
        Speed = 7, Initiative = 9,
        IsFlying = false, IsRanged = true,
        Shots = 12, Experience = 50,
    };
    public static UnitData Priest = new UnitData
    {
        UnitName = "Priest", MaxHealth = 35,
        Faction = Faction.Temple, Tier = UnitTier.Tier3,
        MinDamage = 8, MaxDamage = 12,
        Attack = 13, Defense = 11,
        Speed = 9, Initiative = 11,
        IsFlying = false, IsRanged = true,
        Shots = 12, Experience = 50,
    };
    
    public static UnitData Griffin = new UnitData
    {
        UnitName = "Griffin", MaxHealth = 100,
        Faction = Faction.Temple, Tier = UnitTier.Tier3,
        MinDamage = 16, MaxDamage = 20,
        Attack = 19, Defense = 16,
        Speed = 20, Initiative = 12,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 150,
    };
    public static UnitData RoyalGriffin = new UnitData
    {
        UnitName = "Royal Griffin", MaxHealth = 110,
        Faction = Faction.Temple, Tier = UnitTier.Tier3,
        MinDamage = 16, MaxDamage = 22,
        Attack = 20, Defense = 17,
        Speed = 24, Initiative = 14,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 150,
    };
    
    #endregion Tier3
    
    #region Tier4
    
    public static UnitData Lancer = new UnitData
    {
        UnitName = "Lancer", MaxHealth = 120,
        Faction = Faction.Temple, Tier = UnitTier.Tier4,
        MinDamage = 16, MaxDamage = 24,
        Attack = 18, Defense = 19,
        Speed = 18, Initiative = 12,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 150,
    };    
    public static UnitData Champion = new UnitData
    {
        UnitName = "Champion", MaxHealth = 120,
        Faction = Faction.Temple, Tier = UnitTier.Tier4,
        MinDamage = 16, MaxDamage = 24,
        Attack = 18, Defense = 19,
        Speed = 18, Initiative = 12,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 150,
    };    
    
    public static UnitData Paladin = new UnitData
    {
        UnitName = "Paladin", MaxHealth = 100,
        Faction = Faction.Temple, Tier = UnitTier.Tier4,
        MinDamage = 12, MaxDamage = 16,
        Attack = 16, Defense = 20,
        Speed = 14, Initiative = 14,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 150,
    };
    public static UnitData SacredWarrior = new UnitData
    {
        UnitName = "Sacred Warrior", MaxHealth = 100,
        Faction = Faction.Temple, Tier = UnitTier.Tier4,
        MinDamage = 12, MaxDamage = 16,
        Attack = 16, Defense = 20,
        Speed = 14, Initiative = 14,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 150,
    };
    
    #endregion Tier4
    
    public static UnitData RadiantSpirit = new UnitData
    {
        UnitName = "Radiant Spirit", MaxHealth = 175,
        Faction = Faction.Temple, Tier = UnitTier.Tier5,
        MinDamage = 50, MaxDamage = 50,
        Attack = 22, Defense = 22,
        Speed = 25, Initiative = 15,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 150,
    };    
    public static UnitData Seraph = new UnitData
    {
        UnitName = "Seraph", MaxHealth = 250,
        Faction = Faction.Temple, Tier = UnitTier.Tier5,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    
    // public static UnitData Ballista = new UnitData
    // {
    //     UnitName = "Ballista", MaxHealth = 50,
    //     Faction = Faction.Temple, Tier = UnitTier.Tier3,
    //     MinDamage = 10, MaxDamage = 12,
    //     Attack = 8, Defense = 6,
    //     Speed = 4, Initiative = 6,
    //     IsFlying = false, IsRanged = true,
    //     Shots = 8, Experience = 60,
    // };    
    // public static UnitData Flamethrower = new UnitData
    // {
    //     UnitName = "Flamethrower", MaxHealth = 50,
    //     Faction = Faction.Temple, Tier = UnitTier.Tier3,
    //     MinDamage = 10, MaxDamage = 14,
    //     Attack = 10, Defense = 6,
    //     Speed = 4, Initiative = 8,
    //     IsFlying = false, IsRanged = true,
    //     Shots = 8, Experience = 60,
    // };    
}