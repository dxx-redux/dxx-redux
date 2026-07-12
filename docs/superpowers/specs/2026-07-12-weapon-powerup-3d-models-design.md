# Powerup 3D models — design

Status: approved design · branch `unity` · 2026-07-12

> Scope note (2026-07-12): expanded from weapons-only to **all powerups** at the user's
> direction ("include everything"). The mechanism is powerup-agnostic; the id table in §3 is
> the single source of truth.

## 1. Context

In Descent 1, powerups on the map (laser, energy, keys, missiles, …) are **pre-rendered
turntable sprites**: the artists at Parallax built 3D models, rendered them spinning about
the vertical axis into a `VClip` (animated 2D bitmap sequence), and shipped only the
sprites. The source 3D models were never released and are **not** in the game data (the
PIG's 85 POF models are ships/robots/reactor/mines — no powerups).

The Unity port currently reproduces this faithfully: `ObjectVisuals.Resolve` maps every
type-7 powerup to `Sprite(vclipNum)`, and `BillboardSprite` renders a camera-facing quad
cycling the vclip frames (`unity/Assets/Scripts/Convert/ObjectVisuals.cs:36`,
`unity/Assets/Scripts/Presentation/BillboardSprite.cs`).

Example — the laser powerup (`POW_LASER = 3` → vclip 20): **15 frames, 59×18 px**, bitmap
name `laser`, a 360° turntable at 0.1 s/frame (1.5 s per spin). Visually a single-barrel
laser cannon: reddish-brown angular body, yellow-green hazard stripe, two small top spikes,
gray gun-barrel with a dark muzzle bore. Its sibling `POW_QUAD_FIRE = 12` is unmistakably a
four-barrel cluster with a pinkish yoke — confirming these are genuine 3D objects. The rest
of the family is the same idea rendered as sprites: mechanical cannons/rockets, glowing
orbs (energy/shield), keycards, and utility items (cloak/invuln/extra-life).

This feature **restores** the lost 3D models: each powerup becomes a real 3D mesh that
idle-spins in the level, sourced as AI-generated glTF/GLB art through an override layer,
with the existing sprite as automatic fallback.

The port's plan already anticipates this: `docs/unity-port-plan.md` §7.1 (content override
layer) — "a detailed FBX/glTF … replaces the generated prefab by name, no code change …
Override packs are original content and can ship with the build." §7.1 only covered objects
that *already* have model slots (ship/robots/reactor); this feature extends it to
sprite-only powerups.

## 2. Goals / non-goals

**Goals**
- **Every powerup that renders as a turntable sprite** becomes a real 3D mesh that
  idle-spins, looking as close as feasible to the original sprite.
- Art is **AI-generated GLB**, dropped into a per-powerup override slot; adding/replacing a
  model is a file drop with **zero code change**.
- One powerup-agnostic mechanism covers the whole §3 set; laser is the fully-worked first
  example, then the rest roll through the same pipeline.
- The existing sprite remains the automatic fallback; nothing regresses when no model is
  present.

**Non-goals**
- No change to gameplay: pickup, collision (sphere-radius), amounts, drops, netcode are
  untouched. This is presentation-only.
- The 7 unused/D2-only powerup slots that point at the `exp13` placeholder vclip
  (ids 7, 8, 9, 24, 26, 27, 28) are left untouched — they never appear in D1 levels.
- No PBR-lit look by default — the game is unlit/vertex-lit; override models match that.
- Not touching in-flight weapon *projectile* models or the ship's gun — only the **pickups
  on the map**.

## 3. Scope — the powerup set

All 22 real D1 powerups, keyed by powerup id (`ObjectRecord.SubtypeId`, constants from
`d1/main/powerup.h`; frame-0 bitmap and `Powerup.Size` from the pig):

