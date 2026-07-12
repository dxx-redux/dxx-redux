# HUD Minimap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A live, transparent wireframe proximity minimap rendered picture-in-picture during d1 gameplay, with auto-leveled camera, configurable position/size/range/rotation/opacity, F4 toggle, and ship markers for all players.

**Architecture:** Extract the automap's edge-list builder into a shared module (`mapedges.c`); a new `minimap.c` builds its own whole-level edge list once per level, BFS-depth-limits it around the ship, and draws it with the g3 pipeline into a mirror-style sub-canvas of `Screen_3d_window` each frame — without clearing the canvas, so the game shows through. One engine change: OpenGL `g3_draw_line` learns to honor the canvas fade level for real line alpha.

**Tech Stack:** Plain C, fixed-point math (`fix`, 16.16), CMake + MSYS2 MinGW64 (Ninja), SDL 1.2 + OpenGL.

**Spec:** `docs/superpowers/specs/2026-07-11-hud-minimap-design.md` (approved).

## Global Constraints

- d1 only. Do **not** touch `d2/` in this plan (the port is an explicit follow-up).
- Fixed-point math only in game logic; floats are allowed only inside `arch/ogl/` code.
- Never write to `Automap_visited` (live map-reveal state, savegame-persisted) — the minimap uses its own `Minimap_depth[]` buffer.
- No changes to `netgame_info`, `MULTI_PROTO_VERSION`, demo format, or savegame format.
- Match surrounding code style: tabs for indentation, declarations at the top of blocks (MSVC/C89-friendly), sparse comments in the existing voice.
- There is no test suite. Every task's verify step = incremental build + (where stated) a manual game check. Build command (run from the worktree's `d1/`):

  ```bash
  cd /c/Users/Yermak/Projects/dxx-redux/.claude/worktrees/hud-minimap/d1
  PATH=/c/Programs/msys64/mingw64/bin:$PATH cmake --build build -j
  ```

  Expected last line: `[N/N] Linking CXX executable main\d1x-redux.exe` (exit 0). The `build/` dir is already configured (Ninja, RelWithDebInfo). Note: this machine's environment exports a broken `CC` (`C:\msys64\ucrt64\bin\gcc.exe`); incremental `cmake --build` ignores it, but if you ever need to RE-configure, prefix `CC=/c/Programs/msys64/mingw64/bin/gcc.exe CXX=/c/Programs/msys64/mingw64/bin/g++.exe`.
- Manual game run (needs game data; `d1/hogs/` in this repo has it):

  ```bash
  cd /c/Users/Yermak/Projects/dxx-redux/.claude/worktrees/hud-minimap/d1
  ./build/main/d1x-redux.exe -hogdir hogs
  ```
- Commit after every task, message style: short imperative subject + brief body, ending with `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.

---

### Task 1: Extract shared edge-list machinery into `mapedges.c`

The automap's edge-list code is static inside `automap.c` and tied to the `automap` struct. Move it to a new module parameterized on a `map_edge_list` struct so the minimap can build its own list. The Tab automap must behave identically afterwards.

**Files:**
- Create: `d1/main/mapedges.h`
- Create: `d1/main/mapedges.c`
- Modify: `d1/main/automap.c` (delete moved code; rename field accesses)
- Modify: `d1/main/automap.h` (export `draw_player`)
- Modify: `d1/main/CMakeLists.txt` (add `mapedges.c`)

**Interfaces:**
- Consumes: existing globals (`Segments`, `Highest_segment_index`, `Walls`, `Automap_visited`, `cheats`, `Players`, `Player_num`, `Player_init`).
- Produces (used by Task 4):
  - `typedef struct Edge_info { int verts[2]; ubyte sides[4]; int segnum[4]; ubyte flags; ubyte color; ubyte num_faces; } Edge_info;`
  - `typedef struct map_edge_list { int num_edges; int max_edges; int highest_edge_index; Edge_info *edges; int wall_normal_color, wall_door_color, wall_door_blue, wall_door_gold, wall_door_red; } map_edge_list;`
  - `void map_edge_list_build(map_edge_list *el, int mode);` with modes `MAP_EDGE_BUILD_AUTOMAP` / `MAP_EDGE_BUILD_ALL`
  - `EF_USED`, `EF_DEFINING`, `EF_FRONTIER`, `EF_SECRET`, `EF_GRATE`, `EF_NO_FADE`, `EF_TOO_FAR` flag macros
  - `void draw_player(struct object *obj);` (exported via `automap.h`; definition stays in `automap.c`)

- [ ] **Step 1: Create `d1/main/mapedges.h`**

```c
/*
 * Shared wireframe edge-list machinery for the automap and the HUD minimap.
 * Extracted from automap.c; the algorithm is unchanged.
 */

#ifndef _MAPEDGES_H
#define _MAPEDGES_H

#include "pstypes.h"

#define EF_USED     1   // This edge is used
#define EF_DEFINING 2   // A structure defining edge that should always draw.
#define EF_FRONTIER 4   // An edge between the known and the unknown.
#define EF_SECRET   8   // An edge that is part of a secret wall.
#define EF_GRATE    16  // A grate... draw it all the time.
#define EF_NO_FADE  32  // An edge that doesn't fade with distance
#define EF_TOO_FAR  64  // An edge that is too far away

typedef struct Edge_info {
	int   verts[2];     // 8  bytes
	ubyte sides[4];     // 4  bytes
	int   segnum[4];    // 16 bytes  // This might not need to be stored... If you can access the normals of a side.
	ubyte flags;        // 1  bytes  // See the EF_??? defines above.
	ubyte color;        // 1  bytes
	ubyte num_faces;    // 1  bytes  // 31 bytes...
} Edge_info;

typedef struct map_edge_list {
	int       num_edges;
	int       max_edges;
	int       highest_edge_index;
	Edge_info *edges;
	// Wall colors used while building; refreshed by map_edge_list_build()
	// (palette-dependent, so they can't be compile-time constants).
	int       wall_normal_color;
	int       wall_door_color;
	int       wall_door_blue;
	int       wall_door_gold;
	int       wall_door_red;
} map_edge_list;

#define MAP_EDGE_BUILD_AUTOMAP 0  // automap rules: visited segments drawn, unvisited as frontier, map-powerup tint, cheats
#define MAP_EDGE_BUILD_ALL     1  // every segment drawn (live minimap); no visited/frontier logic

// (Re)build the edge list. The caller owns el->edges (allocate Num_segments*12
// entries) and must set el->edges and el->max_edges before calling.
void map_edge_list_build(map_edge_list *el, int mode);

