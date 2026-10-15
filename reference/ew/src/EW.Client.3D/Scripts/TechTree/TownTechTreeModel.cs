using System;
using System.Collections.Generic;
using System.Linq;
using EW.Scripts.DataTypes.Enums;
using Godot;

namespace EW.Scripts.TechTree;

public sealed class TownTechTreeModel
{
    public const int MaxBuiltDwellings = 7;

    private readonly TownBuildingType[] _commonBuildingOrder =
    [
        TownBuildingType.TownHall,
        TownBuildingType.Fort,
        TownBuildingType.Blacksmith,
        TownBuildingType.Marketplace,
        TownBuildingType.MagesGuild,
        TownBuildingType.Tavern
    ];

    public Godot.Collections.Array<TownTechTreeTheme> TechTreeThemes { get; set; } = new();
    public Town CurrentTown { get; private set; }
    public TurnManager TurnManager { get; set; }
    public string StatusMessage { get; private set; } = string.Empty;

    public void ShowTown(Town town)
    {
        EnsureDefaultThemes();
        CurrentTown = town;
        StatusMessage = string.Empty;
    }

    public void HideTown()
    {
        CurrentTown = null;
        StatusMessage = string.Empty;
    }

    public string GetTitle()
    {
        if (CurrentTown == null)
        {
            return "No Tech Tree";
        }

        return CurrentTheme != null
            ? $"{CurrentTown.Faction} Tech Tree"
            : "No Tech Tree";
    }

    public Texture2D GetBackgroundTexture()
    {
        return CurrentTheme?.BackgroundImage;
    }

    public string GetPlaceholderText()
    {
        if (CurrentTown == null)
        {
            return "This faction has no tech tree.";
        }

        return CurrentTheme != null
            ? $"{CurrentTown.Faction} Tech Tree Background"
            : "This faction has no tech tree.";
    }

    public bool ShouldShowPlaceholder()
    {
        return GetBackgroundTexture() == null;
    }

    public string GetStatusText()
    {
        return $"Built: {FormatBuiltBuildings()}\n{GetBuildLimitStatus()}{FormatStatusMessage()}";
    }

    public IReadOnlyList<TownTechTreeBuildingState> GetCommonBuildingStates()
    {
        return _commonBuildingOrder
            .Select(buildingType => CreateBuildingState(buildingType))
            .ToList();
    }

    public IReadOnlyList<TownTechTreeBuildingState> GetDwellingStates()
    {
        if (CurrentTheme == null)
        {
            return Array.Empty<TownTechTreeBuildingState>();
        }

        return CurrentTheme.GetDwellingDefinitions()
            .Where(dwelling => dwelling != null)
            .Select(dwelling => CreateBuildingState(dwelling.BuildingType))
            .ToList();
    }

    public void TryBuild(TownBuildingType buildingType)
    {
        if (CurrentTown == null)
        {
            return;
        }

        TownTechTreeTheme theme = CurrentTheme;
        int maxLevel = theme?.GetMaxLevel(buildingType) ?? 0;
        if (maxLevel <= 0)
        {
            StatusMessage = "Building is not available in this town.";
            return;
        }

        int nextLevel = CurrentTown.GetBuildingLevel(buildingType) + 1;
        if (TurnManager == null || CurrentTown.OwnerPlayer == null || !TurnManager.IsCurrentPlayer(CurrentTown.OwnerPlayer) || !TurnManager.CurrentPlayer.IsHuman)
        {
            StatusMessage = "Buildings can only be constructed during the owning player's turn.";
            return;
        }

        if (!CurrentTown.CanBuildToday)
        {
            StatusMessage = "This town has already built one structure today.";
            return;
        }

        string dwellingBuildBlocker = GetDwellingBuildBlocker(theme, buildingType);
        if (!string.IsNullOrWhiteSpace(dwellingBuildBlocker))
        {
            StatusMessage = dwellingBuildBlocker;
            return;
        }

        IReadOnlyList<TownBuildingRequirement> missingRequirements = GetMissingRequirements(theme, buildingType, nextLevel);
        if (missingRequirements.Count > 0)
        {
            StatusMessage = $"Requires {FormatRequirements(missingRequirements)}.";
            return;
        }

        IReadOnlyDictionary<ResourceType, int> buildCost = GetBuildCost(theme, buildingType, nextLevel);
        if (!TurnManager.CanAffordResources(CurrentTown.OwnerPlayer, buildCost))
        {
            StatusMessage = $"Not enough resources for {GetBuildingDisplayName(buildingType)} Lv.{nextLevel}.";
            return;
        }

        if (!CurrentTown.TryBuild(buildingType, maxLevel))
        {
            StatusMessage = "This building is already at its maximum level.";
            return;
        }

        TurnManager.TrySpendResources(CurrentTown.OwnerPlayer, buildCost);
        CurrentTown.MarkBuiltToday();
        StatusMessage = $"{GetBuildingDisplayName(buildingType)} upgraded to level {CurrentTown.GetBuildingLevel(buildingType)}.";
    }

