namespace TicTacToe.Data.Entities;

public enum GameStatus
{
    Waiting = 0,
    InProgress = 1,
    XWins = 2,
    OWins = 3,
    Draw = 4,
    Abandoned = 5
}

public class Game
{
    public int Id { get; set; }
    public string ShortCode { get; set; } = string.Empty;
    public string BoardState { get; set; } = "         "; // 9 spaces
    public GameStatus Status { get; set; } = GameStatus.Waiting;
    public char CurrentTurn { get; set; } = 'X';

    public string? PlayerXConnectionId { get; set; }
    public string? PlayerOConnectionId { get; set; }
    public string? PlayerXSessionId { get; set; }
    public string? PlayerOSessionId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public int MoveCount { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
