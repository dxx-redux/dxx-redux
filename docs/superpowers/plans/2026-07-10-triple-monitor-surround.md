# Triple-Monitor Surround Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render the in-mission 3D view across three 4K monitors (NVIDIA Surround, one 11520×2160 screen) as three seamlessly joined, yawed camera views with a configurable 150/180/270° total FOV, toggled from Graphics Options.

**Architecture:** Split the game window into three sub-canvases and call `render_frame()` once per monitor with the camera yawed by −A/0/+A (A = angle/3) and a per-view zoom that makes each view's horizontal FOV exactly A — adjacent frustum edge planes then coincide and geometry is continuous across bezels. All 2D layers (HUD, automap, briefings, titles) are confined to the center monitor. Spec: `docs/superpowers/specs/2026-07-10-triple-monitor-surround-design.md`.

**Tech Stack:** C (C90-style declarations), fixed-point math (`fix` 16.16, `fixang` where 0x10000 = 360°), the game's own 3d/2d libraries, CMake + MSYS2 MinGW64.

## Global Constraints

- d1 only (`d1/` tree). Do NOT touch `d2/`.
- Work on branch `surround-mode`; create it from the current HEAD of `bomb-flare-mode` at Task 1.
- The working tree has unrelated uncommitted changes (`d1/main/CMakeLists.txt`, `d1/misc/CMakeLists.txt`, untracked `d1/network.txt`, `CLAUDE.md`, `.serena/`). NEVER `git add -A` / `git add .` — every commit stages only the files named in its commit step.
- Build command (run from `d1/`, Git Bash):
  `PATH=/c/Programs/msys64/mingw64/bin:$PATH cmake --build build -j`
  Expected: exit code 0 (compiler warnings are normal in this codebase; new warnings in files you touched are not).
- There is no test framework in this repo. The red/green cycle here is: build must pass, then a scripted manual run check. Running the game needs the Descent data files (`descent.hog`/`descent.pig`); launch `./build/main/d1x-redux.exe`, adding `-hogdir <folder>` if data is not next to the exe. If you don't know the data folder, ask the user once and reuse it.
- For windowed surround testing on one monitor: in game, Options → Screen Resolution → Custom → 3840×720, windowed. This gives three 1280×720 (16:9) views.
- Code style: tabs for indentation, K&R-ish braces, C90 declarations at block start, match surrounding code.
- Every commit message ends with the trailer line:
  `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`
- Fixed-point conventions used throughout: `fixang` full circle = 0x10000, so per-view angle A(deg) → fixang = `0x10000 * A / 360` (50°=9102, 60°=10923, 90°=16384). `fix_sincos(a, &s, &c)` (maths/fixc.c) takes such an angle. The engine maps the screen edge to view-space `x/z = View_zoom / Window_scale.x` (see 3d/matrix.c `scale_matrix()`), so per-view zoom for hFOV A is `fixmul(tan(A/2), Window_scale.x)`.

---

### Task 1: Branch + config fields `SurroundMode` / `SurroundAngle`

**Files:**
- Modify: `d1/main/config.h:27-53` (Cfg struct)
- Modify: `d1/main/config.c` (defaults ~line 117, name strings ~line 67, parse ~line 230, validation ~line 238, write ~line 286)

**Interfaces:**
- Consumes: nothing new.
- Produces: `GameCfg.SurroundMode` (int, 0/1, default 0) and `GameCfg.SurroundAngle` (int, one of 150/180/270, default 180), persisted as `SurroundMode=`/`SurroundAngle=` lines in `descent.cfg`. All later tasks read these via `#include "config.h"` (most files already have it).

- [ ] **Step 1: Create the branch**

```bash
cd /c/Users/Yermak/Projects/dxx-redux && git checkout -b surround-mode
```

Expected: `Switched to a new branch 'surround-mode'` (uncommitted CMakeLists changes carry over — that's fine, don't stage them).

- [ ] **Step 2: Add the struct fields**

In `d1/main/config.h`, change the end of the Cfg struct:

```c
	int ClassicDepth;
	int BorderlessWindow;
	int SurroundMode;
	int SurroundAngle;
} __pack__ Cfg;
```

- [ ] **Step 3: Wire up config.c**

After line 67 (`static const char BorderlessWindowStr[] ="BorderlessWindow";`) add:

```c
static const char SurroundModeStr[] ="SurroundMode";
static const char SurroundAngleStr[] ="SurroundAngle";
```

After line 117 (`GameCfg.BorderlessWindow = 0;`) add:

```c
	GameCfg.SurroundMode = 0;
	GameCfg.SurroundAngle = 180;
```

