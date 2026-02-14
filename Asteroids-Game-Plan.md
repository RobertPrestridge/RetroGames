# Asteroids - Multiplayer Asteroid Shooter Game Plan

## Overview

A 2-player cooperative/competitive asteroid shooter built as a Razor Class Library (RCL) that plugs into the existing MultiPlayer Arcade portal. Both players share the same arena, shooting and dodging asteroids in real time. The server is authoritative — all physics, collision detection, and scoring run server-side at 60ms tick intervals (~16 ticks/sec). SignalR broadcasts state diffs to both clients, which render on an HTML5 Canvas.

---

## Game Concept

- **Players:** 2 players per match (cooperative survival or competitive scoring)
- **Objective:** Destroy asteroids to earn points; survive as long as possible
- **Arena:** Wraparound space — objects leaving one edge appear on the opposite side
- **Asteroids:** Spawn in waves of increasing difficulty; large rocks split into medium, medium split into small
- **Game Over:** Both players destroyed, or a configurable time/wave limit is reached

---

## Controls

| Action         | Player 1 (& Default) | Player 2 (Alt) |
|----------------|-----------------------|-----------------|
| Rotate Left    | `A` / `ArrowLeft`    | `A` / `ArrowLeft` |
| Rotate Right   | `D` / `ArrowRight`   | `D` / `ArrowRight` |
| Thrust Forward | `W` / `ArrowUp`      | `W` / `ArrowUp` |
| Fire           | `Space`               | `Space` |

Both players use the same key bindings since they play on separate browsers/tabs. Input is sent to the server via SignalR (`SendInput` method) with a bitmask or individual key events.

---

## Architecture

### Project Structure

```
Asteroids/                         <- Razor Class Library
├── Asteroids.csproj
├── AsteroidsServiceExtensions.cs  <- AddAsteroids() + MapAsteroids()
├── GlobalUsings.cs
├── Engine/
│   ├── AsteroidGame.cs            <- Core game state & tick logic
│   ├── AsteroidGameManager.cs     <- ConcurrentDictionary<string, AsteroidGame>
│   ├── AsteroidGameLoop.cs        <- BackgroundService, PeriodicTimer(60ms)
│   ├── GameObjects/
│   │   ├── Ship.cs                <- Player ship: position, velocity, rotation, health
│   │   ├── Asteroid.cs            <- Asteroid: position, velocity, size, rotation
│   │   ├── Bullet.cs              <- Bullet: position, velocity, lifetime
│   │   └── Particle.cs            <- Visual debris from explosions
│   ├── Physics/
│   │   ├── Vector2.cs             <- 2D vector math (or use System.Numerics)
│   │   └── CollisionHelper.cs     <- Circle-circle collision detection
│   └── Models/
│       ├── PlayerInput.cs         <- Input state: thrust, rotateLeft, rotateRight, fire
│       ├── TickResult.cs          <- Diff payload for SignalR broadcast
│       └── GameConfig.cs          <- Tuning constants (speeds, sizes, wave config)
├── Controllers/
│   ├── AsteroidsController.cs     <- MVC: lobby + play views
│   └── Api/
│       └── AsteroidsApiController.cs <- POST to create game
├── Hubs/
│   └── AsteroidsHub.cs            <- SignalR hub
├── Data/
│   ├── AsteroidsDbContext.cs       <- Match result persistence
│   ├── Entities/
│   │   └── AsteroidMatch.cs
│   └── Migrations/
├── Models/
│   ├── DTOs/
│   │   ├── CreateGameRequestDto.cs
│   │   └── CreateGameResponseDto.cs
│   └── ViewModels/
│       └── AsteroidsPlayViewModel.cs
├── Views/
│   ├── Asteroids/
│   │   ├── Index.cshtml            <- Lobby (enter name, INSERT COIN)
│   │   └── Play.cshtml             <- Canvas game view
│   └── _ViewImports.cshtml
└── wwwroot/
    ├── css/asteroids.css
    └── js/asteroids.js             <- IIFE, canvas rendering, SignalR client
```

### Follows Existing Patterns

