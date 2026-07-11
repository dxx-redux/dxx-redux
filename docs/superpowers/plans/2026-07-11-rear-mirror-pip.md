# Rear-View Mirror (Picture-in-Picture) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A menu-configurable rear-view mirror rendered picture-in-picture at the top of the game view; while enabled, the rear-view key only toggles the mirror instead of switching to full-screen rear view.

**Architecture:** One extra `render_frame(0)` per frame into a small sub-canvas of the 3D window, with a new `Mirror_view` global gating demo-record/lighting calls (extending the surround gates) and selecting a rear-view matrix whose right vector is negated — that negation horizontally flips every projected x, producing a true mirror image in both renderers. Settings are per-pilot (`PlayerCfg`) following the `StickyRearview` pattern; menu UI follows the Triple-Monitor Surround block in Graphics Options.

**Tech Stack:** Plain C, fixed-point maths (`fix`/`fixang`), newmenu UI, PhysFS pilot files, CMake + MSYS2 MinGW64 + Ninja.

**Spec:** `docs/superpowers/specs/2026-07-11-rear-mirror-pip-design.md` (approved). Read it if a requirement here seems ambiguous — the spec governs.

## Global Constraints

- d1 only (`d1/` tree). Do NOT touch `d2/`.
- Plain C, no floats in game logic; the codebase indents with tabs — match surrounding style exactly.
- No test suite exists: every task's gate is a clean build (exit 0, ends linking `d1x-redux.exe`). Pre-existing warnings (e.g. about `Players`) are noise — ignore them.
- Demo format compatibility: the mirror view must never record demo data (no double `newdemo_record_start_frame`, no rearview records from the key while the feature is on).
- Defaults: `MirrorMode=0`, `MirrorPos=1` (top center), `MirrorSize=1` (medium). Size divisors: Large=3, Medium=4, Small=6. Border color `BM_XRGB(6,6,6)`, thickness `max(2, base_h/360)`. Margin `max(2, base_h/64)`.
- Work in the worktree `C:\Users\Yermak\Projects\dxx-redux\.claude\worktrees\mirror`, branch `mirror`. Stage files explicitly (`git add <paths>`, never `-A`/`.`); end every commit message with the line:
  `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`

