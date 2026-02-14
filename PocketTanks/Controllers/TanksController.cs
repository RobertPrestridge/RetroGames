using PocketTanks.Engine;
using PocketTanks.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace PocketTanks.Controllers;

[Route("tanks")]
public class TanksController : Controller
{
    private readonly TanksGameManager _gameManager;

    public TanksController(TanksGameManager gameManager)
    {
        _gameManager = gameManager;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewBag.PlayerName = Request.Cookies["tanks_player_name"] ?? "";
        return View();
    }

    [HttpGet("game/{code}")]
    public IActionResult Play(string code)
    {
        var game = _gameManager.GetGame(code.ToUpperInvariant());
        if (game is null)
            return Redirect("/tanks");

        var sessionId = Request.Cookies[$"tanks_session_{game.ShortCode}"];
        var playerName = Request.Cookies["tanks_player_name"] ?? "PLAYER";

        // If no session cookie, generate one for the joining player
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = Guid.NewGuid().ToString("N");
            Response.Cookies.Append($"tanks_session_{game.ShortCode}", sessionId, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromHours(24)
            });
        }

        var vm = new TanksPlayViewModel
        {
            ShortCode = game.ShortCode,
            SessionId = sessionId,
            PlayerName = playerName
        };

        return View(vm);
    }
}
