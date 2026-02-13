using LightCycles.Engine;
using LightCycles.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace LightCycles.Controllers.Api;

[ApiController]
[Route("api/tron/games")]
public class TronApiController : ControllerBase
{
    private readonly TronGameManager _gameManager;
    private readonly ILogger<TronApiController> _logger;

    public TronApiController(TronGameManager gameManager, ILogger<TronApiController> logger)
    {
        _gameManager = gameManager;
        _logger = logger;
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreateTronGameRequestDto request)
    {
        var playerName = string.IsNullOrWhiteSpace(request.PlayerName)
            ? "PLAYER 1"
            : request.PlayerName.Trim().ToUpperInvariant();

        if (playerName.Length > 20)
            playerName = playerName[..20];

        var (shortCode, sessionId) = _gameManager.CreateGame(playerName);

        // Set session cookie for this game
        Response.Cookies.Append($"tron_session_{shortCode}", sessionId, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromHours(24)
        });

        // Save player name cookie for convenience
        Response.Cookies.Append("tron_player_name", playerName, new CookieOptions
        {
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(30)
        });

        var url = $"{Request.Scheme}://{Request.Host}/tron/game/{shortCode}";

        _logger.LogInformation("Tron API: Game created {ShortCode} by {PlayerName}, URL={Url}", shortCode, playerName, url);

        return Ok(new CreateTronGameResponseDto
        {
            ShortCode = shortCode,
            Url = url,
            SessionId = sessionId
        });
    }

    [HttpGet("{code}")]
    public IActionResult Get(string code)
    {
        var game = _gameManager.GetGame(code.ToUpperInvariant());
        if (game is null)
            return NotFound(new { error = "Game not found." });

        return Ok(new TronGameStatusDto
        {
            ShortCode = game.ShortCode,
            Status = (int)game.Status,
            Player1Name = game.Player1.Name,
            Player2Name = game.Player2?.Name
        });
    }
}
