using Asteroids.Engine.GameObjects;

namespace Asteroids.Engine;

public enum AsteroidGameStatus
{
    Waiting,
    Countdown,
    InProgress,
    GameOver,
    Abandoned
}

public class PlayerInput
{
    public bool Thrust { get; set; }
    public bool RotateLeft { get; set; }
    public bool RotateRight { get; set; }
    public bool Fire { get; set; }
    public bool Nuke { get; set; }
}

public class ExplosionEvent
{
    public float X { get; set; }
    public float Y { get; set; }
    public string Size { get; set; } = "small";
}

public class AsteroidTickResult
{
    public int Tick { get; set; }
    public ShipState P1 { get; set; } = new();
    public ShipState? P2 { get; set; }
    public List<AsteroidState> Asteroids { get; set; } = [];
    public List<BulletState> Bullets { get; set; } = [];
    public List<ExplosionEvent> Explosions { get; set; } = [];
    public int P1Score { get; set; }
    public int P2Score { get; set; }
    public int P1Lives { get; set; }
    public int P2Lives { get; set; }
    public int Wave { get; set; }
    public int? Status { get; set; }
    public int P1Nukes { get; set; }
    public int P2Nukes { get; set; }
}

public class ShipState
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Rotation { get; set; }
    public bool Alive { get; set; }
    public bool Thrusting { get; set; }
    public bool Invulnerable { get; set; }
}

public class AsteroidState
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Rotation { get; set; }
    public string Size { get; set; } = "large";
    public int ShapeVariant { get; set; }
}

public class BulletState
{
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public int Owner { get; set; }
}

public class AsteroidGame
{
    public const float ArenaWidth = 1200f;
    public const float ArenaHeight = 800f;

    private const float ShipThrust = 0.15f;
    private const float ShipMaxSpeed = 5.0f;
    private const float ShipDrag = 0.99f;
    private const float ShipRotationSpeed = 0.07f;
    private const float ShipRadius = 15f;
    private const int ShipStartLives = 3;
    private const int ShipInvulnerableTicks = 48;
    private const int ShipRespawnDelay = 32;
    private const float BulletSpeed = 7.0f;
    private const int BulletLifetime = 60;
    private const int BulletCooldown = 8;
    private const int MaxBulletsPerPlayer = 5;
    private const int WavePauseTicks = 32;
    private const int CountdownDurationTicks = 48;
    private const float SafeSpawnDistance = 100f;

    private readonly object _lock = new();
    private readonly Random _rng = new();

    private PlayerInput _p1Input = new();
    private PlayerInput _p2Input = new();
    private int _wavePauseRemaining;
    private bool _asteroidsChanged;

    public string ShortCode { get; }
    public Ship Player1 { get; } = new();
    public Ship? Player2 { get; private set; }
    public List<Asteroid> Asteroids { get; } = [];
    public List<Bullet> Bullets { get; } = [];
    public AsteroidGameStatus Status { get; private set; } = AsteroidGameStatus.Waiting;
    public int TickCount { get; private set; }
    public int CountdownTicks { get; private set; }
    public int Wave { get; private set; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; private set; }

    public AsteroidGame(string shortCode, string playerName, string sessionId)
    {
        ShortCode = shortCode;
        Player1.X = ArenaWidth / 3f;
        Player1.Y = ArenaHeight / 2f;
        Player1.Rotation = 0f;
        Player1.Lives = ShipStartLives;
        Player1.Name = playerName;
        Player1.SessionId = sessionId;
    }

    public bool AddPlayer2(string playerName, string sessionId, string connectionId)
    {
        lock (_lock)
        {
            if (Status != AsteroidGameStatus.Waiting || Player2 is not null)
                return false;

            Player2 = new Ship
            {
                X = ArenaWidth * 2f / 3f,
                Y = ArenaHeight / 2f,
                Rotation = (float)Math.PI,
                Lives = ShipStartLives,
                Name = playerName,
                SessionId = sessionId,
                ConnectionId = connectionId
            };

            Status = AsteroidGameStatus.Countdown;
            CountdownTicks = CountdownDurationTicks;

            return true;
        }
    }

