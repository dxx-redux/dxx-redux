# Triple-Monitor Surround Mode — Design

- **Date:** 2026-07-10
- **Scope:** d1x-redux only (`d1/`); no d2 port planned
- **Status:** Approved

## Goal

Let the game be played on three 4K monitors presented as a single 11520×2160 screen by
NVIDIA Surround, with the side monitors physically angled inward (wrap-around cockpit
arrangement). The in-mission 3D view spans all three monitors as a continuous panorama with a
configurable total horizontal field of view: **150°, 180°, or 270°**. Controlled by a toggle in
the settings menu.

## Decisions (from brainstorming)

| Question | Decision |
|---|---|
| Monitor arrangement | Angled inward (wrap-around) — side monitors face the player |
| Rendering approach | **A: three yawed planar views**, one per monitor (see Alternatives) |
| 2D layers (HUD, menus, automap, briefings) | Center monitor only |
| Ports | d1 only |

## Alternatives considered

- **B: single ultra-wide planar frustum** — rejected: planar projection cannot reach 180°/270°
  at all, has severe edge stretch even at 150°, and is geometrically wrong for angled monitors.
- **C: cylindrical panorama (render-to-texture + warp shader)** — rejected: requires FBO/shader
  infrastructure foreign to this fixed-function-era renderer, and cylindrical output curves
  straight lines on flat panels. Approach A is already geometrically exact for flat, angled
  monitors.

## User-facing behavior

- Graphics Options (menu.c `graphics_config()`) gains:
  - `[ ] Triple-Monitor Surround` checkbox (default off)
  - Angle radio group: `150 / 180 / 270 degrees` (default 180), grouped like the existing
    Texture Filtering radios.
- Takes effect immediately; no restart.
- Resolution-independent: the mode splits the current game window into thirds whatever its
  size, so it can be tested in a window on a single monitor.
- While enabled, cockpit-art view modes (full cockpit, status bar) are overridden to the
  full-screen-HUD style and restored when disabled.

## Rendering architecture

### Core mechanism

In `game_render_frame_mono()` (main/gamerend.c), when surround is enabled, replace the single
`render_frame(0)` into `Screen_3d_window` with three renders:

1. Split `Screen_3d_window` into three equal-width sub-canvases: left, center, right.
2. Render each with the camera yawed by **−A / 0 / +A**, where **A = total_angle / 3** is the
   per-monitor angle (50° / 60° / 90°), and with per-view zoom giving exactly **A** of
   horizontal FOV.
3. A global "active surround view" state (off / left / center / right) is set around each
   `render_frame` call; the view-matrix helper (below) reads it.

Fixang values for A: 50° = 0x238E, 60° = 0x2AAB, 90° = 0x4000 (full circle = 0x10000).

### View-matrix helper

All player-eye view-matrix call sites route through one new helper,
`g3_set_player_view_matrix(pos, orient, zoom)` (main/render.c), which:

- when surround is inactive, forwards to `g3_set_view_matrix` unchanged;
- when a surround view is active, multiplies `orient` by a heading rotation of ±A (same
  mechanism as the existing rear-view heading multiply at render.c:1404) and substitutes the
  per-view zoom.

Call sites converted: the normal and rear-view paths in `render_frame()` (render.c:1410,
render.c:1422) and the endlevel-sequence paths in `render_endlevel_frame()` (endlevel.c:1024,
endlevel.c:1027 — endlevel builds its own view matrix, so it must go through the helper to
render in surround; it runs inside the per-view `render_frame` calls). Other `g3_set_view_matrix`
users (automap, polyobj menu previews, editor) are intentionally not converted.

Rear view composes with the per-view yaw (180° + ±A), producing a seamless wraparound rear
panorama — intended behavior.

### FOV math

The engine's projection maps the screen edge to view-space `x/z = View_zoom / Window_scale.x`
(3d/matrix.c `scale_matrix()`: zoom ≤ 1 scales the z axis; g3_start_frame computes
`Window_scale` from the current canvas each frame). Therefore:

- half-hFOV = atan(zoom / Window_scale.x); the stock `Render_zoom = 0x9000` (0.5625) yields the
  classic ~74°×59° at 4:3, validating the model.
- Per-view zoom: **zoom = fixmul(tan(A/2), Window_scale.x)**, computed after `g3_start_frame()`
  has set `Window_scale` for the sub-canvas. This makes the horizontal FOV exactly A regardless
  of the user's aspect settings; vertical FOV then follows from the canvas shape via
  `Window_scale.y`, exactly as in normal play.
- `tan(A/2)` is computed as `fix_sincos(A/2, &s, &c)` (maths/fixc.c) then `fixdiv(s, c)`, from
  the **same fixang** used for the yaw, so the yaw step and frustum width agree to fixed-point
  precision.

