using System.Collections.Generic;
using EW.Scripts.Calendar;
using EW.Scripts.DataTypes;
using EW.Scripts.DataTypes.Enums;
using EW.Scripts.Messaging;
using EW.Scripts.Players;
using EW.Scripts.TechTree;
using EW.Scripts.WorldMap;
using Godot;

namespace EW.Scripts;

public partial class Town : CapturableMapObject, IFactionOwned, IArmyOwner, IHandleEvent<NewDay>, IHandleEvent<NewWeek>
{
    [Export] public Faction Faction { get; set; } = Faction.Neutral;
    [Export]
    public Godot.Collections.Array<ArmyUnitStack> Army
    {
        get => _army;
        set => _army = ArmyUtils.Normalize(value);
    }

    [Export]
    public Godot.Collections.Array<TownBuildingLevel> BuildingLevels
    {
        get => _buildingLevels;
        set => _buildingLevels = NormalizeBuildingLevels(value);
    }
    [Export] public int LastBuildDay { get; private set; }
    [Export] public int LastDwellingGrowthWeek { get; private set; }
    [Export] public int LastDwellingGrowthMonth { get; private set; }

    private Godot.Collections.Array<ArmyUnitStack> _army = ArmyUtils.CreateEmptyArmy();
    private Godot.Collections.Array<TownBuildingLevel> _buildingLevels = new();
    private int _currentDaysPassed = 1;
    private int _currentWeek;
    private int _currentMonth;

    public Town()
    {
        MapObjectSize = new Vector2I(7, 7);
    }

    public override void _Ready()
    {
        Army = _army;
        BuildingLevels = _buildingLevels;
        base._Ready();
        SyncCalendarState();
    }

    public bool HasBuilding(TownBuildingType buildingType)
    {
        return GetBuildingLevel(buildingType) > 0;
    }

    public int GetBuildingLevel(TownBuildingType buildingType)
    {
        if (buildingType == TownBuildingType.None)
        {
            return 0;
        }

        foreach (TownBuildingLevel buildingLevel in _buildingLevels)
        {
            if (buildingLevel != null && buildingLevel.BuildingType == buildingType)
            {
                return buildingLevel.Level;
            }
        }

        return 0;
    }

    public bool CanBuild(TownBuildingType buildingType, int maxLevel)
    {
        return buildingType != TownBuildingType.None
            && maxLevel > 0
            && GetBuildingLevel(buildingType) < maxLevel;
    }

    public bool TryBuild(TownBuildingType buildingType, int maxLevel)
    {
        int currentLevel = GetBuildingLevel(buildingType);
        if (!CanBuild(buildingType, maxLevel))
        {
            return false;
        }

        SetBuildingLevel(buildingType, currentLevel + 1);
        GD.Print($"{Name}: {GetBuildingDisplayName(buildingType)} upgraded to level {currentLevel + 1}.");
        return true;
    }

    public bool CanBuildToday => LastBuildDay != _currentDaysPassed;

    public void MarkBuiltToday() => LastBuildDay = _currentDaysPassed;

    public override IReadOnlyDictionary<ResourceType, int> GetTurnIncome()
    {
        Dictionary<ResourceType, int> income = new();

        int townHallLevel = GetBuildingLevel(TownBuildingType.TownHall);
        if (townHallLevel > 0)
        {
            income[ResourceType.Gold] = townHallLevel switch
            {
                1 => 500,
                2 => 1000,
                _ => 2000
            };
        }

        if (GetBuildingLevel(TownBuildingType.Marketplace) >= 2)
        {
            income[ResourceType.Ore] = 1;
            income[ResourceType.Wood] = 1;
        }

        return income;
    }

    public override void OnOwnerTurnStarted(GamePlayer activePlayer)
    {
    }

    public void Handle(NewDay eventData)
    {
        if (eventData == null)
        {
            return;
        }

        _currentDaysPassed = eventData.DaysPassed;
        _currentWeek = eventData.CurrentWeek;
        _currentMonth = eventData.CurrentMonth;
    }

    public void Handle(NewWeek eventData)
    {
        if (eventData == null)
        {
            return;
        }

        _currentDaysPassed = eventData.DaysPassed;
        _currentWeek = eventData.CurrentWeek;
        _currentMonth = eventData.CurrentMonth;

        if (LastDwellingGrowthWeek == _currentWeek && LastDwellingGrowthMonth == _currentMonth)
        {
            return;
        }

        ProduceWeeklyDwellingUnits();
        LastDwellingGrowthWeek = _currentWeek;
        LastDwellingGrowthMonth = _currentMonth;
    }

    public override void OnPlayerInteract(Heroes.Hero playerHero)
    {
        GamePlayer previousOwner = OwnerPlayer;
        base.OnPlayerInteract(playerHero);

        if (!ReferenceEquals(OwnerPlayer, previousOwner))
        {
            GD.Print($"{Name}: town captured by {OwnerPlayer}.");
        }

        if (IsPlayerHero(playerHero))
        {
            GetTree().CurrentScene?.GetNodeOrNull<WorldMapController>("WorldMapController")?.OpenTown(this);
        }
    }

    private bool IsPlayerHero(Heroes.Hero hero)
    {
        TurnManager turnManager = GetTree().CurrentScene?.GetNodeOrNull<TurnManager>("TurnManager");
        return turnManager?.IsLocalPlayer(turnManager.GetPlayerForHero(hero)) == true;
    }

