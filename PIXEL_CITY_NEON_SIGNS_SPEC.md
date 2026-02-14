# Pixel City Neon Signs — Implementation Spec

## Overview

Add 5 large, prominent neon signs with pixel-art text to the Pixel City background (`Portal/Views/Home/_BgPixelCity.cshtml`). Also improve building architecture with setbacks, ledges, and water towers. All signs have glow, pulse, and flicker effects plus ground reflections.

---

## 1. Pixel Font System

Add a 5x7 bitmap font stored as a JavaScript object. Each character is 5 pixels wide and 7 pixels tall. Each row is a 5-bit integer (bit 4 = leftmost pixel, bit 0 = rightmost).

### Characters Needed

```
A R C D E G H L M N O P S T Y B 2 4 7 /
```

### Font Data (decimal values per row)

```javascript
var PIXEL_FONT = {
    'A': [14, 17, 17, 31, 17, 17, 17],   //  .###.  #...#  #...#  #####  #...#  #...#  #...#
    'B': [30, 17, 17, 30, 17, 17, 30],   //  ####.  #...#  #...#  ####.  #...#  #...#  ####.
    'C': [14, 17, 16, 16, 16, 17, 14],   //  .###.  #...#  #....  #....  #....  #...#  .###.
    'D': [30, 17, 17, 17, 17, 17, 30],   //  ####.  #...#  #...#  #...#  #...#  #...#  ####.
    'E': [31, 16, 16, 30, 16, 16, 31],   //  #####  #....  #....  ####.  #....  #....  #####
    'G': [14, 17, 16, 23, 17, 17, 14],   //  .###.  #...#  #....  #.###  #...#  #...#  .###.
    'H': [17, 17, 17, 31, 17, 17, 17],   //  #...#  #...#  #...#  #####  #...#  #...#  #...#
    'L': [16, 16, 16, 16, 16, 16, 31],   //  #....  #....  #....  #....  #....  #....  #####
    'M': [17, 27, 21, 17, 17, 17, 17],   //  #...#  ##.##  #.#.#  #...#  #...#  #...#  #...#
    'N': [17, 25, 21, 19, 17, 17, 17],   //  #...#  ##..#  #.#.#  #..##  #...#  #...#  #...#
    'O': [14, 17, 17, 17, 17, 17, 14],   //  .###.  #...#  #...#  #...#  #...#  #...#  .###.
    'P': [30, 17, 17, 30, 16, 16, 16],   //  ####.  #...#  #...#  ####.  #....  #....  #....
    'R': [30, 17, 17, 30, 20, 18, 17],   //  ####.  #...#  #...#  ####.  #.#..  #..#.  #...#
    'S': [14, 17, 16, 14,  1, 17, 14],   //  .###.  #...#  #....  .###.  ....#  #...#  .###.
    'T': [31,  4,  4,  4,  4,  4,  4],   //  #####  ..#..  ..#..  ..#..  ..#..  ..#..  ..#..
    'Y': [17, 17, 10,  4,  4,  4,  4],   //  #...#  #...#  .#.#.  ..#..  ..#..  ..#..  ..#..
    '2': [14, 17,  1,  2,  4,  8, 31],   //  .###.  #...#  ....#  ...#.  ..#..  .#...  #####
    '4': [ 2,  6, 10, 18, 31,  2,  2],   //  ...#.  ..##.  .#.#.  #..#.  #####  ...#.  ...#.
    '7': [31,  1,  2,  4,  4,  4,  4],   //  #####  ....#  ...#.  ..#..  ..#..  ..#..  ..#..
    '/': [ 1,  1,  2,  4,  8, 16, 16]    //  ....#  ....#  ...#.  ..#..  .#...  #....  #....
};
```

### Rendering Function

Inline in the draw loop (no separate function to keep IIFE clean):

```javascript
// For each character in sign.text:
//   If vertical: offset Y by charIndex * (7 * scale + scale)
//   If horizontal: offset X by charIndex * (5 * scale + scale)
//   For each of 7 rows, read the 5-bit mask
//   For each set bit, draw a filled rectangle of size (scale x scale)
```

---

## 2. Data Structures

### Add to variable declarations (after existing data structures)

```javascript
var neonSigns = [];
```

### Add to init() reset block

```javascript
neonSigns = [];
```

---

## 3. The 5 Neon Signs

Each sign is placed on the tallest building within its screen zone (screen divided into 5 equal horizontal zones).

| # | Text | Color | Scale | Orientation | Style Notes |
|---|------|-------|-------|-------------|-------------|
| 1 | `ARCADE` | `#00ffff` (cyan) | 4 | Horizontal | Centered on building face, near top |
| 2 | `HOTEL` | `#ffaa00` (amber) | 4 | Vertical | Right edge of building, stacked letters |
| 3 | `BAR` | `#39ff14` (green) | 5 | Horizontal | Large scale, short word = compact |
| 4 | `24/7` | `#ff3333` (red) | 3 | Horizontal | Smaller scale, urgent feel |
| 5 | `GAMES` | `#ff00ff` (magenta) | 4 | Horizontal | Matches arcade theme |

### Sign Dimensions (pixels at given scale)

- Horizontal: width = `textLength * (5 * scale + scale) - scale`, height = `7 * scale`
- Vertical: width = `5 * scale`, height = `textLength * (7 * scale + scale) - scale`

