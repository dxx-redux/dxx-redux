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