    private static string GetBuildingDisplayName(TownBuildingType buildingType)
    {
        return buildingType switch
        {
            TownBuildingType.TownHall => "Town Hall",
            TownBuildingType.Fort => "Fort",
            TownBuildingType.Blacksmith => "Blacksmith",
            TownBuildingType.Marketplace => "Marketplace",
            TownBuildingType.MagesGuild => "Mages Guild",
            TownBuildingType.Tavern => "Tavern",
            _ => buildingType.ToString()
        };
    }

    public int GetBuiltDwellingCount(TownTechTreeTheme theme)
    {
        if (theme == null)
        {
            return 0;
        }

        int count = 0;
        foreach (TownDwellingDefinition dwelling in theme.GetDwellingDefinitions())
        {
            if (dwelling != null && GetBuildingLevel(dwelling.BuildingType) > 0)
            {
                count++;
            }
        }

        return count;
    }

    public bool HasDwellingOfTier(UnitTier unitTier, TownTechTreeTheme theme)
    {
        if (theme == null)
        {
            return false;
        }

        foreach (TownDwellingDefinition dwelling in theme.GetDwellingDefinitions())
        {
            if (dwelling == null || dwelling.UnitTier != unitTier)
            {
                continue;
            }

            if (GetBuildingLevel(dwelling.BuildingType) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void SetBuildingLevel(TownBuildingType buildingType, int level)
    {
        foreach (TownBuildingLevel buildingLevel in _buildingLevels)
        {
            if (buildingLevel != null && buildingLevel.BuildingType == buildingType)
            {
                buildingLevel.Level = level;
                return;
            }
        }

        _buildingLevels.Add(new TownBuildingLevel
        {
            BuildingType = buildingType,
            Level = level
        });
    }

    private static Godot.Collections.Array<TownBuildingLevel> NormalizeBuildingLevels(Godot.Collections.Array<TownBuildingLevel> buildingLevels)
    {
        Godot.Collections.Array<TownBuildingLevel> normalizedLevels = new();

        if (buildingLevels == null)
        {
            return normalizedLevels;
        }

        foreach (TownBuildingLevel buildingLevel in buildingLevels)
        {
            if (buildingLevel == null || buildingLevel.BuildingType == TownBuildingType.None)
            {
                continue;
            }

            TownBuildingLevel existingLevel = null;
            foreach (TownBuildingLevel normalizedLevel in normalizedLevels)
            {
                if (normalizedLevel.BuildingType == buildingLevel.BuildingType)
                {
                    existingLevel = normalizedLevel;
                    break;
                }
            }

            if (existingLevel != null)
            {
                existingLevel.Level = Mathf.Max(existingLevel.Level, buildingLevel.Level);
                continue;
            }

            normalizedLevels.Add(new TownBuildingLevel
            {
                BuildingType = buildingLevel.BuildingType,
                Level = Mathf.Max(buildingLevel.Level, 0)
            });
        }

        return normalizedLevels;
    }

    private void ProduceWeeklyDwellingUnits()
    {
        TownTechTreeTheme theme = ResolveTechTreeTheme();
        if (theme == null)
        {
            return;
        }

        foreach (TownDwellingDefinition dwelling in theme.GetDwellingDefinitions())
        {
            if (dwelling?.ProducedUnit == null)
            {
                continue;
            }

            int buildingLevel = GetBuildingLevel(dwelling.BuildingType);
            if (buildingLevel <= 0)
            {
                continue;
            }

            int growth = buildingLevel >= 2
                ? dwelling.WeeklyGrowthLevel2
                : dwelling.WeeklyGrowthLevel1;

            if (growth <= 0)
            {
                continue;
            }

            if (TryAddUnitsToArmy(dwelling.ProducedUnit, growth))
            {
                GD.Print($"{Name}: produced {growth} {dwelling.ProducedUnit.UnitName} from {dwelling.DisplayName}.");
            }
        }
    }

    private bool TryAddUnitsToArmy(EW.Engine.UnitData unit, int amount)
    {
        if (unit == null || amount <= 0)
        {
            return false;
        }

        foreach (ArmyUnitStack armyUnitStack in _army)
        {
            if (armyUnitStack?.Unit == null)
            {
                continue;
            }

            if (ReferenceEquals(armyUnitStack.Unit, unit)
                || (armyUnitStack.Unit.UnitName == unit.UnitName && armyUnitStack.Unit.Tier == unit.Tier))
            {
                armyUnitStack.Amount += amount;
                return true;
            }
        }

        foreach (ArmyUnitStack armyUnitStack in _army)
        {
            if (armyUnitStack == null)
            {
                continue;
            }

            if (!armyUnitStack.IsEmpty)
            {
                continue;
            }

            armyUnitStack.Unit = unit;
            armyUnitStack.Amount = amount;
            return true;
        }

        GD.PushWarning($"{Name}: weekly dwelling growth for {unit.UnitName} was lost because the town army has no free slot.");
        return false;
    }

    private TownTechTreeTheme ResolveTechTreeTheme()
    {
        return TownTechTreeTheme.GetDefault(Faction);
    }

    private void SyncCalendarState()
    {
        GameCalendar calendar = ResolveCalendar();
        if (calendar == null)
        {
            return;
        }

        _currentDaysPassed = calendar.DaysPassed;
        _currentWeek = calendar.CurrentWeek;
        _currentMonth = calendar.CurrentMonth;
    }

    private GameCalendar ResolveCalendar()
    {
        Node sceneRoot = GetTree().CurrentScene ?? GetParent();
        if (sceneRoot == null)
        {
            return null;
        }

        if (sceneRoot is GameCalendar calendar)
        {
            return calendar;
        }

        return sceneRoot.GetNodeOrNull<GameCalendar>("GameCalendar");
    }
}
