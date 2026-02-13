namespace TicTacToe.Models.ViewModels;

public class GameViewModel
{
    public string ShortCode { get; set; } = string.Empty;
    public string BoardState { get; set; } = string.Empty;
    public int Status { get; set; }
    public char CurrentTurn { get; set; }
}