    public TownTechTreeTheme CurrentTheme => ResolveTheme(CurrentTown?.Faction ?? Faction.Neutral);

    private TownTechTreeBuildingState CreateBuildingState(TownBuildingType buildingType)
    {
        if (CurrentTown == null)
        {
            return new TownTechTreeBuildingState
            {
                BuildingType = buildingType,
                DisplayName = GetBuildingDisplayName(buildingType),
                Visible = false
            };
        }

        TownTechTreeTheme theme = CurrentTheme;
        int maxLevel = theme?.GetMaxLevel(buildingType) ?? 0;
        bool supported = maxLevel > 0;
        string displayName = GetBuildingDisplayName(buildingType);

        if (!supported)
        {
            return new TownTechTreeBuildingState
            {
                BuildingType = buildingType,
                DisplayName = displayName,
                Visible = false,
                DwellingDefinition = theme?.GetDwellingDefinition(buildingType)
            };
        }

        int currentLevel = CurrentTown.GetBuildingLevel(buildingType);
        int nextLevel = Mathf.Clamp(currentLevel + 1, 1, maxLevel);
        bool maxed = currentLevel >= maxLevel;
        bool builtToday = !CurrentTown.CanBuildToday;
        string dwellingBlocker = GetDwellingBuildBlocker(theme, buildingType);
        IReadOnlyList<TownBuildingRequirement> missingRequirements = GetMissingRequirements(theme, buildingType, nextLevel);
        bool requirementsMet = missingRequirements.Count == 0;
        IReadOnlyDictionary<ResourceType, int> buildCost = GetBuildCost(theme, buildingType, nextLevel);
        bool canAfford = !maxed && CurrentTown.OwnerPlayer != null && TurnManager?.CanAffordResources(CurrentTown.OwnerPlayer, buildCost) == true;

        string buttonText = maxed
            ? $"{displayName} Lv.{currentLevel}/{maxLevel}"
            : requirementsMet
                ? $"{(currentLevel == 0 ? "Build" : "Upgrade")} {displayName} Lv.{nextLevel}/{maxLevel}"
                : $"{displayName} Lv.{nextLevel}/{maxLevel} Requires {FormatRequirements(missingRequirements)}";

        string tooltipText = maxed
            ? "Already at maximum level."
            : !string.IsNullOrWhiteSpace(dwellingBlocker)
                ? dwellingBlocker
                : !requirementsMet
                    ? $"Requires {FormatRequirements(missingRequirements)}."
                    : !canAfford
                        ? $"Not enough resources: {FormatCosts(buildCost)}."
                        : builtToday
                            ? "This town has already built one structure today."
                            : theme?.GetDwellingDefinition(buildingType) is TownDwellingDefinition dwelling
                                ? $"{dwelling.ProducedUnit?.UnitName ?? "No unit assigned"} ({dwelling.UnitTier})"
                                : string.Empty;

        TownTechTreeMarkerState markerState;
        if (builtToday)
        {
            markerState = TownTechTreeMarkerState.DailyLimit;
        }
        else if (currentLevel > 0)
        {
            markerState = TownTechTreeMarkerState.Built;
        }
        else if (!string.IsNullOrWhiteSpace(dwellingBlocker) || missingRequirements.Count > 0 || !canAfford)
        {
            markerState = TownTechTreeMarkerState.MissingResources;
        }
        else
        {
            markerState = TownTechTreeMarkerState.Available;
        }

        return new TownTechTreeBuildingState
        {
            BuildingType = buildingType,
            DisplayName = displayName,
            Visible = true,
            ButtonText = buttonText,
            TooltipText = tooltipText,
            Disabled = maxed || builtToday || !requirementsMet || !canAfford || !string.IsNullOrWhiteSpace(dwellingBlocker),
            MarkerState = markerState,
            DwellingDefinition = theme?.GetDwellingDefinition(buildingType)
        };
    }

    private string FormatBuiltBuildings()
    {
        if (CurrentTown == null)
        {
            return "-";
        }

        var buildingNames = Enum
            .GetValues<TownBuildingType>()
            .Where(buildingType => buildingType != TownBuildingType.None && CurrentTown.GetBuildingLevel(buildingType) > 0)
            .Select(buildingType => $"{GetBuildingDisplayName(buildingType)} Lv.{CurrentTown.GetBuildingLevel(buildingType)}");

        return string.Join(", ", buildingNames);
    }

    private TownTechTreeTheme ResolveTheme(Faction faction)
    {
        return TechTreeThemes?
            .FirstOrDefault(theme => theme != null && theme.Faction == faction);
    }

    private void EnsureDefaultThemes()
    {
        TechTreeThemes ??= new Godot.Collections.Array<TownTechTreeTheme>();

        foreach (Faction faction in Enum.GetValues<Faction>())
        {
            if (faction == Faction.Neutral)
            {
                continue;
            }

            if (TechTreeThemes.Any(theme => theme != null && theme.Faction == faction))
            {
                continue;
            }

            TechTreeThemes.Add(TownTechTreeTheme.GetDefault(faction));
        }
    }

