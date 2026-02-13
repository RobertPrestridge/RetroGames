namespace LightCycles.Engine;

public enum TronDirection
{
    Up,
    Down,
    Left,
    Right
}

public enum TronGameStatus
{
    Waiting,
    Countdown,
    InProgress,
    Player1Wins,
    Player2Wins,
    Draw,
    Abandoned
}

public class PlayerState
{
    public int X { get; set; }
    public int Y { get; set; }
    public TronDirection Direction { get; set; }
    public TronDirection? PendingDirection { get; set; }
    public bool Alive { get; set; } = true;
    public string Name { get; set; } = string.Empty;
    public string? ConnectionId { get; set; }
    public string SessionId { get; set; } = string.Empty;
}

public class TrailCell
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Player { get; set; }
}

public class TronTickResult
{
    public int P1X { get; set; }
    public int P1Y { get; set; }
    public bool P1Alive { get; set; }
    public int P2X { get; set; }
    public int P2Y { get; set; }
    public bool P2Alive { get; set; }
    public List<TrailCell> NewTrails { get; set; } = [];
    public TronGameStatus Status { get; set; }
    public string? WinnerName { get; set; }
}

public class TronGame
{
    public const int GridWidth = 60;
    public const int GridHeight = 40;
    private const int CountdownDurationTicks = 30; // 3 seconds at 100ms

    private readonly object _lock = new();

    public string ShortCode { get; }
    public byte[,] Grid { get; } = new byte[GridWidth, GridHeight]; // 0=empty, 1=P1 trail, 2=P2 trail
    public PlayerState Player1 { get; } = new();
    public PlayerState? Player2 { get; private set; }
    public TronGameStatus Status { get; private set; } = TronGameStatus.Waiting;
    public int TickCount { get; private set; }
    public int CountdownTicks { get; private set; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; private set; }

    public TronGame(string shortCode, string playerName, string sessionId)
    {
        ShortCode = shortCode;

        // Initialize P1 at left side facing right
        Player1.X = 15;
        Player1.Y = 20;
        Player1.Direction = TronDirection.Right;
        Player1.Name = playerName;
        Player1.SessionId = sessionId;

        // Place P1 starting position on grid
        Grid[Player1.X, Player1.Y] = 1;
    }

    public bool AddPlayer2(string playerName, string sessionId, string connectionId)
    {
        lock (_lock)
        {
            if (Status != TronGameStatus.Waiting || Player2 is not null)
                return false;

            Player2 = new PlayerState
            {
                X = 45,
                Y = 20,
                Direction = TronDirection.Left,
                Name = playerName,
                SessionId = sessionId,
                ConnectionId = connectionId
            };

            // Place P2 starting position on grid
            Grid[Player2.X, Player2.Y] = 2;

            Status = TronGameStatus.Countdown;
            CountdownTicks = CountdownDurationTicks;

            return true;
        }
    }

    public void SetDirection(string sessionId, TronDirection newDirection)
    {
        lock (_lock)
        {
            var player = GetPlayerBySession(sessionId);
            if (player is null || !player.Alive) return;

            // Can't reverse 180 degrees
            if (IsOpposite(player.Direction, newDirection)) return;

            // Queue the direction change for next tick
            player.PendingDirection = newDirection;
        }
    }

