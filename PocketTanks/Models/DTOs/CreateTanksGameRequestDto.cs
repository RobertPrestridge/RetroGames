namespace PocketTanks.Models.DTOs;

public class CreateTanksGameRequestDto
{
    public string PlayerName { get; set; } = string.Empty;
    public bool VsAi { get; set; }
}
