namespace PocketTanks.Engine;

public enum WeaponType
{
    Standard,
    BigShot,
    Sniper,
    DirtMover,
    Bouncer,
    ThreeShot,
    Roller,
    Nuke
}

public class WeaponData
{
    public WeaponType Type { get; init; }
    public string Name { get; init; } = string.Empty;
    public float BlastRadius { get; init; }
    public float Damage { get; init; }
    public float VelocityMultiplier { get; init; } = 1.0f;
    public int MaxBounces { get; init; }
    public bool Rolls { get; init; }
    public int ProjectileCount { get; init; } = 1;
    public float SpreadAngle { get; init; }

    private static readonly Dictionary<WeaponType, WeaponData> _weapons = new()
    {
        [WeaponType.Standard] = new WeaponData
        {
            Type = WeaponType.Standard, Name = "STANDARD", BlastRadius = 30f, Damage = 20f
        },
        [WeaponType.BigShot] = new WeaponData
        {
            Type = WeaponType.BigShot, Name = "BIG SHOT", BlastRadius = 50f, Damage = 35f
        },
        [WeaponType.Sniper] = new WeaponData
        {
            Type = WeaponType.Sniper, Name = "SNIPER", BlastRadius = 15f, Damage = 40f,
            VelocityMultiplier = 1.8f
        },
        [WeaponType.DirtMover] = new WeaponData
        {
            Type = WeaponType.DirtMover, Name = "DIRT MOVER", BlastRadius = 90f, Damage = 5f
        },
        [WeaponType.Bouncer] = new WeaponData
        {
            Type = WeaponType.Bouncer, Name = "BOUNCER", BlastRadius = 25f, Damage = 15f, MaxBounces = 3
        },
        [WeaponType.ThreeShot] = new WeaponData
        {
            Type = WeaponType.ThreeShot, Name = "3-SHOT", BlastRadius = 25f, Damage = 15f,
            ProjectileCount = 3, SpreadAngle = 25f
        },
        [WeaponType.Roller] = new WeaponData
        {
            Type = WeaponType.Roller, Name = "ROLLER", BlastRadius = 20f, Damage = 25f, Rolls = true
        },
        [WeaponType.Nuke] = new WeaponData
        {
            Type = WeaponType.Nuke, Name = "NUKE", BlastRadius = 90f, Damage = 50f
        }
    };

    public static WeaponData Get(WeaponType type) => _weapons[type];

    public static List<WeaponType> GetRandomWeapons(int count, Random? rng = null)
    {
        rng ??= new Random();
        var allTypes = Enum.GetValues<WeaponType>().ToList();

        var result = new List<WeaponType>();

        // Always include one Standard
        result.Add(WeaponType.Standard);
        allTypes.Remove(WeaponType.Standard);

        // Fill remaining slots from shuffled pool, allowing duplicates from the pool
        while (result.Count < count)
        {
            var shuffled = allTypes.OrderBy(_ => rng.Next()).ToList();
            foreach (var t in shuffled)
            {
                if (result.Count >= count) break;
                result.Add(t);
            }
        }

        // Shuffle final list
        for (int i = result.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }

        return result;
    }
}
