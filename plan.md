# Pocket Tanks Implementation Plan

## Game Overview

A 2-player turn-based artillery game inspired by Pocket Tanks. Players control tanks on opposite sides of a destructible terrain, taking turns to adjust angle/power and fire projectiles. The game follows ballistic physics with gravity, terrain deformation on impact, and multiple weapon types. Each player gets 10 turns (20 total), and the player who deals the most damage wins.

---

## Architecture Summary

Follow the existing RCL pattern (like Asteroids/LightCycles):
- **Project**: `PocketTanks/` Razor Class Library
- **Engine**: Server-authoritative game logic with in-memory state
- **Comms**: SignalR hub for real-time updates
- **Frontend**: HTML5 Canvas rendering with retro/neon styling
- **Database**: Only persist completed match results
- **Turn-based hybrid**: Unlike Asteroids (continuous tick), this game uses a turn-based model with physics simulation ticks only during projectile flight

---

## Step-by-Step Implementation

### Step 1: Project Scaffolding

**Create `PocketTanks/PocketTanks.csproj`**
```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.3" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.3">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

**Create `PocketTanks/GlobalUsings.cs`** — standard global usings matching existing games.

**Create `PocketTanks/_ViewImports.cshtml`** — `@using PocketTanks`, `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`.

**Directory structure:**
```
PocketTanks/
├── PocketTanks.csproj
├── PocketTanksServiceExtensions.cs
├── GlobalUsings.cs
├── Controllers/
│   ├── TanksController.cs
│   └── Api/TanksApiController.cs
├── Engine/
│   ├── TanksGame.cs
│   ├── TanksGameManager.cs
│   ├── TanksGameLoop.cs
│   ├── Terrain.cs
│   ├── Projectile.cs
│   └── WeaponType.cs
├── Hubs/
│   └── TanksHub.cs
├── Data/
│   ├── TanksDbContext.cs
│   └── Entities/
│       └── TanksMatch.cs
├── Models/
│   ├── ViewModels/
│   │   └── TanksPlayViewModel.cs
│   └── DTOs/
│       ├── CreateTanksGameRequestDto.cs
│       └── CreateTanksGameResponseDto.cs
├── Views/
│   ├── _ViewImports.cshtml
│   └── Tanks/
│       ├── Index.cshtml
│       └── Play.cshtml
└── wwwroot/
    ├── css/
    │   └── tanks.css
    └── js/
        └── tanks.js