| id | Constant | Sprite (bitmap, px) | Size | Override base name | Group |
|---|---|---|---|---|---|
| 3  | `POW_LASER`             | `laser` 59×18    | 4.0 | `laser`     | primary |
| 13 | `POW_VULCAN_WEAPON`     | `vulcan` 58×20   | 4.0 | `vulcan`    | primary |
| 14 | `POW_SPREADFIRE_WEAPON` | `spread` 62×25   | 4.0 | `spread`    | primary |
| 15 | `POW_PLASMA_WEAPON`     | `plasma` 59×23   | 4.0 | `plasma`    | primary |
| 16 | `POW_FUSION_WEAPON`     | `fusion` 60×28   | 4.0 | `fusion`    | primary |
| 12 | `POW_QUAD_FIRE`         | `quad` 63×39     | 3.0 | `quad`      | primary |
| 10 | `POW_MISSILE_1`         | `cmissil1` 52×30 | 2.5 | `cmissile`  | secondary |
| 11 | `POW_MISSILE_4`         | `cmissil2` 63×42 | 3.0 | `cmissile4` | secondary |
| 18 | `POW_HOMING_AMMO_1`     | `hmissil1` 52×28 | 2.5 | `hmissile`  | secondary |
| 19 | `POW_HOMING_AMMO_4`     | `hmissil2` 55×37 | 3.0 | `hmissile4` | secondary |
| 17 | `POW_PROXIMITY_WEAPON`  | `pbombs` 58×40   | 3.0 | `pbomb`     | secondary |
| 20 | `POW_SMARTBOMB_WEAPON`  | `smissile` 58×37 | 2.5 | `smart`     | secondary |
| 21 | `POW_MEGA_WEAPON`       | `mmissile` 57×37 | 3.0 | `mega`      | secondary |
| 22 | `POW_VULCAN_AMMO`       | `vammo` 39×38    | 1.8 | `vammo`     | ammo |
| 1  | `POW_ENERGY`            | `pwr01` 64×64    | 3.0 | `energy`    | orb |
| 2  | `POW_SHIELD_BOOST`      | `pwr02` 43×42    | 2.5 | `shield`    | orb |
| 4  | `POW_KEY_BLUE`          | `key01` 50×51    | 3.0 | `key_blue`  | key |
| 5  | `POW_KEY_RED`           | `key03` 52×53    | 3.0 | `key_red`   | key |
| 6  | `POW_KEY_GOLD`          | `key02` 52×53    | 3.0 | `key_gold`  | key |
| 23 | `POW_CLOAK`             | `cloak` 53×52    | 2.0 | `cloak`     | utility |
| 25 | `POW_INVULNERABILITY`   | `invuln` 57×42   | 2.6 | `invuln`    | utility |
| 0  | `POW_EXTRA_LIFE`        | `life01` 44×43   | 2.5 | `extralife` | utility |

The id→name table lives in one place (`OverrideModels`) and is the single source of truth
for the powerup set. The 4-packs (11, 19) may supply their own GLB or, if absent, fall
through to the single model (`cmissile`/`hmissile`), then to their own sprite. Keys must
stay colour-identifiable (gameplay-critical); their override is optional per key.

## 4. Decisions locked (from brainstorming, 2026-07-12)

1. **Sourcing:** author via an external AI image-to-3D generator (Meshy/Tripo/Rodin/…), run
   by the user. This repo/tooling produces the generator **inputs** (reference kit) and
   consumes its **outputs** (GLB). We never call the generator from code.
2. **Format:** glTF/GLB, loaded at runtime via **glTFast** (UPM package) — the one new
   dependency, validated before any art is produced.
3. **Spin:** gentle idle rotation about the Descent up-axis (slower than the original
   ~0.67 rev/s; exact rate tunable, default ≈ 0.15 rev/s).
4. **Materials:** rebind loaded meshes to the game's unlit + per-segment-light material for
   visual consistency (the same path `ModelFactory` uses); a per-model toggle can keep the
   GLB's own PBR materials.
5. **Fallback while art is pending:** keep the **existing `BillboardSprite`**. The
   silhouette-carved base mesh (below) is used only as AI input / for a synthesized top
   view — it is **not** shipped as an in-game placeholder.
6. **Drop-in ergonomics:** GLBs arrive at arbitrary scale/orientation and are normalized
   automatically on load (auto-center + auto-scale to the powerup radius; optional 1-line
   rotation offset). The artist/tool need not match Descent's units.
