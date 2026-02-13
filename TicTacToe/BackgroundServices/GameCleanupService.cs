using Microsoft.EntityFrameworkCore;
using TicTacToe.Data;
using TicTacToe.Data.Entities;

namespace TicTacToe.BackgroundServices;

public class GameCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GameCleanupService> _logger;

    public GameCleanupService(IServiceProvider serviceProvider, ILogger<GameCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Game cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during game cleanup");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task CleanupAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cutoff = DateTime.UtcNow.AddHours(-2);

        // Abandon games that have been waiting or in-progress for too long
        var staleGames = await db.Games
            .Where(g => (g.Status == GameStatus.Waiting || g.Status == GameStatus.InProgress)
                        && g.UpdatedAt < cutoff)
            .ToListAsync();

        // Delete completed/abandoned games older than 24 hours
        var deleteCutoff = DateTime.UtcNow.AddHours(-24);
        var oldGames = await db.Games
            .Where(g => g.Status >= GameStatus.XWins && g.CreatedAt < deleteCutoff)
            .ToListAsync();

        var totalGames = await db.Games.CountAsync();
        _logger.LogInformation("Cleanup check: {TotalGames} total games, {StaleCount} stale, {OldCount} to delete",
            totalGames, staleGames.Count, oldGames.Count);

        foreach (var game in staleGames)
        {
            game.Status = GameStatus.Abandoned;
            game.CompletedAt = DateTime.UtcNow;
        }

        db.Games.RemoveRange(oldGames);

        if (staleGames.Count > 0 || oldGames.Count > 0)
        {
            await db.SaveChangesAsync();
            _logger.LogInformation("Cleanup completed: Abandoned {StaleCount} games, deleted {OldCount} games",
                staleGames.Count, oldGames.Count);
        }
    }
}
