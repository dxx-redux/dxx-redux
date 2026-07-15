# D1X-Unity — Feature Parity Review & Implementation Plan

Reviewed 2026-07-13 against the original **Descent 1 / DXX-Redux** C source
(`d1/`) and verified against the current `unity` build (LevelViewer.cs,
GraphicsConfig.cs, NetSession.cs, SoundFactory.cs, ShipSim.cs, ObjectSystem.cs).

Legend: **✅ present** · **🟡 partial** · **❌ missing**

---

## 1. Settings & Options

| Original D1 / Retro setting | Status | Notes on the Unity build |
|---|---|---|
| Display mode / resolution / windowed | ✅ | Settings ▸ Video (borderless/fullscreen/windowed, native list) |
| VSync | ✅ | Video page |
| Framerate cap | ✅ | Video page (Off/30/60/120/144/165/240) |
| MSAA / anti-aliasing | ✅ | Video page (Off/2/4/8×, set on URP asset) |
| Texture filtering (crisp/smooth) | ✅ | Video page (Point/Bilinear, live re-filter) |
| FOV | ✅ | Video page (45–100°) — a Retro/Redux extra, kept |
| Gamma / brightness | 🟡 | Video page "Brightness" 0.5–1.5× drives the light shader. Original gamma is a 0–16 palette LUT; the slider is a fair analogue. |
| Sound FX volume | ❌ | SFX play at fixed per-call gain; **no user control** |
| Music volume | ❌ | Hardcoded `AudioSource.volume = 0.45` |
| Reverse stereo / music type / jukebox | ❌ | Not applicable to the Unity audio path; drop as out-of-scope |
| Reticle type / colour / size | ❌ | Fixed 4-pip green cross, always on |
| FPS counter toggle | ❌ | No on-screen FPS readout |
| "Disable cockpit" / cockpit modes (F3) | ❌ | Only one HUD style (text line). No cockpit/status-bar/full-screen cycle |
| Colored dynamic light | 🟡 | Dynamic lighting is ported but tint is intensity-scaled, not per-source RGB |
| Key rebinding | ✅ | Settings ▸ Controls (subset of actions) |
| Mouse sensitivity / invert | ✅ | Controls page |
| Mouse styles (Rebirth/FlightSim/OldSchool) | ❌ | Single mouse model |
| Joystick config | ❌ | No joystick support |
| Auto-leveling toggle | ❌ | Ship never auto-levels; no toggle |
| Weapon autoselect + ordering | ❌ | Manual 1–5 select only |
| Ship / team colour | ❌ | No pilot colour choice |
| Difficulty (5 levels) | ✅ | Chosen on the main menu (not in a settings page) |
| Pilot system (.plr/.plx) | ❌ | No pilots; prefs are global |

---

## 2. Network Game

The Unity netcode is a **bespoke UDP protocol (v2)**, not the original
`net_udp.c` / `netgame_info` (proto 30008). It is deliberately standalone and
does **not** interoperate with DXX — so the original's wire options don't map
1:1; the gap is about *features*, not the packet format.

| Original netgame feature | Status | Notes |
|---|---|---|
| Host a game / join by IP | ✅ | Host selected mission+level; join by typed IP |
| Anarchy scoring (frags) | ✅ | Kill +1, suicide −1, frag HUD |
| Player position/orientation sync | ✅ | 15 Hz state |
| Fire / death / door / pickup / egg replication | ✅ | Messaged; eggs share net ids |
| **Pilot name entry** | ❌ | Hardcoded `PILOT` / `HOST` |
| Team Anarchy / Robo-Anarchy | ❌ | Anarchy only |
| Cooperative (shared PvE + robot sync) | ❌ | No coop; robots are host-local |
| Bounty | ❌ | — |
| Lobby / game-setup menu (kill goal, time limit, reactor life, difficulty, allowed items, spawn/ammo style, friendly-fire, homing rate, packets/sec, …) | ❌ | No host options at all |
| LAN game browser (list/refresh) | ❌ | Direct IP only |
| Game-info dialog + version check | ❌ | — |
| Observer / JinX spectator | ❌ | — |
| In-game chat (F8) | ❌ | — |
| Kill-list (F7) / kill-matrix scoreboard | ❌ | Frags shown inline on the HUD only |
| Message macros (F9–F12) | ❌ | — |
| Reborn invulnerability window | 🟡 | Respawn exists; brief-invuln unverified |
| Tracker / internet listing | ❌ | Out of scope |

---

## 3. In-Game Features

