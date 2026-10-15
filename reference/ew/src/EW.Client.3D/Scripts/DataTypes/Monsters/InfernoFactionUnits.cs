using EW.Engine;
using EW.Scripts.DataTypes.Enums;
using Godot;

namespace EW.Scripts.DataTypes;

public partial class InfernoFactionUnits : Node
{
    #region Tier1
    public static UnitData Inp = new UnitData
    {
        UnitName = "Inp", MaxHealth = 25,
        Faction = Faction.Inferno, Tier = UnitTier.Tier1,
        MinDamage = 5, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 15, Initiative = 10,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 25,
    };
    public static UnitData Familiar = new UnitData
    {
        UnitName = "Familiar", MaxHealth = 25,
        Faction = Faction.Inferno, Tier = UnitTier.Tier1,
        MinDamage = 5, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 15, Initiative = 10,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 25,
    };
        
    public static UnitData HellHound = new UnitData
    {
        UnitName = "Hell hound", MaxHealth = 25,
        Faction = Faction.Inferno, Tier = UnitTier.Tier1,
        MinDamage = 5, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 15, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 25,
    };
    public static UnitData Cerberus = new UnitData
    {
        UnitName = "Cerberus", MaxHealth = 25,
        Faction = Faction.Inferno, Tier = UnitTier.Tier1,
        MinDamage = 5, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 15, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 25,
    };
    
    public static UnitData HornedDemon = new UnitData
    {
        UnitName = "Horned Demon", MaxHealth = 25,
        Faction = Faction.Inferno, Tier = UnitTier.Tier1,
        MinDamage = 5, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 15, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 25,
    };
    public static UnitData HornedOverseer = new UnitData
    {
        UnitName = "Horned Overseer", MaxHealth = 25,
        Faction = Faction.Inferno, Tier = UnitTier.Tier1,
        MinDamage = 5, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 15, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 25,
    };
    
    #endregion Tier1
    #region Tier2
            
    public static UnitData Gog = new UnitData
    {
        UnitName = "Gog", MaxHealth = 25,
        Faction = Faction.Inferno, Tier = UnitTier.Tier2,
        MinDamage = 5, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 15, Initiative = 10,
        IsFlying = false, IsRanged = true,
        Shots = 0, Experience = 25,
    };
    public static UnitData Succubus = new UnitData
    {
        UnitName = "Succubus", MaxHealth = 25,
        Faction = Faction.Inferno, Tier = UnitTier.Tier2,
        MinDamage = 5, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 15, Initiative = 10,
        IsFlying = false, IsRanged = true,
        Shots = 0, Experience = 25,
    };
    
    public static UnitData HellCharger = new UnitData
    {
        UnitName = "Hell charger", MaxHealth = 25,
        Faction = Faction.Inferno, Tier = UnitTier.Tier2,
        MinDamage = 5, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 15, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 25,
    };
    public static UnitData Nightmare = new UnitData
    {
        UnitName = "Nightmare", MaxHealth = 25,
        Faction = Faction.Inferno, Tier = UnitTier.Tier2,
        MinDamage = 5, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 15, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 25,
    };

    public static UnitData Tormentor = new UnitData
    {
        UnitName = "Tormentor", MaxHealth = 25,
        Faction = Faction.Inferno, Tier = UnitTier.Tier2,
        MinDamage = 5, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 15, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 25,
    };
    public static UnitData Lacerator = new UnitData
    {
        UnitName = "Lacerator", MaxHealth = 25,
        Faction = Faction.Inferno, Tier = UnitTier.Tier2,
        MinDamage = 5, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 15, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 25,
    };
    
    #endregion Tier2
    #region Tier3

    public static UnitData PitFiend = new UnitData
    {
        UnitName = "Pit Fiend", MaxHealth = 250,
        Faction = Faction.Inferno, Tier = UnitTier.Tier3,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    public static UnitData PitLord = new UnitData
    {
        UnitName = "Pit Lord", MaxHealth = 250,
        Faction = Faction.Inferno, Tier = UnitTier.Tier3,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    
    public static UnitData Juggernaut = new UnitData
    {
        UnitName = "Juggernaut", MaxHealth = 250,
        Faction = Faction.Inferno, Tier = UnitTier.Tier3,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    public static UnitData Ravager = new UnitData
    {
        UnitName = "Ravager", MaxHealth = 250,
        Faction = Faction.Inferno, Tier = UnitTier.Tier3,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 200,
    };
        
    public static UnitData Breeder = new UnitData
    {
        UnitName = "Breeder", MaxHealth = 250,
        Faction = Faction.Inferno, Tier = UnitTier.Tier3,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    public static UnitData FelBreeder = new UnitData
    {
        UnitName = "Pit Lord", MaxHealth = 250,
        Faction = Faction.Inferno, Tier = UnitTier.Tier3,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    
    #endregion Tier3
    
    public static UnitData Devil = new UnitData
    {
        UnitName = "Devil", MaxHealth = 250,
        Faction = Faction.Inferno, Tier = UnitTier.Tier4,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    public static UnitData ArchDevil = new UnitData
    {
        UnitName = "Arch Devil", MaxHealth = 250,
        Faction = Faction.Inferno, Tier = UnitTier.Tier4,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 200,
    };
}