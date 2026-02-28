using PocketTanks.Engine;
using Microsoft.AspNetCore.SignalR;

namespace PocketTanks.Hubs;

public class TanksHub : Hub
{
    private readonly TanksGameManager _gameManager;
    private readonly ILogger<TanksHub> _logger;

    public TanksHub(TanksGameManager gameManager, ILogger<TanksHub> logger)
    {
        _gameManager = gameManager;
        _logger = logger;
    }

    public async Task JoinGame(string shortCode, string playerName, string sessionId)
    {
        var groupName = $"tanks_{shortCode}";
        var game = _gameManager.GetGame(shortCode);

        if (game is null)
        {
            await Clients.Caller.SendAsync("Error", "Game not found.");
            return;
        }

        // Check if this session is already a player (reconnect)
        var existingPlayer = game.GetPlayerBySession(sessionId);
        if (existingPlayer is not null)
        {
            existingPlayer.ConnectionId = Context.ConnectionId;
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            var playerNum = game.GetPlayerNumber(sessionId);
            _logger.LogInformation("Tanks P{PlayerNum} reconnected to {ShortCode}", playerNum, shortCode);

            await SendGameState(game, playerNum);
            return;
        }

        // Try to join as P2
        var (success, error, number) = _gameManager.JoinGame(shortCode, playerName, sessionId, Context.ConnectionId);

        if (!success)
        {
            _logger.LogWarning("Tanks JoinGame failed for {ShortCode}: {Error}", shortCode, error);
            await Clients.Caller.SendAsync("Error", error);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation("Tanks P{PlayerNum} joined {ShortCode}: {PlayerName}",
            number, shortCode, playerName);

        // Send game state to the joining player
        await SendGameState(game, number);

        // If P2 just joined, notify P1
        if (number == 2)
        {
            await Clients.OthersInGroup(groupName).SendAsync("OpponentJoined", new
            {
                opponentName = playerName
            });
        }
    }

    public async Task SelectWeapon(string shortCode, int weaponType)
    {
        var game = _gameManager.GetGame(shortCode);
        if (game is null) return;

        var player = game.GetPlayerByConnection(Context.ConnectionId);
        if (player is null) return;

        if (game.SelectWeapon(player.SessionId, (WeaponType)weaponType))
        {
            var groupName = $"tanks_{shortCode}";
            var playerNum = game.GetPlayerNumber(player.SessionId);
            var weapons = playerNum == 1 ? game.Player1Weapons : game.Player2Weapons;

            await Clients.Group(groupName).SendAsync("WeaponSelected", new
            {
                playerNumber = playerNum,
                weaponType = (int)(game.SelectedWeapon ?? WeaponType.Standard),
                weaponName = WeaponData.Get(game.SelectedWeapon ?? WeaponType.Standard).Name,
                weapons = weapons.Select((w, i) => new { index = i, type = (int)w, name = WeaponData.Get(w).Name })
            });
        }
    }

    public async Task SetFiringParams(string shortCode, float angle, float power)
    {
        var game = _gameManager.GetGame(shortCode);
        if (game is null) return;

        var player = game.GetPlayerByConnection(Context.ConnectionId);
        if (player is null) return;

        game.SetFiringParams(player.SessionId, angle, power);

        var groupName = $"tanks_{shortCode}";
        await Clients.OthersInGroup(groupName).SendAsync("AimUpdate", new
        {
            angle,
            power
        });
    }

    public async Task Fire(string shortCode, float angle, float power)
    {
        var game = _gameManager.GetGame(shortCode);
        if (game is null) return;

        var player = game.GetPlayerByConnection(Context.ConnectionId);
        if (player is null) return;

        if (game.Fire(player.SessionId, angle, power))
        {
            var groupName = $"tanks_{shortCode}";
            await Clients.Group(groupName).SendAsync("Fired", new
            {
                playerNumber = game.CurrentTurn,
                angle,
                power
            });
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Tanks player disconnected (ConnectionId={ConnectionId})", Context.ConnectionId);
        _gameManager.HandleDisconnect(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task SendGameState(TanksGame game, int playerNumber)
    {
        var weapons1 = game.Player1Weapons.Select((w, i) => new
        {
            index = i,
            type = (int)w,
            name = WeaponData.Get(w).Name
        });

        var weapons2 = game.Player2Weapons.Select((w, i) => new
        {
            index = i,
            type = (int)w,
            name = WeaponData.Get(w).Name
        });

        await Clients.Caller.SendAsync("GameState", new
        {
            arenaWidth = TanksGame.ArenaWidth,
            arenaHeight = TanksGame.ArenaHeight,
            terrain = game.Terrain.Serialize(),
            player1 = new
            {
                x = game.Player1.X,
                y = game.Player1.Y,
                health = game.Player1.Health,
                score = game.Player1.Score,
                name = game.Player1.Name,
                angle = game.Player1.Angle,
                power = game.Player1.Power
            },
            player2 = game.Player2 is not null ? new
            {
                x = game.Player2.X,
                y = game.Player2.Y,
                health = game.Player2.Health,
                score = game.Player2.Score,
                name = game.Player2.Name,
                angle = game.Player2.Angle,
                power = game.Player2.Power
            } : null,
            status = (int)game.Status,
            playerNumber,
            currentTurn = game.CurrentTurn,
            turnNumber = game.TurnNumber,
            selectedWeapon = game.SelectedWeapon.HasValue ? (int?)game.SelectedWeapon.Value : null,
            player1Weapons = weapons1,
            player2Weapons = weapons2,
            timeRemaining = game.GetPhaseTimeRemaining()
        });
    }
}
