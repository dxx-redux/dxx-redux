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
