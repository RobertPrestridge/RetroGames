using PocketTanks.Engine;
using PocketTanks.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace PocketTanks.Controllers.Api;

[ApiController]
[Route("api/tanks/games")]
public class TanksApiController : ControllerBase
{
    private readonly TanksGameManager _gameManager;
    private readonly ILogger<TanksApiController> _logger;

    public TanksApiController(TanksGameManager gameManager, ILogger<TanksApiController> logger)
    {
        _gameManager = gameManager;
        _logger = logger;
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreateTanksGameRequestDto request)
    {
        var playerName = string.IsNullOrWhiteSpace(request.PlayerName)
            ? "PLAYER 1"
            : request.PlayerName.Trim().ToUpperInvariant();

        if (playerName.Length > 20)
            playerName = playerName[..20];

        var (shortCode, sessionId) = _gameManager.CreateGame(playerName, request.VsAi);

        // Set session cookie for this game
        Response.Cookies.Append($"tanks_session_{shortCode}", sessionId, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromHours(24)
        });

        // Save player name cookie for convenience
        Response.Cookies.Append("tanks_player_name", playerName, new CookieOptions
        {
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(30)
        });

        var url = $"{Request.Scheme}://{Request.Host}/tanks/game/{shortCode}";

        _logger.LogInformation("Tanks API: Game created {ShortCode} by {PlayerName}, URL={Url}", shortCode, playerName, url);

        return Ok(new CreateTanksGameResponseDto
        {
            ShortCode = shortCode,
            Url = url,
            SessionId = sessionId
        });
    }
}
