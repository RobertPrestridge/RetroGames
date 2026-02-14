using Asteroids.Engine;
using Microsoft.AspNetCore.SignalR;

namespace Asteroids.Hubs;

public class AsteroidsHub : Hub
{
    private readonly AsteroidGameManager _gameManager;
    private readonly ILogger<AsteroidsHub> _logger;

    public AsteroidsHub(AsteroidGameManager gameManager, ILogger<AsteroidsHub> logger)
    {
        _gameManager = gameManager;
        _logger = logger;
    }

    public async Task JoinGame(string shortCode, string playerName, string sessionId)
    {
        var groupName = $"asteroids_{shortCode}";
        var game = _gameManager.GetGame(shortCode);

        if (game is null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found.");
            return;
        }

        // Check if this session is already a player (reconnect)
        var existingPlayer = game.GetPlayerBySession(sessionId);
        if (existingPlayer is not null)
        {
            existingPlayer.Ship.ConnectionId = Context.ConnectionId;
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            var playerNum = game.GetPlayerNumber(sessionId);
            _logger.LogInformation("Asteroids P{PlayerNum} reconnected to {ShortCode} (ConnectionId={ConnectionId})",
                playerNum, shortCode, Context.ConnectionId);

            await SendGameState(game, playerNum);
            return;
        }

        // Try to join as P2
        var (success, error, number) = _gameManager.JoinGame(shortCode, playerName, sessionId, Context.ConnectionId);

        if (!success)
        {
            _logger.LogWarning("Asteroids JoinGame failed for {ShortCode}: {Error}", shortCode, error);
            await Clients.Caller.SendAsync("Error", error);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation("Asteroids P{PlayerNum} joined {ShortCode}: {PlayerName} (ConnectionId={ConnectionId})",
            number, shortCode, playerName, Context.ConnectionId);

        // Send game state to the joining player
        await SendGameState(game, number);

        // If P2 just joined, notify P1
        if (number == 2)
        {
            await Clients.OthersInGroup(groupName).SendAsync("OpponentJoined", new
            {
                opponentName = playerName
            });
        }
    }

    public Task SendInput(string shortCode, PlayerInput input)
    {
        var game = _gameManager.GetGame(shortCode);
        if (game is null) return Task.CompletedTask;

        var ship = game.GetShipByConnection(Context.ConnectionId);
        if (ship is null) return Task.CompletedTask;

        game.SetInput(ship.SessionId, input);
        return Task.CompletedTask;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Asteroids player disconnected (ConnectionId={ConnectionId})", Context.ConnectionId);
        _gameManager.HandleDisconnect(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task SendGameState(AsteroidGame game, int playerNumber)
    {
        var state = game.GetFullState(playerNumber);

        await Clients.Caller.SendAsync("GameState", new
        {
            arenaWidth = AsteroidGame.ArenaWidth,
            arenaHeight = AsteroidGame.ArenaHeight,
            playerNumber,
            player1 = new { name = game.Player1.Name, alive = game.Player1.Alive },
            player2 = game.Player2 is not null ? new { name = game.Player2.Name, alive = game.Player2.Alive } : null,
            status = (int)game.Status,
            state
        });
    }
}