7. **Scope:** all 22 real powerups (§3), not only weapons ("include everything").

## 5. Architecture

Presentation-only change; the pure Convert/Game layers are untouched, preserving their
`UnityEngine`-free property.

```
ObjectVisuals.Resolve (Convert, unchanged)  ── type-7 powerup → Sprite (honest fallback)
        │
LevelViewer dispatch (Presentation, 2 sites) ── NEW guard:
        │      if powerup AND OverrideModels.Has(id):
        │          instantiate spinning GLB mesh
        │      else:
        │          existing BillboardSprite path
        ▼
OverrideModels (Presentation, NEW) ── discover GLB, glTFast load, auto-fit,
                                       material rebind, cache; + SpinModel component
```

**Hook points** (`unity/Assets/Scripts/Presentation/LevelViewer.cs`):
- `BuildObjects` — static level placement, powerup branch at ~L392–411.
- `CreateObjectView` — dynamic/in-game objects incl. dropped powerups (`vclipNum == -2`
  path) at ~L604–627.

Both already have the powerup id in hand (`obj.SubtypeId` static / `obj.SubId` dynamic), so
the guard is a localized insertion, not a rewrite.

## 6. Components

### 6.1 `OverrideModels.cs` (new, Presentation)
Owns the powerup override system.
- **Discovery:** scan the override dir(s) once at load; build `Dictionary<int powerupId,
  string glbPath>` from the §3 table (filename `powerup_<name>.glb`).
