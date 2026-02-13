using Microsoft.AspNetCore.Mvc;
using TicTacToe.Models.ViewModels;
using TicTacToe.Services;

namespace TicTacToe.Controllers;

[Route("tictactoe")]
public class GameController : Controller
{
    private readonly IGameService _gameService;

    public GameController(IGameService gameService)
    {
        _gameService = gameService;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("game/{code}")]
    public async Task<IActionResult> Play(string code)
    {
        var game = await _gameService.GetGameByCodeAsync(code.ToUpperInvariant());
        if (game is null)
            return Redirect("/tictactoe");

        var sessionId = Request.Cookies[$"session_{game.ShortCode}"];

        // If no session cookie, generate one for Player O
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = Guid.NewGuid().ToString("N");
            Response.Cookies.Append($"session_{game.ShortCode}", sessionId, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                MaxAge = TimeSpan.FromHours(24)
            });
        }

        var vm = new GameViewModel
        {
            ShortCode = game.ShortCode,
            BoardState = game.BoardState,
            Status = (int)game.Status,
            CurrentTurn = game.CurrentTurn
        };

        ViewBag.SessionId = sessionId;
        return View(vm);
    }
}