| Original in-game feature | Status | Notes |
|---|---|---|
| Flight, collision, physics (fixed-point port) | ✅ | ShipSim / ObjectSystem |
| Weapons: lasers 1–4, quad, vulcan+ammo, spread, plasma, fusion charge, concussion, homing, prox, smart, mega | ✅ 🟡 | All selectable; fusion charge & homing present. Fidelity of spread/recoil/self-damage 🟡 |
| Weapon autoselect / weapon-box fade | ❌ | Manual select, no fade |
| Powerups, keys, energy/shield pickups | ✅ | Pickups + key HUD |
| Reactor + self-destruct countdown + whiteout | ✅ | Countdown, ship-rock, mine flash, dead-reactor lighting |
| Robots + AI + matcen | 🟡 | AI and robots present; matcen spawn/sparkle & boss teleport/cloak/gate-in ❌ |
| Hostages (rescue + score) | 🟡 | Counted & scored; blue flash + HUD prompt ❌ |
| Automap | ✅ | Tab wireframe orbit view |
| Save / load (single-player) | ✅ | Pause menu + F5/F9 |
| Per-level music (+ title/briefing/end) | 🟡 | Level-song rotation ✅ (needs an .sf2); briefing/end-tune states ❌ |
| Briefings | ✅ | BriefingView |
| Damage red flash / powerup pickup flashes | ❌ | No palette-flash feedback |
| Cloak / invuln visuals (fade phases) | 🟡 | Timers tracked; special render ❌ |
| Player-death external orbit cam + tumble + debris | ❌ | 2.5 s timer + "SHIP DESTROYED" text |
| Endlevel EXTERNAL ESCAPE sequence (flythrough/planet) | ❌ | Instant "LEVEL COMPLETE" banner |
| Glitz score/bonus screen | ❌ | Inline score tally only |
| High-score table | ❌ | — |
| Demo record / playback | ❌ | — |
| Rear-view mirror PiP (RearMirror) | ✅ | Settings ▸ Game; R toggles; flipped rear camera, sizes/positions (2026-07-15) |
| Live HUD minimap PiP (HudMinimap) | ✅ | Settings ▸ Game; F4 toggles; minimap.c port — auto-leveled tilted top-down wireframe, hop ranges, heading/north-up, opacity (2026-07-15) |
| Cheats (16) | ❌ | — |
| Screenshots (user) | 🟡 | Autopilot capture only; no user key |
| Pause | ✅ | Esc pause menu |

---

## 4. Implementation Plan

Ordered by **player impact ÷ effort**. Effort: **S** ≈ hours · **M** ≈ a day ·
**L** ≈ multi-day. Every tier ends green on PresentationCheck + Smoke + an
autopilot player-build screenshot pass before deploy.

### Tier 0 — Settings-menu completeness *(in progress this pass)*
Surface every toggle-class option the engine can already back, and add the
cheap missing backers. New `GameConfig` (PlayerPrefs `d1u_*`), two new settings
pages, reachable from both the main menu and the pause menu.

- **Audio page** — Master / SFX / Music volume sliders. **S.**
  Backers: `AudioListener.volume`, a static gain in `SoundFactory.PlayAt`, a
  `Volume` property on `MusicPlayer`.
- **Game page** — Difficulty (moved/mirrored here), FPS counter on/off,
  Reticle on/off, Auto-level on/off, Invert-Y (mirror of Controls). **S–M.**
- **HUD backers** — smoothed FPS readout; gate the reticle draw on the toggle. **S.**
- Restructure the four settings entries (Video/Audio/Controls/Game) into a
  compact tab row on both menus. **S.**

### Tier 1 — High-impact gameplay/UX parity
- **Weapon autoselect** (primary/secondary priority order + auto-switch on
  pickup/empty) + weapon-box swap feedback. **M.** *(playsave.c autoselect tables)*
- **Damage & pickup palette flashes** (red on hit, tint on pickup, cloak
  darken). **S–M.** *(gauges.c / powerup.c flash_effect)*
- **Reticle styles** (classic/cross/angle/dot + size + colour) wired to the new
  Game page. **S.**
- **Hostage feedback** (blue screen flash + rescue sound + HUD line). **S.**
- **Cockpit / HUD modes** (F3 cycle: status-bar / full-screen / minimal). **M**
  (no cockpit *art* — do the layout modes only).

### Tier 2 — Presentation & polish
- **Player-death sequence** — external orbit cam + tumbling ship + submodel
  debris + drop, then respawn. **M–L.** *(gameseq.c dead_player, object.c debris)*
- **Endlevel score/glitz screen** — bonus math (shields/energy/hostages/
  skill), tally roll, then advance. **M.** *(gamemine/endlevel bonus)*
- **Cloak/invuln render** — fade-in/out phases for cloaked ships & robots. **M.**
- **Matcen** spawn effect + **boss** teleport/cloak/gate-in. **M–L.**
- **High-score table** (local `descent.hi` analogue). **S–M.**

### Tier 3 — Netgame depth
- **Pilot name entry** + persisted name (feeds MP + a light pilot record). **S.**
- **Host lobby / setup** page: difficulty, kill goal, time limit, reactor life,
  allowed items, friendly-fire, spawn/ammo style — serialized into the join
  handshake. **M–L.**
- **LAN browser** (broadcast discovery + list). **M.**
- **Team Anarchy** then **Cooperative** (needs robot ownership sync like
  multibot). **L.**
- **Chat (F8)** + **kill-matrix (F7)** scoreboard. **M.**
- **Observer** mode. **M.**

### Tier 4 — Large / optional
- **Demo record & playback.** **L.**
- ~~Rear-view~~ *(done 2026-07-15 as the mirror PiP)*, **user screenshots**, **cheat console.** **S–M.**
- **Full pilot system** (.plr-style records, per-pilot binds/colours). **L.**
- Endlevel **flythrough/escape cinematic.** **L.**

### Non-goals (intentionally dropped)
Reverse-stereo, jukebox/CD music, joystick config (unless requested), D2
content, tracker/internet listing, DXX wire-compatibility.

---

*Verification for every item: `dotnet run PresentationCheck` (compile mirror) +
`dotnet run Smoke` (23-section) + a batch player build driven by the in-process
autopilot flags (`-d1u-auto`, `-d1u-menu-shots`, `-d1u-boom`). Never editor-only,
never OS-level input injection.*
