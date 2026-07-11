# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

DXX-Redux — Descent 1 & 2 source port based on DXX-Retro. Plain C (DOS-era lineage) with CMake.
The repo contains **two sibling ports**: `d1/` (d1x-redux) and `d2/` (d2x-redux). Each is an
independent CMake project — configure and build from inside `d1/` or `d2/`, never from the repo root.

`d1/` and `d2/` are largely parallel copies of the same engine. Fixes and optimizations usually
apply to both, done as a single commit touching the same file in each tree (see e.g. commits to
`d1/arch/ogl/ogl.c` + `d2/arch/ogl/ogl.c`). When changing engine code in one port, check whether
the same code exists in the other and mirror the change unless it's genuinely game-specific.

## Build

Generic (any platform, from `d1/` or `d2/`):

```
cmake -B build -DCMAKE_BUILD_TYPE=RelWithDebInfo
cmake --build build -j4
```

On this machine the toolchain is MSYS2 MinGW64 (gcc/cmake/ninja in `C:/Programs/msys64/mingw64/bin`),
and `d1/build/` is already configured (Ninja, Release). Verified incremental build from Git Bash:

```
PATH=/c/Programs/msys64/mingw64/bin:$PATH cmake --build build -j
```

- `d1/build.sh [Debug|Release]` — full **clean** rebuild (wipes `build/`) plus a `dist/` folder
  with the exe and its MinGW DLLs. Use it for release packaging, not for iteration.
- Visual Studio/MSVC route uses the vcpkg manifest (`vcpkg.json`); requires `VCPKG_ROOT` env var.
- CMake options (see top of `CMakeLists.txt`): `OPENGL`, `SDLMIXER`, `UDP`, `TRACKER`, `PNG`,
  `OPENGLMERGE` default ON; `EDITOR` (level editor + `ui/`), `IPV6`, `ASM` default OFF.

There is no test suite. Verification = both ports still compile + running the game. Running needs
the retail/shareware game data (`descent.hog`/`descent.pig` for d1, not in the repo), located next
to the exe or passed with `-hogdir <folder>`. Executable lands at `build/main/d1x-redux.exe`.

## Architecture

Layout inside each port (each subdirectory is a static library; `main/` builds the executable):

- `main/` — all game logic: `game.c` (loop), `object.c`, `render.c`/`gamerend.c`, `physics.c`,
  `collide.c` (collision responses), `laser.c`/`weapon.c`, `ai.c`, `piggy.c` (PIG/HOG resource
  loading), `mission.c`, `menu.c`/`newmenu.c` (UI), `kconfig.c` (controls), `state.c` (savegames),
  `newdemo.c` (demos). Config split: `config.c` = global settings, `playsave.c` = per-pilot
  files including saved netgame-option defaults.
- `2d/`, `3d/`, `texmap/` — software drawing, 3D transform/clip, software texture mapper.
- `maths/` — fixed-point math. The whole engine uses `fix` (16.16) via `include/maths.h`/`vecmat.h`;
  no floats in game logic.
- `arch/sdl/` — SDL 1.2 platform layer (input, timer, sound); `arch/ogl/` + `xmodel/` — OpenGL
  renderer; `arch/win32|x11|cocoa` — per-OS bits. `d2/libmve/` — movie playback (d2 only).
- File I/O goes through PhysFS (`include/physfsx.h`). Many structs are `__pack__`ed (`pstypes.h`
  forces packing globally — hence `WINDOWS_IGNORE_PACKING_MISMATCH`) and are read/written directly,
  so layout and byte order matter; use the `GET_INTEL_*`/`PUT_INTEL_*` macros (`byteswap.h`).

### Multiplayer (netcode)

- `main/multi.c`/`multi.h` — protocol-level game messages and the `netgame_info` struct;
  `main/net_udp.c` — UDP transport, lobby/game-setup menus, tracker; `main/multibot.c` — robot sync.
- Adding a netgame option follows a fixed pattern (see the AckAckMode / BombFlareTimer commits):
  1. Append a field to the end of `netgame_info` in `multi.h` and **bump `MULTI_PROTO_VERSION`**
     (mismatched versions cannot join each other's games; d1 and d2 have independent values).
  2. Serialize/deserialize it in `net_udp.c` — netgame info is packed field-by-field into a byte
     buffer in `net_udp_send_game_info()` and unpacked in `net_udp_process_game_info()`; both
     sides must stay in sync.
  3. Add the host-side menu entry in the game-setup menus in `net_udp.c`.
  4. Persist the host's default in `playsave.c`.
  5. Implement the gameplay effect (often `collide.c`, `laser.c`, or `multi.c`).