| Aspect | Convention | Asteroids Implementation |
|--------|-----------|--------------------------|
| Service Extensions | `Add{Game}()` / `Map{Game}()` | `AddAsteroids()` / `MapAsteroids()` |
| Routes | `/{game}`, `/{game}/game/{code}` | `/asteroids`, `/asteroids/game/{code}` |
| API | `/api/{game}/games` | `/api/asteroids/games` |
| SignalR Hub | `/hubs/{game}` | `/hubs/asteroids` |
| Hub Groups | `{prefix}_{shortCode}` | `asteroids_{shortCode}` |
| Cookies | `{game}_session_{code}`, `{game}_player_name` | `asteroids_session_{code}`, `asteroids_player_name` |
| Static Assets | `/_content/{Project}/` | `/_content/Asteroids/css/asteroids.css`, `/_content/Asteroids/js/asteroids.js` |
| Frontend Config | `window.{game}Config` | `window.asteroidsConfig` |
| DB Context | Separate per game | `AsteroidsDbContext` with `AsteroidMatches` table |

---

## Game Engine Design

### Coordinate System

- **Arena Size:** 1200 x 800 logical units (floating point)
- **Wraparound:** All objects wrap at edges (ship exits right, appears on left, etc.)
- **Origin:** Top-left (0, 0); positive X = right, positive Y = down

### Game Objects

#### Ship

| Property | Type | Description |
|----------|------|-------------|
| X, Y | float | Position in arena |
| VelocityX, VelocityY | float | Current movement vector |
| Rotation | float | Angle in radians (0 = right/east) |
| Radius | float | Collision radius (~15 units) |
| Alive | bool | Whether ship is active |
| Lives | int | Remaining lives (start with 3) |
| Score | int | Points earned |
| FireCooldown | int | Ticks until next shot allowed |
| InvulnerableTicks | int | Ticks of invulnerability after respawn |

**Ship Physics:**
- Thrust applies acceleration in the direction of `Rotation`
- Velocity has a max speed cap
- Friction/drag gradually slows the ship when not thrusting (multiply velocity by 0.99 per tick)
- Rotation speed: ~0.07 radians per tick

#### Asteroid

| Property | Type | Description |
|----------|------|-------------|
| X, Y | float | Position |
| VelocityX, VelocityY | float | Movement vector |
| Rotation | float | Visual rotation angle |
| RotationSpeed | float | Spin rate (cosmetic) |
| Size | enum | `Large`, `Medium`, `Small` |
| Radius | float | Collision radius (varies by size) |
| ShapeVariant | int | Random shape index for visual variety |

**Asteroid Sizes and Behavior:**

| Size | Radius | Points | Splits Into | Speed Range |
|------|--------|--------|-------------|-------------|
| Large | 40 | 20 | 2 Medium | 0.5 - 1.5 |
| Medium | 20 | 50 | 2 Small | 1.0 - 2.5 |
| Small | 10 | 100 | Nothing (destroyed) | 1.5 - 3.5 |

When an asteroid is destroyed:
1. Remove the asteroid from the game
2. If Large or Medium, spawn 2 asteroids of the next smaller size at the same position
3. New child asteroids get random velocity directions (spread ~60-120 degrees from parent velocity)
4. Speed of children is slightly faster than parent
5. Generate explosion particles at the destruction point

#### Bullet

| Property | Type | Description |
|----------|------|-------------|
| X, Y | float | Position |
| VelocityX, VelocityY | float | Fixed-speed movement |
| OwnerPlayer | int | 1 or 2, identifies who fired |
| TicksRemaining | int | Lifetime counter (expires after ~60 ticks) |
| Radius | float | Collision radius (~2 units) |

- Max 5 bullets active per player at once
- Bullets wrap around the arena like everything else
- Bullets do NOT destroy the other player's ship (cooperative mode)

#### Particle (Client-Side Only)

Particles are generated client-side for visual flair. The server sends explosion events; the client renders debris.

| Property | Type | Description |
|----------|------|-------------|
| X, Y | float | Position |
| VelocityX, VelocityY | float | Scatter velocity |
| TicksRemaining | int | Fade-out lifetime |
| Color | string | Inherited from destroyed object |

### Collision Detection

All collisions use **circle-circle** intersection:
```
distance(a, b) < a.Radius + b.Radius
```

