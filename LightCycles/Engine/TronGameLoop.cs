using LightCycles.Data;
using LightCycles.Data.Entities;
using LightCycles.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace LightCycles.Engine;

public class TronGameLoop : BackgroundService
{
    private readonly TronGameManager _gameManager;
    private readonly IHubContext<TronHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TronGameLoop> _logger;

    public TronGameLoop(
        TronGameManager gameManager,
        IHubContext<TronHub> hubContext,
        IServiceProvider serviceProvider,
        ILogger<TronGameLoop> logger)
    {
        _gameManager = gameManager;
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Tron game loop started");

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickAllGames();
                CleanupStaleGames();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Tron game loop tick");
            }
        }
    }

    private async Task TickAllGames()
    {
        foreach (var game in _gameManager.GetActiveGames())
        {
            var groupName = $"tron_{game.ShortCode}";

            if (game.Status == TronGameStatus.Countdown)
            {
                var result = game.Tick(); // decrements countdown

                // Calculate seconds remaining (before tick decremented it)
                var secondsRemaining = (int)Math.Ceiling(game.CountdownTicks / 10.0);

                if (game.Status == TronGameStatus.InProgress)
                {
                    // Countdown just finished
                    await _hubContext.Clients.Group(groupName).SendAsync("Countdown", new { seconds = 0 });
                }
                else
                {
                    await _hubContext.Clients.Group(groupName).SendAsync("Countdown", new { seconds = secondsRemaining });
                }
            }
            else if (game.Status == TronGameStatus.InProgress)
            {
                var result = game.Tick();
                if (result is null) continue;

                await _hubContext.Clients.Group(groupName).SendAsync("Tick", new
                {
                    p1 = new { x = result.P1X, y = result.P1Y, alive = result.P1Alive },
                    p2 = new { x = result.P2X, y = result.P2Y, alive = result.P2Alive },
                    newTrails = result.NewTrails.Select(t => new { x = t.X, y = t.Y, player = t.Player }),
                    tick = game.TickCount
                });

                // Game over?
                if (result.Status is TronGameStatus.Player1Wins or TronGameStatus.Player2Wins or TronGameStatus.Draw)
                {
                    _logger.LogInformation("Tron game {ShortCode} ended: {Status}", game.ShortCode, result.Status);

                    await _hubContext.Clients.Group(groupName).SendAsync("GameOver", new
                    {
                        status = (int)result.Status,
                        winnerName = result.WinnerName
                    });

                    await PersistMatchResult(game, result);

                    // Schedule removal after a delay
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
            if (game.Status == TronGameStatus.Waiting && game.CreatedAt < cutoff)
            {
                _logger.LogInformation("Removing stale waiting Tron game: {ShortCode}", game.ShortCode);
                _gameManager.RemoveGame(game.ShortCode);
            }
        }
    }

    private async Task PersistMatchResult(TronGame game, TronTickResult result)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TronDbContext>();

            var match = new TronMatch
            {
                ShortCode = game.ShortCode,
                Player1Name = game.Player1.Name,
                Player2Name = game.Player2!.Name,
                WinnerName = result.WinnerName,
                Status = (int)result.Status,
                TickCount = game.TickCount,
                StartedAt = game.StartedAt ?? DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            db.TronMatches.Add(match);
            await db.SaveChangesAsync();

            _logger.LogInformation("Tron match result persisted: {ShortCode}, winner: {Winner}",
                game.ShortCode, result.WinnerName ?? "DRAW");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist Tron match result for {ShortCode}", game.ShortCode);
        }
    }
}
