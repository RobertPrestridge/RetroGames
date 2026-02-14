namespace Asteroids.Data.Entities;

public class AsteroidMatch
{
    public int Id { get; set; }
    public string ShortCode { get; set; } = string.Empty;
    public string Player1Name { get; set; } = string.Empty;
    public string Player2Name { get; set; } = string.Empty;
    public int Player1Score { get; set; }
    public int Player2Score { get; set; }
    public int WavesCompleted { get; set; }
    public int Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
}
