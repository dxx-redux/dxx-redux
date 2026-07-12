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