#endif
```

- [ ] **Step 2: Create `d1/main/mapedges.c`**

This is `automap.c:873-1178` re-homed: `automap_find_edge` → `map_find_edge`, `add_one_edge` → `map_add_one_edge`, `add_one_unknown_edge` → `map_add_one_unknown_edge`, `add_segment_edges` → `map_add_segment_edges`, `add_unknown_segment_edges` → `map_add_unknown_segment_edges`, `automap_build_edge_list` → `map_edge_list_build`; every `am->` becomes `el->`, and the two visited-dependent behaviors are gated on `mode`. The `K_WALL_*` color macros move here from `automap.c:144-148`.

```c
/*
 * Wireframe edge-list construction shared by the automap and the HUD
 * minimap. Extracted from automap.c (the routines that used to live
 * below the "All routines below here are used to build the Edge list"
 * banner); logic is unchanged apart from the map_edge_list parameter
 * and the MAP_EDGE_BUILD_ALL mode, which skips all visited/frontier
 * logic so the whole level is treated as known.
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "dxxerror.h"
#include "3d.h"
#include "inferno.h"
#include "game.h"
#include "player.h"
#include "wall.h"
#include "fuelcen.h"
#include "gameseq.h"
#include "segment.h"
#include "gameseg.h"
#include "cntrlcen.h"
#include "palette.h"
#include "bm.h"
#include "automap.h"
#include "mapedges.h"

#define K_WALL_NORMAL_COLOR     BM_XRGB(29, 29, 29 )
#define K_WALL_DOOR_COLOR       BM_XRGB(5, 27, 5 )
#define K_WALL_DOOR_BLUE        BM_XRGB(0, 0, 31)
#define K_WALL_DOOR_GOLD        BM_XRGB(31, 31, 0)
#define K_WALL_DOOR_RED         BM_XRGB(31, 0, 0)

//finds edge, filling in edge_ptr. if found old edge, returns index, else return -1
static int map_find_edge(map_edge_list *el, int v0, int v1, Edge_info **edge_ptr)
{
	long vv, evv;
	int hash, oldhash;
	int ret, ev0, ev1;

	vv = (v1<<16) + v0;

	oldhash = hash = ((v0*5+v1) % el->max_edges);

	ret = -1;

	while (ret==-1) {
		ev0 = el->edges[hash].verts[0];
		ev1 = el->edges[hash].verts[1];
		evv = (ev1<<16)+ev0;
		if (el->edges[hash].num_faces == 0 ) ret=0;
		else if (evv == vv) ret=1;
		else {
			if (++hash==el->max_edges) hash=0;
			if (hash==oldhash) Error("Edge list full!");
		}
	}

	*edge_ptr = &el->edges[hash];

	if (ret == 0)
		return -1;
	else
		return hash;
}

static void map_add_one_edge( map_edge_list *el, int va, int vb, ubyte color, ubyte side, int segnum, int hidden, int grate, int no_fade )
{
	int found;
	Edge_info *e;
	int tmp;

	if ( el->num_edges >= el->max_edges)	{
		// GET JOHN! (And tell him that his
		// MAX_EDGES_FROM_VERTS formula is hosed.)
		// If he's not around, save the mine,
		// and send him  mail so he can look
		// at the mine later. Don't modify it.
		// This is important if this happens.
		Int3();		// LOOK ABOVE!!!!!!
		return;
	}

	if ( va > vb )	{
		tmp = va;
		va = vb;
		vb = tmp;
	}

	found = map_find_edge(el,va,vb,&e);

	if (found == -1) {
		e->verts[0] = va;
		e->verts[1] = vb;
		e->color = color;
		e->num_faces = 1;
		e->flags = EF_USED | EF_DEFINING;			// Assume a normal line
		e->sides[0] = side;
		e->segnum[0] = segnum;
		if ( (e-el->edges) > el->highest_edge_index )
			el->highest_edge_index = e - el->edges;
		el->num_edges++;
	} else {
		if ( color != el->wall_normal_color )
			e->color = color;
		if ( e->num_faces < 4 ) {
			e->sides[e->num_faces] = side;
			e->segnum[e->num_faces] = segnum;
			e->num_faces++;
		}
	}

	if ( grate )
		e->flags |= EF_GRATE;

	if ( hidden )
		e->flags|=EF_SECRET;		// Mark this as a hidden edge
	if ( no_fade )
		e->flags |= EF_NO_FADE;
}

static void map_add_one_unknown_edge( map_edge_list *el, int va, int vb )
{
	int found;
	Edge_info *e;
	int tmp;

	if ( va > vb )	{
		tmp = va;
		va = vb;
		vb = tmp;
	}

	found = map_find_edge(el,va,vb,&e);
	if (found != -1)
		e->flags|=EF_FRONTIER;		// Mark as a border edge
}

static void map_add_segment_edges(map_edge_list *el, segment *seg, int mode)
{
	int 	is_grate, no_fade;
	ubyte	color;
	int	sn;
	int	segnum = seg-Segments;
	int	hidden_flag;

	for (sn=0;sn<MAX_SIDES_PER_SEGMENT;sn++) {
		int	vertex_list[4];

		hidden_flag = 0;

		is_grate = 0;
		no_fade = 0;

		color = 255;
		if (seg->children[sn] == -1) {
			color = el->wall_normal_color;
		}

		switch( seg->special )	{
		case SEGMENT_IS_FUELCEN:
			color = BM_XRGB( 29, 27, 13 );
			break;
		case SEGMENT_IS_CONTROLCEN:
			if (Control_center_present)
				color = BM_XRGB( 29, 0, 0 );
			break;
		case SEGMENT_IS_ROBOTMAKER:
			color = BM_XRGB( 29, 0, 31 );
			break;
		}

		if (seg->sides[sn].wall_num > -1)	{

			switch( Walls[seg->sides[sn].wall_num].type )	{
			case WALL_DOOR:
				if (Walls[seg->sides[sn].wall_num].keys == KEY_BLUE) {
					no_fade = 1;
					color = el->wall_door_blue;
				} else if (Walls[seg->sides[sn].wall_num].keys == KEY_GOLD) {
					no_fade = 1;
					color = el->wall_door_gold;
				} else if (Walls[seg->sides[sn].wall_num].keys == KEY_RED) {
					no_fade = 1;
					color = el->wall_door_red;
				} else if (!(WallAnims[Walls[seg->sides[sn].wall_num].clip_num].flags & WCF_HIDDEN)) {
					int	connected_seg = seg->children[sn];
					if (connected_seg != -1) {
						int connected_side = find_connect_side(seg, &Segments[connected_seg]);
						int	keytype = Walls[Segments[connected_seg].sides[connected_side].wall_num].keys;
						if ((keytype != KEY_BLUE) && (keytype != KEY_GOLD) && (keytype != KEY_RED))
							color = el->wall_door_color;
						else {
							switch (Walls[Segments[connected_seg].sides[connected_side].wall_num].keys) {
								case KEY_BLUE:	color = el->wall_door_blue;	no_fade = 1; break;
								case KEY_GOLD:	color = el->wall_door_gold;	no_fade = 1; break;
								case KEY_RED:	color = el->wall_door_red;	no_fade = 1; break;
								default:	Error("Inconsistent data.  Supposed to be a colored wall, but not blue, gold or red.\n");
							}
						}

					}
				} else {
					color = el->wall_normal_color;
					hidden_flag = 1;
				}
				break;
			case WALL_CLOSED:
				// Make grates draw properly
				if (WALL_IS_DOORWAY(seg,sn) & WID_RENDPAST_FLAG)
					is_grate = 1;
				else
					hidden_flag = 1;
				color = el->wall_normal_color;
				break;
			case WALL_BLASTABLE:
				// Hostage doors
				color = el->wall_door_color;
				break;
			}
		}

		if (segnum==Player_init[Player_num].segnum)
			color = BM_XRGB(31,0,31);

		if ( color != 255 )	{
			// If they have a map powerup, draw unvisited areas in dark blue.
			if (mode == MAP_EDGE_BUILD_AUTOMAP && (Players[Player_num].flags & PLAYER_FLAGS_MAP_ALL) && (!Automap_visited[segnum]))
				color = BM_XRGB( 0, 0, 25 );

			get_side_verts(vertex_list,segnum,sn);
			map_add_one_edge( el, vertex_list[0], vertex_list[1], color, sn, segnum, hidden_flag, 0, no_fade );
			map_add_one_edge( el, vertex_list[1], vertex_list[2], color, sn, segnum, hidden_flag, 0, no_fade );
			map_add_one_edge( el, vertex_list[2], vertex_list[3], color, sn, segnum, hidden_flag, 0, no_fade );
			map_add_one_edge( el, vertex_list[3], vertex_list[0], color, sn, segnum, hidden_flag, 0, no_fade );

			if ( is_grate )	{
				map_add_one_edge( el, vertex_list[0], vertex_list[2], color, sn, segnum, hidden_flag, 1, no_fade );
				map_add_one_edge( el, vertex_list[1], vertex_list[3], color, sn, segnum, hidden_flag, 1, no_fade );
			}
		}
	}
}

// Adds all the edges from a segment we haven't visited yet.
static void map_add_unknown_segment_edges(map_edge_list *el, segment *seg)
{
	int sn;
	int segnum = seg-Segments;

	for (sn=0;sn<MAX_SIDES_PER_SEGMENT;sn++) {
		int	vertex_list[4];

		// Only add edges that have no children
		if (seg->children[sn] == -1) {
			get_side_verts(vertex_list,segnum,sn);

			map_add_one_unknown_edge( el, vertex_list[0], vertex_list[1] );
			map_add_one_unknown_edge( el, vertex_list[1], vertex_list[2] );
			map_add_one_unknown_edge( el, vertex_list[2], vertex_list[3] );
			map_add_one_unknown_edge( el, vertex_list[3], vertex_list[0] );
		}
	}
}

void map_edge_list_build(map_edge_list *el, int mode)
{
	int	i,e1,e2,s;
	Edge_info * e;

	el->wall_normal_color = K_WALL_NORMAL_COLOR;
	el->wall_door_color = K_WALL_DOOR_COLOR;
	el->wall_door_blue = K_WALL_DOOR_BLUE;
	el->wall_door_gold = K_WALL_DOOR_GOLD;
	el->wall_door_red = K_WALL_DOOR_RED;

	// clear edge list
	for (i=0; i<el->max_edges; i++) {
		el->edges[i].num_faces = 0;
		el->edges[i].flags = 0;
	}
	el->num_edges = 0;
	el->highest_edge_index = -1;

	if (mode == MAP_EDGE_BUILD_ALL) {
		// Live minimap: the whole level counts as known
		for (s=0; s<=Highest_segment_index; s++)
			map_add_segment_edges(el, &Segments[s], mode);
	} else if (cheats.fullautomap || (Players[Player_num].flags & PLAYER_FLAGS_MAP_ALL) )	{
		// Cheating, add all edges as visited
		for (s=0; s<=Highest_segment_index; s++)
			map_add_segment_edges(el, &Segments[s], mode);
	} else {
		// Not cheating, add visited edges, and then unvisited edges
		for (s=0; s<=Highest_segment_index; s++)
			if (Automap_visited[s]) {
				map_add_segment_edges(el, &Segments[s], mode);
			}

		for (s=0; s<=Highest_segment_index; s++)
			if (!Automap_visited[s]) {
				map_add_unknown_segment_edges(el, &Segments[s]);
			}
	}

	// Find unnecessary lines (These are lines that don't have to be drawn because they have small curvature)
	for (i=0; i<=el->highest_edge_index; i++ )	{
		e = &el->edges[i];
		if (!(e->flags&EF_USED)) continue;

		for (e1=0; e1<e->num_faces; e1++ )	{
			for (e2=1; e2<e->num_faces; e2++ )	{
				if ( (e1 != e2) && (e->segnum[e1] != e->segnum[e2]) )	{
					if ( vm_vec_dot( &Segments[e->segnum[e1]].sides[e->sides[e1]].normals[0], &Segments[e->segnum[e2]].sides[e->sides[e2]].normals[0] ) > (F1_0-(F1_0/10))  )	{
						e->flags &= (~EF_DEFINING);
						break;
					}
				}
			}
			if (!(e->flags & EF_DEFINING))
				break;
		}
	}
}
```

Notes for the implementer:
- The original had `#ifdef EDITOR if (Segments[s].segnum != -1)` guards and a `#ifdef COMPACT_SEGS` variant in the loops (`automap.c:1128-1148`, `1161-1168`); both macros are off in this build. Keep them if you prefer a faithful move — the code above drops them because `EDITOR` builds exclude this game path and `COMPACT_SEGS` is never defined in d1. If the build errors on a missing symbol, compare with the original block and add the missing include from `automap.c`'s include list (`automap.c:20-59`).
- The `#ifndef _GAMESEQ_H extern obj_position Player_init[];` fallback at `automap.c:979-981` is replaced by including `gameseq.h`.

- [ ] **Step 3: Gut `automap.c` and re-point it at the shared module**

3a. Add `#include "mapedges.h"` after `#include "automap.h"` (`automap.c:63`).

3b. Delete from `automap.c`:
- the `EF_*` defines (`automap.c:78-84`) and `typedef struct Edge_info` (`automap.c:86-93`)
- the `K_WALL_*` macros for the five wall colors only (`automap.c:144-148`: `K_WALL_NORMAL_COLOR`, `K_WALL_DOOR_COLOR`, `K_WALL_DOOR_BLUE`, `K_WALL_DOOR_GOLD`, `K_WALL_DOOR_RED`) — keep `K_HOSTAGE_COLOR`, `K_FONT_COLOR_20`, `K_GREEN_31`
- in `init_automap_colors` (`automap.c:155-169`): the five lines assigning `am->wall_normal_color` … `am->wall_door_red`
- the prototype `void automap_build_edge_list(automap *am);` (`automap.c:187`)
- everything from the banner comment `//==================================================================` / `// All routines below here are used to build the Edge list` (`automap.c:865`) through the end of `automap_build_edge_list` (`automap.c:1178`) — i.e. `automap_find_edge`, `add_one_edge`, `add_one_unknown_edge`, the `Player_init` extern fallback, `add_segment_edges`, `add_unknown_segment_edges`, `automap_build_edge_list`

3c. In the `automap` struct (`automap.c:95-139`): replace the four edge-list fields and five wall-color fields

```c
	// Edge list variables
	int			num_edges;
	int			max_edges; //set each frame
	int			highest_edge_index;
	Edge_info		*edges;
```
and
```c
	int			wall_normal_color;
	int			wall_door_color;
	int			wall_door_blue;
	int			wall_door_gold;
	int			wall_door_red;
```
with a single embedded list (keep `drawingListBright` and the remaining color fields where they are):
```c
	// Edge list (shared machinery with the HUD minimap)
	map_edge_list		el;
```

3d. Mechanical renames in the remaining automap code (every remaining compile error will be one of these — the full list):

| Location | Old | New |
|---|---|---|
| `adjust_segment_limit` (was `automap.c:731-732`) | `am->highest_edge_index`, `am->edges[i]` | `am->el.highest_edge_index`, `am->el.edges[i]` |
| `draw_all_edges` (was `automap.c:757-859`) | `am->highest_edge_index`, `am->edges[i]`, `am->wall_normal_color`, `e-am->edges`, `am->edges[am->drawingListBright[...]]` | `am->el.highest_edge_index`, `am->el.edges[i]`, `am->el.wall_normal_color`, `e-am->el.edges`, `am->el.edges[am->drawingListBright[...]]` |
| `do_automap` (was `automap.c:645-649`) | `am->num_edges = 0; am->highest_edge_index = -1; am->max_edges = Num_segments*12; MALLOC(am->edges, Edge_info, am->max_edges); MALLOC(am->drawingListBright, int, am->max_edges);` and the follow-up null checks/frees on `am->edges` | same statements on `am->el.num_edges`, `am->el.highest_edge_index`, `am->el.max_edges`, `am->el.edges` (`drawingListBright` sized by `am->el.max_edges`) |
| `do_automap` (was `automap.c:682`) | `automap_build_edge_list(am);` | `map_edge_list_build(&am->el, MAP_EDGE_BUILD_AUTOMAP);` |
| `automap_handler` window-close cleanup (search `d_free` in `automap.c`, around line 600) | `d_free(am->edges)` | `d_free(am->el.edges)` |

3e. `memset(am, 0, sizeof(automap))` in `do_automap` (`automap.c:631`) zeroes the embedded `el` too — no change needed there.

- [ ] **Step 4: Export `draw_player` in `automap.h`**

`draw_player` (`automap.c:205`) is already a non-static global; give it a proper declaration. In `d1/main/automap.h`, after `extern ubyte Automap_visited[MAX_SEGMENTS];`:

```c
struct object;
extern void draw_player( struct object * obj );
```

And in `automap.c` change the definition signature to match the tag-based declaration only if the compiler complains (it won't: `object` is `typedef struct object … object` in `object.h`, so the types are identical).

- [ ] **Step 5: Add `mapedges.c` to the build**

In `d1/main/CMakeLists.txt`, the `add_executable` list is alphabetical; insert between `lighting.c` (line 36) and `menu.c` (line 37):

```cmake
    mapedges.c
```

- [ ] **Step 6: Build**

Run the Global Constraints build command. Expected: compiles and links cleanly. Any error will be a missed rename from Step 3d or a missing include in `mapedges.c` — fix by consulting the tables above and `automap.c:20-59`.

- [ ] **Step 7: Manual check — automap unchanged**

Run the game (Global Constraints run command), start a new game, fly through 2-3 rooms, press Tab:
- map shows visited rooms in gray wireframe, blue/gold/red doors colored, unvisited frontier hidden as before
- F9/F10 still shrink/grow the visible depth
- Esc automap, game continues normally

- [ ] **Step 8: Commit**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/.claude/worktrees/hud-minimap
git add d1/main/mapedges.h d1/main/mapedges.c d1/main/automap.c d1/main/automap.h d1/main/CMakeLists.txt
git commit -m "Extract automap edge-list machinery into mapedges.c

Parameterize the edge-list builder on a map_edge_list struct so the
upcoming HUD minimap can own a second instance. MAP_EDGE_BUILD_ALL mode
treats every segment as known. The automap embeds the struct and behaves
identically; draw_player gets a public declaration for reuse.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: OpenGL line alpha — `g3_draw_line` honors the canvas fade level

**Files:**
- Modify: `d1/arch/ogl/ogl.c:400`

**Interfaces:**
- Consumes: `grd_curcanv->cv_fade_level` (set via `gr_settransblend(fade_level, blend_func)`, `2d/canvas.c`), `GR_FADE_OFF`/`GR_FADE_LEVELS` (`include/gr.h:30-31`).
- Produces: translucent 3D lines whenever the current canvas has a fade level below `GR_FADE_OFF`. Default fade level everywhere is `GR_FADE_OFF`, so all existing rendering (automap included) is pixel-identical.

- [ ] **Step 1: Change the hardcoded alpha**

In `d1/arch/ogl/ogl.c`, function `g3_draw_line` — replace line 400:

```c
	color_array[3] = color_array[7] = 1.0;
```

with (same formula `gr_ucircle`/`gr_disk` already use at `ogl.c:659`/`682`):

```c
	color_array[3] = color_array[7] = (grd_curcanv->cv_fade_level >= GR_FADE_OFF)?1.0:1.0 - (float)grd_curcanv->cv_fade_level / ((float)GR_FADE_LEVELS - 1.0);
```

- [ ] **Step 2: Build**

Run the build command. Expected: clean link.

- [ ] **Step 3: Manual check — nothing changed**

Run the game, open the Tab automap: wireframe lines look exactly as before (fully opaque — the automap never sets a fade level). Fly a bit; laser/weapon effects unchanged.

- [ ] **Step 4: Commit**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/.claude/worktrees/hud-minimap
git add d1/arch/ogl/ogl.c
git commit -m "Honor canvas fade level in OGL g3_draw_line

Derive line alpha from cv_fade_level the way gr_disk/gr_ucircle already
do, instead of hardcoding 1.0. The default fade level is GR_FADE_OFF, so
existing wireframe rendering is unchanged; the HUD minimap uses this for
its translucent overlay.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: Settings, persistence, `Minimap_visible`, F4 toggle

**Files:**
- Modify: `d1/main/playsave.h:109` (six new PlayerCfg fields)
- Modify: `d1/main/playsave.c:113` (defaults), `:447` (read), `:928` (write)
- Modify: `d1/main/game.c:826` area (runtime flag)
- Modify: `d1/main/game.h` (extern, next to the `Mirror_visible` extern — search `extern int Mirror_visible;`)
- Modify: `d1/main/gamecntl.c` (`HandleSystemKey`, between the `KEY_F3` and `KEY_F5` cases at `gamecntl.c:571-586`)

**Interfaces:**
- Produces (used by Tasks 4-5):
  - `PlayerCfg.MinimapMode` (ubyte 0/1), `PlayerCfg.MinimapPos` (0=TL 1=TR 2=BL 3=BR 4=center), `PlayerCfg.MinimapSize` (0/1/2), `PlayerCfg.MinimapRange` (0/1/2), `PlayerCfg.MinimapRotate` (0=heading-up 1=north-up), `PlayerCfg.MinimapOpacity` (1..10)
  - `extern int Minimap_visible;` — runtime show/hide flag, default 1

- [ ] **Step 1: PlayerCfg fields** — in `d1/main/playsave.h`, directly after the `MirrorSize` line (:109):

```c
	ubyte MinimapMode; /* HudMinimap: draw a live PiP proximity map during gameplay */
	ubyte MinimapPos; /* HudMinimap: 0=top left, 1=top right, 2=bottom left, 3=bottom right, 4=center */
	ubyte MinimapSize; /* HudMinimap: 0=small, 1=medium, 2=large */
	ubyte MinimapRange; /* HudMinimap: 0=near, 1=medium, 2=far */
	ubyte MinimapRotate; /* HudMinimap: 0=heading-up, 1=north-up */
	ubyte MinimapOpacity; /* HudMinimap: 1..10, alpha = value*10%% under OpenGL */
