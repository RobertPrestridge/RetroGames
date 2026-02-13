namespace TicTacToe.Models.DTOs;

public class GameStateDto
{
    public string ShortCode { get; set; } = string.Empty;
    public string BoardState { get; set; } = string.Empty;
    public int Status { get; set; }
    public char CurrentTurn { get; set; }
    public int MoveCount { get; set; }
}
