using LightCycles.Engine;
using Microsoft.AspNetCore.SignalR;

namespace LightCycles.Hubs;

public class TronHub : Hub
{
    private readonly TronGameManager _gameManager;
    private readonly ILogger<TronHub> _logger;

    public TronHub(TronGameManager gameManager, ILogger<TronHub> logger)
    {
        _gameManager = gameManager;
        _logger = logger;
    }

    public async Task JoinGame(string shortCode, string playerName, string sessionId)
    {
        var groupName = $"tron_{shortCode}";
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
            existingPlayer.ConnectionId = Context.ConnectionId;
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            var playerNum = game.GetPlayerNumber(sessionId);
            _logger.LogInformation("Tron P{PlayerNum} reconnected to {ShortCode} (ConnectionId={ConnectionId})",
                playerNum, shortCode, Context.ConnectionId);

            await SendGameState(game, playerNum);
            return;
        }

        // Try to join as P2
        var (success, error, number) = _gameManager.JoinGame(shortCode, playerName, sessionId, Context.ConnectionId);

        if (!success)
        {
            _logger.LogWarning("Tron JoinGame failed for {ShortCode}: {Error}", shortCode, error);
            await Clients.Caller.SendAsync("Error", error);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation("Tron P{PlayerNum} joined {ShortCode}: {PlayerName} (ConnectionId={ConnectionId})",
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

    public async Task ChangeDirection(string shortCode, string direction)
    {
        var game = _gameManager.GetGame(shortCode);
        if (game is null) return;

        var player = game.GetPlayerByConnection(Context.ConnectionId);
        if (player is null) return;

        if (!Enum.TryParse<TronDirection>(direction, true, out var dir))
        {
            await Clients.Caller.SendAsync("Error", "Invalid direction.");
            return;
        }

        game.SetDirection(player.SessionId, dir);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Tron player disconnected (ConnectionId={ConnectionId})", Context.ConnectionId);
        _gameManager.HandleDisconnect(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task SendGameState(TronGame game, int playerNumber)
    {
        var trails = game.GetAllTrails();

        await Clients.Caller.SendAsync("GameState", new
        {
            gridWidth = TronGame.GridWidth,
            gridHeight = TronGame.GridHeight,
            player1 = new
            {
                x = game.Player1.X,
                y = game.Player1.Y,
                name = game.Player1.Name,
                alive = game.Player1.Alive
            },
            player2 = game.Player2 is not null ? new
            {
                x = game.Player2.X,
                y = game.Player2.Y,
                name = game.Player2.Name,
                alive = game.Player2.Alive
            } : null,
            status = (int)game.Status,
            playerNumber,
            trails = trails.Select(t => new { x = t.X, y = t.Y, player = t.Player })
        });
    }
}
