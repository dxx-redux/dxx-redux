# D1X-Unity — Descent 1 port to Unity: implementation plan

Status: v1 + progress log (2026-07-12) · branch `unity`

## Progress (updated 2026-07-12, evening)

- **M0–M4 complete**: project scaffold, base/mission DXU rebuild caches with
  auto-invalidation, level rendering with merged overlays + animated
  textures, faithful flight physics/FVI, doors/keys/triggers/matcens/
  fuel-centers, level objects rendered. First in-editor play session
  confirmed by the user: "looks very like original".
- **M5 complete in essentials**: all five primaries (lasers/vulcan/
  spreadfire/plasma/fusion) and five secondaries (concussion/homing/prox/
  smart/mega) with selection keys, robot AI (sight, chase, per-difficulty
  firing with jitter and bursts, claw contact, out-of-sight BFS
  pathfinding through openable doors, still robots chase once shot),
  player death/respawn, matcen spawning, boss teleport +
  self-destruct-on-death, cloak/invuln, scoring (robots/reactor 5000/
  hostage 1000), sounds, explosions, robot pose animation on true
  submodel hierarchies, exact powerup amounts (3+3*(NDL-diff), maxed
  pickups fall back to energy), faithful death drops (robot_info rolls,
  replace-with-energy, bounce physics, timed despawn).
- **M6 partially**: mission-select menu (Esc), level progression with
  weapon carry-over, secret-level routing (Secret_level_table, builtin
  10/21/24 + .msn num_secrets; return to table[n]+1), Tab automap
  (mapedges.c port: visited wireframe, wall/door/key colors, frontier
  hiding, orbit camera), briefings (titles.c port: TXB text, the
  retail screen table, PCX backgrounds, font3-1 typewriter at
  20 ms/char, $-commands incl. the spinning robot window; before every
  level and the endreg.txb ending after the last), music through
  MeltySynth + user-supplied .sf2.
- **Savegames + difficulty**: F5 quicksave / F9 quickload / menu LOAD —
  a binary snapshot of weapons, wall/trigger/door state, reactor,
  player, every object (incl. in-flight weapons and AI state), matcens
  and the RNG seed; restores across missions and levels. The menu
  cycles Trainee..Insane (persisted, recorded in saves); all
  per-difficulty tables read through it.
- **Death and escape**: dying spills the whole loadout as re-collectable
  powerups (drop_player_eggs) and respawns a bare ship; destroying the
  reactor starts the real self-destruct countdown (50..30 s by
  difficulty, voice callouts, siren, T-n readout, whiteout) — escape or
  the mine blows and the level restarts.
- **D1 completion set**: flares (F, stick to walls), blob/vclip weapon
  bolt rendering, lives (3 ships, +1 per 50k, score banks across
  levels) with GAME OVER, savegame v3.
- **Multiplayer (anarchy)**: NetSession (single-threaded UDP 28342,
  host-relay star, mid-game join via WELCOME with mission/level/
  difficulty/roster, 15 Hz states, fire/door/pickup replication,
  victim-authoritative damage, frag scoreboard, timeouts). Menu
  HOST/JOIN by IP; remote ships rendered as interpolated player-ship
  models; robots/exits stripped per anarchy rules; infinite ships with
  random-start respawns. Verified with a real loopback game in Smoke.
- **Build**: D1U/Build Windows Player (also batchmode) →
  `unity/Builds/D1X-Unity/d1x-unity.exe`; HOGs are found next to the
  exe, in an adjacent `hogs/`, or in the repo's `d1/hogs`.
- **Headless verification**: `unity/tools/Smoke` (22 sections, SMOKE OK)
  covers parsing→physics→combat→drops→pathfinding→briefing text→
  savegame roundtrip→death drops→countdown→netgame loopback;
  `unity/tools/PresentationCheck` compiles the Unity-side Presentation
  assembly against the engine module DLLs.
- Scope note: D2 support is explicitly out of scope (user decision,
  2026-07-12). Remaining polish: homing retrack cadence details,
  powerup respawn option for long anarchy games, net egg drops.
Scope: single-player Descent 1 in Unity, fan build (non-commercial, user-supplied game data — same posture as dxx-redux).

## 1. Goals and ground rules

1. Faithful port of D1 gameplay (flight feel, weapons, AI, level logic) to Unity/C#.
2. **No original assets in the repo or build.** The game reads the user's data directory
   (default: `d1/hogs`, configurable) exactly like dxx-redux does.
