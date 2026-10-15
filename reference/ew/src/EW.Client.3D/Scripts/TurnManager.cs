using System;
using System.Collections.Generic;
using System.Linq;
using EW.Scripts.Calendar;
using EW.Scripts.DataTypes;
using EW.Scripts.DataTypes.Enums;
using EW.Scripts.Messaging;
using EW.Scripts.Players;
using Godot;

namespace EW.Scripts;

public sealed class TurnStart : GamePlayerEvent;

public sealed class WorldMapResourceUiRefreshRequested : IGameEvent
{
}

public partial class TurnManager : Node, IHandleEvent<AiTurnCompleted>
{
    private const int MaxActivePlayers = 9;

    [Export] public Godot.Collections.Array<GamePlayer> ActivePlayers { get; set; } = new();
    public GamePlayer EnvironmentPlayer { get; set; }
    public GamePlayer LocalPlayer { get; set; }
    public GamePlayer CurrentPlayer { get; private set; }

    public override void _Ready()
    {
        SyncPlayerOwnership();
        ValidatePlayers();
        LocalPlayer ??= ResolveDefaultLocalPlayer();
        CurrentPlayer = ResolveTurnOrder().FirstOrDefault();

        GD.Print($"--- Adventure Map Initialized: {CurrentPlayer} Turn ---");
        StartTurn();
    }

    public void EndCurrentTurn()
    {
        List<GamePlayer> turnOrder = ResolveTurnOrder();
        if (turnOrder.Count == 0)
        {
            return;
        }

        int currentIndex = turnOrder.FindIndex(player => ReferenceEquals(player, CurrentPlayer));
        bool wrappedToNewDay = currentIndex == turnOrder.Count - 1;
        int nextIndex = currentIndex < 0
            ? 0
            : (currentIndex + 1) % turnOrder.Count;

        CurrentPlayer = turnOrder[nextIndex];
        if (wrappedToNewDay)
        {
            Hub.Instance.Publish(new DayAdvanced());
        }

        GD.Print($"\n--- {CurrentPlayer} TURN STARTING ---");
        StartTurn();
    }

    public string SaveCurrentGame()
    {
        Node currentScene = GetTree().CurrentScene;
        if (currentScene == null)
        {
            GD.PushError($"{Name}: Cannot save because there is no active scene.");
            return string.Empty;
        }

        Error directoryError = SaveGameStore.EnsureSaveDirectoryExists();
        if (directoryError != Error.Ok)
        {
            GD.PushError($"{Name}: Failed to create save directory '{SaveGameStore.SaveDirectoryPath}'. Error: {directoryError}");
            return string.Empty;
        }

        PackedScene packedScene = new();
        Error packError = packedScene.Pack(currentScene);
        if (packError != Error.Ok)
        {
            GD.PushError($"{Name}: Failed to pack current scene for saving. Error: {packError}");
            return string.Empty;
        }

        string savePath = SaveGameStore.CreateSavePath();
        Error saveError = ResourceSaver.Save(packedScene, savePath);
        if (saveError != Error.Ok)
        {
            GD.PushError($"{Name}: Failed to save game to '{savePath}'. Error: {saveError}");
            return string.Empty;
        }

        GD.Print($"Game saved to {savePath}");
        return savePath;
    }

    private void StartTurn()
    {
        SyncPlayerOwnership();

        foreach (Heroes.Hero hero in CurrentPlayer.Heroes)
        {
            if (IsInstanceValid(hero))
            {
                hero.RefillMovementPoints();
            }
        }

        NotifyOwnedMapObjects();
        Hub.Instance?.Publish(new WorldMapResourceUiRefreshRequested());
        Hub.Instance?.Publish(new TurnStart { Player = CurrentPlayer });

        if (CurrentPlayer.IsAi)
        {
            SimulateCurrentAiTurn();
        }
    }

    private void NotifyOwnedMapObjects()
    {
        foreach (Node child in GetParent().GetChildren())
        {
            if (child is ICapturableMapObject capturableObject && IsInstanceValid(child))
            {
                if (ReferenceEquals(capturableObject.OwnerPlayer, CurrentPlayer))
                {
                    ApplyIncome(CurrentPlayer, capturableObject);
                }

                capturableObject.OnOwnerTurnStarted(CurrentPlayer);
            }
        }

        PrintEconomySummary(CurrentPlayer);
    }

    public GamePlayer GetPlayerForHero(Heroes.Hero hero)
    {
        if (hero == null)
        {
            return null;
        }

        if (hero.OwnerPlayer != null)
        {
            return hero.OwnerPlayer;
        }

        foreach (GamePlayer player in ResolveAllPlayers())
        {
            if (player.OwnsHero(hero))
            {
                hero.OwnerPlayer = player;
                return player;
            }
        }

        return null;
    }

    public bool IsCurrentPlayer(GamePlayer player)
    {
        return player != null && ReferenceEquals(CurrentPlayer, player);
    }

    public bool IsLocalPlayer(GamePlayer player)
    {
        return player != null && ReferenceEquals(LocalPlayer, player);
    }