**Collision Pairs Checked Each Tick:**
1. **Bullet vs Asteroid** — Destroys asteroid, awards points to bullet owner, splits asteroid
2. **Ship vs Asteroid** — Destroys ship (lose a life), triggers invulnerability respawn
3. Ship vs Ship — No collision (cooperative)
4. Bullet vs Ship — No collision (cooperative / friendly fire off)
5. Bullet vs Bullet — No collision

### Wave System

| Wave | Large Asteroids | Speed Multiplier |
|------|----------------|-----------------|
| 1 | 4 | 1.0x |
| 2 | 5 | 1.1x |
| 3 | 6 | 1.2x |
| 4 | 7 | 1.3x |
| 5+ | 7 + (wave - 5) | 1.3x + 0.05x per wave |

- New wave spawns when all asteroids are destroyed
- Asteroids spawn at random positions along the arena edges (at least 100 units from any ship)
- Brief 2-second pause between waves (30 ticks at 60ms)

### Game State Machine

```
Waiting → Countdown → InProgress → GameOver
                                  ↗
                      Abandoned ─┘
```

| Status | Value | Description |
|--------|-------|-------------|
| Waiting | 0 | P1 created game, waiting for P2 |
| Countdown | 1 | Both players joined, 3-second countdown |
| InProgress | 2 | Active gameplay |
| GameOver | 3 | Both players out of lives or time expired |
| Abandoned | 4 | Player disconnected before completion |

### Tick Loop (Server-Side)

Each tick (~60ms, ~16 ticks/sec):

1. **Process Inputs** — Apply queued `PlayerInput` for each player (thrust, rotate, fire)
2. **Move Ships** — Update positions based on velocity, apply drag, wrap coordinates
3. **Move Bullets** — Update positions, decrement lifetime, remove expired bullets
4. **Move Asteroids** — Update positions, wrap coordinates
5. **Check Collisions:**
   - Bullet vs Asteroid → destroy asteroid, split if applicable, award points, create explosion event
   - Ship vs Asteroid → if not invulnerable, destroy ship, decrement lives, respawn after 2 seconds
6. **Check Wave Complete** — If no asteroids remain, start next wave after pause
7. **Check Game Over** — If both players have 0 lives, end game
8. **Build Tick Result** — Collect all state changes into a diff payload
9. **Broadcast** — Send `Tick` event to SignalR group

### Tick Result (Diff Payload)

```csharp
public class TickResult
{
    public int Tick { get; set; }

    // Ship states (always sent — positions change frequently)
    public ShipState P1 { get; set; }
    public ShipState P2 { get; set; }

    // Only non-empty when changes occur
    public List<AsteroidState> Asteroids { get; set; }      // Full asteroid list (sent on spawn/destroy)
    public List<BulletState> Bullets { get; set; }           // Active bullets
    public List<ExplosionEvent> Explosions { get; set; }     // New explosions this tick

    // Scoreboard
    public int P1Score { get; set; }
    public int P2Score { get; set; }
    public int P1Lives { get; set; }
    public int P2Lives { get; set; }
    public int Wave { get; set; }

    // Game status if changed
    public int? Status { get; set; }
}
```

---

## SignalR Hub

### Hub Path: `/hubs/asteroids`

### Client → Server Methods

| Method | Parameters | Description |
|--------|-----------|-------------|
| `JoinGame` | `shortCode`, `playerName`, `sessionId` | Join/reconnect to game |
| `SendInput` | `shortCode`, `input` | Send current input state (thrust, rotate, fire booleans) |

### Server → Client Events

| Event | Payload | Description |
|-------|---------|-------------|
| `GameState` | Full game snapshot | Sent on join — complete state for late join / reconnect |
| `OpponentJoined` | `{ opponentName }` | P2 has joined the game |
| `Countdown` | `{ seconds }` | Countdown tick (3, 2, 1, GO) |
| `Tick` | `TickResult` (diff) | Every game tick — positions, bullets, asteroids, explosions |
| `GameOver` | `{ p1Score, p2Score, winner, wave }` | Final results |
| `Error` | `{ message }` | Error message |

### Input Model

```csharp
public class PlayerInput
{
    public bool Thrust { get; set; }
    public bool RotateLeft { get; set; }
    public bool RotateRight { get; set; }
    public bool Fire { get; set; }
}
```

