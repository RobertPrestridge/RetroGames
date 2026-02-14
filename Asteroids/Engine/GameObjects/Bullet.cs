namespace Asteroids.Engine.GameObjects;

public class Bullet
{
    private static int _nextId;

    public int Id { get; set; } = Interlocked.Increment(ref _nextId);
    public float X { get; set; }
    public float Y { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public int OwnerPlayer { get; set; }
    public int TicksRemaining { get; set; }
    public float Radius { get; set; } = 2f;
}
