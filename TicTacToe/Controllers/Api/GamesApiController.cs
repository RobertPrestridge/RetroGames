using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TicTacToe.Models.DTOs;
using TicTacToe.Services;

namespace TicTacToe.Controllers.Api;

[ApiController]
[Route("api/tictactoe/games")]
public class GamesApiController : ControllerBase
{
    private readonly IGameService _gameService;
    private readonly ILogger<GamesApiController> _logger;

    public GamesApiController(IGameService gameService, ILogger<GamesApiController> logger)
    {
        _gameService = gameService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Create()
    {
        var result = await _gameService.CreateGameAsync();

        Response.Cookies.Append($"session_{result.ShortCode}", result.SessionId, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromHours(24)
        });

        var url = $"{Request.Scheme}://{Request.Host}/tictactoe/game/{result.ShortCode}";

        _logger.LogInformation("API: Game created {ShortCode}, URL={Url}", result.ShortCode, url);

        return Ok(new CreateGameResponseDto
        {
            ShortCode = result.ShortCode,
            Url = url,
            SessionId = result.SessionId
        });
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> Get(string code)
    {
        var game = await _gameService.GetGameByCodeAsync(code.ToUpperInvariant());
        if (game is null)
        {
            _logger.LogWarning("API: Game not found for code {Code}", code);
            return NotFound(new { error = "Game not found." });
        }

        _logger.LogInformation("API: Game state requested for {ShortCode}, status={Status}", game.ShortCode, game.Status);

        return Ok(new GameStateDto
        {
            ShortCode = game.ShortCode,
            BoardState = game.BoardState,
            Status = (int)game.Status,
            CurrentTurn = game.CurrentTurn,
            MoveCount = game.MoveCount
        });
    }
}
