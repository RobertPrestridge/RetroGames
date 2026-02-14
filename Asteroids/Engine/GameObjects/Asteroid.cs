namespace Asteroids.Engine.GameObjects;

public enum AsteroidSize
{
    Large,
    Medium,
    Small
}

public class Asteroid
{
    private static int _nextId;

    public int Id { get; set; } = Interlocked.Increment(ref _nextId);
    public float X { get; set; }
    public float Y { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float Rotation { get; set; }
    public float RotationSpeed { get; set; }
    public AsteroidSize Size { get; set; }
    public int ShapeVariant { get; set; }

    public float Radius => Size switch
    {
        AsteroidSize.Large => 40f,
        AsteroidSize.Medium => 20f,
        AsteroidSize.Small => 10f,
        _ => 40f
    };

    public int Points => Size switch
    {
        AsteroidSize.Large => 20,
        AsteroidSize.Medium => 50,
        AsteroidSize.Small => 100,
        _ => 20
    };
}