After the `BorderlessWindowStr` parse branch (lines 229-230) add:

```c
			else if (!strcmp(token, SurroundModeStr))
				GameCfg.SurroundMode = strtol(value, NULL, 10);
			else if (!strcmp(token, SurroundAngleStr))
				GameCfg.SurroundAngle = strtol(value, NULL, 10);
```

(Indent to match the neighbouring `else if` branches — they sit inside `if (*ptr != '\0') {`.)

After line 238 (`if ( GameCfg.MusicVolume > 8 ) GameCfg.MusicVolume = 8;`) add:

```c
	if (GameCfg.SurroundMode)
		GameCfg.SurroundMode = 1;
	if (GameCfg.SurroundAngle != 150 && GameCfg.SurroundAngle != 180 && GameCfg.SurroundAngle != 270)
		GameCfg.SurroundAngle = 180;
```

After line 286 (`PHYSFSX_printf(infile, "%s=%i\n", BorderlessWindowStr, GameCfg.BorderlessWindow);`) add:

```c
	PHYSFSX_printf(infile, "%s=%i\n", SurroundModeStr, GameCfg.SurroundMode);
	PHYSFSX_printf(infile, "%s=%i\n", SurroundAngleStr, GameCfg.SurroundAngle);
```

- [ ] **Step 4: Build**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/d1 && PATH=/c/Programs/msys64/mingw64/bin:$PATH cmake --build build -j
```

Expected: exit 0.

- [ ] **Step 5: Verify persistence**

Run the game, quit from the main menu, then check the written config (it is in the PhysFS write dir — same folder the game reads `descent.cfg` from; find it with `grep -l SurroundMode`):

Expected: `descent.cfg` contains `SurroundMode=0` and `SurroundAngle=180`. Manually edit `SurroundAngle=999`, run + quit again, and confirm it was rewritten as `SurroundAngle=180` (validation works).

- [ ] **Step 6: Commit**

```bash
cd /c/Users/Yermak/Projects/dxx-redux && git add d1/main/config.h d1/main/config.c && git commit -m "Add SurroundMode and SurroundAngle config options

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 2: Surround core — `surround.h`, view state, player-view-matrix helper

**Files:**
- Create: `d1/main/surround.h`
- Modify: `d1/main/render.c` (includes ~line 30; new code after line 1357 `int Rear_view=0;`; edits inside `render_frame()` lines 1374-1424)

**Interfaces:**
- Consumes: `GameCfg.SurroundMode`/`GameCfg.SurroundAngle` (Task 1), `g3_set_view_matrix(vms_vector*, vms_matrix*, fix)` (3d.h), `fix_sincos(fix, fix*, fix*)` (maths.h), `Window_scale` (3d/globvars.c, via local extern), `vm_angles_2_matrix`/`vm_matrix_x_matrix` (vecmat.h).
- Produces (all later tasks use these exact signatures, declared in `d1/main/surround.h`):
  - `extern int Surround_view;` — -1 = off, 0/1/2 = left/center/right view being rendered
  - `int surround_enabled(void);`
  - `fixang surround_view_angle(void);` — per-monitor angle (total/3) as fixang
  - `void surround_ui_rect(int *x, int *w);` — x offset/width of the UI area on the real screen
  - `void surround_set_ui_canvas(void);` — set current canvas to UI area (center monitor; whole screen when off), clearing side monitors
  - `void g3_set_player_view_matrix(vms_vector *view_pos, vms_matrix *view_matrix, fix zoom);`

After this task, behavior is still 100% unchanged (nothing sets `Surround_view` ≥ 0 yet) — that's the point: a safe, reviewable checkpoint.

- [ ] **Step 1: Create `d1/main/surround.h`**

```c
/*
 * Triple-monitor surround rendering.
 * See docs/superpowers/specs/2026-07-10-triple-monitor-surround-design.md
 */

#ifndef _SURROUND_H
#define _SURROUND_H

#include "maths.h"
#include "vecmat.h"

extern int Surround_view;	// -1 = off, 0/1/2 = left/center/right view being rendered

int surround_enabled(void);
fixang surround_view_angle(void);
void surround_ui_rect(int *x, int *w);
void surround_set_ui_canvas(void);
void g3_set_player_view_matrix(vms_vector *view_pos, vms_matrix *view_matrix, fix zoom);

#endif
```

- [ ] **Step 2: Add includes to render.c**

After line 30 (`#include "game.h"`) add:

```c
#include "config.h"
#include "surround.h"
```

- [ ] **Step 3: Add state + helpers to render.c**

Directly after line 1357 (`int Rear_view=0;`) insert:

