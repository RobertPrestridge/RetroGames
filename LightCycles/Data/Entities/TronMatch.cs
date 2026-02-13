namespace LightCycles.Data.Entities;

public class TronMatch
{
    public int Id { get; set; }
    public string ShortCode { get; set; } = string.Empty;
    public string Player1Name { get; set; } = string.Empty;
    public string Player2Name { get; set; } = string.Empty;
    public string? WinnerName { get; set; }
    public int Status { get; set; }
    public int TickCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
}