```

(The plr file is a text key/value format, so inserting mid-struct is safe — the mirror fields set the precedent.)

- [ ] **Step 2: Defaults** — in `d1/main/playsave.c`, `new_player_config`, after the `MirrorSize` default (:113):

```c
	PlayerCfg.MinimapMode = 0; /* HudMinimap */
	PlayerCfg.MinimapPos = 1; /* HudMinimap */
	PlayerCfg.MinimapSize = 1; /* HudMinimap */
	PlayerCfg.MinimapRange = 1; /* HudMinimap */
	PlayerCfg.MinimapRotate = 0; /* HudMinimap */
	PlayerCfg.MinimapOpacity = 6; /* HudMinimap */
```

- [ ] **Step 3: Read** — in `d1/main/playsave.c`, after the `MIRRORSIZE` block (:443-447), same indentation as its neighbors:

```c
				if(!strcmp(word,"MINIMAPMODE"))
					PlayerCfg.MinimapMode = atoi(line) ? 1 : 0;
				if(!strcmp(word,"MINIMAPPOS")) {
					PlayerCfg.MinimapPos = atoi(line);
					if (PlayerCfg.MinimapPos > 4)
						PlayerCfg.MinimapPos = 1;
				}
				if(!strcmp(word,"MINIMAPSIZE")) {
					PlayerCfg.MinimapSize = atoi(line);
					if (PlayerCfg.MinimapSize > 2)
						PlayerCfg.MinimapSize = 1;
				}
				if(!strcmp(word,"MINIMAPRANGE")) {
					PlayerCfg.MinimapRange = atoi(line);
					if (PlayerCfg.MinimapRange > 2)
						PlayerCfg.MinimapRange = 1;
				}
				if(!strcmp(word,"MINIMAPROTATE"))
					PlayerCfg.MinimapRotate = atoi(line) ? 1 : 0;
				if(!strcmp(word,"MINIMAPOPACITY")) {
					PlayerCfg.MinimapOpacity = atoi(line);
					if (PlayerCfg.MinimapOpacity < 1 || PlayerCfg.MinimapOpacity > 10)
						PlayerCfg.MinimapOpacity = 6;
				}