    public bool CanControlHero(Heroes.Hero hero)
    {
        if (hero == null || CurrentPlayer == null || !CurrentPlayer.IsHuman)
        {
            return false;
        }

        return ReferenceEquals(GetPlayerForHero(hero), CurrentPlayer);
    }

    public void AddResource(GamePlayer owner, ResourceType resourceType, int amount)
    {
        ResourceEconomy economy = GetEconomy(owner);
        if (economy == null)
        {
            return;
        }

        Hub.Instance?.Publish(new ResourceIncomeEvent
        {
            Player = owner,
            ResourceType = resourceType,
            Amount = amount
        });
        Hub.Instance?.Publish(new WorldMapResourceUiRefreshRequested());
    }

    public bool CanAffordResources(GamePlayer owner, IReadOnlyDictionary<ResourceType, int> costs)
    {
        ResourceEconomy economy = GetEconomy(owner);
        return economy?.CanAfford(costs) == true;
    }

    public bool TrySpendResources(GamePlayer owner, IReadOnlyDictionary<ResourceType, int> costs)
    {
        ResourceEconomy economy = GetEconomy(owner);
        if (economy == null)
        {
            return false;
        }

        ResourceSpendRequestEvent spendRequest = new()
        {
            Player = owner,
            Costs = costs
        };

        Hub.Instance?.Publish(spendRequest);
        if (!spendRequest.Succeeded)
        {
            return false;
        }

        Hub.Instance?.Publish(new WorldMapResourceUiRefreshRequested());

        GD.Print($"{owner} economy spent: {FormatCostSummary(costs)}. Remaining totals: {economy.Resources}");
        return true;
    }

    private void ApplyIncome(GamePlayer owner, ICapturableMapObject incomeSource)
    {
        foreach (var income in incomeSource.GetTurnIncome())
        {
            AddResource(owner, income.Key, income.Value);
        }
    }

    private ResourceEconomy GetEconomy(GamePlayer owner)
    {
        return owner?.Economy;
    }

    private void PrintEconomySummary(GamePlayer owner)
    {
        ResourceEconomy economy = GetEconomy(owner);
        if (economy != null)
        {
            GD.Print($"{owner} economy totals: {economy.Resources}");
        }
    }

    private void SimulateCurrentAiTurn()
    {
        if (CurrentPlayer?.AiBrain != null)
        {
            CurrentPlayer.AiBrain.ExecuteAiTurn();
        }
        else
        {
            GD.Print($"No AI Brain found for {CurrentPlayer}, auto-skipping turn.");
            EndCurrentTurn();
        }
    }

    public void _on_button_pressed()
    {
        if (CurrentPlayer != null && CurrentPlayer.IsHuman)
        {
            EndCurrentTurn();
        }
    }

    public void Handle(AiTurnCompleted eventData)
    {
        if (eventData?.Player == null || !ReferenceEquals(eventData.Player, CurrentPlayer))
        {
            return;
        }

        EndCurrentTurn();
    }

    private static string FormatCostSummary(IReadOnlyDictionary<ResourceType, int> costs)
    {
        if (costs == null || costs.Count == 0)
        {
            return "-";
        }

        return string.Join(", ", costs.Select(pair => $"{pair.Value} {pair.Key}"));
    }

    private List<GamePlayer> ResolveTurnOrder()
    {
        List<GamePlayer> players = ResolveAllPlayers()
            .Where(player => !player.IsEnvironment)
            .OrderBy(player => player.PlayerNumber)
            .Take(MaxActivePlayers)
            .ToList();

        if (EnvironmentPlayer != null && IsInstanceValid(EnvironmentPlayer))
        {
            players.Add(EnvironmentPlayer);
        }

        return players;
    }

    private IEnumerable<GamePlayer> ResolveAllPlayers()
    {
        HashSet<GamePlayer> players = new();

        foreach (GamePlayer player in ActivePlayers)
        {
            if (player == null || !IsInstanceValid(player) || player.IsEnvironment)
            {
                continue;
            }

            if (players.Add(player))
            {
                yield return player;
            }
        }

        if (EnvironmentPlayer != null && IsInstanceValid(EnvironmentPlayer) && players.Add(EnvironmentPlayer))
        {
            yield return EnvironmentPlayer;
        }
    }

    private void SyncPlayerOwnership()
    {
        foreach (GamePlayer player in ResolveAllPlayers())
        {
            foreach (Heroes.Hero hero in player.Heroes)
            {
                if (IsInstanceValid(hero))
                {
                    Hub.Instance?.Publish(new PlayerHeroEvent
                    {
                        Hero = hero,
                        Player = player
                    });
                }
            }
        }
    }

    private void ValidatePlayers()
    {
        int activePlayerCount = ActivePlayers.Count(player => player != null && IsInstanceValid(player) && !player.IsEnvironment);
        if (activePlayerCount > MaxActivePlayers)
        {
            GD.PushWarning($"TurnManager: only the first {MaxActivePlayers} active players will be used.");
        }
    }

    private GamePlayer ResolveDefaultLocalPlayer()
    {
        return ResolveAllPlayers().FirstOrDefault(player => !player.IsEnvironment && player.IsHuman);
    }
}
