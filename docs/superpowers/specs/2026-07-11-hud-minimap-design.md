# HUD Minimap (live proximity map) — Design

**Date:** 2026-07-11
**Port:** d1 (d1x-redux). d2 port deferred — see Out of scope.
**Branch:** `hud-minimap`

## Problem

The only in-game map is the Tab automap: a modal window that stops the ship and, in
multiplayer, leaves the player a sitting target while reading it. Players want continuous
spatial awareness without giving up control — the way a car head-up display shows
navigation over the road.

## Requirements (agreed)

- Live minimap rendered during normal gameplay, picture-in-picture over the 3D view.
- Off by default; enabled in settings.
- Position selectable: top-left, top-right, bottom-left, bottom-right, center of screen.
- Transparent, car-HUD style: no background, no border; the game shows through.
- Shows only proximity around the ship, not the whole level.
- Auto-leveled: best-effort detection of the dominant "level plane" near the ship
  (levels are true 3D; subjective judgment is acceptable).
- Viewing direction chosen so the map looks as flat as possible while keeping 3D
  perspective (slightly tilted top-down, real perspective projection).
- Shows own ship and other players' ships.
- **Clarified with user:**
  - Rotation mode is configurable: heading-up (car-nav style) or north-up (level-fixed).
  - All connected players are always shown, regardless of game mode (user explicitly
    chose this over the automap's coop/team/`NETGAME_FLAG_SHOW_MAP` gating). Exception:
    players with an active cloak are hidden while cloaked.
  - All nearby geometry is drawn, regardless of exploration state (no fog-of-war).
  - Extra settings: size (S/M/L), range (Near/Medium/Far), opacity, toggle key
    (fixed F4 — see Settings section; user approved the change from "bindable").

## Approach decision

**Chosen — A: wireframe proximity map.** Reuse the automap's rendering technique — a
deduplicated, coplanar-pruned edge list drawn as `g3_draw_line` wireframe from an
arbitrary camera — placed in a live PiP sub-canvas exactly the way the rear-view mirror
hooks `game_render_frame_mono()` (`main/gamerend.c:491-537`). 3D perspective comes free
from the g3 pipeline; per-frame cost is a few thousand line tests.

Rejected:

- **B: second `render_frame` with a top-down camera** (pure mirror clone). A textured
  render from above the ship inside a mine shows the ceiling of the current tunnel;
  occlusion makes it useless as a map. Also the most expensive option.
- **C: flat 2D projection.** Discards the required 3D perspective, loses depth cues in
  multi-story areas, and needs new projection code instead of the battle-tested g3 path.

## Design

### 1. Architecture & integration

New module `main/minimap.c` + `main/minimap.h`.

**Shared edge-list machinery.** The automap's edge-list code — `Edge_info` struct,
hash-based dedup (`automap_find_edge`, `automap.c:873`), per-side wall-type coloring
(`add_segment_edges`, `automap.c:983`), coplanar-edge pruning
(`automap_build_edge_list`, `automap.c:1112`) — is static inside `automap.c`, owned by
the modal automap struct. Extract it into a small edge-list API (struct holding
`edges`, `num_edges`, `max_edges`, `highest_edge_index`) that both the automap and the
minimap instantiate. Same algorithm, two instances; the Tab automap's behavior is
unchanged (it still rebuilds on open).

**Lifecycle.** The minimap builds its edge list once per level (edges store global
vertex indices into `Vertices[]`, so the list stays valid for the whole level). Build
lazily on first rendered frame after level load; free/rebuild on level change. The
minimap ignores visited flags — every segment's edges go into the list (per the
no-fog-of-war requirement).

**Per-frame hook.** A minimap block in `game_render_frame_mono()` directly after the
mirror block, gated the same way:

```
PlayerCfg.MinimapMode && Minimap_visible
&& !Player_is_dead && !Endlevel_sequence
&& Viewer == ConsoleObject
&& Newdemo_state != ND_STATE_PLAYBACK
&& !is_observer()
```

Then: compute the square PiP rect inside `Screen_3d_window` (5 positions with the
mirror's margin logic; on surround, place within the center monitor via the same
`base_x`/`base_w/3` scheme, `gamerend.c:504-507`), `gr_init_sub_canvas` +
`gr_set_current_canvas`, render (below), restore `Screen_3d_window`.

**Rendering a frame.** `g3_start_frame()` → `render_start_frame()` →
`g3_set_view_matrix(cam_pos, cam_orient, zoom)` (the automap's exact pattern,
`automap.c:377-383`) → draw in-range edges with `rotate_list` + `g3_draw_line`, with
distance fade near the range limit → draw ship markers → `g3_end_frame()`.
**No `gr_clear_canvas`, no background, no border** — the live game shows through.
The OGL depth buffer is cleared inside every `ogl_start_frame` (`arch/ogl/ogl.c:1149`),
so the overlay never Z-fights the already-drawn main view.