    public TronTickResult? Tick()
    {
        lock (_lock)
        {
            if (Status == TronGameStatus.Countdown)
            {
                CountdownTicks--;
                if (CountdownTicks <= 0)
                {
                    Status = TronGameStatus.InProgress;
                    StartedAt = DateTime.UtcNow;
                }
                return null; // Countdown ticks don't produce movement results
            }

            if (Status != TronGameStatus.InProgress)
                return null;

            if (Player2 is null) return null;

            TickCount++;

            // Apply pending direction changes
            if (Player1.PendingDirection.HasValue)
            {
                Player1.Direction = Player1.PendingDirection.Value;
                Player1.PendingDirection = null;
            }
            if (Player2.PendingDirection.HasValue)
            {
                Player2.Direction = Player2.PendingDirection.Value;
                Player2.PendingDirection = null;
            }

            // Calculate new positions
            var (p1NewX, p1NewY) = GetNextPosition(Player1);
            var (p2NewX, p2NewY) = GetNextPosition(Player2);

            var newTrails = new List<TrailCell>();

            // Check collisions
            bool p1Dead = false;
            bool p2Dead = false;

            // Wall collision
            if (p1NewX < 0 || p1NewX >= GridWidth || p1NewY < 0 || p1NewY >= GridHeight)
                p1Dead = true;
            if (p2NewX < 0 || p2NewX >= GridWidth || p2NewY < 0 || p2NewY >= GridHeight)
                p2Dead = true;

            // Head-on collision (both moving to same cell)
            if (!p1Dead && !p2Dead && p1NewX == p2NewX && p1NewY == p2NewY)
            {
                p1Dead = true;
                p2Dead = true;
            }

            // Head-on collision (swap positions)
            if (!p1Dead && !p2Dead &&
                p1NewX == Player2.X && p1NewY == Player2.Y &&
                p2NewX == Player1.X && p2NewY == Player1.Y)
            {
                p1Dead = true;
                p2Dead = true;
            }

            // Trail collision (check against existing grid)
            if (!p1Dead && Grid[p1NewX, p1NewY] != 0)
                p1Dead = true;
            if (!p2Dead && Grid[p2NewX, p2NewY] != 0)
                p2Dead = true;

            // Update state
            if (p1Dead) Player1.Alive = false;
            if (p2Dead) Player2.Alive = false;

            // If alive, move and leave trail
            if (!p1Dead)
            {
                Player1.X = p1NewX;
                Player1.Y = p1NewY;
                Grid[p1NewX, p1NewY] = 1;
                newTrails.Add(new TrailCell { X = p1NewX, Y = p1NewY, Player = 1 });
            }
            if (!p2Dead)
            {
                Player2.X = p2NewX;
                Player2.Y = p2NewY;
                Grid[p2NewX, p2NewY] = 2;
                newTrails.Add(new TrailCell { X = p2NewX, Y = p2NewY, Player = 2 });
            }

            // Determine game status
            if (p1Dead && p2Dead)
                Status = TronGameStatus.Draw;
            else if (p1Dead)
                Status = TronGameStatus.Player2Wins;
            else if (p2Dead)
                Status = TronGameStatus.Player1Wins;

            string? winnerName = Status switch
            {
                TronGameStatus.Player1Wins => Player1.Name,
                TronGameStatus.Player2Wins => Player2.Name,
                _ => null
            };

            return new TronTickResult
            {
                P1X = Player1.X,
                P1Y = Player1.Y,
                P1Alive = Player1.Alive,
                P2X = Player2.X,
                P2Y = Player2.Y,
                P2Alive = Player2.Alive,
                NewTrails = newTrails,
                Status = Status,
                WinnerName = winnerName
            };
        }
    }

    public PlayerState? GetPlayerBySession(string sessionId)
    {
        if (Player1.SessionId == sessionId) return Player1;
        if (Player2?.SessionId == sessionId) return Player2;
        return null;
    }

    public PlayerState? GetPlayerByConnection(string connectionId)
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

    public List<TrailCell> GetAllTrails()
    {
        var trails = new List<TrailCell>();
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                if (Grid[x, y] != 0)
                    trails.Add(new TrailCell { X = x, Y = y, Player = Grid[x, y] });
            }
        }
        return trails;
    }

    private static (int x, int y) GetNextPosition(PlayerState player)
    {
        return player.Direction switch
        {
            TronDirection.Up => (player.X, player.Y - 1),
            TronDirection.Down => (player.X, player.Y + 1),
            TronDirection.Left => (player.X - 1, player.Y),
            TronDirection.Right => (player.X + 1, player.Y),
            _ => (player.X, player.Y)
        };
    }

    private static bool IsOpposite(TronDirection current, TronDirection proposed)
    {
        return (current == TronDirection.Up && proposed == TronDirection.Down) ||
               (current == TronDirection.Down && proposed == TronDirection.Up) ||
               (current == TronDirection.Left && proposed == TronDirection.Right) ||
               (current == TronDirection.Right && proposed == TronDirection.Left);
    }
}
