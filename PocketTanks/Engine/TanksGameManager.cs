using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace PocketTanks.Engine;

public class TanksGameManager
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 6;

    private readonly ConcurrentDictionary<string, TanksGame> _games = new();
    private readonly ILogger<TanksGameManager> _logger;

    public TanksGameManager(ILogger<TanksGameManager> logger)
    {
        _logger = logger;
    }

    public (string shortCode, string sessionId) CreateGame(string playerName, bool vsAi = false)
    {
        var shortCode = GenerateUniqueCode();
        var sessionId = Guid.NewGuid().ToString("N");

        var game = new TanksGame(shortCode, playerName, sessionId);

        if (vsAi)
            game.AddAiPlayer2();

        _games[shortCode] = game;

        _logger.LogInformation("Tanks game created: {ShortCode} by {PlayerName} (vsAi={VsAi})", shortCode, playerName, vsAi);

        return (shortCode, sessionId);
    }

    public (bool success, string? error, int playerNumber) JoinGame(
        string shortCode, string playerName, string sessionId, string connectionId)
    {
        if (!_games.TryGetValue(shortCode, out var game))
            return (false, "Game not found.", 0);

        // Reconnecting P1
        if (game.Player1.SessionId == sessionId)
        {
            game.Player1.ConnectionId = connectionId;
            _logger.LogInformation("Tanks P1 reconnected to {ShortCode}", shortCode);
            return (true, null, 1);
        }

        // Reconnecting P2
        if (game.Player2?.SessionId == sessionId)
        {
            game.Player2.ConnectionId = connectionId;
            _logger.LogInformation("Tanks P2 reconnected to {ShortCode}", shortCode);
            return (true, null, 2);
        }

        // New P2 joining
        if (game.Status == TanksGameStatus.Waiting)
        {
            if (game.AddPlayer2(playerName, sessionId, connectionId))
            {
                _logger.LogInformation("Tanks P2 joined {ShortCode}: {PlayerName}", shortCode, playerName);
                return (true, null, 2);
            }
            return (false, "Failed to join game.", 0);
        }

        return (false, "Game is full or already in progress.", 0);
    }

    public TanksGame? GetGame(string shortCode)
    {
        _games.TryGetValue(shortCode, out var game);
        return game;
    }

    public void RemoveGame(string shortCode)
    {
        _games.TryRemove(shortCode, out _);
        _logger.LogInformation("Tanks game removed: {ShortCode}", shortCode);
    }

    public IEnumerable<TanksGame> GetActiveGames()
    {
        return _games.Values.Where(g =>
            g.Status is TanksGameStatus.Countdown or TanksGameStatus.WeaponSelect
                or TanksGameStatus.Aiming or TanksGameStatus.Firing);
    }

    public IEnumerable<TanksGame> GetAllGames()
    {
        return _games.Values;
    }

    public void HandleDisconnect(string connectionId)
    {
        foreach (var game in _games.Values)
        {
            var player = game.GetPlayerByConnection(connectionId);
            if (player is not null)
            {
                player.ConnectionId = null;
                _logger.LogInformation("Tanks player disconnected from {ShortCode}: {Name}", game.ShortCode, player.Name);
                break;
            }
        }
    }

    private string GenerateUniqueCode()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = GenerateCode();
            if (!_games.ContainsKey(code))
                return code;
        }
        throw new InvalidOperationException("Failed to generate a unique short code after 10 attempts.");
    }

    private static string GenerateCode()
    {
        Span<char> result = stackalloc char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            result[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(result);
    }
}
