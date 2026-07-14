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

Effort: **now** = live gameplay effect in this feature · **sync**/**B2** = in the
dialog + persisted + synced host→client now, gameplay effect is a follow-up
(shown with a `*` in the dialog) · **defer** = needs a mode/subsystem not in the
port. Every option — now, sync, and B2 alike — is persisted and travels to
clients; the split is only about whether the *effect* is wired yet.

### A · Core match rules
| Original (field) | Unity | Notes |
|---|---|---|
| Game name (`game_name`) | **now** | Shown in host status / join info |
| Difficulty | **now** | already `ObjectSystem.Difficulty`; now in the dialog |
| Reactor Life (`control_invul_time`, 0..10 ×5 min) | **sync** | in the dialog + persisted + synced. Effect deferred: the port's MP reactor is indestructible, and making it destructible + ending the match on its death needs cross-peer reactor sync (a follow-up). **0 = indestructible** in the port (default); >0 will mean "destructible after N×5 min" once that lands. |
| Max Time (`PlayTimeAllowed`, 0..10 ×5 min) | **now** | level clock; at limit → match over |
| Kill Goal (`KillGoal`, 0..10 ×10) | **now** | first to N frags wins → match over |
| Level / Game mode | n/a | anarchy only; level chosen on the main menu |

### B · Weapons & items
| Original | Unity | Notes |
|---|---|---|
| Allowed Items (13-bit mask) | **now** | disallowed powerups become shield boosts at level prep (`bash_to_shield`) |
| Low Vulcan | **now** | halves vulcan powerup ammo, strips ammo boxes |
| Homing Update Rate (20..30) | **now** | scales homing tracking rate |
| Extra Primaries / Secondaries (×1..8) | **B2** | clone dupable powerups at prep |
| Cap Secondaries (uncapped/6/2) | **B2** | cap homing+smart counts in the mine |
| Vulcan Ammo Style (Gauss, 4-way) | **B2** | death-drop/regen policy; port has a simpler vulcan model |
| Ack-Ack (vulcan detonates mega) | **B2** | needs weapon-vs-weapon collision |
| Bomb-flare Mega timer (5-way) | **B2** | needs weapon-vs-weapon collision |

### C · Spawn / respawn
| Original | Unity | Notes |
|---|---|---|
| Spawn Style invuln (none/½s/2s) | **now** | reborn invulnerability on respawn (`Player.InvulnTime`) |
| Spawn Style = Preview | **defer** | needs the dead-cam free-look |
| New Spawn Algorithm | **now** | farthest-from-others spawn pick |
| Respawn Concussions | **now** | infinite concs (re-drop on fire) |

### E · Access / cosmetic / network
| Original | Unity | Notes |
|---|---|---|
| Max Players (2..8) | **now** | join gate |
| Access: Open / Closed | **now** | Closed = no join after start (Restricted deferred) |
| Network port | **now** | host bind port |
| Bright player ships | **now** | remote ships rendered fullbright |
| Show enemy names | **now** | name tag over remote ships |
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

## Verification
PresentationCheck (compile mirror) + Smoke (its loopback netgame section is
extended to assert the rules propagate host→client through Welcome) + a player
build driven by `-d1u-menu-shots` to eyeball the dialog, then deploy.