The client sends the full input state on every key change (keydown/keyup). The server applies the latest input each tick. This avoids dropped inputs and keeps the server authoritative.

---

## Database

### AsteroidsDbContext

Separate context, same connection string pattern as Tron.

### AsteroidMatch Entity

| Column | Type | Description |
|--------|------|-------------|
| Id | int | PK, auto-increment |
| ShortCode | string(8) | Game code |
| Player1Name | string(20) | P1 display name |
| Player2Name | string(20) | P2 display name |
| Player1Score | int | Final score |
| Player2Score | int | Final score |
| WavesCompleted | int | Highest wave reached |
| Status | int | Final game status |
| StartedAt | datetime2 | Game start time |
| CompletedAt | datetime2 | Game end time |

Match results are persisted when the game ends (same pattern as Tron — scoped DbContext from `IServiceProvider`).

---

## Controllers

### AsteroidsController (MVC)

| Route | Action | Description |
|-------|--------|-------------|
| `GET /asteroids` | `Index()` | Lobby view — enter name, create game |
| `GET /asteroids/game/{code}` | `Play(code)` | Game view — canvas, HUD, SignalR connection |

### AsteroidsApiController (API)

| Route | Method | Description |
|-------|--------|-------------|
| `POST /api/asteroids/games` | `Create` | Create a new game, returns `{ shortCode, url, sessionId }` |

---

## Frontend

### Config Injection (Play.cshtml)

```html
<script>
    window.asteroidsConfig = {
        shortCode: '@Model.ShortCode',
        sessionId: '@ViewBag.SessionId',
        playerName: '@Model.PlayerName',
        hubUrl: '/hubs/asteroids',
        homePath: '/asteroids'
    };
</script>
```

### Canvas Rendering (asteroids.js)

- **IIFE pattern** matching tron.js
- **Canvas size:** 1200 x 800 pixels (matches logical arena)
- **Render loop:** `requestAnimationFrame` for smooth 60fps rendering
- **Interpolation:** Client interpolates between server ticks for smooth movement
- **Visual style:** Vector/wireframe aesthetic with neon glow (matching retro theme)

#### Rendering Details

