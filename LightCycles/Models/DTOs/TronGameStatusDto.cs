namespace LightCycles.Models.DTOs;

public class TronGameStatusDto
{
    public string ShortCode { get; set; } = string.Empty;
    public int Status { get; set; }
    public string Player1Name { get; set; } = string.Empty;
    public string? Player2Name { get; set; }
}