**Scope of effect.** Purely client-side rendering: no `netgame_info` /
`MULTI_PROTO_VERSION` changes, no demo-format changes, no savegame changes.

### 2. Proximity & auto-leveled camera

**Proximity.** A minimap-owned depth buffer (`ubyte Minimap_depth[MAX_SEGMENTS]`).
Never touch `Automap_visited` — it is live map-reveal state, written by the renderer
every frame (`render.c:696`) and saved in savegames (`state.c:1033`). BFS hop-distances
from the ship's current segment, recomputed only when the ship's segment number
changes. An edge is drawn if any adjacent segment has depth ≤ range. Range setting
maps to hops: Near/Medium/Far ≈ 4/6/9 (tuned during implementation). Edges near the
range limit fade out (automap-style `gr_fade_table` fade) for a radar falloff.

**Auto-leveling.** Map "up" = normalized average of per-segment up vectors over
in-range segments. A segment's up = centroid of its top side's vertices minus
centroid of its bottom side's (`Side_to_verts[WTOP]`/`[WBOTTOM]`) — the same math
as the editor's `extract_up_vector_from_segment`, reimplemented locally because
editor code is only compiled with `EDITOR=ON`. The result is temporally smoothed (slew-rate-limited slerp
toward the target, frame-time scaled) so the map re-levels gently instead of snapping
at junctions. If the average degenerates (near-zero length, e.g. opposing ups), hold
the previous up. No world-axis snapping in v1 — smoothing is the stability mechanism.

**In-plane orientation (configurable, `MinimapRotate`):**

- *Heading-up (default):* map north = ship forward vector projected onto the plane
  perpendicular to smoothed-up. When the ship faces nearly straight along ±up (the
  projection degenerates), hold the last stable heading.
- *North-up:* map north = projection of a fixed world axis onto the level plane; pick
  the world axis most perpendicular to smoothed-up, with hysteresis (only switch axes
  when the current one becomes strongly degenerate) so the map doesn't flip mid-flight.

**Camera.** Target = own ship position (ship always at map center). Position = target
+ smoothed-up × distance, with distance per range step chosen so the in-range
geometry roughly fills the PiP's fixed-FOV view. Orientation built with
`vm_vector_2_matrix`: looking down −up, tilted a fixed constant (initially ~18°,
tuned by eye) around the map-right axis so vertical structure reads as 3D while
staying as flat as possible. Zoom is a tuned `fix` constant (automap uses `0x9000`
as its baseline, `automap.c:661`).

**Markers.** Automap-style ship glyphs (sphere + heading arrow, as `draw_player`,
`automap.c:205-235`, draws them). `draw_player` is static to `automap.c`; export it
alongside the edge-list extraction (or reimplement its ~30 lines in `minimap.c` if
export gets awkward — implementation plan decides). Glyphs are scaled for the small
canvas:

- Own ship: always drawn, own player color (team color in team games).
- Other players: every connected player with `Objects[Players[i].objnum].type ==
  OBJ_PLAYER` is drawn in player/team color — in all game modes (user's explicit
  choice; this fork's games are hosted among consenting players). Players whose cloak
  is active are not drawn, so the cloaking device keeps its point.

### 3. Settings, persistence, menu, toggle key

Per-pilot fields in `PlayerCfg` (`playsave.h`), following the mirror pattern
(`playsave.h:107-109`, defaults in `new_player_config`, clamped key/value read and
write in `playsave.c:436-447` / `playsave.c:926-928`):

| Field            | Values                              | Default     |
|------------------|-------------------------------------|-------------|
| `MinimapMode`    | 0 off / 1 on                        | 0 (off)     |
| `MinimapPos`     | 0 TL / 1 TR / 2 BL / 3 BR / 4 center| 1 (top-right) |
| `MinimapSize`    | 0 S / 1 M / 2 L (square, side ≈ view height /6, /4, /3) | 1 (medium) |
| `MinimapRange`   | 0 near / 1 medium / 2 far           | 1 (medium)  |
| `MinimapRotate`  | 0 heading-up / 1 north-up           | 0 (heading-up) |
| `MinimapOpacity` | 1–10 (alpha = value × 10%), menu slider | 6 (60%) |

**Menu.** ~18 items don't fit Graphics Options (fixed `m[31]` array with positional
read-back, `menu.c:1274`). Instead: a dedicated **"Minimap" submenu in the Options
menu** — enable checkbox, position radio group (5), size radio group (3), range radio
group (3), rotation radio group (2), opacity slider. Graphics Options is untouched.