3. **Auto-load legacy content**: on startup the game discovers `DESCENT.HOG`/`DESCENT.PIG`
   and any add-on missions (`*.msn` + `*.hog`) in the hogs directory, and **rebuilds** them
   into our own Unity-friendly format (DXU) in a cache. Rebuild is automatic, incremental
   (hash-based), and works both in the Unity editor and in a shipped player build.
4. Parser layer = **LibDescent** (github.com/InsanityBringer/LibDescent, MIT,
   `netstandard2.0` — verified Unity-compatible, no UnityEditor dependency).
5. The C engine in `d1/` is the behavioral reference. Gameplay constants are transcribed
   from it, not re-tuned (file:line references kept in code comments where feel-critical).

## 2. Source data inventory (what must load)

`C:\Users\Yermak\Projects\dxx-redux\d1\hogs`:

| File | Size | Role |
|---|---|---|
| DESCENT.HOG | 6,856,701 | retail v1.4 — 30 `.rdl` levels (First Strike), palette.256, 26 PCX screens, 35 TXB briefings, 27 HMP songs, 5 fonts, endlevel BBMs |
| DESCENT.PIG | 4,920,305 | retail v1.4 — ~1750 textures, ~250 sounds, all game tables (robots/weapons/powerups/ship), 85 polygon models (embedded gamedata at offset 4) |
| achtung.msn + achtung.hog | 315 + 34,939 | add-on mission |
| bigrat.msn + bigrat.hog | 291 + 42,285 | add-on mission |
| CHAOS.MSN + CHAOS.HOG | 307 + 174,751 | add-on mission (multi-level) |

Acceptance for "port other maps": all three add-on missions selectable and playable through
the auto-load pipeline, with zero per-mission manual work. Any new `.msn`+`.hog` dropped in
the folder must appear on rescan.

## 3. Architecture — three layers

```
┌────────────────────────────────────────────────────────────┐
│ 3. Unity presentation  (Assets/Scripts/Presentation)       │
│    rendering, audio, input, UI/HUD, VFX                    │
├────────────────────────────────────────────────────────────┤
│ 2. Game core  (Assets/Scripts/Game)  — pure C#, no UnityE. │
│    segment graph, physics/FVI, objects, AI, weapons,       │
│    walls/triggers/matcens, level flow                      │
├────────────────────────────────────────────────────────────┤
│ 1. Converter "Rebuilder" (Assets/Scripts/Convert + plugin) │
│    LibDescent.Data (vendored) → DXU cache                  │
│    runs in editor AND in player (netstandard2.0 only)      │
└────────────────────────────────────────────────────────────┘
```

Hard rule: layers 1–2 reference only `System.*` + LibDescent + a thin math shim —
no `UnityEngine` types except in explicitly-marked bridge files. This keeps the converter
runnable in the player, makes the game core unit-testable with plain NUnit/dotnet, and
leaves the door open for headless tools.

## 4. DXU — our native format

