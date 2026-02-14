using Asteroids.Engine;
using Asteroids.Models.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Asteroids.Controllers.Api;

[ApiController]
[Route("api/asteroids/games")]
public class AsteroidsApiController : ControllerBase
{
    private readonly AsteroidGameManager _gameManager;
    private readonly ILogger<AsteroidsApiController> _logger;

    public AsteroidsApiController(AsteroidGameManager gameManager, ILogger<AsteroidsApiController> logger)
    {
        _gameManager = gameManager;
        _logger = logger;
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreateAsteroidsGameRequestDto request)
    {
        var playerName = string.IsNullOrWhiteSpace(request.PlayerName)
            ? "PLAYER 1"
            : request.PlayerName.Trim().ToUpperInvariant();

        if (playerName.Length > 20)
            playerName = playerName[..20];

        var (shortCode, sessionId) = _gameManager.CreateGame(playerName);

        Response.Cookies.Append($"asteroids_session_{shortCode}", sessionId, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromHours(24)
        });

        Response.Cookies.Append("asteroids_player_name", playerName, new CookieOptions
        {
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(30)
        });

        var url = $"{Request.Scheme}://{Request.Host}/asteroids/game/{shortCode}";

        _logger.LogInformation("Asteroids API: Game created {ShortCode} by {PlayerName}, URL={Url}", shortCode, playerName, url);

        return Ok(new CreateAsteroidsGameResponseDto
        {
            ShortCode = shortCode,
            Url = url,
            SessionId = sessionId
        });
    }
}