### Sign Properties Object

```javascript
{
    text: string,
    x: number, y: number,      // top-left position
    w: number, h: number,      // text dimensions
    scale: number,
    color: string,
    vertical: boolean,
    phase: random(0, 2*PI),     // pulse offset
    speed: 0.015 + random(0.01), // pulse speed
    flickerTimer: 0,
    flickering: false,
    drawProgress: 0,            // 0-1, for draw-in animation
    drawSpeed: 0.003 + random(0.005),
    drawn: false
}
```

### Placement Algorithm

```
1. Divide canvas width into 5 equal zones
2. For each zone, find the tallest building with width > 50px
3. Position sign:
   - Horizontal: centered on building face, Y = buildingTop + 12 + random(15% of buildingHeight)
   - Vertical: right-aligned on building face, Y = buildingTop + 15
```

---

## 4. Building Architecture Improvements

### 4a. Setbacks (Stepped Profiles)

For buildings taller than 35% of canvas height, 40% chance of setback:

```javascript
hasSetback: boolean,
setbackH: bh * (0.3 + random(0.2)),      // height of narrower upper section
setbackInset: bw * (0.1 + random(0.1))    // inset per side for upper section
```

**Rendering**: Draw lower section at full width, upper section narrower. Add a ledge highlight at the step.

### 4b. Building Ledges

Horizontal accent lines drawn every 30-50px down each building face:

```javascript
ctx.fillStyle = 'rgba(0,255,255,0.04)';
for (var ly = topY + spacing; ly < H - 10; ly += spacing) {
    ctx.fillRect(b.x, ly, b.w, 1);
}
```

Spacing varies per building: `30 + (buildingIndex * 7 % 20)` for visual variety.

### 4c. Water Towers

15% of buildings (that don't have antennas or AC boxes) get a water tower:

```javascript
hasWaterTower: boolean,
waterTowerW: 10 + random(8),
waterTowerH: 8 + random(6),
waterTowerX: bx + bw * (0.3 + random(0.4)),
waterTowerLegH: 4 + random(4)
```

**Rendering**: Two thin leg columns, a rectangular tank body, and a triangular conical top. Subtle cyan highlight on tank top edge.

---

## 5. Sign Rendering Effects

### 5a. Draw-In Animation

Signs progressively appear character by character when first loaded:

```javascript
if (!sign.drawn) {
    sign.drawProgress = min(1, sign.drawProgress + sign.drawSpeed);
    if (sign.drawProgress >= 1) sign.drawn = true;
}
var charsDrawn = ceil(text.length * drawProgress);
```

### 5b. Pulse / Breathe

Continuous sine-wave brightness oscillation:

```javascript
var pulse = 0.7 + sin(time * sign.speed + sign.phase) * 0.3;
```

Applied to: `ctx.globalAlpha`, `ctx.shadowBlur`

### 5c. Random Flicker

0.08% chance per frame of entering flicker state. Flicker lasts 5-17 frames. During flicker, sign disappears every 3rd frame:

```javascript
if (sign.drawn && !sign.flickering && random() < 0.0008) {
    sign.flickering = true;
    sign.flickerTimer = 5 + floor(random(12));
}
var sFlicker = sign.flickering && (sign.flickerTimer % 3 === 0);
```

### 5d. Visual Layers (back to front)

1. **Dark backing panel**: `rgba(5,5,15,0.85)`, padded by `scale * 2` on each side
2. **Border glow**: 1px stroke in sign color at `pulse * 0.4` alpha, with `shadowBlur = 6 * pulse`
3. **Pixel text**: Filled in sign color at `pulse` alpha, with `shadowBlur = 10 * pulse`
4. **Outer glow pass**: Full rectangle fill at `pulse * 0.15` alpha with `shadowBlur = 25`

---

## 6. Ground Reflections

After existing neon tube reflections, add sign reflections:

```javascript
for (var nsr = 0; nsr < neonSigns.length; nsr++) {
    var sRef = neonSigns[nsr];
    if (!sRef.drawn && sRef.drawProgress < 0.3) continue;
    var refPulse = 0.7 + sin(time * sRef.speed + sRef.phase) * 0.3;
    ctx.globalAlpha = refPulse * 0.06;
    ctx.fillStyle = sRef.color;
    ctx.shadowColor = sRef.color;
    ctx.shadowBlur = 15;
    ctx.fillRect(sRef.x, reflectTop, sRef.w, H - reflectTop);
    ctx.shadowBlur = 0;
}
ctx.globalAlpha = 1;
```

---

## 7. Render Order in draw()

Insert new rendering between existing sections:

```
=== 5. Main Buildings ===          ← MODIFIED: setback rendering, ledges, water towers
=== 6. Neon Tubes & Signs ===      ← EXISTING (unchanged)
=== 6b. Large Neon Signs ===       ← NEW
=== 7. Monorail Track & Pillars ===← EXISTING (unchanged)
...
=== 9. Ground Reflections ===      ← MODIFIED: add sign reflections
```

---

## 8. File Modified

- `Portal/Views/Home/_BgPixelCity.cshtml` — single file, all changes within the `<script>` IIFE

## 9. No Database or Backend Changes

This is purely a frontend canvas rendering enhancement. No migrations, no API changes, no new files.
