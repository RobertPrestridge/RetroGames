namespace PocketTanks.Models.DTOs;

public class CreateTanksGameResponseDto
{
    public string ShortCode { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
}
