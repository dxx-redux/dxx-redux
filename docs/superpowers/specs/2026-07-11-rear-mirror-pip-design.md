# Rear-View Mirror (Picture-in-Picture) — Design

- **Date:** 2026-07-11
- **Scope:** d1x-redux only (`d1/`); no d2 port planned
- **Status:** Approved

## Goal

Show the rear view as a small picture-in-picture "mirror" overlaid on the game view, instead of
switching the whole screen to rear view. Configurable in the menu: position (top left / top
center / top right) and size (small / medium / large ≈ 1/32, 1/16, 1/8 of the screen —
approximate; clean geometry wins over exact area). While the feature is enabled, the rear-view
key (default R) only toggles the mirror on/off; full-screen rear view is unreachable.

## Decisions (from brainstorming)

| Question | Decision |
|---|---|
| Image orientation | **True mirror** — horizontally flipped like a car mirror (enemy behind-left appears on the mirror's left) |
| Flip mechanism | **A: negated right-vector** in the view matrix (see Alternatives) |
| Ports | d1 only |
| Persistence | Per-pilot (`PlayerCfg`, playsave.c) — same as StickyRearview |
| Menu placement | Graphics Options, checkbox + radio groups |
| Draw order | Mirror draws under the HUD text layer (score/messages stay readable) |

## Alternatives considered

- **B: render normal, 2D-flip the rectangle afterwards** — rejected: trivial in the software
  renderer (row reversal) but the OpenGL path would need a `glReadPixels` round-trip every frame
  (pipeline stall; ~1280×720 at Large on 4K) or FBO render-to-texture, infrastructure already
  rejected for surround as foreign to this fixed-function-era renderer.
- **C: flip-x flag inside the `3d/` projection library** — rejected: same reversed-winding
  question as A but touches shared projection code every caller uses, instead of one
  view-matrix call site.

## User-facing behavior

- Graphics Options (menu.c `graphics_config()`) gains:
  - `[ ] Rear View Mirror` checkbox (default off)
  - `Mirror position:` radio group `Top Left / Top Center / Top Right` (default Top Center)
  - `Mirror size:` radio group `Small / Medium / Large` (default Medium)
- Takes effect immediately; no restart. Enabling the checkbox also calls `reset_rear_view()`
  (clears any active full-screen rear view) and makes the mirror visible.
- While enabled, the rear-view key (`Controls.rear_view_count`) toggles mirror visibility only:
  no `Rear_view` flag, no cockpit-art switch (`CM_REAR_VIEW`), no demo rear-view records, and
  hold-to-peek / `StickyRearview` semantics are ignored. With the feature disabled, the key
  behaves exactly as today.
- Mirror starts visible whenever the feature is on (visibility is a session-runtime flag, not
  persisted; the menu checkbox is the persistent switch).
- The kconfig binding keeps its existing "Rear view" label.

## Rendering architecture

### Hook point

In `game_render_frame_mono()` (main/gamerend.c), immediately after the main 3D view renders
(after the surround three-view loop when surround is on, with `Surround_view` back at −1), and
before `update_cockpits()` / gauges / HUD:

1. Compute the mirror rectangle (below) as a sub-canvas of the 3D window.
2. Set a new global `Mirror_view = 1`, `gr_set_current_canvas(&mirror_canv)`,
   `render_frame(0)`, restore `Mirror_view = 0` and the previous canvas.
3. Draw a gray border frame (color `BM_XRGB(6,6,6)`, thickness `max(2, base_h/360)` pixels)
   just **inside** the rectangle's edges — overdrawing the outermost mirror pixels, so no
   clipping against the base canvas is ever needed — so it reads as a mirror.

Render condition (all must hold):

```
PlayerCfg.MirrorMode && Mirror_visible
&& !Player_is_dead && !Endlevel_sequence
&& Viewer == ConsoleObject
&& Newdemo_state != ND_STATE_PLAYBACK
&& !is_observer()
```

### `Mirror_view` effects in `render_frame()` (main/render.c)

- **Demo-record gate:** `newdemo_record_start_frame()` / `newdemo_record_viewer_object()` run
  only when `Surround_view <= 0 && !Mirror_view` (extends the existing surround gate at
  render.c:1447) — the mirror view must never double-record a frame. Per-object render
  records (`newdemo_record_render_object`, object.c) carry the same `!Mirror_view` gate, so
  demos recorded with the mirror visible are identical to mirror-off gameplay.
- **Lighting gate:** same condition added to the `start_lighting_frame()` call (render.c:1454).
- **View matrix:** the existing rear-view branch (render.c:1478) becomes
  `if ((Rear_view || Mirror_view) && Viewer==ConsoleObject)`; it composes the same 180° heading
  matrix as today, then, when `Mirror_view`, negates the resulting matrix's right vector
  (`vm_vec_negate(&viewm.rvec)`) before `g3_set_player_view_matrix(...)`. `Rear_view` and
  `Mirror_view` are never set simultaneously (the feature blocks full-screen rear view).
- Zoom stays `Render_zoom`, and the mirror canvas has the same aspect ratio as the 3D window,
  so the mirror shows exactly what full-screen rear view would show, miniaturized.

### Why the negated right-vector flips the image

Projection maps view-space x through the right vector (`x = dot(p − eye, rvec)`), so negating
`rvec` negates every projected x — a horizontal flip of the whole image — before either
rasterizer runs. World-space visibility (`g3_check_normal_facing` uses world normals and the
viewer position) is unaffected.

The flip reverses every scene polygon's projected winding, and the OpenGL renderer enforces
back-face culling with clockwise front faces (`ogl_start_frame`: `glEnable(GL_CULL_FACE)` +
`glFrontFace(GL_CW)`) — so the mirror pass disables `GL_CULL_FACE` directly after
`g3_start_frame()` while `Mirror_view` is set; the next `ogl_start_frame` re-enables it.
GL-level culling is redundant for the mirror anyway (face selection already happened CPU-side
in world space). `glFrontFace(GL_CCW)` would be the wrong fix: billboard sprite quads are
built after the view rotation, keep their unmirrored winding, and would then be culled.
(Caught by the whole-branch review — this spec originally claimed the OGL path has no
winding-dependent culling, which was wrong.)

The one remaining open risk is the **software** scanline texture mapper receiving
reverse-wound projected polygons — checked in the acceptance run. If it misrenders, the
negation (and only it) is wrapped in `#ifdef OGL` (renderer choice is compile-time), and
software builds get an unflipped mirror; the default OGL build keeps the true mirror.

Object sprites (powerups, explosions) are view-space billboards: their positions mirror
correctly; the sprite bitmaps themselves may draw unflipped, which is imperceptible.

### Geometry

Base rectangle = the 3D game window (`Screen_3d_window`), so cockpit modes (full cockpit,
status bar, F3-shrunken window) scale the mirror naturally. When surround is on, the base is
the **center third** of `Screen_3d_window` (one monitor), matching the surround HUD canvas.

With base size `w × h` and size divisor `div` (Large = 3, Medium = 4, Small = 6):

- mirror size: `mw = w/div`, `mh = h/div` (areas ≈ 1/9, 1/16, 1/36 of the base — nearest clean
  fractions to the requested 1/8, 1/16, 1/32; same aspect as the base, so no distortion)
- margin: `mgn = max(2, h/64)`
- y = `mgn`; x = `mgn` (Top Left) / `(w − mw)/2` (Top Center) / `w − mw − mgn` (Top Right)

The rectangle is a sub-canvas of the base, offset by the center third's origin in surround.

### Why a second scene render is acceptable

Surround already renders three sequential views per frame through the same `render_frame()`
path (per-view depth clear and canvas-relative viewport are established as safe); the mirror
adds one more view at 1/9th resolution or less. In surround + mirror, that is four renders —
still bounded by the same reasoning that justified surround's three.

## Settings & persistence

New per-pilot fields in `PlayerCfg` (main/playsave.h / playsave.c, following the
`StickyRearview` pattern — default in `new_player_config()`, `atoi` line in the plx read loop,
`PHYSFSX_printf` line in the write path):

- `ubyte MirrorMode;` — 0/1, default 0. Line `mirrormode=`.
- `ubyte MirrorPos;` — 0 = top left, 1 = top center, 2 = top right, default 1. Line `mirrorpos=`.
- `ubyte MirrorSize;` — 0 = small, 1 = medium, 2 = large, default 1. Line `mirrorsize=`.

Out-of-range values read from the file fall back to the defaults. Runtime state (not
persisted): `int Mirror_visible` (starts 1), `int Mirror_view` (active-render flag).

## Edge cases

- **Demo recording:** the mirror view records nothing (gates above); R never emits
  rearview/restore-rearview demo records while the feature is on. Demos stay single-view and
  format-compatible.
- **Demo playback:** mirror is hidden; a demo's own recorded full-screen rear view plays back
  untouched (`nd_playback_v_rear`).
- **Death / observer / endlevel / automap:** mirror hidden (render condition); automap and
  endlevel never reach the hook point anyway.
- **Multiplayer:** purely client-side rendering — no netcode, no protocol bump.
- **Cockpit modes:** mirror lives inside the 3D window in all modes; `CM_REAR_VIEW` cockpit art
  is unreachable while the feature is on (only `check_rear_view()` selects it). F3 cockpit
  cycling is not blocked by the mirror (unlike full-screen rear view, which blocks it).
- **Enabling mid-rear-view:** the menu checkbox's enable path calls `reset_rear_view()`.
- **Screenshots:** capture the framebuffer as today, mirror included.
- **Non-OGL build:** feature works; only the horizontal flip may degrade to unflipped if the
  prototype task finds the software mapper can't take reversed winding.

## Out of scope (v1)

- d2 port; mirrored image for guided views (d1 has none); bezel/curvature/shape effects;
  drawing the mirror above the HUD; custom sizes/positions beyond the presets; persistence of
  the R-key visibility toggle; command-line flag.

## Testing

No test suite; verification is build + run (per CLAUDE.md):

1. Build d1 (`cmake --build build -j`, MSYS2 MinGW64 toolchain).
2. **OGL sanity:** mirror shows live, correct geometry (the mirror pass disables GL back-face
   culling; without that, the flipped scene is culled to a stale/blank rectangle).
3. Menu: toggle Rear View Mirror, sweep all 3 positions × 3 sizes; values persist across
   restart in the pilot file.
4. **Flip correctness:** strafe left next to a landmark behind you — it must drift toward the
   mirror's **right** edge (true mirror). If it drifts left, the image is unflipped.
5. R key: toggles the mirror only; with the feature off, classic rear view (tap-toggle and
   hold-to-peek with StickyRearview) still works.
6. Checklist sweep: full cockpit + status bar + F3-shrunken window; surround on (mirror on
   center monitor, positions relative to it); demo record with mirror on → playback shows a
   normal single view; death, observer mode, endlevel flyout, automap all hide it; screenshot
   includes it; HUD text draws over it.

## Files expected to change

- `d1/main/render.c` — `Mirror_view` flag, matrix flip, demo/lighting gates.
- `d1/main/gamerend.c` — mirror render block + border in `game_render_frame_mono()`.
- `d1/main/game.c` / `game.h` — `check_rear_view()` mirror branch, `Mirror_visible`.
- `d1/main/playsave.c` / `playsave.h` — `MirrorMode`, `MirrorPos`, `MirrorSize`.
- `d1/main/menu.c` — Graphics Options checkbox + radio groups.