    public void SetInput(string sessionId, PlayerInput input)
    {
        lock (_lock)
        {
            if (Player1.SessionId == sessionId)
                _p1Input = input;
            else if (Player2?.SessionId == sessionId)
                _p2Input = input;
        }
    }

    public AsteroidTickResult? Tick()
    {
        lock (_lock)
        {
            if (Status == AsteroidGameStatus.Countdown)
            {
                CountdownTicks--;
                if (CountdownTicks <= 0)
                {
                    Status = AsteroidGameStatus.InProgress;
                    StartedAt = DateTime.UtcNow;
                    Wave = 1;
                    SpawnWave();
                }
                return null;
            }

            if (Status != AsteroidGameStatus.InProgress)
                return null;

            if (Player2 is null) return null;

            TickCount++;

            var explosions = new List<ExplosionEvent>();
            _asteroidsChanged = false;

            // Process ship inputs
            ProcessShipInput(Player1, _p1Input);
            ProcessShipInput(Player2, _p2Input);

            // Move ships
            MoveShip(Player1);
            MoveShip(Player2);

            // Handle respawning
            HandleRespawn(Player1, 1);
            HandleRespawn(Player2, 2);

            // Move bullets
            MoveBullets();

            // Move asteroids
            MoveAsteroids();

            // Check nuke activation
            CheckNuke(Player1, _p1Input, explosions);
            CheckNuke(Player2, _p2Input, explosions);

            // Check bullet-asteroid collisions
            CheckBulletAsteroidCollisions(explosions);

            // Check ship-asteroid collisions
            CheckShipAsteroidCollisions(Player1, explosions);
            CheckShipAsteroidCollisions(Player2, explosions);

            // Check if wave is cleared
            if (Asteroids.Count == 0 && _wavePauseRemaining <= 0)
            {
                _wavePauseRemaining = WavePauseTicks;
            }

            if (_wavePauseRemaining > 0)
            {
                _wavePauseRemaining--;
                if (_wavePauseRemaining <= 0 && Asteroids.Count == 0)
                {
                    Wave++;
                    // Award a nuke every 3 waves
                    if (Wave % 3 == 1 && Wave > 1)
                    {
                        Player1.NukesRemaining++;
                        Player2.NukesRemaining++;
                    }
                    SpawnWave();
                }
            }

            // Check game over
            int? statusChange = null;
            if (Player1.Lives <= 0 && !Player1.Alive &&
                Player2.Lives <= 0 && !Player2.Alive)
            {
                Status = AsteroidGameStatus.GameOver;
                statusChange = (int)Status;
            }

            return new AsteroidTickResult
            {
                Tick = TickCount,
                P1 = MakeShipState(Player1, _p1Input),
                P2 = MakeShipState(Player2, _p2Input),
                Asteroids = _asteroidsChanged ? Asteroids.Select(MakeAsteroidState).ToList() : [],
                Bullets = Bullets.Select(MakeBulletState).ToList(),
                Explosions = explosions,
                P1Score = Player1.Score,
                P2Score = Player2.Score,
                P1Lives = Player1.Lives,
                P2Lives = Player2.Lives,
                Wave = Wave,
                Status = statusChange,
                P1Nukes = Player1.NukesRemaining,
                P2Nukes = Player2.NukesRemaining
            };
        }
    }

