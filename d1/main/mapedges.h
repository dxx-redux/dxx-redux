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
