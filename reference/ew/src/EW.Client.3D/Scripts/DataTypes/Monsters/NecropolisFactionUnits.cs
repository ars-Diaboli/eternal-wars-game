using EW.Engine;
using EW.Scripts.DataTypes.Enums;
using Godot;

namespace EW.Scripts.DataTypes;

public partial class NecropolisFactionUnits : Node
{
    #region Tier1
    
    public static UnitData Skeleton = new UnitData
    {
        UnitName = "Devil", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier1,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    public static UnitData SkeletonWarrior = new UnitData
    {
        UnitName = "Skeleton warrior", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier1,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 200,
    };
        
    public static UnitData Ghoul = new UnitData
    {
        UnitName = "Ghoul", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier1,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    public static UnitData Alghoul = new UnitData
    {
        UnitName = "Alghoul", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier1,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    
    public static UnitData PlagueBearer = new UnitData
    {
        UnitName = "Plague Bearer", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier1,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    public static UnitData FleshGolem = new UnitData
    {
        UnitName = "Flesh Golem", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier1,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    
    #endregion Tier1
    
    #region Tier2

    public static UnitData Specter = new UnitData
    {
        UnitName = "Specter", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier2,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    public static UnitData Banshee = new UnitData
    {
        UnitName = "Banshee", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier2,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 200,
    };

    public static UnitData Vampire = new UnitData
    {
        UnitName = "Vampire", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier2,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    public static UnitData VampireLord = new UnitData
    {
        UnitName = "Vampire Lord", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier2,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    
    public static UnitData DeathWeaver = new UnitData
    {
        UnitName = "Death Weaver", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier2,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    public static UnitData FateWeaver = new UnitData
    {
        UnitName = "Fate Weaver", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier2,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    
    #endregion Tier2

    #region Tier3

    public static UnitData DarkKnight = new UnitData
    {
        UnitName = "Dark Knight", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier3,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    public static UnitData DeathKnight = new UnitData
    {
        UnitName = "Death Knight", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier3,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    
    public static UnitData Lich = new UnitData
    {
        UnitName = "Lich", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier3,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = true,
        Shots = 12, Experience = 200,
    };
    public static UnitData ArchLich = new UnitData
    {
        UnitName = "ArchLich", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier3,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = true,
        Shots = 16, Experience = 200,
    };
        
    public static UnitData Wight = new UnitData
    {
        UnitName = "Wight", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier3,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = true,
        Shots = 0, Experience = 200,
    };
    public static UnitData Wraith = new UnitData
    {
        UnitName = "Wraith", MaxHealth = 10,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier3,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = true,
        Shots = 0, Experience = 200,
    };
    
    #endregion Tier3
    
    public static UnitData BoneDragon = new UnitData
    {
        UnitName = "Bone Dragon", MaxHealth = 150,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier4,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    public static UnitData GhostDragon = new UnitData
    {
        UnitName = "Ghost Dragon", MaxHealth = 200,
        Faction = Faction.Necropolis, Tier = UnitTier.Tier4,
        MinDamage = 7, MaxDamage = 8,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 200,
    };
}