    private void ProcessShipInput(Ship ship, PlayerInput input)
    {
        if (!ship.Alive) return;

        if (input.RotateLeft)
            ship.Rotation -= ShipRotationSpeed;
        if (input.RotateRight)
            ship.Rotation += ShipRotationSpeed;

        if (input.Thrust)
        {
            ship.VelocityX += MathF.Cos(ship.Rotation) * ShipThrust;
            ship.VelocityY += MathF.Sin(ship.Rotation) * ShipThrust;

            var speed = MathF.Sqrt(ship.VelocityX * ship.VelocityX + ship.VelocityY * ship.VelocityY);
            if (speed > ShipMaxSpeed)
            {
                ship.VelocityX = ship.VelocityX / speed * ShipMaxSpeed;
                ship.VelocityY = ship.VelocityY / speed * ShipMaxSpeed;
            }
        }

        if (input.Fire && ship.FireCooldown <= 0)
        {
            var playerNum = ship == Player1 ? 1 : 2;
            var bulletCount = Bullets.Count(b => b.OwnerPlayer == playerNum);
            if (bulletCount < MaxBulletsPerPlayer)
            {
                Bullets.Add(new Bullet
                {
                    X = ship.X + MathF.Cos(ship.Rotation) * ShipRadius,
                    Y = ship.Y + MathF.Sin(ship.Rotation) * ShipRadius,
                    VelocityX = MathF.Cos(ship.Rotation) * BulletSpeed + ship.VelocityX * 0.3f,
                    VelocityY = MathF.Sin(ship.Rotation) * BulletSpeed + ship.VelocityY * 0.3f,
                    OwnerPlayer = playerNum,
                    TicksRemaining = BulletLifetime
                });
                ship.FireCooldown = BulletCooldown;
            }
        }

        if (ship.FireCooldown > 0)
            ship.FireCooldown--;

        if (ship.InvulnerableTicks > 0)
            ship.InvulnerableTicks--;

        // Reset nuke latch when key released
        if (!input.Nuke)
            ship.NukeFired = false;
    }

    private void MoveShip(Ship ship)
    {
        if (!ship.Alive) return;

        ship.VelocityX *= ShipDrag;
        ship.VelocityY *= ShipDrag;
        ship.X += ship.VelocityX;
        ship.Y += ship.VelocityY;
        ship.X = Wrap(ship.X, ArenaWidth);
        ship.Y = Wrap(ship.Y, ArenaHeight);
    }

    private void HandleRespawn(Ship ship, int playerNum)
    {
        if (ship.Alive || ship.Lives <= 0) return;

        ship.RespawnTicks--;
        if (ship.RespawnTicks <= 0)
        {
            ship.Alive = true;
            ship.InvulnerableTicks = ShipInvulnerableTicks;
            ship.VelocityX = 0;
            ship.VelocityY = 0;
            ship.X = playerNum == 1 ? ArenaWidth / 3f : ArenaWidth * 2f / 3f;
            ship.Y = ArenaHeight / 2f;
            ship.Rotation = playerNum == 1 ? 0f : MathF.PI;
        }
    }

    private void MoveBullets()
    {
        for (int i = Bullets.Count - 1; i >= 0; i--)
        {
            var b = Bullets[i];
            b.X += b.VelocityX;
            b.Y += b.VelocityY;
            b.X = Wrap(b.X, ArenaWidth);
            b.Y = Wrap(b.Y, ArenaHeight);
            b.TicksRemaining--;
            if (b.TicksRemaining <= 0)
                Bullets.RemoveAt(i);
        }
    }

    private void MoveAsteroids()
    {
        foreach (var a in Asteroids)
        {
            a.X += a.VelocityX;
            a.Y += a.VelocityY;
            a.X = Wrap(a.X, ArenaWidth);
            a.Y = Wrap(a.Y, ArenaHeight);
            a.Rotation += a.RotationSpeed;
        }
    }

    private void CheckBulletAsteroidCollisions(List<ExplosionEvent> explosions)
    {
        for (int bi = Bullets.Count - 1; bi >= 0; bi--)
        {
            var bullet = Bullets[bi];
            for (int ai = Asteroids.Count - 1; ai >= 0; ai--)
            {
                var asteroid = Asteroids[ai];
                if (!CircleCollision(bullet.X, bullet.Y, bullet.Radius, asteroid.X, asteroid.Y, asteroid.Radius))
                    continue;

                // Award points
                var scorer = bullet.OwnerPlayer == 1 ? Player1 : Player2!;
                scorer.Score += asteroid.Points;

                explosions.Add(new ExplosionEvent
                {
                    X = asteroid.X,
                    Y = asteroid.Y,
                    Size = asteroid.Size.ToString().ToLowerInvariant()
                });

                // Split asteroid
                SplitAsteroid(asteroid);

                Bullets.RemoveAt(bi);
                _asteroidsChanged = true;
                break;
            }
        }
    }

