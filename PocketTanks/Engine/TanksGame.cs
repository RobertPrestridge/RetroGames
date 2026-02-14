namespace PocketTanks.Engine;

public enum TanksGameStatus
{
    Waiting,
    Countdown,
    WeaponSelect,
    Aiming,
    Firing,
    GameOver,
    Abandoned
}

public class TankState
{
    public float X { get; set; }
    public float Y { get; set; }
    public int Health { get; set; } = 100;
    public int Score { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string? ConnectionId { get; set; }
    public float Angle { get; set; } = 45f;
    public float Power { get; set; } = 50f;
}

public class ExplosionEvent
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Radius { get; set; }
    public WeaponType WeaponType { get; set; }
    public int TargetPlayer { get; set; }
    public float Damage { get; set; }
    public bool DirectHit { get; set; }
    public int P1Health { get; set; }
    public int P2Health { get; set; }
    public int P1Score { get; set; }
    public int P2Score { get; set; }
}

public class BounceEvent
{
    public float X { get; set; }
    public float Y { get; set; }
}

public class RollerTickEvent
{
    public float X { get; set; }
    public float Y { get; set; }
}

public class TanksTickResult
{
    public List<ProjectilePosition> Projectiles { get; set; } = [];
    public List<ExplosionEvent> Explosions { get; set; } = [];
    public List<BounceEvent> Bounces { get; set; } = [];
    public List<RollerTickEvent> RollerTicks { get; set; } = [];
    public bool FiringComplete { get; set; }
}

public class ProjectilePosition
{
    public float X { get; set; }
    public float Y { get; set; }
    public int Index { get; set; }
}

public class TanksGame
{
    public const float ArenaWidth = 1200f;
    public const float ArenaHeight = 800f;
    public const int TurnsPerPlayer = 10;
    public const int PhysicsTickMs = 16;
    public const float Gravity = 0.15f;
    public const float MaxPower = 100f;
    public const float TankWidth = 30f;
    public const float TankHeight = 16f;
    public const float BarrelLength = 22f;

    private const int CountdownDurationTicks = 30; // 3s at 100ms
    private const float WeaponSelectTimeSec = 15f;
    private const float AimingTimeSec = 15f;

    private readonly object _lock = new();

    public string ShortCode { get; }
    public TanksGameStatus Status { get; private set; } = TanksGameStatus.Waiting;
    public TankState Player1 { get; } = new();
    public TankState? Player2 { get; private set; }
    public bool IsAiGame { get; private set; }
    public string? AiSessionId { get; private set; }
    public Terrain Terrain { get; } = new();
    public int CurrentTurn { get; private set; } = 1; // 1 or 2 (which player)
    public int TurnNumber { get; private set; } = 1; // 1-20
    public List<Projectile> ActiveProjectiles { get; } = [];
    public List<WeaponType> Player1Weapons { get; private set; } = [];
    public List<WeaponType> Player2Weapons { get; private set; } = [];
    public WeaponType? SelectedWeapon { get; private set; }
    public int CountdownTicks { get; private set; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; private set; }
    public DateTime? PhaseStartedAt { get; private set; }

    public TanksGame(string shortCode, string playerName, string sessionId)
    {
        ShortCode = shortCode;
        Player1.Name = playerName;
        Player1.SessionId = sessionId;

        // Generate terrain
        Terrain.Generate();

        // Position P1 at ~20% from left
        Player1.X = ArenaWidth * 0.2f;
        Player1.Y = ArenaHeight - Terrain.GetHeightAt(Player1.X);

        // Generate weapons
        var rng = new Random();
        Player1Weapons = WeaponData.GetRandomWeapons(TurnsPerPlayer, rng);
        Player2Weapons = WeaponData.GetRandomWeapons(TurnsPerPlayer, rng);
    }