```

---

### Step 2: Engine — Core Game Logic

#### `Engine/Terrain.cs`
- **Heightmap**: `float[]` array of 1200 elements (1 per pixel column across 1200px arena width)
- **Generation**: Procedural terrain using layered sine waves with random offsets for hills/valleys. Height range ~200-500px from bottom of 800px arena
- **Deformation**: `Deform(float x, float radius)` — lowers terrain heights within blast radius using circular subtraction. Creates realistic craters
- **Serialization**: Method to produce a compact representation for sending to clients (e.g., every 4th column to reduce payload, client interpolates)

#### `Engine/WeaponType.cs`
Enum + static data for weapon types. Start with a focused set:

| Weapon | Blast Radius | Damage | Special |
|--------|-------------|--------|---------|
| Standard Shot | 30px | 20 | Basic projectile |
| Big Shot | 50px | 35 | Larger explosion |
| Sniper Shot | 15px | 40 | Small but high damage |
| Dirt Mover | 40px | 5 | Mostly deforms terrain, little damage |
| Bouncer | 25px | 15 | Bounces once off terrain before exploding |
| Three Shot | 25px | 12 each | Fires 3 projectiles in a spread |
| Roller | 20px | 25 | Rolls along terrain surface after landing |
| Nuke | 70px | 50 | Massive explosion, rare/powerful |

Each player gets a randomized selection of 10 weapons at game start (drawn from the pool, no duplicates for variety).

#### `Engine/Projectile.cs`
- Properties: `X`, `Y`, `VelocityX`, `VelocityY`, `WeaponType`, `OwnerPlayer`, `Active`, `HasBounced`
- Physics: Standard ballistic trajectory — `Vy += Gravity` each tick, `X += Vx`, `Y += Vy`
- Gravity constant: ~0.15f per tick
- Collision checks: terrain heightmap intersection, out-of-bounds, opponent tank hit

#### `Engine/TanksGame.cs`
Core state machine. Key design:

**Constants:**
- `ArenaWidth = 1200f`, `ArenaHeight = 800f`
- `TurnsPerPlayer = 10` (20 total turns)
- `PhysicsTickMs = 16` (~60 FPS during projectile flight)
- `Gravity = 0.15f`
- `MaxPower = 100f`

**State:**
```
ShortCode, Status (Waiting/Countdown/WeaponSelect/Aiming/Firing/GameOver)
Player1, Player2 (TankState: X, Y, Health=100, Score, Name, SessionId, ConnectionId)
Terrain (heightmap)
CurrentTurn (1 or 2), TurnNumber (1-20)
ActiveProjectiles (list), CurrentWeapon
Player1Weapons, Player2Weapons (List<WeaponType> — 10 each)
```

**Game Flow / Status Transitions:**
1. `Waiting` — P1 created game, waiting for P2
2. `Countdown` — P2 joined, 3-second countdown
3. `WeaponSelect` — Current player picks weapon from their remaining inventory (15-second timer)
4. `Aiming` — Current player adjusts angle/power (15-second timer; has default angle/power)
5. `Firing` — Projectile in flight, server simulates physics ticks. All players watch
6. → Back to `WeaponSelect` for next player, or `GameOver` after 20 turns

**Tank Positioning:**
- P1 placed at ~20% of arena width, on terrain surface
- P2 placed at ~80% of arena width, on terrain surface
- Tanks sit ON the terrain (Y = terrain height at their X)
- After terrain deformation, tanks "settle" to new terrain height (they can sink into craters)

**Damage Calculation:**
- Distance-based: full damage at epicenter, linear falloff to 0 at blast edge
- `damage = baseDamage * (1 - distance/blastRadius)` clamped to 0
- Direct hits (distance < tankRadius) get bonus 1.5x multiplier
- Score = total damage dealt (accumulated across all turns)

**Turn Timer:**
- 15 seconds for weapon select, 15 seconds for aiming
- If timer expires: auto-select first available weapon / fire with current angle+power

#### `Engine/TanksGameManager.cs`
- Same `ConcurrentDictionary<string, TanksGame>` pattern as Asteroids
- `CreateGame(playerName)` → shortCode + sessionId
- `JoinGame(shortCode, playerName, sessionId, connectionId)` → success/error/playerNumber
- `GetGame`, `RemoveGame`, `GetActiveGames`, `HandleDisconnect`

#### `Engine/TanksGameLoop.cs`
Background service but with a key difference from Asteroids:
- **During `Firing` status**: Tick at 16ms (60 FPS) to simulate projectile physics, broadcast projectile position each tick
- **During other statuses**: Tick at 500ms just to check turn timers and cleanup stale games
- This is more efficient than constant 60ms ticks since most game time is spent waiting for player input

---

### Step 3: SignalR Hub

**`Hubs/TanksHub.cs`** — mapped at `/hubs/tanks`

**Client → Server messages:**

| Method | Parameters | When |
|--------|-----------|------|
| `JoinGame` | `shortCode, playerName, sessionId` | On connect |
| `SelectWeapon` | `shortCode, weaponIndex` | During WeaponSelect phase |
| `SetFiringParams` | `shortCode, angle, power` | During Aiming phase (live updates for opponent to see) |
| `Fire` | `shortCode, angle, power` | Player commits to fire |

**Server → Client messages:**

| Message | Payload | When |
|---------|---------|------|
| `GameState` | Full state (terrain, tanks, scores, weapons, status) | On join/reconnect |
| `OpponentJoined` | `{ opponentName }` | P2 joins |
| `Countdown` | `{ seconds }` | 3-2-1 countdown |
| `TurnStart` | `{ currentPlayer, turnNumber, timeLimit }` | New turn begins |
| `WeaponSelected` | `{ playerNumber, weaponType }` | Player chose weapon |
| `AimUpdate` | `{ angle, power }` | Live aim preview for opponent |
| `ProjectileTick` | `{ projectiles: [{x, y}], tick }` | During projectile flight |
| `Explosion` | `{ x, y, radius, terrainUpdate, damage, newHealth }` | Projectile impact |
| `TerrainUpdate` | `{ heights[] }` | Terrain changed (after explosion) |
| `TankPositionUpdate` | `{ p1: {x,y}, p2: {x,y} }` | Tanks settle after terrain change |
| `GameOver` | `{ p1Score, p2Score, winner }` | All turns done |
| `Error` | `string` | Error message |

SignalR group: `tanks_{ShortCode}`

---

### Step 4: Controllers

#### `Controllers/TanksController.cs`
- Route prefix: `/tanks`
- `GET /tanks` → Lobby view (Index.cshtml)
- `GET /tanks/game/{code}` → Play view (Play.cshtml)
- Cookie pattern: `tanks_player_name` (30-day), `tanks_session_{ShortCode}` (24-hour)

#### `Controllers/Api/TanksApiController.cs`
- `POST /api/tanks/games` → Create new game
- Same pattern as Asteroids API controller

---

### Step 5: Views

#### `Views/Tanks/Index.cshtml` (Lobby)
- Retro-styled lobby matching existing games
- Player name input, "INSERT COIN" button
- Join game by code option
- Color theme: **orange/yellow** (to differentiate from other games — TicTacToe=cyan, Tron=pink, Asteroids=green)

#### `Views/Tanks/Play.cshtml` (Game)
HTML structure:
```
┌─────────────────────────────────────────────────┐
│  HUD: P1 Name/Health/Score | Turn X/20 | P2     │
├─────────────────────────────────────────────────┤
│                                                   │
│              Canvas (1200 x 800)                  │
│         Sky gradient + terrain + tanks            │
│                                                   │
├─────────────────────────────────────────────────┤
│  Controls: Angle [-] ◄████████► [+]  Power gauge │
│  Weapon inventory panel | FIRE button             │
└─────────────────────────────────────────────────┘
```

Overlays:
- Waiting for P2 (with share link)
- Countdown (3-2-1)
- Weapon select panel
- Game over (scores + winner)

Config injection: `window.tanksConfig = { shortCode, sessionId, playerName, hubUrl, homePath }`

---

### Step 6: Frontend JavaScript (`tanks.js`)

IIFE pattern matching existing games.

**Canvas Rendering (1200x800):**
- **Sky**: Dark gradient (deep blue to black, matching retro theme)
- **Terrain**: Filled polygon from heightmap with neon outline (orange glow). Interior filled with dark textured color
- **Tanks**: Simple geometric shapes — rectangular body + angled barrel. P1=cyan, P2=magenta (matching arcade theme). Barrel rotates to show current angle
- **Projectile**: Glowing dot with trail effect (fading previous positions)
- **Explosions**: Expanding circle + particle burst (reuse particle system concept from Asteroids)
- **Crater**: Terrain instantly updates after explosion, with brief flash effect
- **HUD overlay on canvas**: Health bars, damage numbers floating up from impact

**Input Handling:**
- Arrow Up/Down or W/S: Adjust angle (1-degree increments, hold for continuous)
- Arrow Left/Right or A/D: Adjust power (1-unit increments)
- Space or Enter: Fire
- Number keys 1-0: Quick-select weapon from inventory
- Mouse: Click weapon cards to select, click FIRE button

**State Machine (client-side):**
- `waiting` → show share link overlay
- `countdown` → show countdown overlay
- `weapon-select` → highlight weapon panel, enable selection (only for active player)
- `aiming` → show angle/power controls, enable adjustment (only for active player)
- `firing` → animate projectile flight, disable controls
- `game-over` → show results overlay

**Projectile Animation:**
- Client receives `ProjectileTick` at ~60 FPS during flight
- Render projectile position with trailing glow
- On `Explosion` event: play explosion animation, update terrain, show damage number

---

### Step 7: CSS Styling (`tanks.css`)

- **Color scheme**: Orange (`#ff8800`) and yellow (`#ffaa00`) as primary accent — warm desert/explosion theme
- Follow existing patterns: neon glow box-shadows, Press Start 2P font, dark backgrounds
- `.tanks-title` with orange glow animation
- `.tanks-canvas-wrapper` with orange neon border
- `.tanks-weapon-card` — small cards showing weapon name, styled like arcade buttons
- `.tanks-weapon-card.selected` — highlighted with glow
- `.tanks-weapon-card.used` — dimmed/crossed out
- `.tanks-controls` — angle/power sliders styled as retro gauges
- `.tanks-fire-btn` — large pulsing "FIRE" button (only active during player's aiming phase)
- Health bars with gradient fills (green→yellow→red)

---

### Step 8: Database

#### `Data/Entities/TanksMatch.cs`
```csharp
public class TanksMatch
{
    public int Id { get; set; }
    public string ShortCode { get; set; }
    public string Player1Name { get; set; }
    public string Player2Name { get; set; }
    public int Player1Score { get; set; }
    public int Player2Score { get; set; }
    public int TotalTurns { get; set; }
    public int Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
}
```

#### `Data/TanksDbContext.cs`
- Same connection string as other games (`DefaultConnection`)
- Single `TanksMatches` DbSet
- EF migration: `dotnet ef migrations add InitialCreate --project PocketTanks --startup-project Portal`

---

### Step 9: Service Extensions & Portal Integration

#### `PocketTanksServiceExtensions.cs`
```csharp
AddPocketTanks(services, configuration)  → DbContext + TanksGameManager (singleton) + TanksGameLoop (hosted service)
MapPocketTanks(endpoints)                → MapHub<TanksHub>("/hubs/tanks")
```

#### Portal Integration
1. **`Portal.csproj`** — Add `<ProjectReference Include="..\PocketTanks\PocketTanks.csproj" />`
2. **`MultiPlayer.slnx`** — Add PocketTanks project
3. **`Portal/Program.cs`** — Add `builder.Services.AddPocketTanks(builder.Configuration)` and `app.MapPocketTanks()`
4. **`Portal/Views/Home/Index.cshtml`** — Add game card:
   - Icon: `💣` or artillery symbol using unicode
   - Title: "POCKET TANKS"
   - Desc: "AIM. FIRE. DESTROY."
   - Color: orange theme (`.game-card-tanks`)
   - Route: `/tanks`

---

### Step 10: Routes Summary

| Route | Purpose |
|-------|---------|
| `/tanks` | Lobby (create/join game) |
| `/tanks/game/{code}` | Play page |
| `/api/tanks/games` | REST API (create game) |
| `/hubs/tanks` | SignalR hub |

---

## Implementation Order

1. **Project scaffolding** — csproj, GlobalUsings, ViewImports, directory structure, service extensions, portal wiring (build compiles)
2. **Engine core** — Terrain generation/deformation, WeaponType, Projectile physics, TanksGame state machine, TanksGameManager
3. **Game loop** — TanksGameLoop background service with turn timer management and physics ticks
4. **SignalR hub** — TanksHub with all client/server messages
5. **Controllers** — TanksController (MVC) + TanksApiController (REST)
6. **Views** — Index.cshtml (lobby) + Play.cshtml (game page with canvas + controls)
7. **Frontend JS** — tanks.js (Canvas rendering, input handling, SignalR connection, game state machine)
8. **CSS** — tanks.css (retro/neon orange theme)
9. **Database** — TanksDbContext, TanksMatch entity, migration
10. **Portal home** — Add game card to Index.cshtml
11. **Polish** — Test multiplayer flow, tune physics constants, balance weapons, add sound effects or visual polish

---

## Key Design Decisions

- **Turn-based with real-time projectile flight**: Hybrid approach — most of the game is turn-based (weapon select, aim), but projectile flight is simulated in real-time on the server and streamed to clients. This matches how Pocket Tanks feels.
- **Server-authoritative**: All physics and game logic on server. Clients send only input (weapon choice, angle, power, fire). Prevents cheating.
- **In-memory game state**: Like LightCycles/Asteroids — no need to persist mid-game state to database. Only match results are saved.
- **Terrain as heightmap**: Simple 1D array is efficient for both physics (collision = compare Y to height[X]) and rendering (draw filled polygon). Deformation is just lowering array values.
- **10 weapons per player from pool**: Adds strategy — players must choose when to use powerful weapons. Randomized selection adds replayability.
- **Orange/yellow theme**: Warm colors evoke explosions and desert warfare, distinguishes from the cool-toned existing games.
