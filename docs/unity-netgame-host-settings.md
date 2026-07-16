# D1X-Unity — Network Game Host Settings

Ports the original Descent 1 host-configurable netgame options (`netgame_info`,
`net_udp_setup_game` / `net_udp_more_game_options`, persisted in the pilot
`.ngp`) into the Unity remake. The Unity netcode is a bespoke UDP protocol and
is **anarchy-only**, so this ports every *anarchy-applicable* option with a real
gameplay effect; options that only mean something in team/coop/bounty or need
subsystems the port lacks are deferred (listed at the bottom).

**UX:** pressing **HOST** opens a setup dialog (not an instant host). The dialog
edits the settings, **persists them to PlayerPrefs** (survives relaunch), and on
**START HOST** hands the rules to `NetSession.Host`, which ships them to every
client in the `MsgWelcome` handshake. `NetSession.ProtocolVersion` bumps **2→3**
(mismatched clients can't join — the port's analogue of `MULTI_PROTO_VERSION`).

## Architecture
- **`NetGameRules`** (D1U.Game, pure) — the wire+gameplay values, with
  `Serialize`/`Deserialize`. A static `NetGameRules.Active` is what gameplay
  reads; set by the host at launch and by clients from the Welcome packet.
  Everything is gated behind `Multiplayer` so single-player is untouched.
- **`NetGameConfig`** (D1U.Presentation) — PlayerPrefs persistence (`d1u_net_*`)
  + the HOST-setup dialog (IMGUI, virtual-720p like the other settings pages).
- **`NetSession`** — `Host(...)` takes the rules; `SendWelcome`/Welcome-handler
  serialize/deserialize them; kill-goal / match-over checks live here.

## Option coverage (original → Unity)

**Status: every option below is live** — in the dialog, persisted, synced
host→client, and driving gameplay (second increment, 2026-07-14). **defer** =
needs a mode/subsystem not in the port. Where the port's simpler models forced
an approximation, the row says so.

### A · Core match rules
| Original (field) | Unity | Notes |
|---|---|---|
| Game name (`game_name`) | **now** | Shown in host status / join info |
| Difficulty | **now** | already `ObjectSystem.Difficulty`; now in the dialog |
| Reactor Life (`control_invul_time`, 0..10 ×5 min) | **now** | reactor invulnerable for N×5 min ("Reactor invulnerable for M:SS" to the shooter, collide.c:727), destructible after; the kill is announced (`MsgReactor`) so every peer starts the countdown, and the blast ends the match ("THE MINE HAS BEEN DESTROYED"). **0 = indestructible (port default)** — *deviates from D1, where 0 = vulnerable at once; chosen so default MP behaviour is unchanged.* Known limit: a client that joins mid-countdown doesn't see the reactor already dead. |
| Max Time (`PlayTimeAllowed`, 0..10 ×5 min) | **now** | level clock; at limit → match over |
| Kill Goal (`KillGoal`, 0..10 ×10) | **now** | first to N frags wins → match over |
| Level / Game mode | n/a | anarchy only; level chosen on the main menu |

### B · Weapons & items
| Original | Unity | Notes |
|---|---|---|
| Allowed Items (13-bit mask) | **now** | disallowed powerups become shield boosts at level prep (`bash_to_shield`); the prep also ports the rest of `multi_prep_level`: hostages & keys → shield boosts, extra-life → invuln, invuln/cloak capped at 3 |
| Low Vulcan | **now** | strips loose ammo boxes at prep, and a picked-up vulcan gun grants half ammo (98, powerup.c:390) |
| Homing Update Rate (20..30) | **now** | sets the NEWHOMER turn cadence (SP stays 25 Hz); the original's trackability-cone scaling is not applied |
| Extra Primaries / Secondaries (×1..8) | **now** | clones dupable powerups at prep (multi.c:4180); prep is deterministic so clone ids align on every peer and pickups replicate |
| Cap Secondaries (uncapped/6/2) | **now** | caps homing+smart units in the mine, downgrading 4-packs that partially fit (multi.c:4223) |
| Vulcan Ammo Style (Gauss, 4-way) | **now**¹ | steady styles (RECHARGING/RESPAWNING) drop exactly the ammo boxes collected this life on death; ¹DUP and DEPLETE are indistinguishable in the port's fixed-ammo pickup model and both behave as DEPLETE |
| Ack-Ack (vulcan detonates mega) | **now** | a vulcan round touching a mega badass-detonates it (collide.c:1728); contact is a ~3-unit proximity test, not swept collision |
| Bomb-flare Mega timer (5-way) | **now** | a prox bomb younger than the window detonates a mega on contact, taking the bomb with it (collide.c:1738) |