    public bool AddPlayer2(string playerName, string sessionId, string connectionId)
    {
        lock (_lock)
        {
            if (Status != TanksGameStatus.Waiting || Player2 is not null)
                return false;

            Player2 = new TankState
            {
                Name = playerName,
                SessionId = sessionId,
                ConnectionId = connectionId,
                Angle = 135f, // Facing left
                Power = 50f
            };

            // Position P2 at ~80% from left
            Player2.X = ArenaWidth * 0.8f;
            Player2.Y = ArenaHeight - Terrain.GetHeightAt(Player2.X);

            Status = TanksGameStatus.Countdown;
            CountdownTicks = CountdownDurationTicks;

            return true;
        }
    }

    public void AddAiPlayer2()
    {
        lock (_lock)
        {
            IsAiGame = true;
            AiSessionId = $"ai_{ShortCode}";

            Player2 = new TankState
            {
                Name = "CPU",
                SessionId = AiSessionId,
                ConnectionId = null,
                Angle = 135f,
                Power = 50f
            };

            Player2.X = ArenaWidth * 0.8f;
            Player2.Y = ArenaHeight - Terrain.GetHeightAt(Player2.X);

            Status = TanksGameStatus.Countdown;
            CountdownTicks = CountdownDurationTicks;
        }
    }

    private static readonly Random _aiRng = new();

    public (WeaponType weaponType, float angle, float power) GetAiMove()
    {
        var weapons = Player2Weapons;
        int idx = _aiRng.Next(weapons.Count);
        WeaponType weaponType = weapons[idx];
        float angle = 95f + (float)(_aiRng.NextDouble() * 70); // 95-165
        float power = 40f + (float)(_aiRng.NextDouble() * 50);  // 40-90
        return (weaponType, angle, power);
    }

    public void TickCountdown()
    {
        lock (_lock)
        {
            if (Status != TanksGameStatus.Countdown) return;

            CountdownTicks--;
            if (CountdownTicks <= 0)
            {
                Status = TanksGameStatus.WeaponSelect;
                StartedAt = DateTime.UtcNow;
                PhaseStartedAt = DateTime.UtcNow;
                CurrentTurn = 1;
                TurnNumber = 1;
            }
        }
    }

    public bool SelectWeapon(string sessionId, WeaponType weaponType)
    {
        lock (_lock)
        {
            if (Status != TanksGameStatus.WeaponSelect && Status != TanksGameStatus.Aiming)
                return false;

            var playerNum = GetPlayerNumber(sessionId);
            if (playerNum != CurrentTurn) return false;

            var weapons = playerNum == 1 ? Player1Weapons : Player2Weapons;

            // Return current weapon to inventory if swapping during Aiming
            if (Status == TanksGameStatus.Aiming && SelectedWeapon.HasValue)
                weapons.Add(SelectedWeapon.Value);

            var idx = weapons.IndexOf(weaponType);
            if (idx < 0) return false;

            SelectedWeapon = weapons[idx];
            weapons.RemoveAt(idx);

            if (Status == TanksGameStatus.WeaponSelect)
            {
                Status = TanksGameStatus.Aiming;
                PhaseStartedAt = DateTime.UtcNow;
            }

            return true;
        }
    }

    public void SetFiringParams(string sessionId, float angle, float power)
    {
        lock (_lock)
        {
            if (Status != TanksGameStatus.Aiming) return;

            var playerNum = GetPlayerNumber(sessionId);
            if (playerNum != CurrentTurn) return;

            var tank = playerNum == 1 ? Player1 : Player2!;
            tank.Angle = Math.Clamp(angle, 0f, 180f);
            tank.Power = Math.Clamp(power, 1f, MaxPower);
        }
    }

    public bool Fire(string sessionId, float angle, float power)
    {
        lock (_lock)
        {
            if (Status != TanksGameStatus.Aiming) return false;

            var playerNum = GetPlayerNumber(sessionId);
            if (playerNum != CurrentTurn) return false;

            var tank = playerNum == 1 ? Player1 : Player2!;
            tank.Angle = Math.Clamp(angle, 0f, 180f);
            tank.Power = Math.Clamp(power, 1f, MaxPower);

            return LaunchProjectiles(tank, playerNum);
        }
    }

