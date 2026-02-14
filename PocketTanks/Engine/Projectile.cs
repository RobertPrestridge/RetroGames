namespace PocketTanks.Engine;

public class Projectile
{
    public float X { get; set; }
    public float Y { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public WeaponType WeaponType { get; set; }
    public int OwnerPlayer { get; set; }
    public bool Active { get; set; } = true;
    public int BounceCount { get; set; }
    public int TicksAlive { get; set; }
    public bool IsRolling { get; set; }
    public float RollDirection { get; set; }

    public void Tick(float gravity)
    {
        if (!Active) return;

        TicksAlive++;

        if (IsRolling)
        {
            // Rolling along terrain - just horizontal movement with friction
            VelocityX *= 0.97f;
            X += VelocityX;

            if (MathF.Abs(VelocityX) < 0.3f)
            {
                // Stopped rolling - detonate
                Active = false;
            }
        }
        else
        {
            VelocityY += gravity;
            X += VelocityX;
            Y += VelocityY;
        }
    }
}