**Seam correctness:** adjacent views yawed by exactly their horizontal FOV share frustum edge
planes, so world geometry is continuous across monitor bezels. This is the standard
triple-head technique and is exact for flat monitors angled at A to each other.

**Perceived FOV per mode** (per-view, 16:9 sub-canvas, square pixels): 150° → 50°×29.4° per
monitor (looks zoomed-in relative to normal play — physically correct for a 150° setup);
180° → 60°×36.0°; 270° → 90°×58.7° (closest to the normal Descent feel).

### Why 3× rendering is acceptable

Three scene renders per frame of a 1996 engine, on hardware that drives three 4K panels, after
the recent `ogl.c` draw-path and `build_object_lists` optimizations. The OGL per-view frame
setup (`ogl_start_frame`: viewport follows canvas, full-screen depth clear between views) is
already safe for sequential views — the same pattern d2 uses for cockpit window views.

## 2D layers (center monitor only)

- **HUD / reticle / gauges / multiplayer & observer overlays:** `game_draw_hud_stuff()` runs
  with the current canvas set to the **center sub-canvas** instead of the full game window.
  All HUD drawing is canvas-relative, so one canvas swap covers it. The reticle remains at the
  center of the middle monitor, which is the true (unrotated) aim direction.
- **Menus:** no change needed — font scaling already clamps horizontal to vertical scale
  (main/gamefont.c:131), and newmenu boxes center on screen.
- **Automap and briefings/titles:** rendered into a centered, screen-width/3 canvas while
  surround is on; side monitors stay black. Automap's own g3 frame picks up canvas dimensions
  automatically.

## Settings & persistence

New fields in `GameCfg` (main/config.h / config.c — machine/display config, alongside
ResolutionX/VSync):

- `int SurroundMode;` — 0/1, default 0. Config line `SurroundMode=`.
- `int SurroundAngle;` — degrees, one of 150/180/270, default 180. Config line
  `SurroundAngle=`. Any other value read from the config file falls back to 180.

No command-line flag and no per-pilot storage in v1.

## Edge cases

- **Demo recording:** `render_frame()` triggers `newdemo_record_start_frame()` /
  `newdemo_record_viewer_object()` when recording; only the **first** rendered view of a frame
  may record, otherwise frames triple-record. Demos stay format-compatible (single-view data);
  playback goes through the surround-aware render path, so watching demos in surround works.
- **Lighting hack:** `start_lighting_frame()` must run once per frame, not once per view, to
  keep the light-smoothing rate unchanged.
- **Shrunken game window (F3−/+):** the split applies to whatever `Screen_3d_window` is; three
  slices inside the smaller window.
- **Non-OGL build:** mechanism is canvas-level and renderer-agnostic; no `#ifdef OGL` in the
  feature. (Software rendering at 11520px is slow, but that is pre-existing.)
- **Toggling mid-game:** state is read each frame from `GameCfg`; no caches to invalidate
  beyond the cockpit-mode override/restore.
- **Screenshots:** capture the full framebuffer as today (all three views).

## Out of scope (v1)

- Bezel-gap compensation (NVIDIA driver-level bezel correction handles this upstream).
- Cylindrical warp rendering; monitor counts other than 3; custom angles beyond the three
  presets; command-line/INI flag; d2 port; per-pilot persistence.

## Testing

No test suite exists in this repo; verification is build + run (per CLAUDE.md):

1. Build d1 (`cmake --build build -j` with the MSYS2 MinGW64 toolchain).
2. Windowed run at a wide aspect (e.g. 3840×720) on a single monitor: verify three
   16:9-proportioned slices at all three angle settings.
3. **Seam check:** position next to a long straight wall edge crossing a view boundary; the
   line must continue straight and objects must cross seams without jumps, at 150/180/270.
4. Checklist sweep: rear view (wraparound rear), automap centered, menus unstretched, briefing
   centered, demo record → playback (no triple-recording), cockpit mode forced to full-screen
   HUD and restored on disable, F3 window shrink, screenshot, toggle on/off mid-game, endlevel
   flyout sequence.
5. Final validation on the real 11520×2160 NVIDIA Surround rig at 270°.

## Files expected to change

- `d1/main/render.c` — surround view state, `g3_set_player_view_matrix` helper, per-view zoom.
- `d1/main/endlevel.c` — route view-matrix calls through the helper.
- `d1/main/gamerend.c` — three-view render loop, HUD canvas swap.
- `d1/main/game.c` — sub-canvas setup alongside `game_init_render_sub_buffers` /
  cockpit-mode override.
- `d1/main/automap.c`, briefing/title screens — centered canvas while enabled.
- `d1/main/config.h`, `d1/main/config.c` — `SurroundMode`, `SurroundAngle`.
- `d1/main/menu.c` — Graphics Options toggle + angle radios.
