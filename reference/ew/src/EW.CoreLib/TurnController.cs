using EW.CoreLib.Events;
using EW.CoreLib.Messaging;
using EW.CoreLib.Types.Events;

namespace EW.CoreLib;

public class TurnController : IHandleEvent<PlayerEndedTurn>
{
   public List<GamePlayer> ActivePlayers { get; set; } = new();
   public GamePlayer EnvironmentPlayer { get; set; }
   public GamePlayer CurrentPlayer { get; private set; }

   private int _playerIndex = 0;

   public TurnController(IReadOnlyCollection<GamePlayer> activePlayers, GamePlayer environmentPlayer, GamePlayer? currentPlayer = null)
   {
      ActivePlayers.AddRange(activePlayers);
      ActivePlayers.Add(environmentPlayer);
      EnvironmentPlayer = environmentPlayer;

      if (currentPlayer != null)
      {
         _playerIndex = ActivePlayers.IndexOf(currentPlayer);
      }
      
      CurrentPlayer = ActivePlayers[_playerIndex];
      
      Hub.Instance.Subscribe(this);
   }

   private void EndCurrentTurn()
   {
      _playerIndex++;
      if (_playerIndex == ActivePlayers.Count) _playerIndex = 0;

      CurrentPlayer = ActivePlayers[_playerIndex];
      if (_playerIndex == 0)
      {
         Hub.Instance.Publish(new DayAdvanced());
      }
   }

   public void Handle(PlayerEndedTurn e)
   {
      EndCurrentTurn();
   }
}