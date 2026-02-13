using Microsoft.EntityFrameworkCore;
using TicTacToe.Data;
using TicTacToe.Data.Entities;

namespace TicTacToe.Services;

public class GameService : IGameService
{
    private static readonly int[][] WinPatterns =
    [
        [0, 1, 2], [3, 4, 5], [6, 7, 8], // rows
        [0, 3, 6], [1, 4, 7], [2, 5, 8], // cols
        [0, 4, 8], [2, 4, 6]             // diagonals
    ];

    private readonly ApplicationDbContext _db;
    private readonly IShortCodeService _shortCodeService;
    private readonly ILogger<GameService> _logger;

    public GameService(ApplicationDbContext db, IShortCodeService shortCodeService, ILogger<GameService> logger)
    {
        _db = db;
        _shortCodeService = shortCodeService;
        _logger = logger;
    }

    public async Task<CreateGameResult> CreateGameAsync()
    {
        var shortCode = await _shortCodeService.GenerateUniqueCodeAsync();
        var sessionId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;

        var game = new Game
        {
            ShortCode = shortCode,
            BoardState = "         ",
            Status = GameStatus.Waiting,
            CurrentTurn = 'X',
            PlayerXSessionId = sessionId,
            CreatedAt = now,
            UpdatedAt = now,
            MoveCount = 0
        };

        _db.Games.Add(game);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Game created: {ShortCode} (PlayerX SessionId={SessionId})", shortCode, sessionId);

        return new CreateGameResult(shortCode, sessionId);
    }

    public async Task<Game?> GetGameByCodeAsync(string shortCode)
    {
        return await _db.Games.FirstOrDefaultAsync(g => g.ShortCode == shortCode);
    }

    public async Task<JoinGameResult> JoinGameAsync(string shortCode, string sessionId, string connectionId)
    {
        var game = await _db.Games.FirstOrDefaultAsync(g => g.ShortCode == shortCode);
        if (game is null)
            return new JoinGameResult(false, null, "Game not found.", null);

        if (game.Status >= GameStatus.XWins)
            return new JoinGameResult(false, null, "Game is already over.", null);

        // Reconnecting Player X
        if (game.PlayerXSessionId == sessionId)
        {
            game.PlayerXConnectionId = connectionId;
            game.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Player X reconnected to game {ShortCode} (SessionId={SessionId})", shortCode, sessionId);
            return new JoinGameResult(true, 'X', null, game);
        }

        // Reconnecting Player O
        if (game.PlayerOSessionId == sessionId)
        {
            game.PlayerOConnectionId = connectionId;
            game.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Player O reconnected to game {ShortCode} (SessionId={SessionId})", shortCode, sessionId);
            return new JoinGameResult(true, 'O', null, game);
        }

        // New Player O joining
        if (game.Status == GameStatus.Waiting && game.PlayerOSessionId is null)
        {
            game.PlayerOSessionId = sessionId;
            game.PlayerOConnectionId = connectionId;
            game.Status = GameStatus.InProgress;
            game.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Player O joined game {ShortCode}, game now InProgress (SessionId={SessionId})", shortCode, sessionId);
            return new JoinGameResult(true, 'O', null, game);
        }

        // Game already has two players and this session doesn't match either
        return new JoinGameResult(false, null, "Game is full.", null);
    }

    public async Task<MakeMoveResult> MakeMoveAsync(string shortCode, int position, string connectionId)
    {
        if (position < 0 || position > 8)
            return new MakeMoveResult(false, "Invalid position.", null, null);

        var game = await _db.Games.FirstOrDefaultAsync(g => g.ShortCode == shortCode);
        if (game is null)
            return new MakeMoveResult(false, "Game not found.", null, null);

        if (game.Status != GameStatus.InProgress)
            return new MakeMoveResult(false, "Game is not in progress.", null, null);

        // Determine which player is making the move
        char playerMark;
        if (game.PlayerXConnectionId == connectionId)
            playerMark = 'X';
        else if (game.PlayerOConnectionId == connectionId)
            playerMark = 'O';
        else
            return new MakeMoveResult(false, "You are not a player in this game.", null, null);

        if (game.CurrentTurn != playerMark)
            return new MakeMoveResult(false, "It's not your turn.", null, null);

        if (game.BoardState[position] != ' ')
            return new MakeMoveResult(false, "Cell is already occupied.", null, null);

        // Apply move
        var board = game.BoardState.ToCharArray();
        board[position] = playerMark;
        game.BoardState = new string(board);
        game.MoveCount++;
        game.UpdatedAt = DateTime.UtcNow;

        // Check for win
        var winLine = CheckWin(board, playerMark);
        if (winLine is not null)
        {
            game.Status = playerMark == 'X' ? GameStatus.XWins : GameStatus.OWins;
            game.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Game {ShortCode}: Player {Mark} wins at move {MoveCount}", shortCode, playerMark, game.MoveCount);
        }
        else if (game.MoveCount >= 9)
        {
            game.Status = GameStatus.Draw;
            game.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Game {ShortCode}: Draw at move {MoveCount}", shortCode, game.MoveCount);
        }
        else
        {
            game.CurrentTurn = playerMark == 'X' ? 'O' : 'X';
        }

        await _db.SaveChangesAsync();
        return new MakeMoveResult(true, null, game, winLine);
    }

    public async Task HandleDisconnectAsync(string connectionId)
    {
        var game = await _db.Games.FirstOrDefaultAsync(g =>
            g.PlayerXConnectionId == connectionId || g.PlayerOConnectionId == connectionId);

        if (game is null) return;

        var playerMark = game.PlayerXConnectionId == connectionId ? 'X' : 'O';

        if (game.PlayerXConnectionId == connectionId)
            game.PlayerXConnectionId = null;
        else
            game.PlayerOConnectionId = null;

        game.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Player {Mark} disconnected from game {ShortCode}", playerMark, game.ShortCode);
    }

    private static int[]? CheckWin(char[] board, char mark)
    {
        foreach (var pattern in WinPatterns)
        {
            if (board[pattern[0]] == mark && board[pattern[1]] == mark && board[pattern[2]] == mark)
                return pattern;
        }
        return null;
    }
}
