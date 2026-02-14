# Background Scene 6: Neon Alley

## Inspiration

Based on the "Chinese Alleyway" animated art by Killer Rabbit Media — a narrow, rain-soaked Asian alleyway at night viewed in perspective, dense with glowing neon signage, warm lantern light, atmospheric haze, and wet ground reflections. The aesthetic blends lo-fi mood with cyberpunk neon intensity.

## Visual Concept

A **first-person perspective view** looking down a narrow alley flanked by tall buildings on both sides. The scene conveys depth via converging perspective lines, layered lighting, and atmospheric fog. Animated rain, flickering neon signs, drifting steam/fog, and shimmering ground reflections bring it to life.

### Color Palette

| Element | Colors |
|---|---|
| Sky (top) | Deep indigo `#050510` to dark purple `#0c0820` |
| Buildings | Dark tones `#0a0a18`, `#0e0c1c`, `#12102a` |
| Neon signs | Cyan `#00ffff`, pink/magenta `#ff00ff`, warm orange `#ff8844`, red `#ff3333` |
| Lanterns | Warm amber `#ffaa44`, soft red `#ff4444` |
| Ambient light | Purple haze `rgba(120, 40, 200, 0.08)` |
| Ground | Dark wet surface with colored reflection streaks |
| Rain | Cyan, pink, and warm-white streaks |
| Steam/fog | Semi-transparent white/purple wisps |

### Scene Layers (back to front)

```
Layer 0 — Sky gradient + faint stars/glow
Layer 1 — Distant buildings (small, faded, centered — vanishing point)
Layer 2 — Mid-ground buildings (left & right walls of alley, perspective lines)
Layer 3 — Neon signs (mounted on buildings, overlapping into alley)
Layer 4 — Hanging lanterns + string lights (draped across alley)
Layer 5 — Steam / fog wisps (drifting horizontally)
Layer 6 — Rain drops (falling diagonally)
Layer 7 — Wet ground with reflections (bottom 20%)
```

## Implementation Approach

### Technology: HTML5 Canvas (like _BgPixelCity.cshtml)

Canvas is the right choice here because:
- Complex perspective geometry (converging lines, variable-width buildings)
- Many animated elements (rain, flickering signs, drifting steam, reflections)
- Pixel-level control for glow/haze effects
- Consistent with the most sophisticated existing background (Pixel City)

### File

`Portal/Views/Home/_BgNeonAlley.cshtml` — self-contained partial view with `<canvas>`, `<style>`, and `<script>` blocks.

### Integration

1. Add `@await Html.PartialAsync("_BgNeonAlley")` to `Index.cshtml` inside `#bgContainer`
2. Add `{ id: 'bgNeonAlley', label: 'ALLEY' }` to the scenes array in the switcher script

## Detailed Element Breakdown

### 1. Sky & Atmosphere

- Vertical gradient from deep indigo to dark purple
- Narrow strip of sky visible between building tops (perspective-narrowing toward center)
- 1-2 faint radial glows suggesting distant city light pollution (purple/magenta)
- Optional: a sliver of moon or distant neon glow at vanishing point

### 2. Buildings (Perspective Walls)

Two rows of buildings forming the alley walls, drawn with simple perspective:

- **Vanishing point**: center-top area of canvas (~50% X, ~25% Y)
- **Left wall**: trapezoid from bottom-left, converging toward vanishing point
- **Right wall**: trapezoid from bottom-right, converging toward vanishing point
- Buildings are segmented into floors with faint horizontal lines
- **Windows**: small rectangles, ~30% randomly lit in warm yellow or cool blue tones
- Window light has soft glow bleeding outward
- Building surfaces have subtle vertical texture lines

### 3. Neon Signs

6-8 neon signs mounted on building walls, projecting into the alley at angles:

- Mix of horizontal bar signs and vertical hanging signs
- Text rendered with the existing `PIXEL_FONT` bitmap system (reuse from Pixel City)
- Suggested sign text: `ARCADE`, `GAMES`, `PLAY`, `BAR`, `HOTEL`, `24/7`, `OPEN`
- Each sign has:
  - A colored rectangle/border as the sign body
  - Pixel-font text inside
  - Multi-layer glow (box-shadow equivalent via `ctx.shadowBlur`)
  - **Flicker animation**: sinusoidal opacity pulsing + random momentary flicker-off
  - Color-coded glow spill onto nearby building surfaces
- Signs at different depths (sizes decrease toward vanishing point)

### 4. Hanging Lanterns & String Lights

- 2-3 strings of lights draped across the alley (catenary curves)
- Small circular lanterns in warm amber/red
- Soft radial glow around each lantern
- Gentle sway animation (sinusoidal horizontal offset)
- Glow intensity pulses subtly

### 5. Steam / Fog

- 3-5 wispy shapes that drift horizontally across the alley
- Semi-transparent (`opacity 0.03-0.08`), white or light purple
- Drawn as wide, soft, horizontal elliptical gradients
- Slow horizontal drift (wrapping around when off-screen)
- Adds depth layering and atmosphere

### 6. Rain

- 40-80 rain drops (scaled to canvas width)
- Slight diagonal fall (wind effect) — not perfectly vertical
- Three color variants matching the dominant neon sources: cyan, pink, warm-white
- Each drop: 1px wide, 10-30px tall, gradient from transparent to color
- Speed varies by drop (parallax: faster = closer)
- Subtle splash effect not needed (keep simple like existing rain scene)

### 7. Wet Ground & Reflections

Bottom 18-22% of the canvas:

- Dark surface with slight purple/blue tint
- **Reflection streaks**: vertical color bands mirroring the neon signs above
  - Same color as corresponding sign, heavily reduced opacity (0.03-0.06)
  - Slight horizontal wobble animation (simulating water ripple)
- **Specular highlights**: small bright dots where rain "hits" the ground (randomly placed, flickering)
- Gradient fade from the alley floor into full dark at the very bottom

## Animation Summary

| Element | Animation Type | Timing |
|---|---|---|
| Neon signs | Sinusoidal glow pulse + random flicker | 2-5s period per sign, random flicker every 3-8s |
| Lanterns | Gentle sway + glow pulse | 3-4s sway period, 2s glow pulse |
| Rain drops | Continuous diagonal fall | 1-3s per drop cycle |
| Steam wisps | Horizontal drift | 15-25s to cross screen |
| Ground reflections | Horizontal wobble | 2-3s period |
| Lit windows | Occasional on/off toggle | Random, every 5-15s per window |

## Performance Considerations

- Use `requestAnimationFrame` for the render loop (consistent with Pixel City)
- Regenerate static elements (buildings, windows) only on resize
- Only animate dynamic elements per frame (rain, signs, steam, reflections)
- Cap rain drop count based on viewport width (`Math.min(80, width / 15)`)
- Redraw only changed regions where feasible, or accept full redraw (Canvas is fast enough for this complexity)

## Estimated Complexity

Comparable to `_BgPixelCity.cshtml` in scope. The perspective geometry is simpler (two converging walls vs. a full skyline + monorail), but the atmospheric effects (rain, steam, reflections) add density. Expect ~600-900 lines of JS.
