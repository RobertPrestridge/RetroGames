using LightCycles.Engine;
using LightCycles.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace LightCycles.Controllers;

[Route("tron")]
public class TronController : Controller
{
    private readonly TronGameManager _gameManager;

    public TronController(TronGameManager gameManager)
    {
        _gameManager = gameManager;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewBag.PlayerName = Request.Cookies["tron_player_name"] ?? "";
        return View();
    }

    [HttpGet("game/{code}")]
    public IActionResult Play(string code)
    {
        var game = _gameManager.GetGame(code.ToUpperInvariant());
        if (game is null)
            return Redirect("/tron");

        var sessionId = Request.Cookies[$"tron_session_{game.ShortCode}"];
        var playerName = Request.Cookies["tron_player_name"] ?? "PLAYER";

        // If no session cookie, generate one for the joining player
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = Guid.NewGuid().ToString("N");
            Response.Cookies.Append($"tron_session_{game.ShortCode}", sessionId, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromHours(24)
            });
        }

        var vm = new TronPlayViewModel
        {
            ShortCode = game.ShortCode,
            SessionId = sessionId,
            PlayerName = playerName
        };

        return View(vm);
    }
}