### C · Spawn / respawn
| Original | Unity | Notes |
|---|---|---|
| Spawn Style invuln (none/½s/2s) | **now** | reborn invulnerability on respawn (`Player.InvulnTime`) |
| Spawn Style = Preview | **defer** | needs the dead-cam free-look (marked `*` in the dialog) |
| New Spawn Algorithm | **now** | weighted pick among the half of starts farthest from enemy ships, ≤2× likelihood fudge (gameseq.c:1315); straight-line distances — the original also path-finds |
| Respawn Concussions | **now** | concs pocketed this life re-drop a POW_MISSILE_1 at a random segment when fired (weapon.c:551, laser.c:1523, fireball.c:679); pickup overflow past the 20-cap re-drops immediately |

### E · Access / cosmetic / network
| Original | Unity | Notes |
|---|---|---|
| Max Players (2..8) | **now** | join gate |
| Access: Open / Closed | **now** | Closed = no join after a 45 s grace (Restricted deferred) |
| Network port | **now** | host bind port |
| Bright player ships | **now** | on (default): remote ships stay fullbright; off: they tint with the mine's static+dynamic light like any object |
| Show enemy names | **now** | name tag projected over remote ships |
| Reduced flash | **now** | scales screen/palette flashes |

## Deferred (need a mode or subsystem the port lacks)
Team Anarchy / Cooperative / Robo-Anarchy / Bounty modes and everything they
gate (team names/colors, No-Friendly-Fire, coop robot sync); Observers (max
observers, broadcast delay, minimal-info); Restricted access (host approval);
per-player colours (Fair/Alt/Preferred colours, colored lighting); Packets-per-
second (fixed at the port's 15 Hz); Retro-protocol, Tracker, custom
models/textures, confirmed-hit-sparks. These stay in the dialog only if cheap
and inert; otherwise they're out until their subsystem lands (see the tiered
`unity-feature-parity-plan.md`).

## Persistence
Every dialog value is a PlayerPref (`d1u_net_*`), written on START HOST and on
BACK, loaded in the `NetGameConfig` constructor — the equivalent of the pilot
`.ngp`. Defaults match D1's `netgame_set_defaults()` except Reactor Life (see A).

## Pickup-cap fidelity (2026-07-15 audit)
Every powerup pickup was audited against `do_powerup`/`pick_up_*`: caps
(concs 20, homing 10, prox 10, smart 5, mega 5, vulcan 784, laser L4,
energy/shields 200) all hold and at-cap powerups stay in the world. Six
deviations found and fixed: duplicate/maxed weapons in netgames now **stay**
instead of converting to energy (laser/quad/spread/plasma/fusion); a duplicate
vulcan in MP takes nothing and stays; MP homing overflow re-drops singles at
random segments (weapon.c:537); a spare key of a held colour stays in SP; an SP
duplicate vulcan grabs one box (98) not the gun amount; Low Vulcan halves the
gun-pickup grant. Refusal messages are throttled viewer-side (HM_REDUNDANT
analogue). Smoke §22b asserts all of it.

## Verification
PresentationCheck (compile mirror) + Smoke: §22 asserts the rules propagate
host→client through Welcome and MatchOver fires; §22b exercises the gameplay
effects on a real level (prep conversions/dup/cap, reactor-life gate + kill,
ack-ack, bomb-flare window, respawn concs, steady vulcan drops). Player build
driven by `-d1u-menu-shots`, then deploy. `ProtocolVersion` history: 3 = rules
in Welcome, 4 = MsgReactor.
