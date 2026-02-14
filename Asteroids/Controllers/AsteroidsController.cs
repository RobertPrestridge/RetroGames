using Asteroids.Engine;
using Asteroids.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Asteroids.Controllers;

[Route("asteroids")]
public class AsteroidsController : Controller
{
    private readonly AsteroidGameManager _gameManager;

    public AsteroidsController(AsteroidGameManager gameManager)
    {
        _gameManager = gameManager;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewBag.PlayerName = Request.Cookies["asteroids_player_name"] ?? "";
        return View();
    }

    [HttpGet("game/{code}")]
    public IActionResult Play(string code)
    {
        var game = _gameManager.GetGame(code.ToUpperInvariant());
        if (game is null)
            return Redirect("/asteroids");

        var sessionId = Request.Cookies[$"asteroids_session_{game.ShortCode}"];
        var playerName = Request.Cookies["asteroids_player_name"] ?? "PLAYER";

        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = Guid.NewGuid().ToString("N");
            Response.Cookies.Append($"asteroids_session_{game.ShortCode}", sessionId, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromHours(24)
            });
        }

        var vm = new AsteroidsPlayViewModel
        {
            ShortCode = game.ShortCode,
            SessionId = sessionId,
            PlayerName = playerName
        };

        return View(vm);
    }
}
