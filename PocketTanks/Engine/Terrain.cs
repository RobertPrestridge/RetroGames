namespace PocketTanks.Engine;

public class Terrain
{
    public const int Width = 1200;
    private const float MinHeight = 200f;
    private const float MaxHeight = 500f;

    public float[] Heights { get; private set; } = new float[Width];

    public void Generate(int? seed = null)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();

        // Layered sine waves with random offsets
        var layers = new[]
        {
            (amplitude: 80f, frequency: 0.002f, offset: (float)(rng.NextDouble() * Math.PI * 2)),
            (amplitude: 50f, frequency: 0.005f, offset: (float)(rng.NextDouble() * Math.PI * 2)),
            (amplitude: 30f, frequency: 0.012f, offset: (float)(rng.NextDouble() * Math.PI * 2)),
            (amplitude: 15f, frequency: 0.025f, offset: (float)(rng.NextDouble() * Math.PI * 2)),
            (amplitude: 8f, frequency: 0.05f, offset: (float)(rng.NextDouble() * Math.PI * 2)),
        };

        float baseHeight = (MinHeight + MaxHeight) / 2f;

        for (int x = 0; x < Width; x++)
        {
            float height = baseHeight;
            foreach (var (amplitude, frequency, offset) in layers)
            {
                height += amplitude * MathF.Sin(x * frequency + offset);
            }

            Heights[x] = Math.Clamp(height, MinHeight, MaxHeight);
        }
    }

    public void Deform(float centerX, float radius)
    {
        int startX = Math.Max(0, (int)(centerX - radius));
        int endX = Math.Min(Width - 1, (int)(centerX + radius));

        for (int x = startX; x <= endX; x++)
        {
            float dx = x - centerX;
            float dist = MathF.Abs(dx);
            if (dist < radius)
            {
                // Circular subtraction - deeper at center
                float depth = MathF.Sqrt(radius * radius - dx * dx);
                Heights[x] = MathF.Max(10f, Heights[x] - depth);
            }
        }
    }

    public float GetHeightAt(float x)
    {
        if (x < 0) return 0;
        if (x >= Width - 1) return Heights[Width - 1];

        int ix = (int)x;
        float frac = x - ix;

        if (ix >= Width - 1) return Heights[Width - 1];

        return Heights[ix] * (1f - frac) + Heights[ix + 1] * frac;
    }

    /// <summary>
    /// Returns every 4th column for compact network payload. Client interpolates.
    /// </summary>
    public float[] Serialize()
    {
        int count = (Width + 3) / 4;
        var result = new float[count];
        for (int i = 0; i < count; i++)
        {
            int x = i * 4;
            if (x < Width)
                result[i] = MathF.Round(Heights[x], 1);
        }
        return result;
    }
}
