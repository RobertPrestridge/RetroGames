using Microsoft.AspNetCore.SignalR;
using TicTacToe.Services;

namespace TicTacToe.Hubs;

public class GameHub : Hub
{
    private readonly IGameService _gameService;
    private readonly ILogger<GameHub> _logger;

    public GameHub(IGameService gameService, ILogger<GameHub> logger)
    {
        _gameService = gameService;
        _logger = logger;
    }

    public async Task JoinGame(string shortCode, string sessionId)
    {
        var groupName = $"game_{shortCode}";
        var result = await _gameService.JoinGameAsync(shortCode, sessionId, Context.ConnectionId);

        if (!result.Success)
        {
            _logger.LogWarning("JoinGame failed for {ShortCode}: {Error} (SessionId={SessionId}, ConnectionId={ConnectionId})",
                shortCode, result.Error, sessionId, Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", result.Error);
            return;
        }

        _logger.LogInformation("Player {PlayerMark} joined game {ShortCode} (SessionId={SessionId}, ConnectionId={ConnectionId})",
            result.PlayerMark, shortCode, sessionId, Context.ConnectionId);

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        // Send full game state to the joining player
        var game = result.Game!;
        await Clients.Caller.SendAsync("GameState", new
        {
            board = game.BoardState,
            status = (int)game.Status,
            currentTurn = game.CurrentTurn,
            playerMark = result.PlayerMark,
            moveCount = game.MoveCount
        });

        // Notify opponent if game just started (player O joined)
        if (result.PlayerMark == 'O' && game.Status == Data.Entities.GameStatus.InProgress)
        {
            await Clients.OthersInGroup(groupName).SendAsync("OpponentJoined");
        }
    }

    public async Task MakeMove(string shortCode, int position)
    {
        var result = await _gameService.MakeMoveAsync(shortCode, position, Context.ConnectionId);

        if (!result.Success)
        {
            _logger.LogWarning("MakeMove failed for {ShortCode} at position {Position}: {Error} (ConnectionId={ConnectionId})",
                shortCode, position, result.Error, Context.ConnectionId);
            await Clients.Caller.SendAsync("Error", result.Error);
            return;
        }

        var game = result.Game!;
        var groupName = $"game_{shortCode}";

        _logger.LogInformation("Move made in game {ShortCode}: position {Position}, mark {Mark}, moveCount {MoveCount}",
            shortCode, position, game.BoardState[position], game.MoveCount);

        await Clients.Group(groupName).SendAsync("MoveMade", new
        {
            position,
            mark = game.BoardState[position],
            currentTurn = game.CurrentTurn,
            moveCount = game.MoveCount
        });

        // Check if game is over
        if (game.Status >= Data.Entities.GameStatus.XWins)
        {
            _logger.LogInformation("Game {ShortCode} ended with status {Status}", shortCode, game.Status);

            await Clients.Group(groupName).SendAsync("GameOver", new
            {
                status = (int)game.Status,
                winLine = result.WinLine
            });
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Player disconnected (ConnectionId={ConnectionId})", Context.ConnectionId);

        if (exception is not null)
        {
            _logger.LogWarning(exception, "Player disconnected with error (ConnectionId={ConnectionId})", Context.ConnectionId);
        }

        await _gameService.HandleDisconnectAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