**Toggle key.** Fixed **F4** flips runtime flag `Minimap_visible` (default 1,
mirroring `Mirror_visible`, `game.c:826`) with a HUD message, only while
`MinimapMode` is on. Effective visibility = `MinimapMode && Minimap_visible`.
Rationale (user-approved change from the original "bindable" wording): plain F4
is unused in live gameplay and already reserved in kconfig's `system_keys`
(`kconfig.c:71`) so it cannot collide with user bindings, and it sits naturally
next to F3 (cockpit view toggle). A rebindable control was investigated and
rejected for v1 — the D1X key table is a fixed weapon-select structure, and the
main keyboard table is a hand-authored 50-slot config-screen grid whose entries
hard-code UI coordinates and navigation links; extending it is disproportionate.
Demo playback maps F4 to its percentage display, but the minimap is gated off
during playback, so there is no conflict.

### 4. Engine changes, edge cases, risks

**OGL line alpha (the one engine change).** OGL `g3_draw_line`
(`arch/ogl/ogl.c:383-408`) hardcodes vertex alpha 1.0 and ignores
`grd_curcanv->cv_fade_level`. Change it to derive alpha from the canvas fade level the
way `gr_disk`/`gr_ucircle` already do (`ogl.c:677-694`). The default fade level
everywhere else is full-bright, so the automap and all existing wireframe rendering
stay pixel-identical. The minimap sets the canvas fade level from `MinimapOpacity`
before drawing (via `gr_settransblend`) and restores it after.

**Software renderer fallback.** Software lines can't alpha-blend; `MinimapOpacity`
maps to `gr_fade_table` dimming instead. The map is readable but not translucent —
accepted degradation, feature works in both renderers.

**Known landmine — viewport cache.** `OGL_VIEWPORT` (`include/internal.h:37`)
re-issues `glViewport` only when W×H change, ignoring position. The square minimap
(side from view height) normally can't match the mirror's 4:3 rect (from view width),
but add a defensive 1-px width nudge if the minimap's dims would equal the previous
PiP's, and explicitly test mirror + minimap enabled together.

**Other cases.**

- Center position overlaps the reticle: acceptable because there's no background and
  alpha is low (user-selected placement).
- Cockpit modes: the PiP lives inside `Screen_3d_window`, so it follows whatever
  window the cockpit mode provides (full, status bar, cockpit).
- Level end / secret exit: free edge list + reset depth buffer on level load.
- Demo recording is unaffected (the minimap never calls `render_frame`, so no
  once-per-frame record/lighting hooks fire); demo playback hides the minimap (gate).
- Multiplayer marker jitter at low net update rates matches the automap's behavior —
  no interpolation in v1.
- Performance: edge scan is O(total edges) ≈ `Num_segments × 12` cheap flag/depth
  tests per frame plus a few hundred line draws; BFS only on segment change. No
  measurable frame cost expected.

## Testing (manual — no test suite in this repo)

Verification baseline: d1 configures, builds, links (`build/main/d1x-redux.exe`), game
runs with retail data.

1. SP, small level (D1 level 1): enable minimap, verify geometry matches
   surroundings, ship centered, heading-up rotation tracks turns.
2. SP, vertically twisty level (e.g. D1 level 7+ shafts): auto-level re-levels
   smoothly through vertical sections; no snapping or spinning.
3. North-up mode: map stays level-fixed while flying loops; no axis flips.
4. All 5 positions × 3 sizes × 3 ranges; opacity slider visibly changes blend (OGL).
5. Software renderer: map draws dimmed, no crashes, opacity maps to dimming.
6. Mirror + minimap enabled together: both PiPs render at correct positions
   (viewport-cache landmine).
7. 2-player LAN anarchy: both players' markers visible in FFA; cloaked player
   disappears from the map; observer mode shows no minimap.
8. Demo: record with minimap on (plays back clean, no minimap during playback).
9. Death/respawn, endlevel sequence, Tab automap open/close: no minimap bleed-through,
   automap unchanged.
10. Cockpit / status-bar / full-screen modes; surround mode (PiP on center monitor).

## Out of scope (v1)

- d2 port — the whole feature plus the `g3_draw_line` alpha change mirror to
  `d2/` as a follow-up, per repo convention for engine changes.
- Robots/hostiles/powerups on the map (players only, per requirements).
- True translucency in the software renderer.
- Minimap during demo playback or observer mode.
- Fog-of-war / visited-only mode.
- Marker interpolation between net updates.
- Rebindable minimap toggle key (fixed F4 in v1).