    private void CheckShipAsteroidCollisions(Ship ship, List<ExplosionEvent> explosions)
    {
        if (!ship.Alive || ship.InvulnerableTicks > 0) return;

        for (int ai = Asteroids.Count - 1; ai >= 0; ai--)
        {
            var asteroid = Asteroids[ai];
            if (!CircleCollision(ship.X, ship.Y, ShipRadius, asteroid.X, asteroid.Y, asteroid.Radius))
                continue;

            // Ship hit
            ship.Alive = false;
            ship.Lives--;
            ship.RespawnTicks = ShipRespawnDelay;
            ship.VelocityX = 0;
            ship.VelocityY = 0;

            explosions.Add(new ExplosionEvent { X = ship.X, Y = ship.Y, Size = "ship" });

            // Also destroy the asteroid
            explosions.Add(new ExplosionEvent
            {
                X = asteroid.X,
                Y = asteroid.Y,
                Size = asteroid.Size.ToString().ToLowerInvariant()
            });
            SplitAsteroid(asteroid);
            _asteroidsChanged = true;

            break;
        }
    }

    private void CheckNuke(Ship ship, PlayerInput input, List<ExplosionEvent> explosions)
    {
        if (!ship.Alive || !input.Nuke || ship.NukeFired) return;

        ship.NukeFired = true;

        if (ship.NukesRemaining <= 0) return;

        ship.NukesRemaining--;

        // Award points for all destroyed asteroids
        foreach (var asteroid in Asteroids)
        {
            ship.Score += asteroid.Points;
        }

        // Create nuke explosion at ship position
        explosions.Add(new ExplosionEvent
        {
            X = ship.X,
            Y = ship.Y,
            Size = "nuke"
        });

        // Destroy all asteroids
        Asteroids.Clear();
        _asteroidsChanged = true;
    }

    private void SplitAsteroid(Asteroid asteroid)
    {
        Asteroids.Remove(asteroid);

        if (asteroid.Size == AsteroidSize.Small)
            return;

        var newSize = asteroid.Size == AsteroidSize.Large ? AsteroidSize.Medium : AsteroidSize.Small;
        var speed = MathF.Sqrt(asteroid.VelocityX * asteroid.VelocityX + asteroid.VelocityY * asteroid.VelocityY);
        var baseAngle = MathF.Atan2(asteroid.VelocityY, asteroid.VelocityX);

        for (int i = 0; i < 2; i++)
        {
            var spreadAngle = baseAngle + (i == 0 ? -0.5f : 0.5f) + (float)(_rng.NextDouble() * 0.8 - 0.4);
            var newSpeed = speed * (1.2f + (float)_rng.NextDouble() * 0.5f);

            Asteroids.Add(new Asteroid
            {
                X = asteroid.X,
                Y = asteroid.Y,
                VelocityX = MathF.Cos(spreadAngle) * newSpeed,
                VelocityY = MathF.Sin(spreadAngle) * newSpeed,
                Size = newSize,
                Rotation = (float)(_rng.NextDouble() * MathF.Tau),
                RotationSpeed = (float)(_rng.NextDouble() * 0.06 - 0.03),
                ShapeVariant = _rng.Next(5)
            });
        }
    }