```c
int Surround_view = -1;		//which surround view is being rendered: -1=off, 0=left, 1=center, 2=right

static grs_canvas Surround_ui_canvas;

extern vms_vector Window_scale;		//3d/globvars.c; set by g3_start_frame() for the current canvas

int surround_enabled(void)
{
	return GameCfg.SurroundMode &&
		(GameCfg.SurroundAngle == 150 || GameCfg.SurroundAngle == 180 || GameCfg.SurroundAngle == 270);
}

fixang surround_view_angle(void)
{
	return (fixang)((0x10000L * (GameCfg.SurroundAngle / 3)) / 360);
}

void surround_ui_rect(int *x, int *w)
{
	if (surround_enabled()) {
		*w = grd_curscreen->sc_canvas.cv_bitmap.bm_w / 3;
		*x = *w;
	} else {
		*x = 0;
		*w = grd_curscreen->sc_canvas.cv_bitmap.bm_w;
	}
}

void surround_set_ui_canvas(void)
{
	int x, w;

	if (!surround_enabled()) {
		gr_set_current_canvas(NULL);
		return;
	}

	gr_set_current_canvas(NULL);
	gr_clear_canvas(BM_XRGB(0,0,0));	//blank the side monitors
	surround_ui_rect(&x, &w);
	gr_init_sub_canvas(&Surround_ui_canvas, &grd_curscreen->sc_canvas, x, 0, w, grd_curscreen->sc_canvas.cv_bitmap.bm_h);
	gr_set_current_canvas(&Surround_ui_canvas);
}

//set the view matrix for a player-eye view. When a surround view is active,
//yaw by -A/0/+A (A = per-monitor angle) and set zoom so the horizontal FOV is
//exactly A - adjacent frustum edges then coincide, making seams invisible.
//Must be called after g3_start_frame() (uses Window_scale of the current canvas).
void g3_set_player_view_matrix(vms_vector *view_pos, vms_matrix *view_matrix, fix zoom)
{
	vms_angvec av;
	vms_matrix rotm, viewm;
	fixang a;
	fix s, c;

	if (Surround_view < 0) {
		g3_set_view_matrix(view_pos, view_matrix, zoom);
		return;
	}

	a = surround_view_angle();

	av.p = av.b = 0;
	av.h = (fixang)((Surround_view - 1) * (int)a);
	vm_angles_2_matrix(&rotm, &av);
	vm_matrix_x_matrix(&viewm, view_matrix, &rotm);

	fix_sincos((fix)(a / 2), &s, &c);
	g3_set_view_matrix(view_pos, &viewm, fixmul(fixdiv(s, c), Window_scale.x));
}
```

- [ ] **Step 4: Convert render_frame() call sites and add once-per-frame guards**

In `render_frame()`:

Line 1374-1379 — demo recording must fire once per frame, not once per view. Change:

```c
	if ( Newdemo_state == ND_STATE_RECORDING )	{
		if (eye_offset >= 0 )	{
```

to:

```c
	if ( Newdemo_state == ND_STATE_RECORDING )	{
		if (eye_offset >= 0 && Surround_view <= 0)	{	//in surround, only the first view records
```

Line 1381 — same for the light-smoothing hack. Change:

```c
	start_lighting_frame(Viewer);		//this is for ugly light-smoothing hack
```

to:

```c
	if (Surround_view <= 0)		//in surround, once per frame, not per view
		start_lighting_frame(Viewer);		//this is for ugly light-smoothing hack
```

Lines 1410, 1420, 1422 — swap `g3_set_view_matrix` → `g3_set_player_view_matrix` (arguments unchanged):

```c
		g3_set_player_view_matrix(&Viewer_eye,&viewm,Render_zoom);
```
```c
		g3_set_player_view_matrix(&Viewer_eye,&Viewer->orient,fixdiv(Render_zoom,Zoom_factor));
```
```c
		g3_set_player_view_matrix(&Viewer_eye,&Viewer->orient,Render_zoom);
```

(1420 is inside `#ifdef JOHN_ZOOM` — convert it anyway so the debug path compiles consistently.)

- [ ] **Step 5: Build**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/d1 && PATH=/c/Programs/msys64/mingw64/bin:$PATH cmake --build build -j
```

Expected: exit 0. Watch for implicit-declaration warnings in render.c — there must be none for the new functions.

- [ ] **Step 6: Verify unchanged behavior**

Run the game, fly around a level for ~30 seconds, toggle rear view (R). Expected: identical to before (Surround_view is never set yet; helper passes straight through).

- [ ] **Step 7: Commit**

```bash
cd /c/Users/Yermak/Projects/dxx-redux && git add d1/main/surround.h d1/main/render.c && git commit -m "Add surround view state and player view matrix helper

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 3: Graphics Options — surround toggle + angle radios