**Ships:**
- Drawn as classic triangular ship outlines (wireframe)
- P1 = green (#00ff00), P2 = orange (#ff8800)
- Thrust flame rendered when thrust input active
- Flash/blink during invulnerability frames

**Asteroids:**
- Drawn as irregular polygons (pre-generated vertex arrays per ShapeVariant)
- 3-5 shape variants per size for visual variety
- Wireframe with slight glow effect
- Rotate visually based on `RotationSpeed`

**Bullets:**
- Small bright dots with short trail/glow
- Color matches firing player

**Explosions (client-side particles):**
- Burst of 8-15 line segments scattering outward
- Fade out over 20-30 frames
- Color matches destroyed object

**HUD (overlay, not canvas):**
- Top-left: P1 score + lives (ship icons)
- Top-right: P2 score + lives (ship icons)
- Top-center: Current wave number
- Waiting overlay: "WAITING FOR PLAYER 2..." with game code
- Countdown overlay: "3... 2... 1... GO!"
- Game over overlay: Final scores, winner, "PLAY AGAIN" button

### Input Handling

```javascript
var inputState = { thrust: false, rotateLeft: false, rotateRight: false, fire: false };

document.addEventListener('keydown', function(e) {
    var changed = false;
    switch (e.key) {
        case 'ArrowUp': case 'w': case 'W':
            if (!inputState.thrust) { inputState.thrust = true; changed = true; }
            break;
        case 'ArrowLeft': case 'a': case 'A':
            if (!inputState.rotateLeft) { inputState.rotateLeft = true; changed = true; }
            break;
        case 'ArrowRight': case 'd': case 'D':
            if (!inputState.rotateRight) { inputState.rotateRight = true; changed = true; }
            break;
        case ' ':
            if (!inputState.fire) { inputState.fire = true; changed = true; }
            break;
    }
    if (changed) {
        e.preventDefault();
        connection.invoke('SendInput', config.shortCode, inputState);
    }
});

document.addEventListener('keyup', function(e) {
    // Mirror logic — set false, send if changed
});
```

---

## Portal Integration

### Program.cs Changes

```csharp
using Asteroids;

builder.Services.AddAsteroids(builder.Configuration);
// ...
app.MapAsteroids();
```

### Portal.csproj

```xml
<ProjectReference Include="..\Asteroids\Asteroids.csproj" />
```

### Home/Index.cshtml — New Game Card

```html
<a href="/asteroids" class="game-card game-card-asteroids">
    <div class="game-card-icon game-card-icon-asteroids">&#9788;&#10026;</div>
    <div class="game-card-title">ASTEROIDS</div>
    <div class="game-card-desc">DESTROY THE ROCKS</div>
</a>
```

Styled with a green/lime neon border to differentiate from cyan (TicTacToe) and magenta (Tron).

---

## Cookies

| Cookie | Value | Flags | Expiry |
|--------|-------|-------|--------|
| `asteroids_session_{ShortCode}` | GUID session ID | HttpOnly, SameSite=Strict | 24 hours |
| `asteroids_player_name` | Display name | SameSite=Strict | 30 days |

---

## Routes Summary

| Route | Purpose |
|-------|---------|
| `/asteroids` | Lobby — enter name, create/join game |
| `/asteroids/game/{code}` | Gameplay canvas |
| `/api/asteroids/games` | Create game API |
| `/hubs/asteroids` | SignalR hub |

---

## EF Core Migrations

```bash
dotnet ef migrations add InitialCreate --project Asteroids --startup-project Portal
dotnet ef database update --project Asteroids --startup-project Portal
```

---

## Implementation Order

1. **Project scaffolding** — Create `Asteroids.csproj`, folder structure, `GlobalUsings.cs`, service extensions
2. **Game engine core** — `Ship`, `Asteroid`, `Bullet`, `Vector2`/collision helpers, `AsteroidGame` with tick logic
3. **Game manager + loop** — `AsteroidGameManager` (ConcurrentDictionary), `AsteroidGameLoop` (BackgroundService)
4. **SignalR hub** — `AsteroidsHub` with `JoinGame`, `SendInput`, disconnect handling
5. **Controllers + views** — Lobby, Play view, API controller, cookie handling
6. **Frontend** — Canvas rendering, SignalR client, input handling, HUD
7. **Database** — `AsteroidsDbContext`, `AsteroidMatch` entity, migrations
8. **Portal wiring** — Add project reference, call service extensions in Program.cs, add game card
9. **Polish** — Particle effects, sound cues (optional), wave transitions, game-over screen
10. **Testing** — Multi-browser testing, edge cases (simultaneous kills, respawn collisions, disconnect/reconnect)

---

## Tuning Constants (GameConfig)

| Constant | Value | Description |
|----------|-------|-------------|
| TickInterval | 60ms | Server tick rate (~16/sec) |
| ArenaWidth | 1200 | Logical arena width |
| ArenaHeight | 800 | Logical arena height |
| ShipThrust | 0.15 | Acceleration per tick |
| ShipMaxSpeed | 5.0 | Maximum velocity magnitude |
| ShipDrag | 0.99 | Velocity multiplier per tick (friction) |
| ShipRotationSpeed | 0.07 | Radians per tick |
| ShipRadius | 15 | Collision radius |
| ShipLives | 3 | Starting lives |
| ShipInvulnerableTicks | 48 | ~3 seconds of invulnerability |
| ShipRespawnDelay | 32 | ~2 seconds before respawn |
| BulletSpeed | 7.0 | Bullet velocity |
| BulletLifetime | 60 | Ticks before bullet expires |
| BulletCooldown | 8 | Ticks between shots |
| MaxBulletsPerPlayer | 5 | Active bullet cap |
| LargeAsteroidRadius | 40 | Collision radius |
| MediumAsteroidRadius | 20 | Collision radius |
| SmallAsteroidRadius | 10 | Collision radius |
| LargeAsteroidPoints | 20 | Score for destroying |
| MediumAsteroidPoints | 50 | Score for destroying |
| SmallAsteroidPoints | 100 | Score for destroying |
| WavePauseTicks | 32 | ~2 second pause between waves |
| CountdownTicks | 48 | ~3 second pre-game countdown |
| SafeSpawnDistance | 100 | Min distance from ships for asteroid spawn |