- **Load:** glTFast `GltfImport` from file bytes → instantiate to a `GameObject`
  hierarchy; combine into shared, cached geometry (one load per powerup, reused across
  instances, mirroring `ModelFactory`'s caching).
- **Auto-fit:** compute renderer bounds → recenter to pivot → uniform-scale so max extent ≈
  `2 × Powerup.Size` (the sprite's on-map diameter, per §3). Optional per-model rotation
  offset from an adjacent `powerup_<name>.json` manifest.
- **Material rebind (default):** replace loaded materials with the game's model shader +
  base texture, driven by a `MaterialPropertyBlock` for per-segment light (same as
  `ModelFactory.Instantiate`). Toggle in the manifest to keep source PBR.
- **API:** `bool Has(int powerupId)`, `GameObject TryInstantiate(int powerupId, float
  light)` → returns null on any failure (caller falls back to sprite).
- **Lifetime:** `IDisposable`, owns loaded meshes/materials like `ModelFactory`.

### 6.2 `SpinModel.cs` (new, Presentation)
MonoBehaviour: rotates its transform about the object's up-axis at a configurable rev/s
(default gentle). Added to each override instance. Frame-rate independent
(`Time.deltaTime`).

### 6.3 `LevelViewer.cs` (changed)
- Instantiate `OverrideModels` alongside `modelFactory` (share `baseDxuData`, texture
  factory, model shader).
- Both dispatch sites: `if (isPowerup && overrideModels.TryInstantiate(id, light) is {} go)
  { place + add SpinModel; } else { existing sprite path; }`.
- Dispose with the rest on teardown.

### 6.4 Reference-kit tool (new, editor menu + extends the extractor)
Produces per-powerup AI-generator input into `overrides/refkit/<name>/`:
- `frames.png` — full turntable contact sheet (upscaled, transparent-composited).
- `view_broadside.png`, `view_quarter.png`, `view_muzzle.png` — the clearest single frames,
  upscaled (nearest-neighbor, ~8×). (For orbs/keys the three views are just the most
  representative frames.)
- `base.glb` — silhouette-carved base mesh (shape-from-silhouette over the known turntable
  angles + projected pixel skin). Seeds the generator and synthesizes the top view the
  turntable lacks. **Not shipped in-game.**
- `spec.txt` — pixel dimensions, `Powerup.Size`, dominant palette colors, feature notes,
  and the target-orientation/scale contract from §7.

The carve is intentionally low-fidelity (its job is to seed, not to ship); a coplanar
turntable visual hull captures horizontal cross-sections and height well, with soft
top/bottom and thin features — acceptable for AI seeding.

### 6.5 glTFast dependency
Added to `unity/Packages/manifest.json`. Runtime import only (no editor-time asset baking
required), so the same code path works in editor and player builds. URP material output is
either discarded (default rebind) or kept (PBR toggle).

## 7. Drop-in contract (for the artist / AI tool)

A powerup GLB is valid if:
- It contains one mesh (or a small hierarchy) with an embedded base-color texture.
- Any scale — normalized on load to the powerup radius.
- Any orientation — a `powerup_<name>.json` may specify `rotationEuler` and `scaleMul`
  overrides; default assumes +Y up, +Z forward, which the reference kit documents.
- Pivot anywhere — recentered to bounds center on load.

Filename: `powerup_<base-name>.glb` (names in §3), placed in the override models dir.

## 8. Override directories

Resolution order (first hit wins), all runtime-readable in editor and player:
1. External user mod dir (for modders): `<config>/overrides/models/` (default
   `%LOCALAPPDATA%\D1XUnity\overrides\models\`, and next to the exe / `overrides/models/`).
2. Shipped pack: `StreamingAssets/overrides/models/` (our original content, bundled in the
   build).

Absent dirs = today's behavior exactly (all sprites).

## 9. Error handling & fallback

Every failure mode degrades to the existing sprite, logged once per powerup:
- Override dir missing / file missing → sprite.
- glTFast load throws / unsupported GLB → sprite.
- Empty/degenerate mesh or zero bounds → sprite.
- Manifest parse error → load GLB with defaults.

No failure can break level build or gameplay.

## 10. Verification

- **F0 gate:** glTFast loads a sample GLB at runtime **in-editor and in a built player**
  (headless where possible) — proves the format/dependency before any art exists.
- **`PresentationCheck`** (`unity/tools/PresentationCheck`) compiles the new Presentation
  code against the Unity module DLLs headlessly — keeps the editor-lock verification gap
  closed.
- **Laser end-to-end:** generate the laser GLB → integrate → in-editor eyeball spin, scale,
  and look **side-by-side with the original sprite** (place both; confirm the 3D reads as
  the same cannon at the same on-map size).
- **Fallback proof:** remove a powerup's GLB → its sprite returns, no errors.
- **Regression:** the 7 placeholder slots and all non-powerup objects render unchanged; a
  level with no override dir is pixel-identical to today.

## 11. Milestones

- **F0 — Dependency spike.** Add glTFast; runtime-load a throwaway GLB in editor + player.
  AC: a cube GLB appears in-scene at runtime in a built player.
- **F1 — Pipeline.** `OverrideModels` + `SpinModel` + both dispatch guards + auto-fit +
  material rebind. AC: a throwaway GLB spins in place at correct scale; removing it restores
  the sprite; `PresentationCheck` green.
- **F2 — Laser example.** Reference-kit tool; produce the laser kit; user generates the
  laser GLB; integrate + verify end-to-end vs. sprite.
- **F3 — Roll-out (grouped).** Reference kits for the rest, generated + integrated by group:
  primaries → secondaries/ammo → orbs (energy/shield) → keys → utility (cloak/invuln/
  extra-life). Confirm the full §3 set (decide 4-pack reuse vs. own model).
- **F4 — Modding + polish.** External user mod dir wired; per-powerup spin/scale tuning;
  short `overrides/README` documenting the drop-in contract.

## 12. Risks

| Risk | Mitigation |
|---|---|
| glTFast runtime load fails in player builds | F0 gate validates before art; a minimal built-in GLB reader is the fallback (static mesh + base texture subset) |
| AI models arrive off-scale/off-axis | bounds auto-fit + per-model manifest override; reference kit documents the contract |
| Unlit rebind loses AI detail (normal maps, PBR) | per-model PBR toggle keeps source materials when wanted |
| Carved base mesh too rough to seed the generator | it seeds only; the upscaled sprite views are the primary reference; carve is optional per powerup |
| Keys/orbs lose gameplay legibility as 3D | keep keys colour-coded; override is optional per id; sprite fallback always available |
| 22 powerups is a lot of art | pipeline is id-agnostic and drop-in; roll out by group; any id without a GLB simply stays a sprite |
```