```

- [ ] **Step 4: Write** — in `d1/main/playsave.c`, after the `mirrorsize` line (:928):

```c
		PHYSFSX_printf(fout,"minimapmode=%i\n",PlayerCfg.MinimapMode); /* HudMinimap */
		PHYSFSX_printf(fout,"minimappos=%i\n",PlayerCfg.MinimapPos); /* HudMinimap */
		PHYSFSX_printf(fout,"minimapsize=%i\n",PlayerCfg.MinimapSize); /* HudMinimap */
		PHYSFSX_printf(fout,"minimaprange=%i\n",PlayerCfg.MinimapRange); /* HudMinimap */
		PHYSFSX_printf(fout,"minimaprotate=%i\n",PlayerCfg.MinimapRotate); /* HudMinimap */
		PHYSFSX_printf(fout,"minimapopacity=%i\n",PlayerCfg.MinimapOpacity); /* HudMinimap */
```

- [ ] **Step 5: Runtime flag** — in `d1/main/game.c`, after the `Mirror_visible` definition (:826):

```c
int Minimap_visible = 1;	//HUD minimap PiP shown; F4 toggles this while PlayerCfg.MinimapMode is on
```

In `d1/main/game.h`, next to `extern int Mirror_visible;`:

```c
extern int Minimap_visible;
```

- [ ] **Step 6: F4 handler** — in `d1/main/gamecntl.c`, `HandleSystemKey`, insert between the end of the `KEY_F3` case (`break;` at :578) and the `KEY_F5` case block (:580):

```c
		KEY_MAC(case KEY_COMMAND+KEY_4:)
		case KEY_F4:
			if (PlayerCfg.MinimapMode)
			{
				Minimap_visible = !Minimap_visible;
				HUD_init_message_literal(HM_DEFAULT, Minimap_visible ? "Minimap on" : "Minimap off");
			}
			break;
