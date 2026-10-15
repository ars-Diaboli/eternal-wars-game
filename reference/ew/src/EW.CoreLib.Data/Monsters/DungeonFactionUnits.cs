using EW.CoreLib.Types;

namespace EW.CoreLib.Data.Monsters;


public partial class DungeonFactionUnits
{
    #region Tier1

    public static UnitData Troglodyte = new UnitData
    {
        UnitName = "Troglodyte", MaxHealth = 10,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier1,
        MinDamage = 10, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 10,
    };
    public static UnitData DeepTroglodyte = new UnitData
    {
        UnitName = "Deep Troglodyte", MaxHealth = 10,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier1,
        MinDamage = 10, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 10,
    };
    
    public static UnitData Scout = new UnitData
    {
        UnitName = "Scout", MaxHealth = 10,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier1,
        MinDamage = 10, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 10,
    };
    public static UnitData Stalker = new UnitData
    {
        UnitName = "Stalker", MaxHealth = 10,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier1,
        MinDamage = 10, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 10,
    };
    
    #endregion Tier1
    
    #region Tier2
    
    public static UnitData Medusa = new UnitData
    {
        UnitName = "Medusa", MaxHealth = 10,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier2,
        MinDamage = 10, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = true,
        Shots = 0, Experience = 10,
    };
    public static UnitData PutridMedusa = new UnitData
    {
        UnitName = "Putrid Medusa", MaxHealth = 10,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier2,
        MinDamage = 10, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = true,
        Shots = 0, Experience = 10,
    };
    
    public static UnitData Minotaur = new UnitData
    {
        UnitName = "Minotaur", MaxHealth = 10,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier2,
        MinDamage = 10, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 10,
    };
    public static UnitData MinotaurGuard = new UnitData
    {
        UnitName = "Minotaur Guard", MaxHealth = 10,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier2,
        MinDamage = 10, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = false,
        Shots = 0, Experience = 10,
    };
    
    #endregion Tier2
    
    #region Tier3
                    
    public static UnitData DarkRaider = new UnitData
    {
        UnitName = "Dark Raider", MaxHealth = 10,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier3,
        MinDamage = 10, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = true,
        Shots = 0, Experience = 10,
    };
    public static UnitData GrimRaider = new UnitData
    {
        UnitName = "Grim Raider", MaxHealth = 10,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier3,
        MinDamage = 10, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = true,
        Shots = 0, Experience = 10,
    };
    
    public static UnitData ShadowMistress = new UnitData
    {
        UnitName = "Shadow Mistress", MaxHealth = 10,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier3,
        MinDamage = 10, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = true,
        Shots = 0, Experience = 10,
    };
    public static UnitData ShadowMatriarch = new UnitData
    {
        UnitName = "Shadow Matriarch", MaxHealth = 10,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier3,
        MinDamage = 10, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = true,
        Shots = 0, Experience = 10,
    };
    
    #endregion Tier3
    
    #region Tier4
    
    public static UnitData Manticore = new UnitData
    {
        UnitName = "Manticore", MaxHealth = 10,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier4,
        MinDamage = 10, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 10,
    };
    public static UnitData Scorpicore = new UnitData
    {
        UnitName = "Scorpicore", MaxHealth = 10,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier4,
        MinDamage = 10, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 10,
    };
        
    public static UnitData Spectator = new UnitData
    {
        UnitName = "Spectator", MaxHealth = 10,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier4,
        MinDamage = 10, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = true,
        Shots = 0, Experience = 10,
    };
    public static UnitData Berholder = new UnitData
    {
        UnitName = "Berholder", MaxHealth = 10,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier4,
        MinDamage = 10, MaxDamage = 10,
        Attack = 10, Defense = 10,
        Speed = 10, Initiative = 10,
        IsFlying = false, IsRanged = true,
        Shots = 0, Experience = 10,
    };
    
    #endregion Tier4
    
    public static UnitData ShadowHorror = new UnitData
    {
        UnitName = "Shadow Horror", MaxHealth = 250,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier5,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 200,
    };
    public static UnitData ShadowReaper = new UnitData
    {
        UnitName = "Shadow Reaper", MaxHealth = 250,
        Faction = Faction.Dungeon, Tier = UnitTier.Tier5,
        MinDamage = 50, MaxDamage = 50,
        Attack = 30, Defense = 30,
        Speed = 30, Initiative = 16,
        IsFlying = true, IsRanged = false,
        Shots = 0, Experience = 200,
    };

}