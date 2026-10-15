using System.Text.Json;
using EW.CoreLib.Interfaces;
using EW.CoreLib.Map;
using EW.CoreLib.Types;

namespace EW.CoreLib;

public partial class GameScenarioController
{
    public void SaveToJsonFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var directoryPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var json = JsonSerializer.Serialize(this.ToState(), JsonOptions);
        File.WriteAllText(filePath, json);
    }

    public static GameScenarioController LoadFromJsonFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var json = File.ReadAllText(filePath);
        var state = JsonSerializer.Deserialize<GameScenarioControllerStateDto>(json, JsonOptions)
                    ?? throw new InvalidDataException("Scenario state file is empty or invalid.");

        var controller = new GameScenarioController();
        controller.FromState(state);
        
        return controller;
    }

    public GameScenarioControllerStateDto ToState()
    {
        var mapState = Map.ToState();

        // Hero owner indices are relational (they reference this controller's player list), so
        // they can only be resolved here; GameMap.ToState leaves them at their default.
        var ownerIndexByHero = Map.MapHeroes.OfType<Hero>()
            .ToDictionary(hero => hero.ObjectId, hero => ResolveOwnerPlayerIndex(hero.OwnerPlayer));
        foreach (var heroState in mapState.Heroes)
        {
            heroState.OwnerPlayerIndex = ownerIndexByHero[heroState.ObjectId];
        }

        return new GameScenarioControllerStateDto
        {
            Metadata = Metadata?.ToState(),
            Map = mapState,
            Players = Players.Select(player => player.ToState()).ToList(),
            SelectedHeroObjectId = UiHost.SelectedHero?.ObjectId,
            HostPlayerName = UiHost.HostPlayer.DisplayName,
            CurrentPlayerName = TurnController.CurrentPlayer.DisplayName
        };
    }

    public void FromState(GameScenarioControllerStateDto state)
    {
        if (state.Map == null)
        {
            throw new InvalidDataException("Scenario state is missing map data.");
        }

        if (state.Players.Count == 0)
        {
            throw new InvalidDataException("Scenario state does not contain any players.");
        }

        var players = new List<GamePlayer>();
        foreach (var playerDto in state.Players)
        {
            var player = new GamePlayer { DisplayName = playerDto.DisplayName, ControlType = playerDto.ControlType, PlayerColor = playerDto.Color };
            player.FromState(playerDto);
            players.Add(player);
        }
        var hostPlayer = players.First(p => p.DisplayName == state.HostPlayerName);
        var currentPlayer = players.First(p => p.DisplayName == state.CurrentPlayerName);

        var map = new GameMap();
        map.FromState(state.Map);

        var heroesById = LinkHeroesToOwners(map, state.Map, players);

        Map = map;
        Players = players;
        UiHost = new UiHost {HostPlayer = hostPlayer};
        // The environment player is not part of the persisted player list; it is recreated
        // here the same way the primary constructor does.
        var environment = new GamePlayer
        {
            DisplayName = "Environment",
            PlayerColor = PlayerColor.Neutral,
            ControlType = PlayerControlType.Environment
        };
        TurnController = new TurnController(players, environment, currentPlayer);
        Metadata = new ScenarioMetadata();
        Metadata.FromState(state.Metadata);

        if (!string.IsNullOrWhiteSpace(state.SelectedHeroObjectId)
            && heroesById.TryGetValue(state.SelectedHeroObjectId, out var selectedHero))
        {
            UiHost.SelectHero(selectedHero);
        }
    }

    private static Dictionary<string, Hero> LinkHeroesToOwners(
        GameMap map, MapStateDto mapState, IReadOnlyList<GamePlayer> players)
    {
        var ownerIndexByHero = mapState.Heroes.ToDictionary(hero => hero.ObjectId, hero => hero.OwnerPlayerIndex);
        var heroesById = new Dictionary<string, Hero>();

        foreach (var hero in map.MapHeroes.OfType<Hero>())
        {
            var ownerPlayerIndex = ownerIndexByHero[hero.ObjectId];
            if (ownerPlayerIndex < 0 || ownerPlayerIndex >= players.Count)
            {
                throw new InvalidDataException($"Hero '{hero.ObjectId}' contains an invalid owner player index.");
            }

            var owner = players[ownerPlayerIndex];
            hero.OwnerPlayer = owner;
            owner.Heroes.Add(hero);
            heroesById[hero.ObjectId] = hero;
        }

        return heroesById;
    }

    private int ResolveOwnerPlayerIndex(GamePlayer ownerPlayer)
    {
        var ownerPlayerIndex = ownerPlayer == null ? -1 : Players.IndexOf(ownerPlayer);
        if (ownerPlayerIndex < 0)
        {
            throw new InvalidDataException("Scenario contains a map object owned by a player outside the scenario player list.");
        }

        return ownerPlayerIndex;
    }
}