**Build command** (Git Bash; from the worktree's `d1/` directory):

```bash
cd /c/Users/Yermak/Projects/dxx-redux/.claude/worktrees/mirror/d1
# First time only (the worktree has no build dir yet):
PATH=/c/Programs/msys64/mingw64/bin:$PATH cmake -B build -G Ninja -DCMAKE_BUILD_TYPE=Release
# Every task:
PATH=/c/Programs/msys64/mingw64/bin:$PATH cmake --build build -j
```

Expected tail on success: `Linking CXX executable main\d1x-redux.exe` (exit code 0).

---

### Task 1: Pilot-file settings (MirrorMode / MirrorPos / MirrorSize)

**Files:**
- Modify: `d1/main/playsave.h:106` (struct fields)
- Modify: `d1/main/playsave.c:110` (defaults), `d1/main/playsave.c:432` (read), `d1/main/playsave.c:910` (write)

**Interfaces:**
- Consumes: nothing (first task).
- Produces: `PlayerCfg.MirrorMode` (ubyte 0/1), `PlayerCfg.MirrorPos` (ubyte 0=top left, 1=top center, 2=top right), `PlayerCfg.MirrorSize` (ubyte 0=small, 1=medium, 2=large) — guaranteed in-range after load. Tasks 2-4 read these.

- [ ] **Step 1: Configure the worktree build (first time only)**

Run the two build commands from the header (configure, then build) to prove the worktree compiles before any change. Expected: configure ends `Build files have been written`, build ends linking `d1x-redux.exe`.

- [ ] **Step 2: Add the struct fields**

In `d1/main/playsave.h`, directly after `ubyte StickyRearview; /* StickyRearview */` (line 106), add:

```c
	ubyte MirrorMode; /* RearMirror: R key toggles a PiP mirror instead of full-screen rear view */
	ubyte MirrorPos; /* RearMirror: 0=top left, 1=top center, 2=top right */
	ubyte MirrorSize; /* RearMirror: 0=small, 1=medium, 2=large */
```

- [ ] **Step 3: Add the defaults**

In `d1/main/playsave.c`, in `new_player_config()`, directly after `PlayerCfg.StickyRearview = 0; /* StickyRearview */` (line 110), add:

```c
	PlayerCfg.MirrorMode = 0; /* RearMirror */
	PlayerCfg.MirrorPos = 1; /* RearMirror */
	PlayerCfg.MirrorSize = 1; /* RearMirror */
```

- [ ] **Step 4: Add the read lines (with range clamping)**

In `d1/main/playsave.c`, in the pilot-file read loop, directly after the `STICKYREARVIEW` pair (lines 431-432), add (match the surrounding tab indentation):

```c
				if(!strcmp(word,"MIRRORMODE"))
					PlayerCfg.MirrorMode = atoi(line) ? 1 : 0;
				if(!strcmp(word,"MIRRORPOS")) {
					PlayerCfg.MirrorPos = atoi(line);
					if (PlayerCfg.MirrorPos > 2)
						PlayerCfg.MirrorPos = 1;
				}
				if(!strcmp(word,"MIRRORSIZE")) {
					PlayerCfg.MirrorSize = atoi(line);
					if (PlayerCfg.MirrorSize > 2)
						PlayerCfg.MirrorSize = 1;
				}
```

(`word` is the uppercased token, `line` the value — same convention as every neighboring line. The fields are `ubyte`, so negative file values wrap to >2 and hit the clamp.)

- [ ] **Step 5: Add the write lines**

In `d1/main/playsave.c`, in the `[toggles]` write block, directly after `PHYSFSX_printf(fout,"stickyrearview=%i\n",PlayerCfg.StickyRearview); /* StickyRearview */` (line 910), add:

```c
		PHYSFSX_printf(fout,"mirrormode=%i\n",PlayerCfg.MirrorMode); /* RearMirror */
		PHYSFSX_printf(fout,"mirrorpos=%i\n",PlayerCfg.MirrorPos); /* RearMirror */
		PHYSFSX_printf(fout,"mirrorsize=%i\n",PlayerCfg.MirrorSize); /* RearMirror */
```

- [ ] **Step 6: Build**

Run the build command. Expected: clean link, exit 0.

- [ ] **Step 7: Commit**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/.claude/worktrees/mirror
git add d1/main/playsave.h d1/main/playsave.c
git commit -m "Add rear-mirror pilot-file settings (MirrorMode/MirrorPos/MirrorSize)

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: Runtime state, R-key toggle, and render_frame mirror hooks

**Files:**
- Modify: `d1/main/game.h:158` (externs), `d1/main/game.c:826-858` (`check_rear_view`)
- Modify: `d1/main/render.c:1359` (flag), `d1/main/render.c:1447-1455` (gates), `d1/main/render.c:1478` (view matrix)

**Interfaces:**
- Consumes: `PlayerCfg.MirrorMode` (Task 1); existing `g3_set_player_view_matrix(vms_vector*, vms_matrix*, fix)` from `surround.h`; existing `Surround_view` global (−1 when no surround view is active).
- Produces: `int Mirror_visible` (defined in game.c, 1 = PiP shown while feature on; toggled by the rear-view key) and `int Mirror_view` (defined in render.c, 1 only while the mirror view is being rendered). Both externed in `game.h`. Task 3 sets `Mirror_view` around its `render_frame` call and reads `Mirror_visible`; Task 4 sets `Mirror_visible` on enable.

- [ ] **Step 1: Add the externs**

In `d1/main/game.h`, directly after `extern int Rear_view;           // if true, looking back.` (line 158), add:

```c
extern int Mirror_visible;      // rear mirror PiP shown (only meaningful while PlayerCfg.MirrorMode)
extern int Mirror_view;         // true while the PiP mirror view is being rendered this frame
```

- [ ] **Step 2: Define `Mirror_visible` and add the key branch**

In `d1/main/game.c`, directly above the `#define LEAVE_TIME 0x4000` line (line 826, just before `check_rear_view`), add:

```c
int Mirror_visible = 1;		//rear mirror PiP shown; the rear-view key toggles this while PlayerCfg.MirrorMode is on
```

Then in `check_rear_view()`, directly after the demo-playback early return

```c
	if (Newdemo_state == ND_STATE_PLAYBACK)
		return;
```

add:

```c
	if (PlayerCfg.MirrorMode) {		//mirror mode: the key only toggles the PiP mirror
		if (Controls.rear_view_count > 0) {
			Controls.rear_view_count = 0;
			Mirror_visible = !Mirror_visible;
		}
		return;
	}
```

(The early return means: no `Rear_view` flag, no `CM_REAR_VIEW` cockpit switch, no `newdemo_record_rearview()`/`restore_rearview()` records, and hold-to-peek/`StickyRearview` semantics are skipped entirely while the feature is on — exactly the spec's behavior. `Controls.rear_view_state` is deliberately ignored.)

- [ ] **Step 3: Define `Mirror_view` in render.c**

In `d1/main/render.c`, directly after `int Rear_view=0;` (line 1359), add:

```c
int Mirror_view=0;	//rendering the PiP mirror view (never set together with Rear_view)
```

- [ ] **Step 4: Extend the once-per-frame gates**

In `d1/main/render.c` inside `render_frame()` (lines 1447-1455), change

```c
	if ( Newdemo_state == ND_STATE_RECORDING )	{
		if (eye_offset >= 0 && Surround_view <= 0)	{	//in surround, only the first view records
			newdemo_record_start_frame(FrameTime );
			newdemo_record_viewer_object(Viewer);
		}
	}

	if (Surround_view <= 0)		//in surround, once per frame, not per view
		start_lighting_frame(Viewer);		//this is for ugly light-smoothing hack
```

to

```c
	if ( Newdemo_state == ND_STATE_RECORDING )	{
		if (eye_offset >= 0 && Surround_view <= 0 && !Mirror_view)	{	//only the first view of a frame records
			newdemo_record_start_frame(FrameTime );
			newdemo_record_viewer_object(Viewer);
		}
	}

	if (Surround_view <= 0 && !Mirror_view)		//once per frame, not per view
		start_lighting_frame(Viewer);		//this is for ugly light-smoothing hack
```

(The mirror renders after the main view with `Surround_view == -1`, so without `!Mirror_view` it would pass the `<= 0` check and double-record every demo frame.)

- [ ] **Step 5: Mirror view matrix (the flip)**

In `d1/main/render.c` (line 1478), change the rear-view branch head

```c
	if (Rear_view && (Viewer==ConsoleObject)) {
```

to

```c
	if ((Rear_view || Mirror_view) && (Viewer==ConsoleObject)) {
```

and inside that branch, between `vm_matrix_x_matrix(&viewm,&Viewer->orient,&headm);` and `g3_set_player_view_matrix(&Viewer_eye,&viewm,Render_zoom);`, add:

```c
		if (Mirror_view) {
			vm_vec_negate(&viewm.rvec);	//negated right vector flips every projected x: true mirror image
		}
```

IMPORTANT: keep the braces — `vm_vec_negate` is a `do{...}while(0);` macro that already ends in a semicolon (`d1/include/vecmat.h:186`), so an unbraced `if` body followed by `else` would not compile.

- [ ] **Step 6: Build**

Run the build command. Expected: clean link, exit 0. (The feature is still invisible — nothing sets `Mirror_view` yet.)

- [ ] **Step 7: Commit**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/.claude/worktrees/mirror
git add d1/main/game.h d1/main/game.c d1/main/render.c
git commit -m "Add mirror-mode key toggle and render_frame mirror hooks

While PlayerCfg.MirrorMode is on, the rear-view key toggles Mirror_visible
instead of engaging full-screen rear view. Mirror_view renders the rear view
with a negated right vector (true mirror image) and is excluded from the
once-per-frame demo-record and lighting calls.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: PiP render block and border in game_render_frame_mono

**Files:**
- Modify: `d1/main/gamerend.c:489-491` (between the main-view render block and `update_cockpits()`)

**Interfaces:**
- Consumes: `PlayerCfg.MirrorMode/MirrorPos/MirrorSize` (Task 1); `Mirror_visible`, `Mirror_view` via `game.h` (Task 2); existing `surround_enabled()` (surround.h), `Screen_3d_window`, `gr_init_sub_canvas`, `gr_set_current_canvas`, `gr_setcolor`, `gr_box` (inclusive right/bot edges, canvas-relative, clipped — see `d1/2d/box.c`), `BM_XRGB`, `render_frame(0)`, and the externs `Viewer`, `ConsoleObject`, `Player_is_dead`, `Endlevel_sequence`, `Newdemo_state`, `is_observer()` — all already in scope in gamerend.c (object.h arrives transitively; `Viewer` is already used at gamerend.c:356).
- Produces: the visible feature. No new symbols.

- [ ] **Step 1: Insert the mirror render block**

In `d1/main/gamerend.c`, in `game_render_frame_mono()`, directly after the closing brace of the main-view render (the `}` at line 489, after the `else { gr_set_current_canvas(&Screen_3d_window); render_frame(0); }` block) and BEFORE `update_cockpits();` (line 491), insert:

```c
	if (PlayerCfg.MirrorMode && Mirror_visible
		&& !Player_is_dead && !Endlevel_sequence
		&& Viewer == ConsoleObject
		&& Newdemo_state != ND_STATE_PLAYBACK
		&& !is_observer())
	{
		static grs_canvas mirror_canv;
		static const int mirror_divisor[3] = { 6, 4, 3 };	//small, medium, large
		int base_x = 0, base_w = Screen_3d_window.cv_bitmap.bm_w;
		int base_h = Screen_3d_window.cv_bitmap.bm_h;
		int size = PlayerCfg.MirrorSize;
		int mw, mh, mgn, mx, bt, i;

		if (surround_enabled())	{	//mirror lives on the center monitor
			base_w /= 3;
			base_x = base_w;
		}

		if (size > 2)
			size = 1;
		mw = base_w / mirror_divisor[size];
		mh = base_h / mirror_divisor[size];
		mgn = base_h / 64;
		if (mgn < 2)
			mgn = 2;
		if (PlayerCfg.MirrorPos == 0)		//top left
			mx = mgn;
		else if (PlayerCfg.MirrorPos == 2)	//top right
			mx = base_w - mw - mgn;
		else					//top center
			mx = (base_w - mw) / 2;

		gr_init_sub_canvas(&mirror_canv, &Screen_3d_window, base_x + mx, mgn, mw, mh);
		gr_set_current_canvas(&mirror_canv);
		Mirror_view = 1;
		render_frame(0);
		Mirror_view = 0;

		bt = base_h / 360;			//border, drawn inside the mirror's edges
		if (bt < 2)
			bt = 2;
		gr_setcolor(BM_XRGB(6,6,6));
		for (i = 0; i < bt; i++)
			gr_box(i, i, mw - 1 - i, mh - 1 - i);

		gr_set_current_canvas(&Screen_3d_window);
	}
```

- [ ] **Step 2: Build**

Run the build command. Expected: clean link, exit 0.

- [ ] **Step 3: Commit**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/.claude/worktrees/mirror
git add d1/main/gamerend.c
git commit -m "Render the rear mirror as picture-in-picture

One extra render_frame into a bordered sub-canvas at the top of the 3D
window (center monitor in surround), sized w/6, w/4 or w/3. Hidden during
death, observer mode, demo playback and the endlevel sequence.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

(Manual smoke test — optional, needs game data: the menu UI doesn't exist until Task 4, so to see the mirror now, set `mirrormode=1` in the pilot's `.plx` file `[toggles]` section, run `./build/main/d1x-redux.exe -hogdir <folder with descent.hog>`, and press R.)

---

### Task 4: Graphics Options menu UI

**Files:**
- Modify: `d1/main/menu.c:1232` (option-index statics), `d1/main/menu.c:1273/1276` (array sizes), `d1/main/menu.c:1319-1321` (menu items), `d1/main/menu.c:1363-1365` (apply block)

**Interfaces:**
- Consumes: `PlayerCfg.MirrorMode/MirrorPos/MirrorSize` (Task 1); `Mirror_visible`, `Rear_view`, `reset_rear_view()` via `game.h` (Task 2 / existing).
- Produces: user-facing configuration. No new symbols beyond the three `opt_gr_mirror*` statics.

- [ ] **Step 1: Add the option-index variables**

In `d1/main/menu.c`, directly after `int opt_gr_surround, opt_gr_surroundangle;` (line 1232), add:

```c
int opt_gr_mirror, opt_gr_mirrorpos, opt_gr_mirrorsize;
```

- [ ] **Step 2: Grow the item arrays**

In `graphics_config()` (line 1270), the mirror block adds 9 items. Change `newmenu_item m[22];` (OGL branch, line 1273) to `newmenu_item m[31];` and `newmenu_item m[11];` (non-OGL branch, line 1276) to `newmenu_item m[20];`.

- [ ] **Step 3: Add the menu items**

Still in `graphics_config()`, directly after the `#endif` of the `m[opt_gr_texfilt+GameCfg.TexFilt].value=1;` block (line 1319) and BEFORE the `m[nitems].type = NM_TYPE_TEXT; m[nitems].text = "Framerate"; nitems++;` line (line 1321), insert (groups 0 and 1 are taken by texture filtering and surround angle):

```c
	opt_gr_mirror = nitems;
	m[nitems].type = NM_TYPE_CHECK; m[nitems].text="Rear View Mirror"; m[nitems].value = PlayerCfg.MirrorMode; nitems++;
	m[nitems].type = NM_TYPE_TEXT; m[nitems].text = "Mirror Position:"; nitems++;
	opt_gr_mirrorpos = nitems;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Top Left"; m[nitems].value = (PlayerCfg.MirrorPos == 0); m[nitems].group = 2; nitems++;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Top Center"; m[nitems].value = (PlayerCfg.MirrorPos == 1); m[nitems].group = 2; nitems++;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Top Right"; m[nitems].value = (PlayerCfg.MirrorPos == 2); m[nitems].group = 2; nitems++;
	m[nitems].type = NM_TYPE_TEXT; m[nitems].text = "Mirror Size:"; nitems++;
	opt_gr_mirrorsize = nitems;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Small"; m[nitems].value = (PlayerCfg.MirrorSize == 0); m[nitems].group = 3; nitems++;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Medium"; m[nitems].value = (PlayerCfg.MirrorSize == 1); m[nitems].group = 3; nitems++;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "Large"; m[nitems].value = (PlayerCfg.MirrorSize == 2); m[nitems].group = 3; nitems++;
```

- [ ] **Step 4: Apply the values after the menu closes**

Still in `graphics_config()`, directly after the closing brace of the surround apply block (the `}` at line 1363, after `reset_cockpit();` / its enclosing braces) and BEFORE `PlayerCfg.maxFps=atoi(framerate_string);` (line 1365), insert:

```c
	{
		int old_mirror = PlayerCfg.MirrorMode;

		PlayerCfg.MirrorMode = m[opt_gr_mirror].value;
		if (m[opt_gr_mirrorpos].value)
			PlayerCfg.MirrorPos = 0;
		else if (m[opt_gr_mirrorpos+1].value)
			PlayerCfg.MirrorPos = 1;
		else if (m[opt_gr_mirrorpos+2].value)
			PlayerCfg.MirrorPos = 2;
		if (m[opt_gr_mirrorsize].value)
			PlayerCfg.MirrorSize = 0;
		else if (m[opt_gr_mirrorsize+1].value)
			PlayerCfg.MirrorSize = 1;
		else if (m[opt_gr_mirrorsize+2].value)
			PlayerCfg.MirrorSize = 2;

		if (PlayerCfg.MirrorMode && !old_mirror) {
			if (Rear_view)
				reset_rear_view();	//feature blocks full-screen rear view
			Mirror_visible = 1;		//always start visible on enable
		}
	}
```

(`Rear_view` can only be nonzero in-game, so `reset_rear_view()` — which touches the cockpit — never runs from the main menu. The radio groups guarantee exactly one `.value` is set, and Task 1's load clamping guarantees the initial radio state has exactly one selected.)

- [ ] **Step 5: Build**

Run the build command. Expected: clean link, exit 0.

- [ ] **Step 6: Commit**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/.claude/worktrees/mirror
git add d1/main/menu.c
git commit -m "Add Rear View Mirror settings to Graphics Options

Checkbox plus position (top left/center/right) and size (small/medium/large)
radio groups, applied on menu close like the surround options. Enabling the
mirror clears any active full-screen rear view and starts the mirror visible.

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

## Final verification (after all tasks)

1. Whole-branch code review (subagent-driven-development's final review step).
2. User acceptance run (needs game data, e.g. `-hogdir Q:/d1x-redux-1.1`), from the spec's testing section:
   - Menu: toggle Rear View Mirror; sweep 3 positions × 3 sizes; settings survive restart (pilot file).
   - **Flip correctness:** strafe left next to a landmark behind you — it must drift toward the mirror's **right** edge. Drifting left means the image is unflipped (regression).
   - R toggles only the mirror; with the feature off, classic rear view (tap-toggle + hold-to-peek with Sticky Rearview) is unchanged.
   - Full cockpit, status bar, F3-shrunken window; surround on (mirror on center monitor).
   - Demo record with mirror on → playback shows a normal single view (no triple-record, no mirror).
   - Death, observer mode, endlevel flyout, automap hide it; screenshots include it; HUD text draws over it.
3. Post-review fix (applied as commit 258bf0d): the default OGL build is **not** winding-safe — `ogl_start_frame` enables `GL_CULL_FACE` with CW front faces, so the flipped scene was culled wholesale. The mirror pass now disables GL culling while `Mirror_view` is set (`render.c`, directly after `g3_start_frame()`), and per-object demo records are gated `&& !Mirror_view` (`object.c`). Contingency for the **software** (non-OGL) build stays: if the acceptance run shows corrupted mirror geometry there, wrap the `vm_vec_negate` call in `#ifdef OGL` so software builds get an unflipped mirror — only on observed breakage.