    public bool AutoFire()
    {
        lock (_lock)
        {
            if (Status != TanksGameStatus.Aiming && Status != TanksGameStatus.WeaponSelect)
                return false;

            var tank = CurrentTurn == 1 ? Player1 : Player2!;

            // Auto-select weapon if still in weapon select
            if (Status == TanksGameStatus.WeaponSelect)
            {
                var weapons = CurrentTurn == 1 ? Player1Weapons : Player2Weapons;
                if (weapons.Count > 0)
                {
                    SelectedWeapon = weapons[0];
                    weapons.RemoveAt(0);
                }
                else
                {
                    SelectedWeapon = WeaponType.Standard;
                }
                Status = TanksGameStatus.Aiming;
            }

            return LaunchProjectiles(tank, CurrentTurn);
        }
    }

    private bool LaunchProjectiles(TankState tank, int playerNum)
    {
        if (SelectedWeapon is null) return false;

        var weapon = WeaponData.Get(SelectedWeapon.Value);

        // Calculate barrel tip position
        float angleRad = tank.Angle * MathF.PI / 180f;
        float barrelTipX = tank.X + MathF.Cos(angleRad) * BarrelLength;
        float barrelTipY = tank.Y - MathF.Sin(angleRad) * BarrelLength;

        float powerScale = tank.Power / MaxPower * 12f * weapon.VelocityMultiplier; // Max velocity scalar

        if (weapon.ProjectileCount > 1)
        {
            // Three shot - spread
            float baseAngle = tank.Angle;
            for (int i = 0; i < weapon.ProjectileCount; i++)
            {
                float spreadOffset = (i - (weapon.ProjectileCount - 1) / 2f) * weapon.SpreadAngle;
                float shotAngle = (baseAngle + spreadOffset) * MathF.PI / 180f;

                ActiveProjectiles.Add(new Projectile
                {
                    X = barrelTipX,
                    Y = barrelTipY,
                    VelocityX = MathF.Cos(shotAngle) * powerScale,
                    VelocityY = -MathF.Sin(shotAngle) * powerScale,
                    WeaponType = SelectedWeapon.Value,
                    OwnerPlayer = playerNum
                });
            }
        }
        else
        {
            ActiveProjectiles.Add(new Projectile
            {
                X = barrelTipX,
                Y = barrelTipY,
                VelocityX = MathF.Cos(angleRad) * powerScale,
                VelocityY = -MathF.Sin(angleRad) * powerScale,
                WeaponType = SelectedWeapon.Value,
                OwnerPlayer = playerNum
            });
        }

        Status = TanksGameStatus.Firing;
        return true;
    }