**Files:**
- Modify: `d1/main/menu.c` (opt vars line 1229, `graphics_config()` lines 1268-1340, include after line 25)

**Interfaces:**
- Consumes: `GameCfg.SurroundMode`/`SurroundAngle` (Task 1); `surround_enabled()` (Task 2); `init_cockpit()`, `select_cockpit(int)`, `reset_cockpit()` (game.h — already declared); `PlayerCfg.PreferredCockpitMode` (playsave.h, already used in this file).
- Produces: user-visible settings UI. No new symbols.

- [ ] **Step 1: Add include**

After line 25 (`#include "game.h"`) add:

```c
#include "surround.h"
```

- [ ] **Step 2: Add option-index variables**

Line 1229, append to the existing declaration list:

```c
int opt_gr_texfilt, opt_gr_brightness, opt_gr_reticlemenu, opt_gr_alphafx, opt_gr_dynlightcolor, opt_gr_vsync, opt_gr_multisample, opt_gr_fpsindi, opt_gr_disablecockpit;
int opt_gr_classicdepth;
int opt_gr_surround, opt_gr_surroundangle;
```

- [ ] **Step 3: Grow the menu arrays**

In `graphics_config()` (lines 1270-1275) — 5 new items in both variants:

```c
#ifdef OGL
	newmenu_item m[22];
	int i = 0;
#else
	newmenu_item m[11];
#endif
```

- [ ] **Step 4: Add the menu items**

After the `opt_gr_disablecockpit` item (line 1307) and BEFORE the `#ifdef OGL / m[opt_gr_texfilt...` block (line 1308), insert:

```c
	opt_gr_surround = nitems;
	m[nitems].type = NM_TYPE_CHECK; m[nitems].text="Triple-Monitor Surround"; m[nitems].value = GameCfg.SurroundMode; nitems++;
	m[nitems].type = NM_TYPE_TEXT; m[nitems].text = "Surround Angle:"; nitems++;
	opt_gr_surroundangle = nitems;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "150 degrees"; m[nitems].value = (GameCfg.SurroundAngle == 150); m[nitems].group = 1; nitems++;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "180 degrees"; m[nitems].value = (GameCfg.SurroundAngle == 180); m[nitems].group = 1; nitems++;
	m[nitems].type = NM_TYPE_RADIO; m[nitems].text = "270 degrees"; m[nitems].value = (GameCfg.SurroundAngle == 270); m[nitems].group = 1; nitems++;
```

(`group = 1` — group 0 is taken by the Texture Filtering radios.)

- [ ] **Step 5: Apply the values after the menu closes**

After line 1335 (`PlayerCfg.DisableCockpit = m[opt_gr_disablecockpit].value;`) insert:

```c
	{
		int old_surround = surround_enabled();

		GameCfg.SurroundMode = m[opt_gr_surround].value;
		if (m[opt_gr_surroundangle].value)
			GameCfg.SurroundAngle = 150;
		else if (m[opt_gr_surroundangle+1].value)
			GameCfg.SurroundAngle = 180;
		else if (m[opt_gr_surroundangle+2].value)
			GameCfg.SurroundAngle = 270;

		if (surround_enabled() != old_surround) {
			if (surround_enabled())
				init_cockpit();		//Task 4 makes this force the full-screen HUD style
			else
				select_cockpit(PlayerCfg.PreferredCockpitMode);
			reset_cockpit();
		}
	}
```

(`init_cockpit()` no-ops outside the game screen — safe from the main menu.)

- [ ] **Step 6: Build**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/d1 && PATH=/c/Programs/msys64/mingw64/bin:$PATH cmake --build build -j
```

Expected: exit 0.

- [ ] **Step 7: Verify**

Run → Options → Graphics Options. Expected: "Triple-Monitor Surround" checkbox and the three angle radios, one selected (180 by default); selecting another radio deselects the previous; Texture Filtering radios unaffected. Enable + pick 270, leave menu, quit game; `descent.cfg` shows `SurroundMode=1`, `SurroundAngle=270`. (The game view doesn't change yet — the render loop comes in Task 5.)

- [ ] **Step 8: Commit**

```bash
cd /c/Users/Yermak/Projects/dxx-redux && git add d1/main/menu.c && git commit -m "Add Triple-Monitor Surround toggle to Graphics Options

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 4: Force full-screen HUD cockpit while surround is on

**Files:**
- Modify: `d1/main/game.c` (`init_cockpit()` ~line 190; include after line 31)

