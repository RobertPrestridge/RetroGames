using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TicTacToe.Data;

namespace TicTacToe.Services;

public class ShortCodeService : IShortCodeService
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 6;

    private readonly ApplicationDbContext _db;

    public ShortCodeService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string> GenerateUniqueCodeAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = GenerateCode();
            var exists = await _db.Games.AnyAsync(g => g.ShortCode == code);
            if (!exists)
                return code;
        }

        throw new InvalidOperationException("Failed to generate a unique short code after 10 attempts.");
    }

    private static string GenerateCode()
    {
        Span<char> result = stackalloc char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            result[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(result);
    }
}