    private void SpawnWave()
    {
        var count = Math.Min(4 + Wave - 1, 11);
        var speedMult = 1.0f + (Wave - 1) * 0.1f;

        for (int i = 0; i < count; i++)
        {
            float x, y;
            do
            {
                // Spawn on edges
                var edge = _rng.Next(4);
                x = edge switch
                {
                    0 => 0,
                    1 => ArenaWidth,
                    2 => (float)(_rng.NextDouble() * ArenaWidth),
                    _ => (float)(_rng.NextDouble() * ArenaWidth)
                };
                y = edge switch
                {
                    0 => (float)(_rng.NextDouble() * ArenaHeight),
                    1 => (float)(_rng.NextDouble() * ArenaHeight),
                    2 => 0,
                    _ => ArenaHeight
                };
            } while (DistanceSq(x, y, Player1.X, Player1.Y) < SafeSpawnDistance * SafeSpawnDistance ||
                     (Player2 is not null && DistanceSq(x, y, Player2.X, Player2.Y) < SafeSpawnDistance * SafeSpawnDistance));

            var angle = (float)(_rng.NextDouble() * MathF.Tau);
            var speed = (0.5f + (float)_rng.NextDouble() * 1.0f) * speedMult;

            Asteroids.Add(new Asteroid
            {
                X = x,
                Y = y,
                VelocityX = MathF.Cos(angle) * speed,
                VelocityY = MathF.Sin(angle) * speed,
                Size = AsteroidSize.Large,
                Rotation = (float)(_rng.NextDouble() * MathF.Tau),
                RotationSpeed = (float)(_rng.NextDouble() * 0.04 - 0.02),
                ShapeVariant = _rng.Next(5)
            });
        }

        _asteroidsChanged = true;
    }

    public PlayerState? GetPlayerBySession(string sessionId)
    {
        if (Player1.SessionId == sessionId) return new PlayerState(Player1, 1);
        if (Player2?.SessionId == sessionId) return new PlayerState(Player2, 2);
        return null;
    }

    public Ship? GetShipByConnection(string connectionId)
    {
        if (Player1.ConnectionId == connectionId) return Player1;
        if (Player2?.ConnectionId == connectionId) return Player2;
        return null;
    }

    public int GetPlayerNumber(string sessionId)
    {
        if (Player1.SessionId == sessionId) return 1;
        if (Player2?.SessionId == sessionId) return 2;
        return 0;
    }

    public AsteroidTickResult GetFullState(int playerNumber)
    {
        lock (_lock)
        {
            return new AsteroidTickResult
            {
                Tick = TickCount,
                P1 = MakeShipState(Player1, _p1Input),
                P2 = Player2 is not null ? MakeShipState(Player2, _p2Input) : null,
                Asteroids = Asteroids.Select(MakeAsteroidState).ToList(),
                Bullets = Bullets.Select(MakeBulletState).ToList(),
                Explosions = [],
                P1Score = Player1.Score,
                P2Score = Player2?.Score ?? 0,
                P1Lives = Player1.Lives,
                P2Lives = Player2?.Lives ?? 0,
                Wave = Wave,
                Status = (int)Status,
                P1Nukes = Player1.NukesRemaining,
                P2Nukes = Player2?.NukesRemaining ?? 0
            };
        }
    }

    private static ShipState MakeShipState(Ship s, PlayerInput input) => new()
    {
        X = s.X,
        Y = s.Y,
        Rotation = s.Rotation,
        Alive = s.Alive,
        Thrusting = input.Thrust && s.Alive,
        Invulnerable = s.InvulnerableTicks > 0
    };

    private static AsteroidState MakeAsteroidState(Asteroid a) => new()
    {
        Id = a.Id,
        X = a.X,
        Y = a.Y,
        Rotation = a.Rotation,
        Size = a.Size.ToString().ToLowerInvariant(),
        ShapeVariant = a.ShapeVariant
    };

    private static BulletState MakeBulletState(Bullet b) => new()
    {
        Id = b.Id,
        X = b.X,
        Y = b.Y,
        Owner = b.OwnerPlayer
    };

    private static bool CircleCollision(float x1, float y1, float r1, float x2, float y2, float r2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        var dist = r1 + r2;
        return dx * dx + dy * dy < dist * dist;
    }

    private static float DistanceSq(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return dx * dx + dy * dy;
    }

    private static float Wrap(float value, float max)
    {
        if (value < 0) return value + max;
        if (value >= max) return value - max;
        return value;
    }

}

public record PlayerState(Ship Ship, int Number);