**Interfaces:**
- Consumes: `surround_enabled()` (Task 2); `PlayerCfg.CurrentCockpitMode`, `CM_*` constants (already used here).
- Produces: guarantee used by Task 5 — while surround is enabled, `PlayerCfg.CurrentCockpitMode` is never a cockpit-art mode (except CM_LETTERBOX during endlevel), so `render_gauges()`/cockpit art never draw.

- [ ] **Step 1: Add include**

After line 31 (`#include "game.h"`) add:

```c
#include "surround.h"
```

- [ ] **Step 2: Add the override in init_cockpit()**

After the observer check (lines 190-191):

```c
	if (is_observer() && !can_draw_observer_cockpit())
		PlayerCfg.CurrentCockpitMode = CM_FULL_SCREEN;

	if (surround_enabled() && PlayerCfg.CurrentCockpitMode != CM_LETTERBOX)
		PlayerCfg.CurrentCockpitMode = CM_FULL_SCREEN;	//cockpit art can't stretch across 3 monitors
```

(CM_LETTERBOX stays — the endlevel letterbox band is still correct in surround; the three views render inside it. This also maps CM_REAR_VIEW to full screen: rear view still works, drawn full-window.)

- [ ] **Step 3: Build**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/d1 && PATH=/c/Programs/msys64/mingw64/bin:$PATH cmake --build build -j
```

Expected: exit 0.

- [ ] **Step 4: Verify**

Run, start a game, select full cockpit (F3 cycling), then Options → Graphics Options → enable surround → back to game. Expected: view is full-screen HUD style (no cockpit art). Disable surround in the menu. Expected: cockpit art returns (via the Task 3 restore path). 

- [ ] **Step 5: Commit**

```bash
cd /c/Users/Yermak/Projects/dxx-redux && git add d1/main/game.c && git commit -m "Force full-screen HUD cockpit while surround mode is on

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 5: Three-view render loop + HUD on center monitor

**Files:**
- Modify: `d1/main/gamerend.c` (`game_render_frame_mono()` lines 467-496; include after line 38)

**Interfaces:**
- Consumes: `Surround_view`, `surround_enabled()` (Task 2); `gr_init_sub_canvas` (gr.h); `Screen_3d_window` (game.h).
- Produces: the visible feature. No new symbols.

- [ ] **Step 1: Add include**

After line 38 (`#include "game.h"`) add:

```c
#include "surround.h"
```

- [ ] **Step 2: Replace the single render with the three-view loop**

Replace lines 469-471:

```c
	gr_set_current_canvas(&Screen_3d_window);
	
	render_frame(0);
```

with:

```c
	if (surround_enabled())	{
		static grs_canvas surround_canv;
		int vw = Screen_3d_window.cv_bitmap.bm_w / 3;
		int vh = Screen_3d_window.cv_bitmap.bm_h;
		int i;

		for (i = 0; i < 3; i++)	{
			int w = (i == 2) ? (Screen_3d_window.cv_bitmap.bm_w - 2 * vw) : vw;	//last view absorbs the rounding remainder

			gr_init_sub_canvas(&surround_canv, &Screen_3d_window, i * vw, 0, w, vh);
			gr_set_current_canvas(&surround_canv);
			Surround_view = i;
			render_frame(0);
		}
		Surround_view = -1;
	} else	{
		gr_set_current_canvas(&Screen_3d_window);

		render_frame(0);
	}
```

- [ ] **Step 3: Point the HUD at the center monitor**

Replace line 488 (`gr_set_current_canvas(&Screen_3d_window);` — the one right before `game_draw_hud_stuff();`):

```c
	if (surround_enabled())	{
		static grs_canvas surround_hud_canv;
		int vw = Screen_3d_window.cv_bitmap.bm_w / 3;

		gr_init_sub_canvas(&surround_hud_canv, &Screen_3d_window, vw, 0, vw, Screen_3d_window.cv_bitmap.bm_h);
		gr_set_current_canvas(&surround_hud_canv);
	} else
		gr_set_current_canvas(&Screen_3d_window);
```

