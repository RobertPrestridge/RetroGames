using Asteroids.Data;
using Asteroids.Data.Entities;
using Asteroids.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Asteroids.Engine;

public class AsteroidGameLoop : BackgroundService
{
    private readonly AsteroidGameManager _gameManager;
    private readonly IHubContext<AsteroidsHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AsteroidGameLoop> _logger;

    public AsteroidGameLoop(
        AsteroidGameManager gameManager,
        IHubContext<AsteroidsHub> hubContext,
        IServiceProvider serviceProvider,
        ILogger<AsteroidGameLoop> logger)
    {
        _gameManager = gameManager;
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Asteroids game loop started");

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(60));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickAllGames();
                CleanupStaleGames();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Asteroids game loop tick");
            }
        }
    }

    private async Task TickAllGames()
    {
        foreach (var game in _gameManager.GetActiveGames())
        {
            var groupName = $"asteroids_{game.ShortCode}";

            if (game.Status == AsteroidGameStatus.Countdown)
            {
                var result = game.Tick();

                var secondsRemaining = (int)Math.Ceiling(game.CountdownTicks / 16.0);

                if (game.Status == AsteroidGameStatus.InProgress)
                {
                    await _hubContext.Clients.Group(groupName).SendAsync("Countdown", new { seconds = 0 });
                }
                else
                {
                    await _hubContext.Clients.Group(groupName).SendAsync("Countdown", new { seconds = secondsRemaining });
                }
            }
            else if (game.Status == AsteroidGameStatus.InProgress)
            {
                var result = game.Tick();
                if (result is null) continue;

                await _hubContext.Clients.Group(groupName).SendAsync("Tick", result);

                if (result.Status is (int)AsteroidGameStatus.GameOver)
                {
                    _logger.LogInformation("Asteroids game {ShortCode} ended", game.ShortCode);

                    await _hubContext.Clients.Group(groupName).SendAsync("GameOver", new
                    {
                        p1Score = result.P1Score,
                        p2Score = result.P2Score,
                        wave = result.Wave,
                        winner = result.P1Score >= result.P2Score ? game.Player1.Name : game.Player2!.Name
                    });

                    await PersistMatchResult(game);

                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(10_000);
                        _gameManager.RemoveGame(game.ShortCode);
                    });
                }
            }
        }
    }

    private void CleanupStaleGames()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-5);
        foreach (var game in _gameManager.GetAllGames())
        {
            if (game.Status == AsteroidGameStatus.Waiting && game.CreatedAt < cutoff)
            {
                _logger.LogInformation("Removing stale waiting Asteroids game: {ShortCode}", game.ShortCode);
                _gameManager.RemoveGame(game.ShortCode);
            }
        }
    }

    private async Task PersistMatchResult(AsteroidGame game)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AsteroidsDbContext>();

            var match = new AsteroidMatch
            {
                ShortCode = game.ShortCode,
                Player1Name = game.Player1.Name,
                Player2Name = game.Player2!.Name,
                Player1Score = game.Player1.Score,
                Player2Score = game.Player2.Score,
                WavesCompleted = game.Wave,
                Status = (int)game.Status,
                StartedAt = game.StartedAt ?? DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            db.AsteroidMatches.Add(match);
            await db.SaveChangesAsync();

            _logger.LogInformation("Asteroids match result persisted: {ShortCode}", game.ShortCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist Asteroids match result for {ShortCode}", game.ShortCode);
        }
    }
}
