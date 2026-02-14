using PocketTanks.Data;
using PocketTanks.Data.Entities;
using PocketTanks.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace PocketTanks.Engine;

public class TanksGameLoop : BackgroundService
{
    private readonly TanksGameManager _gameManager;
    private readonly IHubContext<TanksHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TanksGameLoop> _logger;

    public TanksGameLoop(
        TanksGameManager gameManager,
        IHubContext<TanksHub> hubContext,
        IServiceProvider serviceProvider,
        ILogger<TanksGameLoop> logger)
    {
        _gameManager = gameManager;
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Tanks game loop started");

        // Use a fast timer - we'll only do physics work when games are firing
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(TanksGame.PhysicsTickMs));

        int slowTickCounter = 0;
        const int slowTickInterval = 500 / TanksGame.PhysicsTickMs; // ~31 ticks
        int countdownTickCounter = 0;
        const int countdownTickInterval = 100 / TanksGame.PhysicsTickMs; // ~6 ticks = 100ms

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickFiringGames();

                countdownTickCounter++;
                if (countdownTickCounter >= countdownTickInterval)
                {
                    countdownTickCounter = 0;
                    await TickCountdownGames();
                }

                slowTickCounter++;
                if (slowTickCounter >= slowTickInterval)
                {
                    slowTickCounter = 0;
                    await HandleAiTurns();
                    await CheckTurnTimers();
                    CleanupStaleGames();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Tanks game loop tick");
            }
        }
    }

    private async Task TickFiringGames()
    {
        foreach (var game in _gameManager.GetActiveGames().Where(g => g.Status == TanksGameStatus.Firing))
        {
            var groupName = $"tanks_{game.ShortCode}";
            var result = game.PhysicsTick();

            // Send projectile positions
            if (result.Projectiles.Count > 0)
            {
                await _hubContext.Clients.Group(groupName).SendAsync("ProjectileTick", new
                {
                    projectiles = result.Projectiles.Select(p => new { x = p.X, y = p.Y, index = p.Index })
                });
            }

            // Send bounce events
            foreach (var bounce in result.Bounces)
            {
                await _hubContext.Clients.Group(groupName).SendAsync("Bounce", new
                {
                    x = bounce.X,
                    y = bounce.Y
                });
            }

            // Send roller tick events
            if (result.RollerTicks.Count > 0)
            {
                await _hubContext.Clients.Group(groupName).SendAsync("RollerTick", new
                {
                    positions = result.RollerTicks.Select(r => new { x = r.X, y = r.Y })
                });
            }

            // Send explosions
            foreach (var explosion in result.Explosions)
            {
                await _hubContext.Clients.Group(groupName).SendAsync("Explosion", new
                {
                    x = explosion.X,
                    y = explosion.Y,
                    radius = explosion.Radius,
                    weaponType = (int)explosion.WeaponType,
                    targetPlayer = explosion.TargetPlayer,
                    damage = Math.Round(explosion.Damage, 1),
                    directHit = explosion.DirectHit,
                    p1Health = explosion.P1Health,
                    p2Health = explosion.P2Health,
                    p1Score = explosion.P1Score,
                    p2Score = explosion.P2Score,
                    terrain = game.Terrain.Serialize()
                });

                // Send tank position updates after terrain deformation
                await _hubContext.Clients.Group(groupName).SendAsync("TankPositionUpdate", new
                {
                    p1 = new { x = game.Player1.X, y = game.Player1.Y },
                    p2 = game.Player2 != null ? new { x = game.Player2.X, y = game.Player2.Y } : null
                });
            }

            // Firing complete - send turn start or game over
            if (result.FiringComplete)
            {
                if (game.Status == TanksGameStatus.GameOver)
                {
                    _logger.LogInformation("Tanks game {ShortCode} ended. P1={P1Score}, P2={P2Score}",
                        game.ShortCode, game.Player1.Score, game.Player2?.Score);

                    await _hubContext.Clients.Group(groupName).SendAsync("GameOver", new
                    {
                        p1Score = game.Player1.Score,
                        p2Score = game.Player2?.Score ?? 0,
                        winner = game.GetWinnerName(),
                        p1Health = game.Player1.Health,
                        p2Health = game.Player2?.Health ?? 0
                    });

                    await PersistMatchResult(game);

                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(10_000);
                        _gameManager.RemoveGame(game.ShortCode);
                    });
                }
                else
                {
                    await SendTurnStart(game);
                }
            }
        }
    }

    private async Task TickCountdownGames()
    {
        foreach (var game in _gameManager.GetActiveGames().Where(g => g.Status == TanksGameStatus.Countdown))
        {
            var groupName = $"tanks_{game.ShortCode}";
            var prevStatus = game.Status;

            game.TickCountdown();

            var secondsRemaining = (int)Math.Ceiling(game.CountdownTicks / 10.0);

            if (game.Status == TanksGameStatus.WeaponSelect)
            {
                // Countdown just finished
                await _hubContext.Clients.Group(groupName).SendAsync("Countdown", new { seconds = 0 });
                await SendTurnStart(game);
            }
            else
            {
                await _hubContext.Clients.Group(groupName).SendAsync("Countdown", new { seconds = secondsRemaining });
            }
        }
    }

    private async Task CheckTurnTimers()
    {
        foreach (var game in _gameManager.GetActiveGames()
            .Where(g => g.Status is TanksGameStatus.WeaponSelect or TanksGameStatus.Aiming))
        {
            if (game.IsPhaseTimedOut())
            {
                var groupName = $"tanks_{game.ShortCode}";
                _logger.LogInformation("Tanks turn timer expired for {ShortCode}, auto-firing", game.ShortCode);

                game.AutoFire();

                await _hubContext.Clients.Group(groupName).SendAsync("AutoFire", new
                {
                    currentPlayer = game.CurrentTurn
                });
            }
        }
    }

    private async Task HandleAiTurns()
    {
        foreach (var game in _gameManager.GetActiveGames()
            .Where(g => g.IsAiGame && g.CurrentTurn == 2
                && g.Status is TanksGameStatus.WeaponSelect or TanksGameStatus.Aiming))
        {
            var elapsed = game.PhaseStartedAt.HasValue
                ? (DateTime.UtcNow - game.PhaseStartedAt.Value).TotalSeconds
                : 0;

            // Wait ~1.5 seconds into the phase so it doesn't feel instant
            if (elapsed < 1.5) continue;

            var groupName = $"tanks_{game.ShortCode}";

            if (game.Status == TanksGameStatus.WeaponSelect)
            {
                var (weaponType, angle, power) = game.GetAiMove();
                game.SelectWeapon(game.AiSessionId!, weaponType);

                // Store the AI's chosen angle/power on P2's tank state
                game.Player2!.Angle = angle;
                game.Player2.Power = power;

                var weapons = game.Player2Weapons;
                await _hubContext.Clients.Group(groupName).SendAsync("WeaponSelected", new
                {
                    playerNumber = 2,
                    weaponType = (int)(game.SelectedWeapon ?? WeaponType.Standard),
                    weaponName = game.SelectedWeapon.HasValue
                        ? WeaponData.Get(game.SelectedWeapon.Value).Name
                        : "Standard",
                    weapons = weapons.Select((w, i) => new { index = i, type = (int)w, name = WeaponData.Get(w).Name })
                });
            }
            else if (game.Status == TanksGameStatus.Aiming)
            {
                var angle = game.Player2!.Angle;
                var power = game.Player2.Power;
                game.Fire(game.AiSessionId!, angle, power);

                await _hubContext.Clients.Group(groupName).SendAsync("Fired", new
                {
                    player = 2,
                    angle,
                    power
                });
            }
        }
    }

    private async Task SendTurnStart(TanksGame game)
    {
        var groupName = $"tanks_{game.ShortCode}";
        var weapons = game.CurrentTurn == 1 ? game.Player1Weapons : game.Player2Weapons;

        await _hubContext.Clients.Group(groupName).SendAsync("TurnStart", new
        {
            currentPlayer = game.CurrentTurn,
            turnNumber = game.TurnNumber,
            timeLimit = 15,
            weapons = weapons.Select((w, i) => new
            {
                index = i,
                type = (int)w,
                name = WeaponData.Get(w).Name
            })
        });
    }

    private void CleanupStaleGames()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-5);
        foreach (var game in _gameManager.GetAllGames())
        {
            if (game.Status == TanksGameStatus.Waiting && game.CreatedAt < cutoff)
            {
                _logger.LogInformation("Removing stale waiting Tanks game: {ShortCode}", game.ShortCode);
                _gameManager.RemoveGame(game.ShortCode);
            }
        }
    }

    private async Task PersistMatchResult(TanksGame game)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TanksDbContext>();

            var match = new TanksMatch
            {
                ShortCode = game.ShortCode,
                Player1Name = game.Player1.Name,
                Player2Name = game.Player2?.Name ?? "UNKNOWN",
                Player1Score = game.Player1.Score,
                Player2Score = game.Player2?.Score ?? 0,
                TotalTurns = game.TurnNumber,
                Status = (int)game.Status,
                StartedAt = game.StartedAt ?? DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            db.TanksMatches.Add(match);
            await db.SaveChangesAsync();

            _logger.LogInformation("Tanks match result persisted: {ShortCode}, winner: {Winner}",
                game.ShortCode, game.GetWinnerName() ?? "DRAW");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist Tanks match result for {ShortCode}", game.ShortCode);
        }
    }
}