    public TanksTickResult PhysicsTick()
    {
        lock (_lock)
        {
            var result = new TanksTickResult();

            if (Status != TanksGameStatus.Firing) return result;

            foreach (var proj in ActiveProjectiles.Where(p => p.Active))
            {
                proj.Tick(Gravity);

                // Out of bounds check
                if (proj.X < -50 || proj.X > ArenaWidth + 50 || proj.Y > ArenaHeight + 200)
                {
                    proj.Active = false;
                    continue;
                }

                // Terrain collision (Y is from top, terrain height is from bottom)
                float terrainY = ArenaHeight - Terrain.GetHeightAt(proj.X);
                if (proj.Y >= terrainY && proj.X >= 0 && proj.X < Terrain.Width)
                {
                    var weapon = WeaponData.Get(proj.WeaponType);

                    // Bouncer logic
                    if (weapon.MaxBounces > 0 && proj.BounceCount < weapon.MaxBounces)
                    {
                        proj.BounceCount++;
                        proj.VelocityY = -MathF.Abs(proj.VelocityY) * 0.6f;
                        proj.Y = terrainY - 2f;
                        result.Bounces.Add(new BounceEvent { X = proj.X, Y = proj.Y });
                        continue;
                    }

                    // Roller logic
                    if (weapon.Rolls && !proj.IsRolling)
                    {
                        proj.IsRolling = true;
                        proj.Y = terrainY;
                        proj.VelocityY = 0;
                        // Keep horizontal velocity, apply terrain slope direction
                        if (MathF.Abs(proj.VelocityX) < 1f)
                            proj.VelocityX = proj.VelocityX >= 0 ? 3f : -3f;
                        continue;
                    }

                    // Impact!
                    proj.Active = false;
                    var explosion = ApplyExplosion(proj.X, proj.Y, weapon, proj.OwnerPlayer);
                    result.Explosions.Add(explosion);
                    continue;
                }

                // Tank collision check
                if (proj.TicksAlive > 5) // Skip first few ticks to avoid self-hit
                {
                    if (CheckTankHit(proj, Player1, 1, result) || CheckTankHit(proj, Player2!, 2, result))
                        continue;
                }

                // Roller terrain following
                if (proj.IsRolling)
                {
                    float rollerTerrainY = ArenaHeight - Terrain.GetHeightAt(proj.X);
                    proj.Y = rollerTerrainY;
                    result.RollerTicks.Add(new RollerTickEvent { X = proj.X, Y = proj.Y });

                    if (!proj.Active)
                    {
                        var weapon = WeaponData.Get(proj.WeaponType);
                        result.Explosions.Add(ApplyExplosion(proj.X, proj.Y, weapon, proj.OwnerPlayer));
                    }
                }

                if (proj.Active)
                {
                    result.Projectiles.Add(new ProjectilePosition
                    {
                        X = proj.X,
                        Y = proj.Y,
                        Index = ActiveProjectiles.IndexOf(proj)
                    });
                }
            }

            // Check if all projectiles are done
            if (!ActiveProjectiles.Any(p => p.Active))
            {
                result.FiringComplete = true;
                ActiveProjectiles.Clear();
                AdvanceTurn();
            }

            return result;
        }
    }

    private bool CheckTankHit(Projectile proj, TankState tank, int tankPlayer, TanksTickResult result)
    {
        float dx = proj.X - tank.X;
        float dy = proj.Y - tank.Y;
        float halfW = TankWidth / 2f;
        float halfH = TankHeight / 2f;

        if (MathF.Abs(dx) < halfW + 5f && MathF.Abs(dy) < halfH + 5f)
        {
            proj.Active = false;
            var weapon = WeaponData.Get(proj.WeaponType);
            var explosion = ApplyExplosion(proj.X, proj.Y, weapon, proj.OwnerPlayer);
            result.Explosions.Add(explosion);
            return true;
        }

        return false;
    }

    private ExplosionEvent ApplyExplosion(float x, float y, WeaponData weapon, int ownerPlayer)
    {
        // Deform terrain
        Terrain.Deform(x, weapon.BlastRadius);

        // Calculate damage to both tanks
        float p1Damage = CalculateDamage(x, y, Player1, weapon);
        float p2Damage = Player2 != null ? CalculateDamage(x, y, Player2, weapon) : 0;

        bool directHit = false;
        int targetPlayer = 0;
        float totalDamage = 0;

        // Apply damage to opponent (not self)
        if (ownerPlayer == 1 && p2Damage > 0)
        {
            Player2!.Health = Math.Max(0, Player2.Health - (int)Math.Round(p2Damage));
            Player1.Score += (int)Math.Round(p2Damage);
            targetPlayer = 2;
            totalDamage = p2Damage;
            directHit = GetDistanceToTank(x, y, Player2) < TankWidth / 2f;
        }
        else if (ownerPlayer == 2 && p1Damage > 0)
        {
            Player1.Health = Math.Max(0, Player1.Health - (int)Math.Round(p1Damage));
            Player2!.Score += (int)Math.Round(p1Damage);
            targetPlayer = 1;
            totalDamage = p1Damage;
            directHit = GetDistanceToTank(x, y, Player1) < TankWidth / 2f;
        }

        // Self-damage (reduced)
        if (ownerPlayer == 1 && p1Damage > 0)
        {
            float selfDmg = p1Damage * 0.5f;
            Player1.Health = Math.Max(0, Player1.Health - (int)Math.Round(selfDmg));
        }
        else if (ownerPlayer == 2 && p2Damage > 0)
        {
            float selfDmg = p2Damage * 0.5f;
            Player2!.Health = Math.Max(0, Player2.Health - (int)Math.Round(selfDmg));
        }

        // Update tank Y positions (settle on terrain)
        Player1.Y = ArenaHeight - Terrain.GetHeightAt(Player1.X);
        if (Player2 != null)
            Player2.Y = ArenaHeight - Terrain.GetHeightAt(Player2.X);

        return new ExplosionEvent
        {
            X = x,
            Y = y,
            Radius = weapon.BlastRadius,
            WeaponType = weapon.Type,
            TargetPlayer = targetPlayer,
            Damage = totalDamage,
            DirectHit = directHit,
            P1Health = Player1.Health,
            P2Health = Player2?.Health ?? 0,
            P1Score = Player1.Score,
            P2Score = Player2?.Score ?? 0
        };
    }