(`show_netplayerinfo()` after it inherits the same canvas — intended: it's HUD-class UI.)

- [ ] **Step 4: Build**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/d1 && PATH=/c/Programs/msys64/mingw64/bin:$PATH cmake --build build -j
```

Expected: exit 0.

- [ ] **Step 5: Verify — the core feature check**

Run windowed 3840×720 (Options → Screen Resolution → Custom 3840×720, windowed). Enable surround, 180°:

1. Three side-by-side views; the world continues across the two boundaries.
2. **Seam check:** park facing a long straight wall/door edge that crosses a boundary; the line must stay straight (no kink, no gap, no duplicated strip). Yaw slowly: objects must cross boundaries smoothly.
3. HUD, reticle, weapon text: middle third only.
4. Rear view (R): wraparound rear panorama, still seamless.
5. Repeat the seam check at 150° and 270°.
6. Toggle surround off in-game: instantly back to one normal view.

If seams show a kink, the per-view hFOV ≠ yaw step — re-check `g3_set_player_view_matrix` math (Task 2) before proceeding.

- [ ] **Step 6: Commit**

```bash
cd /c/Users/Yermak/Projects/dxx-redux && git add d1/main/gamerend.c && git commit -m "Render three yawed views in surround mode with centered HUD

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 6: Endlevel sequence through the helper

**Files:**
- Modify: `d1/main/endlevel.c` (lines 856, 1024, 1027; include after line 44)

**Interfaces:**
- Consumes: `g3_set_player_view_matrix` (Task 2 — exact signature `void g3_set_player_view_matrix(vms_vector *, vms_matrix *, fix)`).
- Produces: endlevel flyout renders in surround. No new symbols.

- [ ] **Step 1: Add include**

After line 44 (`#include "game.h"`) add:

```c
#include "surround.h"
```

- [ ] **Step 2: Convert the three view-matrix call sites**

Line 856 in `render_external_scene()`:

```c
	g3_set_player_view_matrix(&Viewer->pos,&Viewer->orient,Render_zoom);
```

Lines 1024 and 1027 in `endlevel_render_mine()`:

```c
	if (Endlevel_sequence == EL_LOOKBACK) {
		vms_matrix headm,viewm;
		vms_angvec angles = {0,0,0x7fff};

		vm_angles_2_matrix(&headm,&angles);
		vm_matrix_x_matrix(&viewm,&Viewer->orient,&headm);
		g3_set_player_view_matrix(&Viewer_eye,&viewm,Render_zoom);
	}
	else
		g3_set_player_view_matrix(&Viewer_eye,&Viewer->orient,Render_zoom);
```

(These run inside the per-view `render_frame()` calls — `render_frame` delegates to `render_endlevel_frame` while `Surround_view` is set, so each view gets its own yaw.)

- [ ] **Step 3: Build**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/d1 && PATH=/c/Programs/msys64/mingw64/bin:$PATH cmake --build build -j
```

Expected: exit 0.

- [ ] **Step 4: Verify**

With surround on (windowed test setup), play level 1 to the end: cheats `gabbagabbahey` then `racerx` (invulnerability), destroy the reactor, fly out the exit tunnel. Expected: the tunnel flight, look-back view, and outside terrain/starfield all span the three views continuously (letterbox band is normal); no view shows a duplicated center image.

- [ ] **Step 5: Commit**

```bash
cd /c/Users/Yermak/Projects/dxx-redux && git add d1/main/endlevel.c && git commit -m "Route endlevel view matrices through surround helper

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 7: Automap on the center monitor

**Files:**
- Modify: `d1/main/automap.c` (`draw_automap()` lines 348-370, `automap_apply_changes`-era init lines 711-714; include after line 31)

**Interfaces:**
- Consumes: `surround_set_ui_canvas()`, `surround_ui_rect(int*, int*)` (Task 2); `GWIDTH`/`GHEIGHT` (gr.h — current-canvas macros).
- Produces: none.

- [ ] **Step 1: Add include**

After line 31 (`#include "game.h"`) add:

```c
#include "surround.h"
```

- [ ] **Step 2: Route the background/text through the UI canvas**

In `draw_automap()`, replace lines 348-370. Old:

```c
	gr_set_current_canvas(NULL);
	show_fullscr(&am->automap_background);
```

New (note every `SWIDTH`→`GWIDTH`, `SHEIGHT`→`GHEIGHT` in the text lines — GWIDTH/GHEIGHT are current-canvas macros, so these coordinates become canvas-relative; identical values when surround is off):

```c
	surround_set_ui_canvas();
	show_fullscr(&am->automap_background);
	gr_set_curfont(HUGE_FONT);
	gr_set_fontcolor(BM_XRGB(20, 20, 20), -1);
	if (!MacHog)
		gr_string((GWIDTH/8), (GHEIGHT/16), TXT_AUTOMAP);
	else
		gr_string(80*(GWIDTH/640.0), 36*(GHEIGHT/480.0), TXT_AUTOMAP);
	gr_set_curfont(GAME_FONT);
	gr_set_fontcolor(BM_XRGB(20, 20, 20), -1);
	if (!MacHog)
	{
		gr_string((GWIDTH/4.923), (GHEIGHT/1.126), TXT_TURN_SHIP);
		gr_string((GWIDTH/4.923), (GHEIGHT/1.083), TXT_SLIDE_UPDOWN);
		gr_string((GWIDTH/4.923), (GHEIGHT/1.043), "F9/F10 Changes viewing distance");
	}
	else
	{
		// for the Mac automap they're shown up the top, hence the different layout
		gr_string(265*(GWIDTH/640.0), 27*(GHEIGHT/480.0), TXT_TURN_SHIP);
		gr_string(265*(GWIDTH/640.0), 44*(GHEIGHT/480.0), TXT_SLIDE_UPDOWN);
		gr_string(265*(GWIDTH/640.0), 61*(GHEIGHT/480.0), "F9/F10 Changes viewing distance");
	}
```

(The font-color and TXT lines are unchanged content — only SWIDTH/SHEIGHT swapped.)

- [ ] **Step 3: Center the 3D map view canvas**

Replace lines 711-714:

```c
	if (!MacHog)
		gr_init_sub_canvas(&am->automap_view, &grd_curscreen->sc_canvas, (SWIDTH/23), (SHEIGHT/6), (SWIDTH/1.1), (SHEIGHT/1.45));
	else
		gr_init_sub_canvas(&am->automap_view, &grd_curscreen->sc_canvas, 38*(SWIDTH/640.0), 77*(SHEIGHT/480.0), 564*(SWIDTH/640.0), 381*(SHEIGHT/480.0));
```

with:

```c
	{
		int ux, uw;

		surround_ui_rect(&ux, &uw);
		if (!MacHog)
			gr_init_sub_canvas(&am->automap_view, &grd_curscreen->sc_canvas, ux+(uw/23), (SHEIGHT/6), (uw/1.1), (SHEIGHT/1.45));
		else
			gr_init_sub_canvas(&am->automap_view, &grd_curscreen->sc_canvas, ux+38*(uw/640.0), 77*(SHEIGHT/480.0), 564*(uw/640.0), 381*(SHEIGHT/480.0));
	}
```

- [ ] **Step 4: Build**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/d1 && PATH=/c/Programs/msys64/mingw64/bin:$PATH cmake --build build -j
```

Expected: exit 0.

- [ ] **Step 5: Verify**

Surround on, in game, press Tab. Expected: automap background, labels, and rotating map confined to the middle third; side thirds solid black (no stale game frame — `surround_set_ui_canvas` clears each draw). Surround off: automap identical to before the change.

- [ ] **Step 6: Commit**

```bash
cd /c/Users/Yermak/Projects/dxx-redux && git add d1/main/automap.c && git commit -m "Center automap on middle monitor in surround mode

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 8: Briefings and title screens on the center monitor

**Files:**
- Modify: `d1/main/titles.c` (lines 112, 684-687, 796, 801-804, 1068, 1147; include after line 28)

**Interfaces:**
- Consumes: `surround_set_ui_canvas()` (Task 2); `GWIDTH`/`GHEIGHT` (gr.h).
- Produces: none.

Briefing text positions already adapt: `rescale_x/y` (titles.c:64-72) use `GWIDTH`/`GHEIGHT`, which follow the current canvas. Only the canvas selection and two `SWIDTH`-based bitmap scale spots need changing.

- [ ] **Step 1: Add include**

After line 28 (`#include "gr.h"`) add:

```c
#include "surround.h"
```

- [ ] **Step 2: Swap the canvas at the three draw/init sites**

Line 112 (title screen EVENT_WINDOW_DRAW), line 1068 (briefing EVENT_WINDOW_DRAW), line 1147 (`do_briefing_screens` init) — each is `gr_set_current_canvas( NULL );` / `gr_set_current_canvas(NULL);`. Replace each with:

```c
			surround_set_ui_canvas();
```

(match the indentation at each site; `show_fullscr(&ts->title_bm)` / `show_fullscr(&br->background)` then stretch to the UI canvas.)

- [ ] **Step 3: Make the two bitmap-scale spots canvas-based**

`show_animated_bitmap()` lines 684-687 — replace SWIDTH/SHEIGHT with GWIDTH/GHEIGHT:

```c
	if (((float)GWIDTH/320) < ((float)GHEIGHT/200))
		scale = ((float)GWIDTH/320);
	else
		scale = ((float)GHEIGHT/200);
```

`show_briefing_bitmap()` line 796:

```c
	bitmap_canv = gr_create_sub_canvas(grd_curcanv, rescale_x(220), rescale_y(55), (bmp->bm_w*(GWIDTH/(HIRESMODE ? 640 : 320))),(bmp->bm_h*(GHEIGHT/(HIRESMODE ? 480 : 200))));
```

and lines 801-804:

```c
	if (((float)GWIDTH/(HIRESMODE ? 640 : 320)) < ((float)GHEIGHT/(HIRESMODE ? 480 : 200)))
		scale = ((float)GWIDTH/(HIRESMODE ? 640 : 320));
	else
		scale = ((float)GHEIGHT/(HIRESMODE ? 480 : 200));
```

CAUTION: in `show_briefing_bitmap()`, make the GWIDTH/GHEIGHT-based `scale` computation happen BEFORE `gr_set_current_canvas(bitmap_canv)` (line 798) or after the restore (line 810) — GWIDTH must refer to the briefing canvas, not the small bitmap sub-canvas. Move lines 801-804 to just before line 797 (`curcanv_save = grd_curcanv;`), keeping the `#ifdef OGL` guard:

```c
	grs_canvas	*curcanv_save, *bitmap_canv;
#ifdef OGL
	float scale = 1.0;

	if (((float)GWIDTH/(HIRESMODE ? 640 : 320)) < ((float)GHEIGHT/(HIRESMODE ? 480 : 200)))
		scale = ((float)GWIDTH/(HIRESMODE ? 640 : 320));
	else
		scale = ((float)GHEIGHT/(HIRESMODE ? 480 : 200));
#endif

	bitmap_canv = gr_create_sub_canvas(grd_curcanv, rescale_x(220), rescale_y(55), (bmp->bm_w*(GWIDTH/(HIRESMODE ? 640 : 320))),(bmp->bm_h*(GHEIGHT/(HIRESMODE ? 480 : 200))));
	curcanv_save = grd_curcanv;
	gr_set_current_canvas(bitmap_canv);

#ifdef OGL
	ogl_ubitmapm_cs(0,0,bmp->bm_w*scale,bmp->bm_h*scale,bmp,255,F1_0);
#else
	gr_bitmapm(0, 0, bmp);
#endif
	gr_set_current_canvas(curcanv_save);

	d_free(bitmap_canv);
```

(This replaces the whole body between the declarations and `d_free` — same statements, reordered, SWIDTH/SHEIGHT→GWIDTH/GHEIGHT.)

- [ ] **Step 4: Build**

```bash
cd /c/Users/Yermak/Projects/dxx-redux/d1 && PATH=/c/Programs/msys64/mingw64/bin:$PATH cmake --build build -j
```

Expected: exit 0.

- [ ] **Step 5: Verify**

Surround on: start a New Game → briefing screens: background, text, spinning robot, and animated door bitmaps all inside the middle third, sides black; advance through several screens. Quit to launcher and relaunch to see the title/logo screens centered the same way. Surround off: pixel-identical to before (GWIDTH == SWIDTH on a full-screen canvas).

- [ ] **Step 6: Commit**

```bash
cd /c/Users/Yermak/Projects/dxx-redux && git add d1/main/titles.c && git commit -m "Center briefing and title screens in surround mode

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

---

### Task 9: Full verification sweep (spec checklist)

**Files:** none expected; fix-up commits only if a check fails.

**Interfaces:** none.

- [ ] **Step 1: Run the complete spec checklist** (windowed 3840×720 unless noted), all with surround enabled at 180° unless noted:

1. Seam check at 150°, 180°, 270° (straight wall edge across both boundaries; slow yaw).
2. Rear view: wraparound rear, seamless, HUD label behavior normal.
3. Automap: centered, sides black.
4. Menus in-game and main menu: centered, unstretched.
5. Briefing + title screens: centered.
6. Demo: record ~10s (F5 twice), play it back from the main menu. Expected: playback runs at normal speed (no triple-time bug) and renders in surround; the same demo file also plays with surround off as a normal single view.
7. Cockpit: full-cockpit selected → enable surround → full-screen HUD; disable → cockpit art restored.
8. F3 view size: shrink/grow the game window — three slices track the window.
9. Screenshot key: full 3-view image captured.
10. Toggle surround on/off repeatedly mid-game: no crash, no stuck view state (`Surround_view` visible only as normal rendering).
11. Endlevel flyout (cheats `gabbagabbahey`, `racerx`): continuous across views.
12. Multiplayer sanity (optional if no second instance available): host a LAN game alone; HUD/score overlays centered.

- [ ] **Step 2: Report results**

Record pass/fail per item in the task notes. Any FAIL: fix within the owning task's file(s), rebuild, re-verify, commit as `Fix <symptom> in surround mode` with the standard trailer.

- [ ] **Step 3: Final validation on the real rig (user)**

Ask the user to run on the 11520×2160 NVIDIA Surround setup at 270° and confirm: seams invisible on physical bezels, HUD on center panel, performance acceptable. This is the acceptance gate for the feature.
