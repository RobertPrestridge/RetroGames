namespace Asteroids.Engine.GameObjects;

public class Ship
{
    public float X { get; set; }
    public float Y { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float Rotation { get; set; }
    public float Radius { get; set; } = 15f;
    public bool Alive { get; set; } = true;
    public int Lives { get; set; } = 3;
    public int Score { get; set; }
    public int FireCooldown { get; set; }
    public int InvulnerableTicks { get; set; }
    public int RespawnTicks { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ConnectionId { get; set; }
    public string SessionId { get; set; } = string.Empty;
}