Two artifact kinds, produced by the Rebuilder, stored in the cache directory
(`%LOCALAPPDATA%\D1XUnity\cache\`, configurable):

**`base.dxu`** — built from DESCENT.PIG + DESCENT.HOG core files:
- Wall-texture array: all 64×64 wall bitmaps decoded (RLE→indexed→RGBA32), stored as raw
  slices for a `Texture2DArray`; per-texture flags (transparent/supertransparent/animated).
- Object-texture set (model skins), gauge/HUD bitmaps, fonts (optional, later).
- Models: interpreter bytecode pre-baked to triangle meshes — positions (fix→float),
  per-face UVs + texture slice, face normals, submesh per texture; submodel tree
  (parents + pivot offsets), 5 named poses (rest/alert/fire/recoil/flinch) as Euler
  keyframes, gun points, LOD chain, dying/dead variants. (LibDescent `PolymodelExtractor`
  + `ModelOpCode` do the walking; we bake, never interpret at runtime.)
- Tables: robots, weapons, powerups, player ship, vclips/eclips/wclips, TmapInfo — as
  plain serializable records (these drive gameplay data; also exported as JSON next to the
  cache for debugging/modding inspection).
- Sounds: 250 clips converted u8/11025 → 16-bit PCM chunks.
- Music: HMP → standard MIDI (LibDescent `Midi`), plus pre-rendered WAV/OGG via MeltySynth
  (MIT, pure C#) + a bundled GM SoundFont at rebuild time — runtime just plays audio clips.
  A `music/` override folder allows a jukebox (user OGGs) like Rebirth.
- Palette + fade tables (for palette-faithful effects/flash).

**`<mission>.dxu`** — one per mission (built-in First Strike = the descent.hog levels;
add-ons = their hog), per level:
- Static mine mesh: pre-triangulated (quad/tri sides per the C rules in
  `gameseg.c:create_walls_on_side`), vertex streams = position, uv0, texture-slice +
  overlay-slice + overlay-rotation (encoded in extra UV/color channels),
  per-vertex baked light (from `uvl.l`) as Color32.
- Door/wall faces as separate small mesh pieces (they animate/hide independently).
- Segment graph: vertices, per-segment children[6], side planes/types, wall links,
  special (fuelcen/matcen/reactor), static_light — this is the collision + AI world model.
- Entities: walls (type/flags/clip/keys/trigger), triggers (+control-center trigger),
  matcens, object placements (robots/powerups/hostages/reactor/player+coop starts).
- Endlevel data (exit tunnel target, terrain/satellite params) — used later (M7).

Container: `DXU1` magic, versioned chunk list (id, length, deflate-compressed payload),
one JSON manifest chunk for humans. **Cache key = SHA-256 of source file(s) + converter
version**; mismatch ⇒ silent rebuild with a progress screen (base ≈ seconds, mission ≪ s).

## 5. Auto-load pipeline (runtime flow)

1. Boot → resolve hogs dir (player config JSON / editor settings asset; default `d1/hogs`).
2. Verify `DESCENT.HOG`+`DESCENT.PIG` (size-based version check like `piggy.c`); missing ⇒
   friendly setup screen asking for the folder.
3. Ensure `base.dxu` fresh, else rebuild (LibDescent `Descent1PIGFile`, `HOGFile`).
4. Scan for missions: built-in First Strike + every `*.msn` (LibDescent `MissionFile`,
   semantics per `d1/main/mission.c:250` `read_mission_file`).
5. Mission select UI lists them; picking one ensures `<mission>.dxu` fresh (rebuild on
   demand), then loads level 1.
6. "Rescan" available from the menu — drop-in hogs appear without restart.

## 6. Unity project setup

- **Unity 6 LTS** (not yet installed on this machine — install Unity Hub + 6000.x LTS),
  URP, Input System package. Project lives at repo root: **`unity/`** (sibling of `d1/`,
  `d2/`), product name "D1X-Unity".
- LibDescent vendored as source: `unity/Assets/Plugins/LibDescent.Data/` + asmdef
  (MIT notice preserved). `System.Numerics` comes with Unity's .NET profile.
- Assembly layout (asmdefs): `LibDescent.Data` ← `D1U.Convert` ← `D1U.Game` ←
  `D1U.Presentation`; editor-only tooling in `D1U.Editor` (import inspectors, model/level
  preview windows).
- A parallel plain-.NET solution (`unity/tools/D1U.sln`) referencing the same Convert/Game
  sources for fast NUnit tests against the real `d1/hogs` data (no Unity needed) — this is
  also the pre-M0 de-risk path while Unity installs.

## 7. Rendering design

- **Texture2DArray, not an atlas**, for wall textures: Descent faces tile UVs beyond 0..1,
  which atlases can't wrap; a 64×64 array preserves tiling, mips cleanly, and keeps UVs
  untouched. Per-face slice index rides in a vertex channel.
- One custom shader (URP, unlit-style) for the mine: base slice sample × vertex light,
  optional overlay slice with 2-bit rotation (UV transforms per `arch/ogl/ogl.c:876-891`),
  alpha discard for transparent (255) and supertransparent (254) texels — baked into the
  slice alpha at convert time.
- **Animated textures via an indirection buffer**: faces store a *texture id*; a small
  per-frame-updated lookup (structured buffer / 1D texture) maps id → current slice.
  This reproduces the original `Textures[]` indirection, so eclips (glow panels) and
  wclips (door frames) animate by updating one table — no mesh or material churn.
- Lighting: baked per-vertex light from the level file. Reactor-destroyed flash and
  weapon/explosion dynamic light emulated first as a global/vertex modulate
  (`render.c:239-283` semantics), Unity point lights only as optional flair later.
- Models: prefab per model — GameObject per submodel at its pivot, submesh per texture,
  pose lerp at the AI's constant rate (no Animator; matches `ai_frame_animation`).
- View: cockpit-less first (HUD overlay), letterbox/cockpit art in M7.

### 7.1 Content override layer (HD models / textures — designed in from M1)

Every renderable has a stable content id: models by slot name (pyro, reactor, robot type
ids — cf. `d1/xmodel/xmodelnames.h`), textures/sounds by their PIG name. The presentation
layer resolves id → asset through an **override chain**: hand-authored Unity asset
(Addressables/`overrides/` mod folder) → DXU-generated default. Consequences:

- A detailed FBX/glTF ship or robot (PBR materials, LODs, rigged) replaces the generated
  prefab by name, no code change. Precedent: the C engine's `xmodel/` does exactly this
  with D2X-XL .ase models; ours generalizes it to any Unity-importable asset.
- Gameplay is unaffected by visual swaps: collision is sphere-radius + segment world (the
  hitbox never comes from the render mesh); gun points/animation fall back to table data,
  or the override prefab provides its own muzzle markers and Animator (map the 5 AI pose
  states to animation states).
- HD wall textures: the id→slice indirection points at a higher-resolution
  Texture2DArray (all slices one size, e.g. 512×512 — standard texture-pack approach);
  parallel arrays add normal/emission maps for the custom mine shader. Animated textures
  keep working because animation happens at the indirection table, not in materials.
- Override packs are original content and can ship with the build; only the base game
  data stays user-supplied.

## 8. Game core port map (C → C#)

| C source (LOC) | C# module | Phase |
|---|---|---|
| maths/vecmat (fix→float shim, named ops) | `D1U.Game.Math` | M3 |
| gameseg.c (1,863) segment queries | `World.SegmentGraph` | M2–M3 |
| physics.c (1,119) + controls.c | `Sim.ShipPhysics` | M3 |
| fvi.c (1,296) swept-sphere vs segments | `Sim.Fvi` | M3 |
| collide.c (1,998) response matrix | `Sim.Collide` | M4–M5 |
| object.c (2,308) object model + frame loop | `Sim.ObjectSystem` | M3 |
| wall.c/switch.c/fuelcen.c/cntrlcen.c (~2,800) | `World.Interactive` | M4 |
| laser.c/weapon.c/fireball.c (~4,000) | `Combat.Weapons` | M5 |
| ai.c/aipath.c (4,616) | `Combat.AI` | M5 |
| powerup.c/hostage.c/morph.c (~1,100) | `Sim.Pickups`, `FX.Morph` | M4–M5 |
| gameseq.c/game.c flow (~3,000) | `Flow.GameSequence` | M6 |
| endlevel.c (1,533) | `Flow.EndLevel` (simplified) | M7 |
| multi/net (15.7k) + newdemo (3.6k) | — deferred | M8 |

Non-negotiables transcribed exactly (with source refs): 64 Hz thrust/drag sub-stepping
(`physics.c:191,498-546`), wall-slide/bounce rules and the ±1.0 wall_part saturation,
post-hit velocity rewrite, fvi radius fudges and transparent-texel pass-through,
homing 25 Hz tick accumulator, difficulty-indexed robot/weapon tables, awareness events,
boss cloak/teleport timers. Physics is **kinematic C# over the segment graph — not PhysX**;
float replaces fix (safe: demos/MP don't require bit-exactness), 64-bit seconds for timers,
`d_rand` LCG kept verbatim.

### 8.1 Physics backend abstraction (PhysX-swappable later)

The game core never calls collision math directly; it goes through an interface
(`ICollisionWorld`): `Sweep(pos, dir, radius, flags)` → hit (point/normal/wall/object/
segment path), `Raycast` (AI visibility, FQ_TRANSWALL semantics), `Contains(point)` →
segment. Object transforms are authoritative in the game core; Unity GameObjects only
mirror them. That means:

- Default backend = the FVI port (feel-faithful, deterministic).
- A later `PhysXCollisionWorld` can be dropped in: MeshColliders generated per level from
  the DXU mine mesh, sphere sweeps/casts via `Physics.SphereCast`, door faces as toggleable
  colliders, triggers as volumes. Feasible without touching gameplay code.
- What a full PhysX swap must still reimplement as glue (why it's not free): transparent-
  texel weapon pass-through (texture query at hit UV), fly-through flags changing as doors
  open, the traversed-segment list used by side-crossing triggers, Descent's specific
  slide/bounce response quirks, out-of-mine recovery. And PhysX is not deterministic
  across machines/versions — fine for SP, a liability if M8 multiplayer/demos come.
- Pragmatic hybrid from day one: PhysX for cosmetic-only physics (debris tumbling, gibs,
  particle collisions) where feel-fidelity doesn't matter; custom FVI for ship, robots,
  and weapons where it does.

## 9. Audio / input / UI

- SFX: AudioClips from base.dxu PCM; positional via AudioSource with the original
  linked-to-object semantics (`digi_link_sound_to_pos/object` call sites define what loops).
- Music: pre-rendered at rebuild (see §4); per-level song table from `descent.sng`.
- Input: Unity Input System; two default profiles — classic D1 keys and modern
  mouse-flight; full rebinding UI in M6.
- UI: UGUI. M2 dev fly-cam HUD → M4 shields/energy/keys → M6 menus (mission select from
  auto-discovery, difficulty, pilot profile, save/load) → M7 cockpit art, automap.

## 10. Milestones and acceptance criteria

- **M0 Scaffold** — install Unity 6 LTS; create `unity/` URP project; vendor LibDescent +
  asmdefs; tools solution runs a smoke test that lists DESCENT.HOG lumps and parses all
  33 RDLs (30 built-in + add-ons) without exceptions.
- **M1 Base import** — base.dxu end-to-end: texture array pages, tables (+JSON dump),
  model meshes with hierarchy/poses, sounds, music. AC: editor "asset browser" window
  shows any texture/model (textured, posed) and plays any sound/song.
- **M2 Levels + auto-load** — mission discovery, `<mission>.dxu` build, mine mesh render.
  AC: fly-cam through `level01` and `CHAOS` L1 fully textured with baked light + working
  animated textures; hash-invalidation proven (touch a hog ⇒ auto rebuild).
- **M3 Flight** — ship physics/FVI/collide-vs-world; doors open on approach (locks off).
  AC: side-by-side feel check vs `d1x-redux.exe` (same accel curves, wall slide, wiggle);
  traverse all of level01 through doors.
- **M4 Interactivity** — keys/switches/triggers, blastable/illusion walls, matcens,
  fuelcen energy, hostages, powerup pickup, reactor + countdown + self-destruct.
  AC: level01 completable start→reactor→exit (simple fade-out endlevel).
- **M5 Combat** — player weapons incl. quad/fusion/homing quirks, robots with AI + anim +
  robot weapons, damage/explosions/drops, bosses. AC: First Strike L1–L7 clearable;
  weapon/robot numbers spot-checked against table JSON.
- **M6 Game flow** — menus, mission select (incl. achtung/bigrat/CHAOS), difficulty,
  scoring/extra lives, briefings (TXB), save/load, full audio. AC: full First Strike
  campaign + all three add-on missions playable start to finish via auto-load.
- **M7 Parity polish** — endlevel exterior flyout, cockpit/automap, dynamic light feel,
  cloak/invuln VFX, secret levels, config UI.
- **M8 Deferred** — multiplayer (position-sync architecture maps to Unity netcode),
  demo playback, D2 support (LibDescent already parses D2 — architecture stays open).

## 11. Risks and mitigations

| Risk | Mitigation |
|---|---|
| LibDescent gaps vs retail PIG edge cases | `d1/` C code is the reference; vendored source is patchable; NUnit tests run against the real hogs |
| Feel drift (float, frame timing) | transcribe constants with refs; side-by-side redux comparisons per milestone; keep FrameTime semantics + 64 Hz substep |
| Texture tiling vs modern batching | Texture2DArray (wrap-safe) + indirection buffer for animation |
| Robot anim/pose edge cases (turrets, boss) | model viewer in M1 exposes all poses early |
| Scope creep in UI/menus | menus are deliberately minimal until M6 |
| Unity editor not installed / version churn | pure-.NET tools solution keeps converter+core development unblocked |

## 12. Immediate next steps (M0)

1. Install Unity Hub + Unity 6 LTS (manual step).
2. `unity/tools/` .NET solution: vendor LibDescent.Data, write the smoke test against
   `d1/hogs` (HOG lump list, 33 RDL parses, PIG open + table counts).
3. Commit plan + scaffold on branch `unity`.
