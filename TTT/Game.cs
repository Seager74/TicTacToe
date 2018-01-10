using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;

namespace TTT
{
    public class Client
    {
        public string Name { get; set; }
        public Client Opponent { get; set; }
        public bool Isplaying { get; set; }
        public bool WaitingForMove { get; set; }
        public bool LookingForOpponent { get; set; }

        public string ConnectionId { get; set; }
    }

    public class GameInformation
    {
        public string OpponentName { get; set; }
        public string Winner { get; set; }
        public int MarkerPosition { get; set; }

    }
    public class Game : Hub
    {
        public static List<Client> _clients = new List<Client>();
        public static List<TicTacToe> _games = new List<TicTacToe>();

        public object _syncRoot = new object();
        public void RegisterClient(string data)
        {
            lock (_syncRoot)
            {
                var client = _clients.FirstOrDefault(x => x.ConnectionId == Context.ConnectionId);
                if (client == null)
                {
                    client = new Client { ConnectionId = Context.ConnectionId, Name = data };
                    _clients.Add(client);
                }
                client.Isplaying = false; 
            }
            Clients.Client(Context.ConnectionId).registerComplete();

        }

        private Random random = new Random();
        public void FindOpponent()
        {
            var player = _clients.FirstOrDefault(x => x.ConnectionId == Context.ConnectionId);
            if (player == null) return;

            player.LookingForOpponent = true;

            var opponent = _clients.Where(x => x.ConnectionId != Context.ConnectionId && x.LookingForOpponent && !x.Isplaying).OrderBy(x => Guid.NewGuid()).FirstOrDefault();
            if (opponent == null)
            {
                Clients.Client(Context.ConnectionId).noOpponents();
                return;
            }

            player.Isplaying = true;
            player.LookingForOpponent = false;
            opponent.Isplaying = true;
            opponent.LookingForOpponent = false;

            player.Opponent = opponent;
            opponent.Opponent = player;

            Clients.Client(Context.ConnectionId).foundOpponent(opponent.Name);
            Clients.Client(opponent.ConnectionId).foundOpponent(player.Name);

            if (random.Next(0, 5000) % 2 == 0)
            {
                player.WaitingForMove = false;
                opponent.WaitingForMove = true;

                Clients.Client(player.ConnectionId).waitingForMarkerPlacement(opponent.Name);
                Clients.Client(opponent.ConnectionId).waitingForOpponent(opponent.Name);
            }
            else {
                player.WaitingForMove = true;
                opponent.WaitingForMove = false;

                Clients.Client(opponent.ConnectionId).waitingForMarkerPlacement(opponent.Name);
                Clients.Client(player.ConnectionId).waitingForOpponent(opponent.Name);
            }

            lock (_syncRoot)
            {
                _games.Add(new TicTacToe { player1 = player, player2 = opponent });
            }
        }

        public void play(int position)
        {
            var game = _games.FirstOrDefault(x => x.player1.ConnectionId == Context.ConnectionId || x.player2.ConnectionId == Context.ConnectionId);
            if(game == null || game.IsGameOver) return;

            int marker = 0;

            if (game.player2.ConnectionId == Context.ConnectionId)
            {
                marker = 1;
            }
            var player = marker == 0 ? game.player2 : game.player2;

            if (player.WaitingForMove) return;

            Clients.Client(game.player1.ConnectionId).addMarkerPlacement(new GameInformation { OpponentName = player.Name, MarkerPosition = position });
            Clients.Client(game.player2.ConnectionId).addMarkerPlacement(new GameInformation { OpponentName = player.Name, MarkerPosition = position });

            if (game.Play(marker, position))
            {
                _games.Remove(game);
                
                Clients.Client(game.player1.ConnectionId).gameOver(player.Name);
                Clients.Client(game.player2.ConnectionId).gameOver(player.Name);
            }

            if (game.IsGameOver && game.IsDraw)
            {
                _games.Remove(game);
                Clients.Client(game.player1.ConnectionId).gameOver("It`s a draw!");
                Clients.Client(game.player2.ConnectionId).gameOver("It`s a draw!");

            }

            if (!game.IsGameOver)
            {
                player.WaitingForMove = !player.WaitingForMove;
                player.Opponent.WaitingForMove = !player.Opponent.WaitingForMove;

                Clients.Client(player.Opponent.ConnectionId).waitingForMarkerPlacement(player.Name);
            }
        }
    }
}