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