    private string GetBuildingDisplayName(TownBuildingType buildingType)
    {
        TownDwellingDefinition dwelling = CurrentTheme?.GetDwellingDefinition(buildingType);
        if (dwelling != null && !string.IsNullOrWhiteSpace(dwelling.DisplayName))
        {
            return dwelling.DisplayName;
        }

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

    private static IReadOnlyDictionary<ResourceType, int> GetBuildCost(TownTechTreeTheme theme, TownBuildingType buildingType, int nextLevel)
    {
        Dictionary<ResourceType, int> costs = new();
        TownBuildingLevelCost levelCost = theme?.GetLevelCost(buildingType, nextLevel);
        if (levelCost?.Costs == null)
        {
            return costs;
        }

        foreach (TownBuildingResourceCost resourceCost in levelCost.Costs)
        {
            if (resourceCost == null || resourceCost.Amount <= 0)
            {
                continue;
            }

            costs[resourceCost.ResourceType] = resourceCost.Amount;
        }

        return costs;
    }

    private string GetBuildLimitStatus()
    {
        if (CurrentTown == null || TurnManager == null)
        {
            return "Builds today: -";
        }

        int builtDwellings = CurrentTown.GetBuiltDwellingCount(CurrentTheme);
        string dailyStatus = CurrentTown.CanBuildToday
            ? "Builds today: 0/1"
            : "Builds today: 1/1";
        return $"{dailyStatus} | Dwellings: {builtDwellings}/{MaxBuiltDwellings}";
    }

    private string FormatStatusMessage()
    {
        return string.IsNullOrWhiteSpace(StatusMessage)
            ? string.Empty
            : $"\n{StatusMessage}";
    }

    private IReadOnlyList<TownBuildingRequirement> GetMissingRequirements(TownTechTreeTheme theme, TownBuildingType buildingType, int nextLevel)
    {
        List<TownBuildingRequirement> missingRequirements = new();
        if (CurrentTown == null)
        {
            return missingRequirements;
        }

        TownBuildingLevelCost levelCost = theme?.GetLevelCost(buildingType, nextLevel);
        if (levelCost?.Requirements == null)
        {
            return missingRequirements;
        }

        foreach (TownBuildingRequirement requirement in levelCost.Requirements)
        {
            if (requirement == null || requirement.BuildingType == TownBuildingType.None)
            {
                continue;
            }

            if (CurrentTown.GetBuildingLevel(requirement.BuildingType) >= requirement.MinLevel)
            {
                continue;
            }

            missingRequirements.Add(requirement);
        }

        return missingRequirements;
    }

    private static string FormatRequirements(IReadOnlyList<TownBuildingRequirement> requirements)
    {
        if (requirements == null || requirements.Count == 0)
        {
            return "-";
        }

        return string.Join(", ", requirements.Select(requirement =>
            $"{requirement.BuildingType} Lv.{requirement.MinLevel}"));
    }

    private static string FormatCosts(IReadOnlyDictionary<ResourceType, int> costs)
    {
        if (costs == null || costs.Count == 0)
        {
            return "-";
        }

        return string.Join(", ", costs.Select(pair => $"{pair.Value} {pair.Key}"));
    }

    private string GetDwellingBuildBlocker(TownTechTreeTheme theme, TownBuildingType buildingType)
    {
        TownDwellingDefinition dwelling = theme?.GetDwellingDefinition(buildingType);
        if (dwelling == null || CurrentTown == null || CurrentTown.GetBuildingLevel(buildingType) > 0)
        {
            return null;
        }

        int builtDwellings = CurrentTown.GetBuiltDwellingCount(theme);
        if (builtDwellings >= MaxBuiltDwellings)
        {
            return $"This town can only build {MaxBuiltDwellings} dwellings.";
        }

        HashSet<UnitTier> coveredTiers = new();
        foreach (TownDwellingDefinition builtDwelling in theme.GetDwellingDefinitions())
        {
            if (builtDwelling != null && CurrentTown.GetBuildingLevel(builtDwelling.BuildingType) > 0)
            {
                coveredTiers.Add(builtDwelling.UnitTier);
            }
        }

        coveredTiers.Add(dwelling.UnitTier);
        int missingTierCountAfterBuild = 0;
        foreach (UnitTier unitTier in Enum.GetValues<UnitTier>())
        {
            if (!coveredTiers.Contains(unitTier))
            {
                missingTierCountAfterBuild++;
            }
        }

        int remainingDwellingBuildsAfterBuild = MaxBuiltDwellings - (builtDwellings + 1);
        if (remainingDwellingBuildsAfterBuild < missingTierCountAfterBuild)
        {
            return "You must leave room to build at least one dwelling for every unit tier.";
        }

        return null;
    }
}