    private float CalculateDamage(float expX, float expY, TankState tank, WeaponData weapon)
    {
        float dist = GetDistanceToTank(expX, expY, tank);
        if (dist >= weapon.BlastRadius) return 0;

        float damage = weapon.Damage * (1f - dist / weapon.BlastRadius);

        // Direct hit bonus
        if (dist < TankWidth / 2f)
            damage *= 1.5f;

        return MathF.Max(0, damage);
    }

    private static float GetDistanceToTank(float x, float y, TankState tank)
    {
        float dx = x - tank.X;
        float dy = y - tank.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private void AdvanceTurn()
    {
        TurnNumber++;
        if (TurnNumber > TurnsPerPlayer * 2 || Player1.Health <= 0 || Player2?.Health <= 0)
        {
            Status = TanksGameStatus.GameOver;
            return;
        }

        CurrentTurn = CurrentTurn == 1 ? 2 : 1;
        SelectedWeapon = null;
        Status = TanksGameStatus.WeaponSelect;
        PhaseStartedAt = DateTime.UtcNow;
    }

    public bool IsPhaseTimedOut()
    {
        if (PhaseStartedAt is null) return false;

        double elapsed = (DateTime.UtcNow - PhaseStartedAt.Value).TotalSeconds;
        return Status switch
        {
            TanksGameStatus.WeaponSelect => elapsed >= WeaponSelectTimeSec,
            TanksGameStatus.Aiming => elapsed >= AimingTimeSec,
            _ => false
        };
    }

    public float GetPhaseTimeRemaining()
    {
        if (PhaseStartedAt is null) return 0;

        double elapsed = (DateTime.UtcNow - PhaseStartedAt.Value).TotalSeconds;
        float limit = Status switch
        {
            TanksGameStatus.WeaponSelect => WeaponSelectTimeSec,
            TanksGameStatus.Aiming => AimingTimeSec,
            _ => 0
        };

        return MathF.Max(0, limit - (float)elapsed);
    }

    public int GetPlayerNumber(string sessionId)
    {
        if (Player1.SessionId == sessionId) return 1;
        if (Player2?.SessionId == sessionId) return 2;
        return 0;
    }

    public TankState? GetPlayerBySession(string sessionId)
    {
        if (Player1.SessionId == sessionId) return Player1;
        if (Player2?.SessionId == sessionId) return Player2;
        return null;
    }

    public TankState? GetPlayerByConnection(string connectionId)
    {
        if (Player1.ConnectionId == connectionId) return Player1;
        if (Player2?.ConnectionId == connectionId) return Player2;
        return null;
    }

    public string? GetWinnerName()
    {
        if (Status != TanksGameStatus.GameOver) return null;
        if (Player1.Score > (Player2?.Score ?? 0)) return Player1.Name;
        if ((Player2?.Score ?? 0) > Player1.Score) return Player2!.Name;
        return null; // Draw
    }
}
