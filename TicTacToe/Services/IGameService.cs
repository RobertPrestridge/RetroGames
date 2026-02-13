using TicTacToe.Data.Entities;

namespace TicTacToe.Services;

public record CreateGameResult(string ShortCode, string SessionId);
public record JoinGameResult(bool Success, char? PlayerMark, string? Error, Game? Game);
public record MakeMoveResult(bool Success, string? Error, Game? Game, int[]? WinLine);

public interface IGameService
{
    Task<CreateGameResult> CreateGameAsync();
    Task<Game?> GetGameByCodeAsync(string shortCode);
    Task<JoinGameResult> JoinGameAsync(string shortCode, string sessionId, string connectionId);
    Task<MakeMoveResult> MakeMoveAsync(string shortCode, int position, string connectionId);
    Task HandleDisconnectAsync(string connectionId);
}