```

(F4 is in kconfig's `system_keys` (`kconfig.c:71`) so it can't be user-bound; demo playback's F4 percentage toggle lives in the separate demo key handler (`gamecntl.c:381`) and is unaffected. When `MinimapMode` is off, F4 deliberately does nothing.)

- [ ] **Step 7: Build**

Run the build command. Expected: clean link.

- [ ] **Step 8: Manual check — persistence + F4 round-trip**

1. Run the game once, quit. Open the pilot file `d1/hogs/<pilot>.plr` (text): confirm `minimapmode=0 … minimapopacity=6` lines exist.
2. Edit the file: set `minimapmode=1`. Run the game, start a level, press F4 twice: HUD shows "Minimap off" then "Minimap on".
3. Quit; confirm the plr still has `minimapmode=1`.

- [ ] **Step 9: Commit**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/.claude/worktrees/hud-minimap
git add d1/main/playsave.h d1/main/playsave.c d1/main/game.c d1/main/game.h d1/main/gamecntl.c
git commit -m "Add HUD minimap pilot settings and F4 visibility toggle

Six per-pilot fields (mode/pos/size/range/rotate/opacity) with defaults,
clamped plr read and write. F4 flips the Minimap_visible runtime flag
with a HUD message while the feature is enabled; F4 is a reserved system
key so it cannot collide with user bindings.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: Minimap module + render hook + level reset

The core: `minimap.c` owns the edge list, depth BFS, auto-level camera, and PiP drawing; `gamerend.c` calls it right after the mirror block behind the same gates; `gameseq.c` resets it on level start.

**Files:**
- Create: `d1/main/minimap.h`
- Create: `d1/main/minimap.c`
- Modify: `d1/main/gamerend.c` (hook after the mirror block, :537)
- Modify: `d1/main/gameseq.c:1184` (reset next to `automap_clear_visited()`)
- Modify: `d1/main/CMakeLists.txt` (add `minimap.c`)

**Interfaces:**
- Consumes: `map_edge_list` / `map_edge_list_build` / `Edge_info` / `EF_*` (Task 1), `draw_player` (Task 1), `PlayerCfg.Minimap*` (Task 3), `Minimap_visible` (Task 3), modified `g3_draw_line` alpha (Task 2). Existing engine: `Screen_3d_window` (`screens.h:110`), `surround_enabled()` (`render.c:1368`), `g3_start_frame`/`g3_set_view_matrix`/`g3_end_frame`, `render_start_frame`/`rotate_list` (`render.c:615/627`), `Segment_points`, `gr_settransblend` (`gr.h:258`), `gr_fade_table`, `fix_sincos` (`maths.h:96`), `Side_to_verts`+`WTOP`+`WBOTTOM` (`segment.h:40/42/152`), `player_rgb`/`player_rgb_alt`/`selected_player_rgb` (`gauges.h:60-63`), `CONNECT_PLAYING` (`multi.h:151`), `PLAYER_FLAGS_CLOAKED` (`player.h:51`).
- Produces: `void minimap_level_reset(void);` and `void draw_minimap(void);`

- [ ] **Step 1: Create `d1/main/minimap.h`**

```c
/*
 * Live HUD minimap: transparent wireframe proximity map drawn
 * picture-in-picture during gameplay.
 */

#ifndef _MINIMAP_H
#define _MINIMAP_H

// Invalidate per-level state (edge list, depths, smoothed camera).
// Call when a new level starts.
void minimap_level_reset(void);

// Render the minimap PiP into Screen_3d_window. Caller applies the
// visibility gates (see game_render_frame_mono).
void draw_minimap(void);

