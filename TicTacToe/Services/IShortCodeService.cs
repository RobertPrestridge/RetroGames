namespace TicTacToe.Services;

public interface IShortCodeService
{
    Task<string> GenerateUniqueCodeAsync();
}
