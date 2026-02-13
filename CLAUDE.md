# MultiPlayer Arcade

Multi-game portal with real-time multiplayer games. Portal + Razor Class Library architecture — each game is its own project, the Portal is the single running web host.

## Tech Stack

- .NET 10, C# MVC + API, ASP.NET Core
- EF Core 10 + SQL Server Express (`.\SQLEXPRESS`, database `TicTacToe`)
- SignalR for real-time gameplay
- Bootstrap 5, jQuery, Press Start 2P font, retro/neon dark theme
- No auth — players identified by session cookies (`session_{ShortCode}`)

## Commands

```bash
dotnet build MultiPlayer.slnx
dotnet run --project Portal                    # http://localhost:5238
dotnet ef migrations add <Name> --project TicTacToe --startup-project Portal
dotnet ef database update --project TicTacToe --startup-project Portal
dotnet ef migrations add <Name> --project LightCycles --startup-project Portal
dotnet ef database update --project LightCycles --startup-project Portal
```

## Testing Multiplayer

Same-browser tabs share cookies (both see Player 1). Use incognito or a different browser for Player 2.

## Architecture

```
MultiPlayer.slnx
├── Portal/                    ← Web host (the running app)
│   ├── Program.cs             ← DI, middleware, calls Add/Map for each game
│   ├── Controllers/           ← HomeController (game selection at /)
│   ├── Views/Shared/          ← _Layout.cshtml (retro theme, @section Styles/Scripts)
│   ├── Views/Home/            ← Index.cshtml (game card grid)
│   └── wwwroot/               ← Shared: bootstrap, jquery, signalr, site.css, favicon
│
└── TicTacToe/                 ← Razor Class Library
    ├── TicTacToeServiceExtensions.cs
    ├── GlobalUsings.cs
    ├── Controllers/           ← GameController, Api/GamesApiController
    ├── Views/Game/            ← Index.cshtml (lobby), Play.cshtml (board)
    ├── Hubs/GameHub.cs        ← SignalR hub
    ├── Services/              ← GameService, ShortCodeService
    ├── Data/                  ← ApplicationDbContext, Game entity, migrations
    ├── Models/                ← DTOs, ViewModels
    ├── BackgroundServices/    ← GameCleanupService
    └── wwwroot/               ← game.css, game.js (served at /_content/TicTacToe/)
│
└── LightCycles/               ← Razor Class Library (Tron Light Cycles)
    ├── LightCyclesServiceExtensions.cs
    ├── GlobalUsings.cs
    ├── Engine/                ← TronGame, TronGameManager, TronGameLoop
    ├── Controllers/           ← TronController, Api/TronApiController
    ├── Views/Tron/            ← Index.cshtml (lobby), Play.cshtml (canvas)
    ├── Hubs/TronHub.cs        ← SignalR hub
    ├── Data/                  ← TronDbContext, TronMatch entity
    ├── Models/                ← DTOs, ViewModels
    └── wwwroot/               ← tron.css, tron.js (served at /_content/LightCycles/)
```

## Adding a New Game

1. New RCL project with `<AddRazorSupportForMvc>true</AddRazorSupportForMvc>` + `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
2. Create `MyGameServiceExtensions.cs` with `AddMyGame()` + `MapMyGame()` extension methods
3. Reference from `Portal.csproj`, wire up in `Portal/Program.cs`
4. Add game card to `Portal/Views/Home/Index.cshtml`

## Routes

| Route | Purpose |
|---|---|
| `/` | Portal — game selection |
| `/tictactoe` | TicTacToe lobby |
| `/tictactoe/game/{code}` | TicTacToe play |
| `/api/tictactoe/games` | TicTacToe API |
| `/hubs/tictactoe` | TicTacToe SignalR |
| `/tron` | Tron lobby |
| `/tron/game/{code}` | Tron play |
| `/api/tron/games` | Tron API |
| `/hubs/tron` | Tron SignalR |

## TicTacToe Details

### SignalR Hub (`/hubs/tictactoe`)

Groups: `game_{ShortCode}` (2 members). Client calls `JoinGame(shortCode, sessionId)` and `MakeMove(shortCode, position)`. Server sends `GameState`, `OpponentJoined`, `MoveMade`, `GameOver`, `Error`.

C# `char` serializes as JSON string (`"X"`), not a char code. JS receives strings directly.

### Game Entity

Board: 9-char string (spaces=empty, X/O, row-major). Status enum: Waiting=0, InProgress=1, XWins=2, OWins=3, Draw=4, Abandoned=5. `RowVersion` for optimistic concurrency.

### Frontend

`window.gameConfig = { shortCode, sessionId, hubUrl, homePath }` injected by Play.cshtml. `game.js` IIFE uses `config.hubUrl` for SignalR, `config.homePath` for new game redirect. Static assets at `~/_content/TicTacToe/css/game.css` and `~/_content/TicTacToe/js/game.js`.

### Cleanup Service

Runs every 5 min. Abandons inactive games after 2 hours, deletes completed games after 24 hours.

## LightCycles (Tron) Details

### Architecture

Server-authoritative real-time game. Game state lives in-memory (`ConcurrentDictionary`), not in the database. Only completed match results are persisted to `TronMatches` table.

### Engine

- **TronGame** — single game state: 60x40 byte grid, two `PlayerState` objects, tick logic with collision detection
- **TronGameManager** — singleton `ConcurrentDictionary<string, TronGame>`, creates/joins/removes games
- **TronGameLoop** — `BackgroundService` with `PeriodicTimer(100ms)`, ticks all active games, broadcasts state diffs via SignalR

### SignalR Hub (`/hubs/tron`)

Groups: `tron_{ShortCode}`. Client calls `JoinGame(shortCode, playerName, sessionId)` and `ChangeDirection(shortCode, direction)`. Server sends `GameState`, `OpponentJoined`, `Countdown`, `Tick`, `GameOver`, `Error`.

Direction values: `"up"`, `"down"`, `"left"`, `"right"`. 180° reversal blocked on both client and server.

### Game Flow

1. P1 creates game → Waiting status
2. P2 joins → Countdown status (3 seconds / 30 ticks)
3. Countdown reaches 0 → InProgress
4. Server ticks at 100ms: moves players, checks collisions, broadcasts `Tick` diffs
5. Collision → GameOver, match result persisted to DB, game removed after 10s

### Frontend

`window.tronConfig = { shortCode, sessionId, playerName, hubUrl, homePath }` injected by Play.cshtml. `tron.js` IIFE renders on HTML5 Canvas (60x40 grid, ~13px cells). P1=cyan, P2=magenta. Arrow keys + WASD for input.

### Database

Separate `TronDbContext` with `TronMatches` table. Same connection string as TicTacToe. Match records: player names, winner, tick count, timestamps.

### Cookies

- `tron_player_name` — display name (30-day expiry)
- `tron_session_{ShortCode}` — session identity per game (24-hour expiry)