#endif
```

- [ ] **Step 2: Create `d1/main/minimap.c`**

```c
/*
 * Live HUD minimap. Renders the automap edge list (mapedges.c) as a
 * translucent wireframe into a small sub-canvas of the 3D window, from
 * an auto-leveled, slightly tilted top-down camera centered on the
 * player's ship. Geometry is limited to segments within a few hops of
 * the ship (own BFS buffer - Automap_visited is live game state and
 * must not be touched). Purely client-side: no net, demo or savegame
 * impact.
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "dxxerror.h"
#include "3d.h"
#include "inferno.h"
#include "u_mem.h"
#include "render.h"
#include "object.h"
#include "game.h"
#include "screens.h"
#include "surround.h"
#include "player.h"
#include "playsave.h"
#include "segment.h"
#include "gameseg.h"
#include "segpoint.h"
#include "gauges.h"
#include "palette.h"
#include "bm.h"
#include "automap.h"
#include "mapedges.h"
#include "minimap.h"
#ifdef NETWORK
#include "multi.h"
#endif

// ---- tuning constants ----
#define MINIMAP_TILT_ANG	3277		// ~18deg off straight-down (0x10000 = 360deg) so 3D structure reads
#define MINIMAP_LEVEL_RATE	(F1_0*3)	// auto-level slew: fraction of remaining turn applied per second
#define MINIMAP_MIN_VEC		(F1_0/16)	// below this magnitude a direction is degenerate
#define MINIMAP_ZOOM		0x9000		// same baseline as the automap
#define MINIMAP_AXIS_HYST	((F1_0*3)/4)	// north-up: re-pick the north axis when |dot(axis,up)| exceeds this

static const int Minimap_hops[3] = { 4, 6, 9 };			//near, medium, far
static const fix Minimap_cam_dist[3] = { 55*F1_0, 85*F1_0, 125*F1_0 };

static map_edge_list Minimap_edges;
static int Minimap_built = 0;
static ubyte Minimap_depth[MAX_SEGMENTS];	// 0 = out of range, 1 = ship's segment, hops+1 = fringe
static int Minimap_last_segnum = -1;
static vms_vector Minimap_up;			// smoothed map "up"; zero = uninitialized
static vms_vector Minimap_north;		// smoothed in-plane screen-up direction; zero = uninitialized
static int Minimap_axis = -1;			// north-up mode: world axis used as north (-1 = unset)

void minimap_level_reset(void)
{
	if (Minimap_edges.edges)
		d_free(Minimap_edges.edges);
	Minimap_edges.edges = NULL;
	Minimap_built = 0;
	Minimap_last_segnum = -1;
	Minimap_up.x = Minimap_up.y = Minimap_up.z = 0;
	Minimap_north.x = Minimap_north.y = Minimap_north.z = 0;
	Minimap_axis = -1;
}

// Breadth-first hop distances from the ship's segment, capped at
// max_depth (the fringe ring max_depth+1 is marked but not expanded).
static void minimap_compute_depths(int start_seg, int max_depth)
{
	static int queue[MAX_SEGMENTS];
	int head = 0, tail = 0;

	memset(Minimap_depth, 0, sizeof(Minimap_depth));

	if (start_seg < 0 || start_seg > Highest_segment_index)
		return;

	Minimap_depth[start_seg] = 1;
	queue[tail++] = start_seg;

	while (head < tail) {
		int curseg = queue[head++];
		int d = Minimap_depth[curseg];
		int i;

		if (d > max_depth)
			continue;

		for (i = 0; i < MAX_SIDES_PER_SEGMENT; i++) {
			int child = Segments[curseg].children[i];
			if (child >= 0 && !Minimap_depth[child]) {
				Minimap_depth[child] = d + 1;
				queue[tail++] = child;
			}
		}
	}
}

// Segment "up" = centroid of the top side's verts minus centroid of the
// bottom side's (the editor's extract_up_vector_from_segment math -
// editor code isn't compiled in game builds). Not normalized.
static void minimap_segment_up(int segnum, vms_vector *up)
{
	segment *seg = &Segments[segnum];
	vms_vector vtop, vbot;
	int i;

	vtop.x = vtop.y = vtop.z = 0;
	vbot.x = vbot.y = vbot.z = 0;
	for (i = 0; i < 4; i++) {
		vm_vec_add2(&vtop, &Vertices[seg->verts[(int)Side_to_verts[WTOP][i]]]);
		vm_vec_add2(&vbot, &Vertices[seg->verts[(int)Side_to_verts[WBOTTOM][i]]]);
	}
	vm_vec_sub(up, &vtop, &vbot);
}

// Slew cur toward target (both unit vectors) at MINIMAP_LEVEL_RATE.
static void minimap_smooth_dir(vms_vector *cur, vms_vector *target)
{
	fix k = fixmul(FrameTime, MINIMAP_LEVEL_RATE);
	vms_vector delta;

	if (k > F1_0)
		k = F1_0;

	if (cur->x == 0 && cur->y == 0 && cur->z == 0) {
		*cur = *target;		//first frame: snap
		return;
	}

	vm_vec_sub(&delta, target, cur);
	vm_vec_scale_add2(cur, &delta, k);
	if (vm_vec_normalize(cur) < MINIMAP_MIN_VEC)
		*cur = *target;		//blend of near-opposite dirs collapsed: snap
}

void draw_minimap(void)
{
	static grs_canvas minimap_canv;
	static const int size_divisor[3] = { 6, 4, 3 };	//small, medium, large
	object *ship = &Objects[Players[Player_num].objnum];
	int base_x = 0, base_w = Screen_3d_window.cv_bitmap.bm_w;
	int base_h = Screen_3d_window.cv_bitmap.bm_h;
	int size = PlayerCfg.MinimapSize;
	int range = PlayerCfg.MinimapRange;
	int op = PlayerCfg.MinimapOpacity;
	int side, mgn, mx, my, hops, i, color, row;
	vms_vector up_target, north_target, cam_fvec, cam_uvec, cam_pos;
	vms_matrix cam_orient;
	fix st, ct, dist;

	if (size > 2)
		size = 1;
	if (range > 2)
		range = 1;
	if (op < 1 || op > 10)
		op = 6;
	hops = Minimap_hops[range];
	dist = Minimap_cam_dist[range];

	// ---- lazy per-level edge list build ----
	if (!Minimap_built) {
		if (!Minimap_edges.edges) {
			Minimap_edges.max_edges = Num_segments * 12;
			MALLOC(Minimap_edges.edges, Edge_info, Minimap_edges.max_edges);
			if (!Minimap_edges.edges)
				return;
		}
		map_edge_list_build(&Minimap_edges, MAP_EDGE_BUILD_ALL);
		Minimap_built = 1;
		Minimap_last_segnum = -1;
	}

	// ---- proximity depths, only when the ship changes segment ----
	if (ship->segnum != Minimap_last_segnum) {
		minimap_compute_depths(ship->segnum, hops);
		Minimap_last_segnum = ship->segnum;
	}

	// ---- auto-level: average up over in-range segments, then smooth ----
	up_target.x = up_target.y = up_target.z = 0;
	for (i = 0; i <= Highest_segment_index; i++) {
		if (Minimap_depth[i]) {
			vms_vector segup;
			minimap_segment_up(i, &segup);
			if (vm_vec_normalize(&segup) >= MINIMAP_MIN_VEC)
				vm_vec_add2(&up_target, &segup);
		}
	}
	if (vm_vec_normalize(&up_target) < MINIMAP_MIN_VEC) {
		if (Minimap_up.x == 0 && Minimap_up.y == 0 && Minimap_up.z == 0)
			return;		//no usable orientation yet; try next frame
		up_target = Minimap_up;	//degenerate average: hold previous
	}
	minimap_smooth_dir(&Minimap_up, &up_target);

	// ---- in-plane "north" (what points up the minimap) ----
	if (PlayerCfg.MinimapRotate == 0) {
		// heading-up: ship forward projected onto the level plane
		north_target = ship->orient.fvec;
		vm_vec_scale_add2(&north_target, &Minimap_up, -vm_vec_dot(&north_target, &Minimap_up));
		if (vm_vec_normalize(&north_target) < MINIMAP_MIN_VEC) {
			if (Minimap_north.x == 0 && Minimap_north.y == 0 && Minimap_north.z == 0)
				return;
			north_target = Minimap_north;	//facing straight along up: hold heading
		}
	} else {
		// north-up: most-perpendicular world axis, with hysteresis
		fix comp[3];
		comp[0] = Minimap_up.x; comp[1] = Minimap_up.y; comp[2] = Minimap_up.z;
		if (Minimap_axis < 0 || abs(comp[Minimap_axis]) > MINIMAP_AXIS_HYST) {
			int best = 0;
			if (abs(comp[1]) < abs(comp[best])) best = 1;
			if (abs(comp[2]) < abs(comp[best])) best = 2;
			Minimap_axis = best;
		}
		north_target.x = north_target.y = north_target.z = 0;
		if (Minimap_axis == 0)
			north_target.x = F1_0;
		else if (Minimap_axis == 1)
			north_target.y = F1_0;
		else
			north_target.z = F1_0;
		vm_vec_scale_add2(&north_target, &Minimap_up, -vm_vec_dot(&north_target, &Minimap_up));
		if (vm_vec_normalize(&north_target) < MINIMAP_MIN_VEC) {
			if (Minimap_north.x == 0 && Minimap_north.y == 0 && Minimap_north.z == 0)
				return;
			north_target = Minimap_north;
		}
	}
	minimap_smooth_dir(&Minimap_north, &north_target);
	// re-orthogonalize against up after smoothing
	vm_vec_scale_add2(&Minimap_north, &Minimap_up, -vm_vec_dot(&Minimap_north, &Minimap_up));
	if (vm_vec_normalize(&Minimap_north) < MINIMAP_MIN_VEC) {
		Minimap_north.x = Minimap_north.y = Minimap_north.z = 0;
		return;		//re-derive next frame
	}

	// ---- camera: above the ship, looking down with a slight tilt ----
	fix_sincos(MINIMAP_TILT_ANG, &st, &ct);
	cam_fvec.x = cam_fvec.y = cam_fvec.z = 0;
	vm_vec_scale_add2(&cam_fvec, &Minimap_up, -ct);
	vm_vec_scale_add2(&cam_fvec, &Minimap_north, st);
	cam_uvec.x = cam_uvec.y = cam_uvec.z = 0;
	vm_vec_scale_add2(&cam_uvec, &Minimap_north, ct);
	vm_vec_scale_add2(&cam_uvec, &Minimap_up, st);
	vm_vector_2_matrix(&cam_orient, &cam_fvec, &cam_uvec, NULL);
	cam_pos = ship->pos;
	vm_vec_scale_add2(&cam_pos, &cam_fvec, -dist);

	// ---- PiP sub-canvas (square; on the center monitor in surround) ----
	if (surround_enabled()) {
		base_w /= 3;
		base_x = base_w;
	}
	side = base_h / size_divisor[size];
	if (PlayerCfg.MirrorMode) {
		// OGL_VIEWPORT (include/internal.h) caches on W/H only and
		// ignores position - never exactly match the mirror's dims.
		static const int mirror_divisor[3] = { 6, 4, 3 };
		int msize = (PlayerCfg.MirrorSize > 2) ? 1 : PlayerCfg.MirrorSize;
		if (side == base_w / mirror_divisor[msize] && side == base_h / mirror_divisor[msize])
			side--;
	}
	mgn = base_h / 64;
	if (mgn < 2)
		mgn = 2;
	switch (PlayerCfg.MinimapPos) {
	case 0:		//top left
		mx = mgn;			my = mgn;			break;
	case 2:		//bottom left
		mx = mgn;			my = base_h - side - mgn;	break;
	case 3:		//bottom right
		mx = base_w - side - mgn;	my = base_h - side - mgn;	break;
	case 4:		//center
		mx = (base_w - side) / 2;	my = (base_h - side) / 2;	break;
	default:	//top right
		mx = base_w - side - mgn;	my = mgn;			break;
	}

	gr_init_sub_canvas(&minimap_canv, &Screen_3d_window, base_x + mx, my, side, side);
	gr_set_current_canvas(&minimap_canv);
	// No canvas clear and no background - the game view shows through.

#ifdef OGL
	gr_settransblend(((10 - op) * (GR_FADE_LEVELS - 1)) / 10, GR_BLEND_NORMAL);
#endif

	g3_start_frame();
	render_start_frame();
	g3_set_view_matrix(&cam_pos, &cam_orient, MINIMAP_ZOOM);

	// ---- edges ----
	row = 31;
#ifndef OGL
	row = (op * 31) / 10;	//software lines can't blend; dim via fade table instead
#endif
	for (i = 0; i <= Minimap_edges.highest_edge_index; i++) {
		Edge_info *e = &Minimap_edges.edges[i];
		int j, dmin, erow;
		g3s_codes cc;

		if (!(e->flags & EF_USED))
			continue;
		if (!(e->flags & (EF_DEFINING | EF_GRATE)))
			continue;

		dmin = 255;
		for (j = 0; j < e->num_faces; j++)
			if (Minimap_depth[e->segnum[j]] && Minimap_depth[e->segnum[j]] < dmin)
				dmin = Minimap_depth[e->segnum[j]];
		if (dmin > hops + 1)
			continue;	//no face in range

		cc = rotate_list(2, e->verts);
		if (cc.uand)
			continue;	//both points off the same side of the frustum

		erow = (dmin > hops) ? row / 2 : row;	//fringe ring fades out
		gr_setcolor(gr_fade_table[e->color + 256 * erow]);
		g3_draw_line(&Segment_points[e->verts[0]], &Segment_points[e->verts[1]]);
	}

	// ---- ship markers ----
	selected_player_rgb = player_rgb;
#ifdef NETWORK
	if (Netgame.BlackAndWhitePyros)
		selected_player_rgb = player_rgb_alt;
	if (Game_mode & GM_TEAM)
		color = get_team(Player_num);
	else
#endif
		color = Player_num;	// Note link to above if!

	gr_setcolor(BM_XRGB(selected_player_rgb[color].r, selected_player_rgb[color].g, selected_player_rgb[color].b));
	draw_player(ship);

#ifdef NETWORK
	// All connected players, all game modes (user's explicit choice for
	// this fork) - except active cloaks, so the cloaking device keeps
	// its point.
	if (Game_mode & GM_MULTI) {
		for (i = (Netgame.host_is_obs ? 1 : 0); i < N_players; i++) {
			if (i == Player_num)
				continue;
			if (Players[i].connected != CONNECT_PLAYING)
				continue;
			if (Objects[Players[i].objnum].type != OBJ_PLAYER)
				continue;
			if (Players[i].flags & PLAYER_FLAGS_CLOAKED)
				continue;
			color = (Game_mode & GM_TEAM) ? get_team(i) : i;
			gr_setcolor(BM_XRGB(selected_player_rgb[color].r, selected_player_rgb[color].g, selected_player_rgb[color].b));
			draw_player(&Objects[Players[i].objnum]);
		}
	}
#endif

	g3_end_frame();

#ifdef OGL
	gr_settransblend(GR_FADE_OFF, GR_BLEND_NORMAL);
#endif
	gr_set_current_canvas(&Screen_3d_window);
}
```

Implementation notes:
- `abs()` on `fix` is fine (`fix` is a 32-bit int; the codebase uses `abs` on fixes elsewhere).
- `Netgame.host_is_obs` / `Netgame.BlackAndWhitePyros` / `get_team` / `CONNECT_PLAYING` come from `multi.h` (automap.c uses the identical idiom, `automap.c:387-416`).
- The sphere in `draw_player` ignores alpha (hardcoded 1.0 in `g3_draw_sphere`) — markers render more opaque than the wireframe. That's accepted v1 behavior (markers are the "important information").
- If the compiler reports a missing symbol, take the include from `automap.c:20-59` — it uses every global this file touches.

- [ ] **Step 3: Hook into `game_render_frame_mono`**

In `d1/main/gamerend.c`, add the include near the top of the file with the other `main/` includes (search for `#include "playsave.h"`):

```c
#include "minimap.h"
```

Then insert directly after the mirror block's closing brace (`gamerend.c:537`, before `update_cockpits();`):

```c
	if (PlayerCfg.MinimapMode && Minimap_visible
		&& !Player_is_dead && !Endlevel_sequence
		&& Viewer == ConsoleObject
		&& Newdemo_state != ND_STATE_PLAYBACK
		&& !is_observer())
	{
		draw_minimap();
	}
```

(Identical gates to the mirror, `gamerend.c:491-495` — every symbol already compiles in this function.)

- [ ] **Step 4: Level reset**

In `d1/main/gameseq.c`, add `#include "minimap.h"` next to the other includes at the top of the file (search for `#include "automap.h"`; if absent, add both near `#include "gauges.h"`), then after `automap_clear_visited();` (:1184):

```c
	minimap_level_reset();
```

- [ ] **Step 5: Add to build** — in `d1/main/CMakeLists.txt`, insert between `mglobal.c` and `mission.c`:

```cmake
    minimap.c
```

- [ ] **Step 6: Build**

Run the build command. Expected: clean link.

- [ ] **Step 7: Manual check — the minimap renders**

Pilot file still has `minimapmode=1` from Task 3 (re-add if you recreated the pilot). Run the game, start Level 1:
1. A translucent gray wireframe map appears top-right, your ship marker centered with a forward arrow.
2. Fly forward: geometry scrolls; in heading-up mode the map rotates so your nose points up; nearby door edges show in door colors.
3. Fly into a vertical shaft: the map re-levels smoothly (no snapping) within ~a second.
4. Press F4: map disappears / reappears with HUD messages.
5. Press Tab: the full automap still works; Esc back — minimap unaffected.
6. Die (self-destruct with a bomb or reactor): minimap hidden while dead, back after respawn.

- [ ] **Step 8: Commit**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/.claude/worktrees/hud-minimap
git add d1/main/minimap.h d1/main/minimap.c d1/main/gamerend.c d1/main/gameseq.c d1/main/CMakeLists.txt
git commit -m "Render the HUD minimap as a live transparent PiP

New minimap module: whole-level edge list (built once per level via
mapedges), own BFS depth buffer around the ship's segment, auto-leveled
camera from smoothed segment up-vectors with heading-up/north-up modes,
tilted top-down g3 wireframe drawn into a mirror-style sub-canvas with
no clear so the game shows through. Ship markers for self and all
connected players except active cloaks. Hooked into
game_render_frame_mono behind the mirror's gates; reset on level start.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 5: Minimap Options submenu

**Files:**
- Modify: `d1/main/menu.c` (new `minimap_config()`; new entry + dispatch in `do_options_menu`/`options_menuset`, :833-870 and :2543-2566)

**Interfaces:**
- Consumes: `PlayerCfg.Minimap*` (Task 3), `Minimap_visible` (Task 3).
- Produces: `void minimap_config();` reachable via Options → "Minimap Options...".

- [ ] **Step 1: Add `minimap_config()`**

In `d1/main/menu.c`, after the end of `graphics_config()` (the function starting at :1272 — place the new function directly after its closing brace). Check `newmenu_do1`'s exact parameter list in `newmenu.h` first; the call below matches the existing `graphics_config`/`reticle_config` usage pattern in this file — mirror whatever they pass:

```c
void minimap_config()
{
	newmenu_item m[20];
	int opt_mm_enable, opt_mm_pos, opt_mm_size, opt_mm_range, opt_mm_rotate, opt_mm_opacity;
	int nitems = 0;
	int i;

	opt_mm_enable = nitems;
	m[nitems].type = NM_TYPE_CHECK; m[nitems].text = "Show Minimap"; m[nitems].value = PlayerCfg.MinimapMode; nitems++;
	m[nitems].type = NM_TYPE_TEXT; m[nitems].text = "Position:"; nitems++;
	opt_mm_pos = nitems;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Top Left"; m[nitems].value = (PlayerCfg.MinimapPos == 0); m[nitems].group = 1; nitems++;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Top Right"; m[nitems].value = (PlayerCfg.MinimapPos == 1); m[nitems].group = 1; nitems++;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Bottom Left"; m[nitems].value = (PlayerCfg.MinimapPos == 2); m[nitems].group = 1; nitems++;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Bottom Right"; m[nitems].value = (PlayerCfg.MinimapPos == 3); m[nitems].group = 1; nitems++;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Center"; m[nitems].value = (PlayerCfg.MinimapPos == 4); m[nitems].group = 1; nitems++;
	m[nitems].type = NM_TYPE_TEXT; m[nitems].text = "Size:"; nitems++;
	opt_mm_size = nitems;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Small"; m[nitems].value = (PlayerCfg.MinimapSize == 0); m[nitems].group = 2; nitems++;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Medium"; m[nitems].value = (PlayerCfg.MinimapSize == 1); m[nitems].group = 2; nitems++;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Large"; m[nitems].value = (PlayerCfg.MinimapSize == 2); m[nitems].group = 2; nitems++;
	m[nitems].type = NM_TYPE_TEXT; m[nitems].text = "Range:"; nitems++;
	opt_mm_range = nitems;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Near"; m[nitems].value = (PlayerCfg.MinimapRange == 0); m[nitems].group = 3; nitems++;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Medium"; m[nitems].value = (PlayerCfg.MinimapRange == 1); m[nitems].group = 3; nitems++;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Far"; m[nitems].value = (PlayerCfg.MinimapRange == 2); m[nitems].group = 3; nitems++;
	m[nitems].type = NM_TYPE_TEXT; m[nitems].text = "Rotation:"; nitems++;
	opt_mm_rotate = nitems;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Heading Up"; m[nitems].value = (PlayerCfg.MinimapRotate == 0); m[nitems].group = 4; nitems++;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "North Up"; m[nitems].value = (PlayerCfg.MinimapRotate == 1); m[nitems].group = 4; nitems++;
	opt_mm_opacity = nitems;
	m[nitems].type = NM_TYPE_SLIDER; m[nitems].text = "Opacity"; m[nitems].value = PlayerCfg.MinimapOpacity; m[nitems].min_value = 1; m[nitems].max_value = 10; nitems++;

	newmenu_do1( NULL, "Minimap Options", nitems, m, NULL, NULL, 0 );

	{
		int old_mode = PlayerCfg.MinimapMode;

		PlayerCfg.MinimapMode = m[opt_mm_enable].value;
		for (i = 0; i < 5; i++)
			if (m[opt_mm_pos + i].value)
				PlayerCfg.MinimapPos = i;
		for (i = 0; i < 3; i++)
			if (m[opt_mm_size + i].value)
				PlayerCfg.MinimapSize = i;
		for (i = 0; i < 3; i++)
			if (m[opt_mm_range + i].value)
				PlayerCfg.MinimapRange = i;
		for (i = 0; i < 2; i++)
			if (m[opt_mm_rotate + i].value)
				PlayerCfg.MinimapRotate = i;
		PlayerCfg.MinimapOpacity = m[opt_mm_opacity].value;

		if (PlayerCfg.MinimapMode && !old_mode)
			Minimap_visible = 1;	//always start visible on enable
	}
}
```

(The pilot file is written by `options_menuset`'s `EVENT_WINDOW_CLOSE` → `write_player_file()` (`menu.c:855-861`), same as the other option submenus — no explicit save needed here.)

- [ ] **Step 2: Wire into the Options menu**

In `do_options_menu` (`menu.c:2543-2566`): change `MALLOC(m, newmenu_item, 11)` to `MALLOC(m, newmenu_item, 12)`, add after the `m[10]` line:

```c
	m[11].type = NM_TYPE_MENU;   m[11].text="Minimap Options...";
```

and change `newmenu_do3( NULL, TXT_OPTIONS, 11, m, options_menuset, NULL, 0, NULL );` to pass `12`.

In `options_menuset` (`menu.c:841-851`), add to the `EVENT_NEWMENU_SELECTED` switch after `case 10`:

```c
				case 11: minimap_config();		break;
```

If `minimap_config` is defined below `options_menuset` in the file, add a forward declaration above `options_menuset`:

```c
void minimap_config();
```

- [ ] **Step 3: Build**

Run the build command. Expected: clean link.

- [ ] **Step 4: Manual check — every knob works**

1. Options → Minimap Options: all items present; toggle "Show Minimap" on; exit menus (this writes the plr).
2. In-game: map appears. Re-enter the menu and try each position (all four corners + center — center overlays the reticle but stays readable), each size, each range (Far clearly shows more rooms), Rotation "North Up" (map stops rotating with the ship; ship marker rotates instead), Opacity 2 vs 10 (clearly more/less transparent under OpenGL).
3. Quit; confirm the plr file contains the chosen values.

- [ ] **Step 5: Commit**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/.claude/worktrees/hud-minimap
git add d1/main/menu.c
git commit -m "Add Minimap Options submenu under Options

Enable checkbox, position (4 corners + center), size, range and
rotation radio groups plus an opacity slider, applied on menu close
like the other option submenus. Enabling always starts the minimap
visible.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 6: Full verification pass

No new code — run the spec's acceptance matrix against the finished build and fix anything that fails (small fixes inline in this task; anything structural goes back to the owning task).

**Files:**
- None expected (fixes only if checks fail).

- [ ] **Step 1: Build from clean state**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/.claude/worktrees/hud-minimap/d1
rm -rf build
PATH=/c/Programs/msys64/mingw64/bin:$PATH CC=/c/Programs/msys64/mingw64/bin/gcc.exe CXX=/c/Programs/msys64/mingw64/bin/g++.exe cmake -B build -G Ninja -DCMAKE_BUILD_TYPE=RelWithDebInfo
PATH=/c/Programs/msys64/mingw64/bin:$PATH cmake --build build -j
```

Expected: full build succeeds end-to-end.

- [ ] **Step 2: Manual test matrix** (from the spec — check each):

1. SP Level 1: geometry matches surroundings, ship centered, heading-up tracks turns.
2. SP vertically twisty level (level 7+ or a shaft): smooth re-leveling, no snapping/spinning.
3. North-up mode: map stays level-fixed through loops, no axis flips while maneuvering.
4. All 5 positions × 3 sizes × 3 ranges; opacity slider visibly changes blend.
5. Mirror + minimap both enabled: both PiPs render in their correct rectangles (viewport-cache landmine); try mirror Large + minimap sizes.
6. 2-player LAN anarchy (two instances with `-udp` on localhost): both markers visible in FFA; cloaked player vanishes from the map; observer sees no minimap.
7. Demo: record a short demo with minimap on; playback shows no minimap and looks normal otherwise.
8. Death/respawn, endlevel exit sequence, Tab automap open/close: no bleed-through, automap unchanged (colors, F9/F10 depth keys).
9. Cockpit / status-bar / full-screen modes: PiP stays inside the 3D window.
10. Surround mode (if configured): minimap on the center monitor.
11. F4 with feature disabled: does nothing. F4 in demo playback: shows the demo percentage as before (existing handler).

- [ ] **Step 3: Software renderer spot-check (optional if no SW build available)**

If a non-OGL build is configured (`cmake -B build-sw -DOPENGL=OFF`), verify: map draws with dimmed (not blended) lines, opacity changes dimming, no crash. If no SW build is set up, note it in the PR description as untested.

- [ ] **Step 4: Update docs and finish**

- If any tuning constant changed during testing (`MINIMAP_TILT_ANG`, hops, distances, `MINIMAP_LEVEL_RATE`), update the spec's numbers.
- Commit any fixes with focused messages.
- Feature complete: hand off via the finishing-a-development-branch flow (PR to fork main; PR body notes the d2 port follow-up and, if applicable, the untested SW renderer).
