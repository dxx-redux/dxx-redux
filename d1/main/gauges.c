/*
THE COMPUTER CODE CONTAINED HEREIN IS THE SOLE PROPERTY OF PARALLAX
SOFTWARE CORPORATION ("PARALLAX").  PARALLAX, IN DISTRIBUTING THE CODE TO
END-USERS, AND SUBJECT TO ALL OF THE TERMS AND CONDITIONS HEREIN, GRANTS A
ROYALTY-FREE, PERPETUAL LICENSE TO SUCH END-USERS FOR USE BY SUCH END-USERS
IN USING, DISPLAYING,  AND CREATING DERIVATIVE WORKS THEREOF, SO LONG AS
SUCH USE, DISPLAY OR CREATION IS FOR NON-COMMERCIAL, ROYALTY OR REVENUE
FREE PURPOSES.  IN NO EVENT SHALL THE END-USER USE THE COMPUTER CODE
CONTAINED HEREIN FOR REVENUE-BEARING PURPOSES.  THE END-USER UNDERSTANDS
AND AGREES TO THE TERMS HEREIN AND ACCEPTS THE SAME BY USE OF THIS FILE.
COPYRIGHT 1993-1998 PARALLAX SOFTWARE CORPORATION.  ALL RIGHTS RESERVED.
*/

/*
 *
 * Inferno gauge drivers
 *
 */

#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <stdarg.h>
#include <stdbool.h>

#include "inferno.h"
#include "game.h"
#include "screens.h"
#include "gauges.h"
#include "physics.h"
#include "dxxerror.h"
#include "menu.h"
#include "collide.h"
#include "newdemo.h"
#include "player.h"
#include "gamefont.h"
#include "hostage.h"
#include "bm.h"
#include "text.h"
#include "powerup.h"
#include "sounds.h"
#include "multi.h"
#include "endlevel.h"
#include "wall.h"
#include "text.h"
#include "render.h"
#include "piggy.h"
#include "laser.h"
#include "playsave.h"
#include "rle.h"
#include "byteswap.h"
#include "cntrlcen.h"
#ifdef OGL
#include "ogl_init.h"
#endif
#include "net_udp.h"
#include "scores.h"
#include <math.h>

//bitmap numbers for gauges
#define GAUGE_SHIELDS			0		//0..9, in decreasing order (100%,90%...0%)
#define GAUGE_INVULNERABLE		10		//10..19
#define N_INVULNERABLE_FRAMES		10
#define GAUGE_SPEED		   	20		//unused
#define GAUGE_ENERGY_LEFT		21
#define GAUGE_ENERGY_RIGHT		22
#define GAUGE_NUMERICAL			23
#define GAUGE_BLUE_KEY			24
#define GAUGE_GOLD_KEY			25
#define GAUGE_RED_KEY			26
#define GAUGE_BLUE_KEY_OFF		27
#define GAUGE_GOLD_KEY_OFF		28
#define GAUGE_RED_KEY_OFF		29
#define SB_GAUGE_BLUE_KEY		30
#define SB_GAUGE_GOLD_KEY		31
#define SB_GAUGE_RED_KEY		32
#define SB_GAUGE_BLUE_KEY_OFF		33
#define SB_GAUGE_GOLD_KEY_OFF		34
#define SB_GAUGE_RED_KEY_OFF		35
#define SB_GAUGE_ENERGY			36
#define GAUGE_LIVES			37
#define GAUGE_SHIPS			38
#define GAUGE_SHIPS_LAST		45
#define RETICLE_CROSS			46
#define RETICLE_PRIMARY			48
#define RETICLE_SECONDARY		51
#define RETICLE_LAST			55
#define GAUGE_HOMING_WARNING_ON	56
#define GAUGE_HOMING_WARNING_OFF	57
#define SML_RETICLE_CROSS		58
#define SML_RETICLE_PRIMARY		60
#define SML_RETICLE_SECONDARY		63
#define SML_RETICLE_LAST		67
#define KEY_ICON_BLUE			68
#define KEY_ICON_YELLOW			69
#define KEY_ICON_RED			70

//Coordinats for gauges
// cockpit keys

#define GAUGE_BLUE_KEY_X_L		45
#define GAUGE_BLUE_KEY_Y_L		152
#define GAUGE_BLUE_KEY_X_H		91
#define GAUGE_BLUE_KEY_Y_H		374
#define GAUGE_BLUE_KEY_X		(HIRESMODE?GAUGE_BLUE_KEY_X_H:GAUGE_BLUE_KEY_X_L)
#define GAUGE_BLUE_KEY_Y		(HIRESMODE?GAUGE_BLUE_KEY_Y_H:GAUGE_BLUE_KEY_Y_L)

#define GAUGE_GOLD_KEY_X_L		44
#define GAUGE_GOLD_KEY_Y_L		162
#define GAUGE_GOLD_KEY_X_H		89
#define GAUGE_GOLD_KEY_Y_H		395
#define GAUGE_GOLD_KEY_X		(HIRESMODE?GAUGE_GOLD_KEY_X_H:GAUGE_GOLD_KEY_X_L)
#define GAUGE_GOLD_KEY_Y		(HIRESMODE?GAUGE_GOLD_KEY_Y_H:GAUGE_GOLD_KEY_Y_L)

#define GAUGE_RED_KEY_X_L		43
#define GAUGE_RED_KEY_Y_L		172
#define GAUGE_RED_KEY_X_H		87
#define GAUGE_RED_KEY_Y_H		417
#define GAUGE_RED_KEY_X			(HIRESMODE?GAUGE_RED_KEY_X_H:GAUGE_RED_KEY_X_L)
#define GAUGE_RED_KEY_Y			(HIRESMODE?GAUGE_RED_KEY_Y_H:GAUGE_RED_KEY_Y_L)

// status bar keys

#define SB_GAUGE_KEYS_X_L		11
#define SB_GAUGE_KEYS_X_H		26
#define SB_GAUGE_KEYS_X			(HIRESMODE?SB_GAUGE_KEYS_X_H:SB_GAUGE_KEYS_X_L)

#define SB_GAUGE_BLUE_KEY_Y_L		153
#define SB_GAUGE_GOLD_KEY_Y_L		169
#define SB_GAUGE_RED_KEY_Y_L		185

#define SB_GAUGE_BLUE_KEY_Y_H		390
#define SB_GAUGE_GOLD_KEY_Y_H		422
#define SB_GAUGE_RED_KEY_Y_H		454

#define SB_GAUGE_BLUE_KEY_Y		(HIRESMODE?SB_GAUGE_BLUE_KEY_Y_H:SB_GAUGE_BLUE_KEY_Y_L)
#define SB_GAUGE_GOLD_KEY_Y		(HIRESMODE?SB_GAUGE_GOLD_KEY_Y_H:SB_GAUGE_GOLD_KEY_Y_L)
#define SB_GAUGE_RED_KEY_Y		(HIRESMODE?SB_GAUGE_RED_KEY_Y_H:SB_GAUGE_RED_KEY_Y_L)

// cockpit enery gauges

#define LEFT_ENERGY_GAUGE_X_L 	70
#define LEFT_ENERGY_GAUGE_Y_L 	131
#define LEFT_ENERGY_GAUGE_W_L 	64
#define LEFT_ENERGY_GAUGE_H_L 	8

#define LEFT_ENERGY_GAUGE_X_H 	137
#define LEFT_ENERGY_GAUGE_Y_H 	314
#define LEFT_ENERGY_GAUGE_W_H 	133
#define LEFT_ENERGY_GAUGE_H_H 	21

#define LEFT_ENERGY_GAUGE_X 	(HIRESMODE?LEFT_ENERGY_GAUGE_X_H:LEFT_ENERGY_GAUGE_X_L)
#define LEFT_ENERGY_GAUGE_Y 	(HIRESMODE?LEFT_ENERGY_GAUGE_Y_H:LEFT_ENERGY_GAUGE_Y_L)
#define LEFT_ENERGY_GAUGE_W 	(HIRESMODE?LEFT_ENERGY_GAUGE_W_H:LEFT_ENERGY_GAUGE_W_L)
#define LEFT_ENERGY_GAUGE_H 	(HIRESMODE?LEFT_ENERGY_GAUGE_H_H:LEFT_ENERGY_GAUGE_H_L)

#define RIGHT_ENERGY_GAUGE_X 	(HIRESMODE?380:190)
#define RIGHT_ENERGY_GAUGE_Y 	(HIRESMODE?314:131)
#define RIGHT_ENERGY_GAUGE_W 	(HIRESMODE?133:64)
#define RIGHT_ENERGY_GAUGE_H 	(HIRESMODE?21:8)

// sb energy gauge

#define SB_ENERGY_GAUGE_X 		(HIRESMODE?196:98)
#define SB_ENERGY_GAUGE_Y 		(HIRESMODE?390:155)
#define SB_ENERGY_GAUGE_W 		(HIRESMODE?32:16)
#define SB_ENERGY_GAUGE_H 		(HIRESMODE?82:41)

#define SB_ENERGY_NUM_X 		(SB_ENERGY_GAUGE_X+(HIRESMODE?4:2))
#define SB_ENERGY_NUM_Y 		(HIRESMODE?457:190)

#define SHIELD_GAUGE_X 			(HIRESMODE?292:146)
#define SHIELD_GAUGE_Y			(HIRESMODE?374:155)
#define SHIELD_GAUGE_W 			(HIRESMODE?70:35)
#define SHIELD_GAUGE_H			(HIRESMODE?77:32)

#define SHIP_GAUGE_X 			(SHIELD_GAUGE_X+(HIRESMODE?11:5))
#define SHIP_GAUGE_Y			(SHIELD_GAUGE_Y+(HIRESMODE?10:5))

#define SB_SHIELD_GAUGE_X 		(HIRESMODE?247:123)		//139
#define SB_SHIELD_GAUGE_Y 		(HIRESMODE?395:163)

#define SB_SHIP_GAUGE_X 		(SB_SHIELD_GAUGE_X+(HIRESMODE?11:5))
#define SB_SHIP_GAUGE_Y 		(SB_SHIELD_GAUGE_Y+(HIRESMODE?10:5))

#define SB_SHIELD_NUM_X 		(SB_SHIELD_GAUGE_X+(HIRESMODE?21:12))	//151
#define SB_SHIELD_NUM_Y 		(SB_SHIELD_GAUGE_Y-(HIRESMODE?16:7))			//156 -- MWA used to be hard coded to 156

#define NUMERICAL_GAUGE_X		(HIRESMODE?308:154)
#define NUMERICAL_GAUGE_Y		(HIRESMODE?316:130)
#define NUMERICAL_GAUGE_W		(HIRESMODE?38:19)
#define NUMERICAL_GAUGE_H		(HIRESMODE?55:22)

#define PRIMARY_W_PIC_X			(HIRESMODE?135:64)
#define PRIMARY_W_PIC_Y			(HIRESMODE?370:154)
#define PRIMARY_W_TEXT_X		HUD_SCALE_X(HIRESMODE?182:87)
#define PRIMARY_W_TEXT_Y		HUD_SCALE_Y(HIRESMODE?400:157)
#define PRIMARY_AMMO_X			HUD_SCALE_X(HIRESMODE?186:93)
#define PRIMARY_AMMO_Y			HUD_SCALE_Y(HIRESMODE?420:171)

#define SECONDARY_W_PIC_X		(HIRESMODE?405:234)
#define SECONDARY_W_PIC_Y		(HIRESMODE?370:154)
#define SECONDARY_W_TEXT_X		HUD_SCALE_X(HIRESMODE?462:207)
#define SECONDARY_W_TEXT_Y		HUD_SCALE_Y(HIRESMODE?400:157)
#define SECONDARY_AMMO_X		HUD_SCALE_X(HIRESMODE?475:213)
#define SECONDARY_AMMO_Y		HUD_SCALE_Y(HIRESMODE?425:171)

#define SB_LIVES_X			(HIRESMODE?550:266)
#define SB_LIVES_Y			(HIRESMODE?450:185)
#define SB_LIVES_LABEL_X		(HIRESMODE?475:237)
#define SB_LIVES_LABEL_Y		(SB_LIVES_Y+1)

#define SB_SCORE_RIGHT_L		301
#define SB_SCORE_RIGHT_H		605
#define SB_SCORE_RIGHT			(HIRESMODE?SB_SCORE_RIGHT_H:SB_SCORE_RIGHT_L)

#define SB_SCORE_Y			(HIRESMODE?398:158)
#define SB_SCORE_LABEL_X		(HIRESMODE?475:237)

#define SB_SCORE_ADDED_RIGHT		(HIRESMODE?SB_SCORE_RIGHT_H:SB_SCORE_RIGHT_L)
#define SB_SCORE_ADDED_Y		(HIRESMODE?413:165)

#define HOMING_WARNING_X		HUD_SCALE_X(HIRESMODE?13:7)
#define HOMING_WARNING_Y		HUD_SCALE_Y(HIRESMODE?416:171)

#define BOMB_COUNT_X			(HIRESMODE?468:210)
#define BOMB_COUNT_Y			(HIRESMODE?445:186)
#define SB_BOMB_COUNT_X			(HIRESMODE?342:171)
#define SB_BOMB_COUNT_Y			(HIRESMODE?458:191)

// defining box boundries for weapon pictures
#define PRIMARY_W_BOX_LEFT_L		63
#define PRIMARY_W_BOX_TOP_L		151		//154
#define PRIMARY_W_BOX_RIGHT_L		(PRIMARY_W_BOX_LEFT_L+58)
#define PRIMARY_W_BOX_BOT_L		(PRIMARY_W_BOX_TOP_L+42)

#define PRIMARY_W_BOX_LEFT_H		121
#define PRIMARY_W_BOX_TOP_H		364
#define PRIMARY_W_BOX_RIGHT_H		241
#define PRIMARY_W_BOX_BOT_H		(PRIMARY_W_BOX_TOP_H+106)		//470

#define PRIMARY_W_BOX_LEFT		(HIRESMODE?PRIMARY_W_BOX_LEFT_H:PRIMARY_W_BOX_LEFT_L)
#define PRIMARY_W_BOX_TOP		(HIRESMODE?PRIMARY_W_BOX_TOP_H:PRIMARY_W_BOX_TOP_L)
#define PRIMARY_W_BOX_RIGHT		(HIRESMODE?PRIMARY_W_BOX_RIGHT_H:PRIMARY_W_BOX_RIGHT_L)
#define PRIMARY_W_BOX_BOT		(HIRESMODE?PRIMARY_W_BOX_BOT_H:PRIMARY_W_BOX_BOT_L)

#define SECONDARY_W_BOX_LEFT_L		202	//207
#define SECONDARY_W_BOX_TOP_L		151
#define SECONDARY_W_BOX_RIGHT_L		264	//(SECONDARY_W_BOX_LEFT+54)
#define SECONDARY_W_BOX_BOT_L		(SECONDARY_W_BOX_TOP_L+42)

#define SECONDARY_W_BOX_LEFT_H		403
#define SECONDARY_W_BOX_TOP_H		364
#define SECONDARY_W_BOX_RIGHT_H		531
#define SECONDARY_W_BOX_BOT_H		(SECONDARY_W_BOX_TOP_H+106)		//470

#define SECONDARY_W_BOX_LEFT		(HIRESMODE?SECONDARY_W_BOX_LEFT_H:SECONDARY_W_BOX_LEFT_L)
#define SECONDARY_W_BOX_TOP		(HIRESMODE?SECONDARY_W_BOX_TOP_H:SECONDARY_W_BOX_TOP_L)
#define SECONDARY_W_BOX_RIGHT		(HIRESMODE?SECONDARY_W_BOX_RIGHT_H:SECONDARY_W_BOX_RIGHT_L)
#define SECONDARY_W_BOX_BOT		(HIRESMODE?SECONDARY_W_BOX_BOT_H:SECONDARY_W_BOX_BOT_L)

#define SB_PRIMARY_W_BOX_LEFT_L		34		//50
#define SB_PRIMARY_W_BOX_TOP_L		154
#define SB_PRIMARY_W_BOX_RIGHT_L	(SB_PRIMARY_W_BOX_LEFT_L+55)
#define SB_PRIMARY_W_BOX_BOT_L		(195)

#define SB_PRIMARY_W_BOX_LEFT_H		68
#define SB_PRIMARY_W_BOX_TOP_H		381
#define SB_PRIMARY_W_BOX_RIGHT_H	179
#define SB_PRIMARY_W_BOX_BOT_H		473

#define SB_PRIMARY_W_BOX_LEFT		(HIRESMODE?SB_PRIMARY_W_BOX_LEFT_H:SB_PRIMARY_W_BOX_LEFT_L)
#define SB_PRIMARY_W_BOX_TOP		(HIRESMODE?SB_PRIMARY_W_BOX_TOP_H:SB_PRIMARY_W_BOX_TOP_L)
#define SB_PRIMARY_W_BOX_RIGHT		(HIRESMODE?SB_PRIMARY_W_BOX_RIGHT_H:SB_PRIMARY_W_BOX_RIGHT_L)
#define SB_PRIMARY_W_BOX_BOT		(HIRESMODE?SB_PRIMARY_W_BOX_BOT_H:SB_PRIMARY_W_BOX_BOT_L)

#define SB_SECONDARY_W_BOX_LEFT_L	169
#define SB_SECONDARY_W_BOX_TOP_L	154
#define SB_SECONDARY_W_BOX_RIGHT_L	(SB_SECONDARY_W_BOX_LEFT_L+54)
#define SB_SECONDARY_W_BOX_BOT_L	(153+42)

#define SB_SECONDARY_W_BOX_LEFT_H	338
#define SB_SECONDARY_W_BOX_TOP_H	381
#define SB_SECONDARY_W_BOX_RIGHT_H	449
#define SB_SECONDARY_W_BOX_BOT_H	473

#define SB_SECONDARY_W_BOX_LEFT		(HIRESMODE?SB_SECONDARY_W_BOX_LEFT_H:SB_SECONDARY_W_BOX_LEFT_L)	//210
#define SB_SECONDARY_W_BOX_TOP		(HIRESMODE?SB_SECONDARY_W_BOX_TOP_H:SB_SECONDARY_W_BOX_TOP_L)
#define SB_SECONDARY_W_BOX_RIGHT	(HIRESMODE?SB_SECONDARY_W_BOX_RIGHT_H:SB_SECONDARY_W_BOX_RIGHT_L)
#define SB_SECONDARY_W_BOX_BOT		(HIRESMODE?SB_SECONDARY_W_BOX_BOT_H:SB_SECONDARY_W_BOX_BOT_L)

#define SB_PRIMARY_W_PIC_X		(SB_PRIMARY_W_BOX_LEFT+1)	//51
#define SB_PRIMARY_W_PIC_Y		(HIRESMODE?382:154)
#define SB_PRIMARY_W_TEXT_X		HUD_SCALE_X(SB_PRIMARY_W_BOX_LEFT+(HIRESMODE?50:24))	//(51+23)
#define SB_PRIMARY_W_TEXT_Y		HUD_SCALE_Y(HIRESMODE?390:157)
#define SB_PRIMARY_AMMO_X		HUD_SCALE_X(SB_PRIMARY_W_BOX_LEFT+(HIRESMODE?58:30))	//((SB_PRIMARY_W_BOX_LEFT+33)-3)	//(51+32)
#define SB_PRIMARY_AMMO_Y		HUD_SCALE_Y(HIRESMODE?410:171)

#define SB_SECONDARY_W_PIC_X		(HIRESMODE?385:(SB_SECONDARY_W_BOX_LEFT+27))	//(212+27)
#define SB_SECONDARY_W_PIC_Y		(HIRESMODE?382:154)
#define SB_SECONDARY_W_TEXT_X		HUD_SCALE_X(SB_SECONDARY_W_BOX_LEFT+2)	//212
#define SB_SECONDARY_W_TEXT_Y		HUD_SCALE_Y(HIRESMODE?390:157)
#define SB_SECONDARY_AMMO_X		HUD_SCALE_X(SB_SECONDARY_W_BOX_LEFT+(HIRESMODE?(14):11))	//(212+9)
#define SB_SECONDARY_AMMO_Y		HUD_SCALE_Y(HIRESMODE?414:171)

#define WS_SET				0		//in correct state
#define WS_FADING_OUT			1
#define WS_FADING_IN			2
#define FADE_SCALE			(2*i2f(GR_FADE_LEVELS)/REARM_TIME)		// fade out and back in REARM_TIME, in fade levels per seconds (int)
#define MAX_SHOWN_LIVES 4

#define COCKPIT_PRIMARY_BOX		(!HIRESMODE?0:4)
#define COCKPIT_SECONDARY_BOX		(!HIRESMODE?1:5)
#define SB_PRIMARY_BOX			(!HIRESMODE?2:6)
#define SB_SECONDARY_BOX		(!HIRESMODE?3:7)

// scaling gauges
#define BASE_WIDTH (HIRESMODE? 640 : 320)
#define BASE_HEIGHT	(HIRESMODE? 480 : 200)
#ifdef OGL
#define HUD_SCALE_X(x)		((int) ((double) (x) * ((double)grd_curscreen->sc_w/BASE_WIDTH) + 0.5))
#define HUD_SCALE_Y(y)		((int) ((double) (y) * ((double)grd_curscreen->sc_h/BASE_HEIGHT) + 0.5))
#define HUD_SCALE_X_AR(x)	(HUD_SCALE_X(100) > HUD_SCALE_Y(100) ? HUD_SCALE_Y(x) : HUD_SCALE_X(x))
#define HUD_SCALE_Y_AR(y)	(HUD_SCALE_Y(100) > HUD_SCALE_X(100) ? HUD_SCALE_X(y) : HUD_SCALE_Y(y))
#else
#define HUD_SCALE_X(x)		(x)
#define HUD_SCALE_Y(y)		(y)
#define HUD_SCALE_X_AR(x)	(x)
#define HUD_SCALE_Y_AR(y)	(y)
#endif

bitmap_index Gauges[MAX_GAUGE_BMS_MAC]; // Array of all gauge bitmaps.
grs_bitmap deccpt;
grs_bitmap *WinBoxOverlay[2] = { NULL, NULL }; // Overlay subbitmaps for both weapon boxes

static int score_display;
static fix score_time;
static int old_weapon[2]		= {-1,-1};
static int invulnerable_frame = 0;
int weapon_box_states[2];
fix weapon_box_fade_values[2];
int	Color_0_31_0 = -1;
extern fix ThisLevelTime;
extern fix Cruise_speed;
extern int linedotscale;

int Observer_message_y_start = 0;

typedef struct gauge_box {
	int left,top;
	int right,bot;		//maximal box
} gauge_box;

//first two are primary & secondary
//seconds two are the same for the status bar
gauge_box gauge_boxes[] = {

// primary left/right low res
		{PRIMARY_W_BOX_LEFT_L,PRIMARY_W_BOX_TOP_L,PRIMARY_W_BOX_RIGHT_L,PRIMARY_W_BOX_BOT_L},
		{SECONDARY_W_BOX_LEFT_L,SECONDARY_W_BOX_TOP_L,SECONDARY_W_BOX_RIGHT_L,SECONDARY_W_BOX_BOT_L},

//sb left/right low res
		{SB_PRIMARY_W_BOX_LEFT_L,SB_PRIMARY_W_BOX_TOP_L,SB_PRIMARY_W_BOX_RIGHT_L,SB_PRIMARY_W_BOX_BOT_L},
		{SB_SECONDARY_W_BOX_LEFT_L,SB_SECONDARY_W_BOX_TOP_L,SB_SECONDARY_W_BOX_RIGHT_L,SB_SECONDARY_W_BOX_BOT_L},

// primary left/right hires
		{PRIMARY_W_BOX_LEFT_H,PRIMARY_W_BOX_TOP_H,PRIMARY_W_BOX_RIGHT_H,PRIMARY_W_BOX_BOT_H},
		{SECONDARY_W_BOX_LEFT_H,SECONDARY_W_BOX_TOP_H,SECONDARY_W_BOX_RIGHT_H,SECONDARY_W_BOX_BOT_H},

// sb left/right hires
		{SB_PRIMARY_W_BOX_LEFT_H,SB_PRIMARY_W_BOX_TOP_H,SB_PRIMARY_W_BOX_RIGHT_H,SB_PRIMARY_W_BOX_BOT_H},
		{SB_SECONDARY_W_BOX_LEFT_H,SB_SECONDARY_W_BOX_TOP_H,SB_SECONDARY_W_BOX_RIGHT_H,SB_SECONDARY_W_BOX_BOT_H},
	};

//store delta x values from left of box
span weapon_window_left[] = {
	{71,114},
	{69,116},
	{68,117},
	{66,118},
	{66,119},
	{66,119},
	{65,119},
	{65,119},
	{65,119},
	{65,119},
	{65,119},
	{65,119},
	{65,119},
	{65,119},
	{65,119},
	{64,119},
	{64,119},
	{64,119},
	{64,119},
	{64,119},
	{64,119},
	{64,119},
	{64,119},
	{64,119},
	{63,119},
	{63,118},
	{63,118},
	{63,118},
	{63,118},
	{63,118},
	{63,118},
	{63,118},
	{63,118},
	{63,118},
	{63,118},
	{63,118},
	{63,118},
	{63,117},
	{63,117},
	{64,116},
	{65,115},
	{66,113},
	{68,111},
};


//store delta x values from left of box
span weapon_window_right[] = {
	{208,255},
	{206,257},
	{205,258},
	{204,259},
	{203,260},
	{203,260},
	{203,260},
	{203,260},
	{203,260},
	{203,261},
	{203,261},
	{203,261},
	{203,261},
	{203,261},
	{203,261},
	{203,261},
	{203,261},
	{203,261},
	{203,262},
	{203,262},
	{203,262},
	{203,262},
	{203,262},
	{203,262},
	{203,262},
	{203,262},
	{204,263},
	{204,263},
	{204,263},
	{204,263},
	{204,263},
	{204,263},
	{204,263},
	{204,263},
	{204,263},
	{204,263},
	{204,263},
	{204,263},
	{205,263},
	{206,262},
	{207,261},
	{208,260},
	{211,255},
};


//store delta x values from left of box
span weapon_window_left_hires[] = {
	{141,231},
	{139,234},
	{137,235},
	{136,237},
	{135,238},
	{134,239},
	{133,240},
	{132,240},
	{131,241},
	{131,241},
	{130,242},
	{129,242},
	{129,242},
	{129,243},
	{128,243},
	{128,243},
	{128,243},
	{128,243},
	{128,243},
	{127,243},
	{127,243},
	{127,243},
	{127,243},
	{127,243},
	{127,243},
	{127,243},
	{127,243},
	{127,243},
	{127,243},
	{126,243},
	{126,243},
	{126,243},
	{126,243},
	{126,242},
	{126,242},
	{126,242},
	{126,242},
	{126,242},
	{126,242},
	{125,242},
	{125,242},
	{125,242},
	{125,242},
	{125,242},
	{125,242},
	{125,242},
	{125,242},
	{125,242},
	{125,242},
	{124,242},
	{124,242},
	{124,241},
	{124,241},
	{124,241},
	{124,241},
	{124,241},
	{124,241},
	{124,241},
	{124,241},
	{124,241},
	{123,241},
	{123,241},
	{123,241},
	{123,241},
	{123,241},
	{123,241},
	{123,241},
	{123,241},
	{123,241},
	{123,241},
	{123,241},
	{123,241},
	{122,241},
	{122,241},
	{122,240},
	{122,240},
	{122,240},
	{122,240},
	{122,240},
	{122,240},
	{122,240},
	{122,240},
	{121,240},
	{121,240},
	{121,240},
	{121,240},
	{121,240},
	{121,240},
	{121,240},
	{121,239},
	{121,239},
	{121,239},
	{121,238},
	{121,238},
	{121,238},
	{122,237},
	{122,237},
	{123,236},
	{123,235},
	{124,234},
	{125,233},
	{126,232},
	{126,231},
	{128,230},
	{130,228},
	{131,226},
	{133,223},
};


//store delta x values from left of box
span weapon_window_right_hires[] = {
	{416,509},
	{413,511},
	{412,513},
	{410,514},
	{409,515},
	{408,516},
	{407,517},
	{407,518},
	{406,519},
	{406,519},
	{405,520},
	{405,521},
	{405,521},
	{404,521},
	{404,522},
	{404,522},
	{404,522},
	{404,522},
	{404,522},
	{404,523},
	{404,523},
	{404,523},
	{404,523},
	{404,523},
	{404,523},
	{404,523},
	{404,523},
	{404,523},
	{404,523},
	{404,524},
	{404,524},
	{404,524},
	{404,524},
	{405,524},
	{405,524},
	{405,524},
	{405,524},
	{405,524},
	{405,524},
	{405,525},
	{405,525},
	{405,525},
	{405,525},
	{405,525},
	{405,525},
	{405,525},
	{405,525},
	{405,525},
	{405,525},
	{405,526},
	{405,526},
	{406,526},
	{406,526},
	{406,526},
	{406,526},
	{406,526},
	{406,526},
	{406,526},
	{406,526},
	{406,527},
	{406,527},
	{406,527},
	{406,527},
	{406,527},
	{406,527},
	{406,527},
	{406,527},
	{406,527},
	{406,527},
	{406,527},
	{406,527},
	{406,527},
	{406,528},
	{406,528},
	{407,528},
	{407,528},
	{407,528},
	{407,528},
	{407,528},
	{407,528},
	{407,528},
	{407,529},
	{407,529},
	{407,529},
	{407,529},
	{407,529},
	{407,529},
	{407,529},
	{407,529},
	{408,529},
	{408,529},
	{408,529},
	{409,529},
	{409,529},
	{409,529},
	{410,529},
	{410,528},
	{411,527},
	{412,527},
	{413,526},
	{414,525},
	{415,524},
	{416,524},
	{417,522},
	{419,521},
	{422,519},
	{424,518},
};

static inline void hud_bitblt_free (int x, int y, int w, int h, grs_bitmap *bm)
{
#ifdef OGL
	ogl_ubitmapm_cs (x,y,w,h,bm,-1,F1_0);
#else
	gr_ubitmapm(x, y, bm);
#endif
}

static inline void hud_bitblt (int x, int y, grs_bitmap *bm)
{
#ifdef OGL
	ogl_ubitmapm_cs (x,y,HUD_SCALE_X (bm->bm_w),HUD_SCALE_Y (bm->bm_h),bm,-1,F1_0);
#else
	gr_ubitmapm(x, y, bm);
#endif
}

int get_pnum_for_hud()
{
	if (is_observer() && is_observing_player())
		return Current_obs_player;
	else
		return Player_num;
}

void hud_show_score()
{
	char	score_str[20];
	int	w, h, aw;

	int pnum = get_pnum_for_hud();

	if (HUD_toolong)
		return;

	gr_set_curfont( GAME_FONT );

	if ( (Game_mode & GM_MULTI) && !((Game_mode & GM_MULTI_COOP) || (Game_mode & GM_MULTI_ROBOTS)) ) {
		sprintf(score_str, "%s: %5d", TXT_KILLS, Players[pnum].net_kills_total);
	} else {
		sprintf(score_str, "%s: %5d", TXT_SCORE, Players[pnum].score);
  	}

	gr_get_string_size(score_str, &w, &h, &aw );

	if (Color_0_31_0 == -1)
		Color_0_31_0 = BM_XRGB(0,31,0);
	gr_set_fontcolor(Color_0_31_0, -1);

	gr_string(grd_curcanv->cv_bitmap.bm_w-w-FSPACX(1), FSPACY(1), score_str);
}

void hud_show_timer_count()
{
#ifdef NETWORK
	char	score_str[20];
	int	w, h, aw,i;
	fix timevar=0;
#endif

	if (HUD_toolong)
		return;

#ifdef NETWORK
	if ((Game_mode & GM_NETWORK) && Netgame.PlayTimeAllowed)
	{
		timevar=i2f (Netgame.PlayTimeAllowed*5*60);
		i=f2i(timevar-ThisLevelTime);
		i++;

		sprintf(score_str, "T - %5d", i);
		gr_get_string_size(score_str, &w, &h, &aw );

		if (Color_0_31_0 == -1)
			Color_0_31_0 = BM_XRGB(0,31,0);

		gr_set_fontcolor(Color_0_31_0, -1);

		if (i>-1 && !Control_center_destroyed)
			gr_string(grd_curcanv->cv_bitmap.bm_w-w-FSPACX(12), LINE_SPACING+FSPACY(1), score_str);
	}
#endif
}

void hud_show_score_added()
{
	int	color;
	int	w, h, aw;
	char	score_str[20];

	if ( (Game_mode & GM_MULTI) && !((Game_mode & GM_MULTI_COOP) || (Game_mode & GM_MULTI_ROBOTS)) )
		return;

	if (score_display == 0)
		return;

	gr_set_curfont( GAME_FONT );

	score_time -= FrameTime;
	if (score_time > 0) {
		color = f2i(score_time * 20) + 12;

		if (color < 10) color = 12;
		if (color > 31) color = 30;

		color = color - (color % 4);

		if (cheats.enabled)
			sprintf(score_str, "%s", TXT_CHEATER);
		else
			sprintf(score_str, "%5d", score_display);

		gr_get_string_size(score_str, &w, &h, &aw );
		gr_set_fontcolor(BM_XRGB(0, color, 0),-1 );
		gr_string(grd_curcanv->cv_bitmap.bm_w-w-FSPACX(1), LINE_SPACING+FSPACY(1), score_str);
	} else {
		score_time = 0;
		score_display = 0;
	}
}

void sb_show_score()
{
	char	score_str[20];
	int x,y;
	int	w, h, aw;

	int pnum = get_pnum_for_hud();

	gr_set_curfont( GAME_FONT );
	gr_set_fontcolor(BM_XRGB(0,20,0),-1 );

	if ( (Game_mode & GM_MULTI) && !((Game_mode & GM_MULTI_COOP) || (Game_mode & GM_MULTI_ROBOTS)) )
		gr_printf(HUD_SCALE_X(SB_SCORE_LABEL_X),HUD_SCALE_Y(SB_SCORE_Y),"%s:", TXT_KILLS);
	else
		gr_printf(HUD_SCALE_X(SB_SCORE_LABEL_X),HUD_SCALE_Y(SB_SCORE_Y),"%s:", TXT_SCORE);

	gr_set_curfont( GAME_FONT );
	if ( (Game_mode & GM_MULTI) && !( (Game_mode & GM_MULTI_COOP) || (Game_mode & GM_MULTI_ROBOTS) ) )
		sprintf(score_str, "%5d", Players[pnum].net_kills_total);
	else
		sprintf(score_str, "%5d", Players[pnum].score);
	gr_get_string_size(score_str, &w, &h, &aw );

	x = HUD_SCALE_X(SB_SCORE_RIGHT)-w-FSPACX(1);
	y = HUD_SCALE_Y(SB_SCORE_Y);

	//erase old score
	gr_setcolor(BM_XRGB(0,0,0));
	gr_rect(x,y,HUD_SCALE_X(SB_SCORE_RIGHT),y+LINE_SPACING);

	if ( (Game_mode & GM_MULTI) && !((Game_mode & GM_MULTI_COOP) || (Game_mode & GM_MULTI_ROBOTS)) )
		gr_set_fontcolor(BM_XRGB(0,20,0),-1 );
	else
		gr_set_fontcolor(BM_XRGB(0,31,0),-1 );

	gr_string(x,y,score_str);
}

void sb_show_score_added()
{
	int	color;
	int w, h, aw;
	char	score_str[32];
	static int x;
	static	int last_score_display = -1;

	if ( (Game_mode & GM_MULTI) && !((Game_mode & GM_MULTI_COOP) || (Game_mode & GM_MULTI_ROBOTS)) )
		return;

	if (score_display == 0)
		return;

	gr_set_curfont( GAME_FONT );

	score_time -= FrameTime;
	if (score_time > 0) {
		if (score_display != last_score_display)
			last_score_display = score_display;

		color = f2i(score_time * 20) + 10;

		if (color < 10) color = 10;
		if (color > 31) color = 31;

		if (cheats.enabled)
			sprintf(score_str, "%s", TXT_CHEATER);
		else
			sprintf(score_str, "%5d", score_display);

		gr_get_string_size(score_str, &w, &h, &aw );
		x = HUD_SCALE_X(SB_SCORE_ADDED_RIGHT)-w-FSPACX(1);
		gr_set_fontcolor(BM_XRGB(0, color, 0),-1 );
		gr_string(x, HUD_SCALE_Y(SB_SCORE_ADDED_Y), score_str);
	} else {
		//erase old score
		gr_setcolor(BM_XRGB(0,0,0));
		gr_rect(x,HUD_SCALE_Y(SB_SCORE_ADDED_Y),HUD_SCALE_X(SB_SCORE_ADDED_RIGHT),HUD_SCALE_Y(SB_SCORE_ADDED_Y)+LINE_SPACING);
		score_time = 0;
		score_display = 0;
	}
}

//	-----------------------------------------------------------------------------
void play_homing_warning(void)
{
	fix beep_delay;
	static fix64 Last_warning_beep_time = 0; // Time we last played homing missile warning beep.

	int pnum = get_pnum_for_hud();

	if (Endlevel_sequence || Player_is_dead)
		return;

	if (Players[pnum].homing_object_dist >= 0) {
		beep_delay = Players[pnum].homing_object_dist/128;
		if (beep_delay > F1_0)
			beep_delay = F1_0;
		else if (beep_delay < F1_0/8)
			beep_delay = F1_0/8;

		if (GameTime64 - Last_warning_beep_time > beep_delay/2 || Last_warning_beep_time > GameTime64) {
			digi_play_sample( SOUND_HOMING_WARNING, F1_0 );
			Last_warning_beep_time = GameTime64;
		}
	}
}

//	-----------------------------------------------------------------------------
void show_homing_warning(void)
{
	int pnum = get_pnum_for_hud();

	if (Endlevel_sequence)
	{
		PIGGY_PAGE_IN( Gauges[GAUGE_HOMING_WARNING_OFF] );
		hud_bitblt( HOMING_WARNING_X, HOMING_WARNING_Y, &GameBitmaps[Gauges[GAUGE_HOMING_WARNING_OFF].index]);
		return;
	}

	gr_set_current_canvas( NULL );

	if (Players[pnum].homing_object_dist >= 0)
	{
		if (GameTime64 & 0x4000)
		{
			PIGGY_PAGE_IN(Gauges[GAUGE_HOMING_WARNING_ON]);
			hud_bitblt( HOMING_WARNING_X, HOMING_WARNING_Y, &GameBitmaps[Gauges[GAUGE_HOMING_WARNING_ON].index]);
		}
		else
		{
			PIGGY_PAGE_IN(Gauges[GAUGE_HOMING_WARNING_OFF]);
			hud_bitblt( HOMING_WARNING_X, HOMING_WARNING_Y, &GameBitmaps[Gauges[GAUGE_HOMING_WARNING_OFF].index]);
		}
	}
	else
	{
		PIGGY_PAGE_IN(Gauges[GAUGE_HOMING_WARNING_OFF]);
		hud_bitblt( HOMING_WARNING_X, HOMING_WARNING_Y, &GameBitmaps[Gauges[GAUGE_HOMING_WARNING_OFF].index]);
	}
}

void hud_show_homing_warning(void)
{
	int pnum = get_pnum_for_hud();

	if (Players[pnum].homing_object_dist >= 0) {
		if (GameTime64 & 0x4000) {
			gr_set_curfont( GAME_FONT );
			gr_set_fontcolor(BM_XRGB(0,31,0),-1 );
			gr_string(0x8000, grd_curcanv->cv_bitmap.bm_h-LINE_SPACING,TXT_LOCK);
		}
	}
}

void hud_show_keys(void)
{
	int pnum = get_pnum_for_hud();

	grs_bitmap *blue,*yellow,*red;
	int y=HUD_SCALE_Y_AR(GameBitmaps[Gauges[GAUGE_LIVES].index].bm_h+2)+FSPACY(1);

	PIGGY_PAGE_IN(Gauges[KEY_ICON_BLUE]);
	PIGGY_PAGE_IN(Gauges[KEY_ICON_YELLOW]);
	PIGGY_PAGE_IN(Gauges[KEY_ICON_RED]);

	blue=&GameBitmaps[Gauges[KEY_ICON_BLUE].index];
	yellow=&GameBitmaps[Gauges[KEY_ICON_YELLOW].index];
	red=&GameBitmaps[Gauges[KEY_ICON_RED].index];

	if (Players[pnum].flags & PLAYER_FLAGS_BLUE_KEY)
		hud_bitblt_free(FSPACX(2),y,HUD_SCALE_X_AR(blue->bm_w),HUD_SCALE_Y_AR(blue->bm_h),blue);

	if (Players[pnum].flags & PLAYER_FLAGS_GOLD_KEY)
		hud_bitblt_free(FSPACX(2)+HUD_SCALE_X_AR(blue->bm_w+3),y,HUD_SCALE_X_AR(yellow->bm_w),HUD_SCALE_Y_AR(yellow->bm_h),yellow);

	if (Players[pnum].flags & PLAYER_FLAGS_RED_KEY)
		hud_bitblt_free(FSPACX(2)+HUD_SCALE_X_AR(blue->bm_w+yellow->bm_w+6),y,HUD_SCALE_X_AR(red->bm_w),HUD_SCALE_Y_AR(red->bm_h),red);
}

void hud_show_energy(void)
{
	int pnum = get_pnum_for_hud();

	if (PlayerCfg.HudMode<2) {
		gr_set_curfont( GAME_FONT );
		gr_set_fontcolor(BM_XRGB(0,31,0),-1 );
		if (Game_mode & GM_MULTI)
		     gr_printf(FSPACX(1), (grd_curcanv->cv_bitmap.bm_h-(LINE_SPACING*5)),"%s: %i", TXT_ENERGY, f2ir(Players[pnum].energy));
		else
		     gr_printf(FSPACX(1), (grd_curcanv->cv_bitmap.bm_h-LINE_SPACING),"%s: %i", TXT_ENERGY, f2ir(Players[pnum].energy));
	}

	if (Newdemo_state==ND_STATE_RECORDING )
		newdemo_record_player_energy(f2ir(Players[pnum].energy));
}

static inline const char *SECONDARY_WEAPON_NAMES_VERY_SHORT(const unsigned u)
{
	switch(u)
	{
		default:
			Int3();
		case CONCUSSION_INDEX:	return TXT_CONCUSSION;
		case HOMING_INDEX:		return TXT_HOMING;
		case PROXIMITY_INDEX:	return TXT_PROXBOMB;
		case SMART_INDEX:		return TXT_SMART;
		case MEGA_INDEX:		return TXT_MEGA;
	}
}

void show_bomb_count(int x,int y,int bg_color,int always_show,int right_align)
{
	int bomb,count,w=0,h=0,aw=0;
	char txt[5],*t;

	int pnum = get_pnum_for_hud();

	if (!PlayerCfg.BombGauge)
		return;

	bomb = which_bomb();
	count = Players[pnum].secondary_ammo[bomb];

	count = min(count,99);	//only have room for 2 digits - cheating give 200

	if (always_show && count == 0)		//no bombs, draw nothing on HUD
		return;

	if (count)
		gr_set_fontcolor(gr_find_closest_color(55,0,0),bg_color);
	else
		gr_set_fontcolor(bg_color,bg_color);	//erase by drawing in background color

	snprintf(txt, sizeof(txt), "B:%02d", count);

	while ((t=strchr(txt,'1')) != NULL)
		*t = '\x84';	//convert to wide '1'

	if (right_align)
		gr_get_string_size(txt, &w, &h, &aw );

	gr_string(x-w,y,txt);
}

void hud_show_weapons_mode(int type,int vertical,int x,int y){
	int i,w,h,aw;
	char weapon_str[10];

	int pnum = get_pnum_for_hud();

	if (vertical){
		y=y+(LINE_SPACING*4);
	}
	if (type==0){
		for (i=4;i>=0;i--){
			if (Players[pnum].primary_weapon==i)
				gr_set_fontcolor(BM_XRGB(20,0,0),-1);
			else{
				if (player_has_weapon(pnum, i, 0) & HAS_WEAPON_FLAG)
					gr_set_fontcolor(BM_XRGB(0,15,0),-1);
				else
					gr_set_fontcolor(BM_XRGB(3,3,3),-1);
			}
			switch(i){
				case 0:
					sprintf(weapon_str,"%c%i",
						(Players[pnum].flags & PLAYER_FLAGS_QUAD_LASERS)?'Q':'L',
						Players[pnum].laser_level+1);
					break;
				case 1:
				if (PlayerCfg.CockpitMode[1]==CM_FULL_SCREEN)
					sprintf(weapon_str,"V%i", f2i(Players[pnum].primary_ammo[1] * VULCAN_AMMO_SCALE));
				else
					sprintf(weapon_str,"V%i", f2i(Players[pnum].primary_ammo[1] * VULCAN_AMMO_SCALE));
					break;
				case 2:
					sprintf(weapon_str,"S");break;
				case 3:
					sprintf(weapon_str,"P");break;
				case 4:
					sprintf(weapon_str,"F");break;

			}
			gr_get_string_size(weapon_str, &w, &h, &aw );
			if (vertical){
				y-=h+FSPACX(2);
			}else
			     x-=w+FSPACY(3);
			gr_string(x, y, weapon_str);
		}
	} else {
		for (i=4;i>=0;i--){
			if (Players[pnum].secondary_weapon==i)
				gr_set_fontcolor(BM_XRGB(20,0,0),-1);
			else{
				if (Players[pnum].secondary_ammo[i]>0)
					gr_set_fontcolor(BM_XRGB(0,15,0),-1);
				else
					gr_set_fontcolor(BM_XRGB(0,6,0),-1);
			}
			sprintf(weapon_str,"%i",Players[pnum].secondary_ammo[i]);
			gr_get_string_size(weapon_str, &w, &h, &aw );
			if (vertical){
				y-=h+FSPACX(2);
			}else
			     x-=w+FSPACY(3);
			gr_string(x, y, weapon_str);
		}
	}
	gr_set_fontcolor(BM_XRGB(0,31,0),-1 );
}

void hud_show_weapons(void)
{
	int	y;

	int pnum = get_pnum_for_hud();

	gr_set_curfont( GAME_FONT );
	gr_set_fontcolor(BM_XRGB(0,31,0),-1 );

	y = grd_curcanv->cv_bitmap.bm_h;

	if (Game_mode & GM_MULTI)
		y -= LINE_SPACING*4;

	if (PlayerCfg.HudMode==1){
		hud_show_weapons_mode(0,0,grd_curcanv->cv_bitmap.bm_w,y-(LINE_SPACING*2));
		hud_show_weapons_mode(1,0,grd_curcanv->cv_bitmap.bm_w,y-LINE_SPACING);
	}
	else if (PlayerCfg.HudMode==2){
		int x1,x2;
		int w, aw;
		gr_get_string_size("V1000", &w, &x1, &aw );
		gr_get_string_size("0 ", &x2, &x1, &aw);
		y=grd_curcanv->cv_bitmap.bm_h/1.75;
		x1=grd_curcanv->cv_bitmap.bm_w/2.1-(FSPACX(40)+w);
		x2=grd_curcanv->cv_bitmap.bm_w/1.9+(FSPACX(42)+x2);
		hud_show_weapons_mode(0,1,x1,y);
		hud_show_weapons_mode(1,1,x2,y);
		gr_set_fontcolor(BM_XRGB(14,14,23),-1 );
		gr_printf(x2, y-(LINE_SPACING*4),"%i", f2ir(Players[pnum].shields));
		gr_set_fontcolor(BM_XRGB(25,18,6),-1 );
		gr_printf(x1, y-(LINE_SPACING*4),"%i", f2ir(Players[pnum].energy));
	}
	else
	{
		const char *disp_primary_weapon_name;
		char    weapon_str[32];
		int	w, h, aw;

		switch (Players[pnum].primary_weapon) {
			case 0:
				if (Players[pnum].flags & PLAYER_FLAGS_QUAD_LASERS)
					sprintf(weapon_str, "%s %s %i", TXT_QUAD, TXT_LASER, Players[pnum].laser_level+1);
				else
					sprintf(weapon_str, "%s %i", TXT_LASER, Players[pnum].laser_level+1);
				disp_primary_weapon_name = weapon_str;
				break;
			case 1:
				sprintf(weapon_str, "%s: %i", TXT_W_VULCAN_S, f2i(Players[pnum].primary_ammo[Players[pnum].primary_weapon] * VULCAN_AMMO_SCALE));
				disp_primary_weapon_name = weapon_str;
				break;
			case 2:
				disp_primary_weapon_name = TXT_W_SPREADFIRE_S;
				break;
			case 3:
				disp_primary_weapon_name = TXT_W_PLASMA_S;
				break;
			case 4:
				disp_primary_weapon_name = TXT_W_FUSION_S;
				break;
			default:
				Int3();
				disp_primary_weapon_name = "";
				break;
		}

		gr_get_string_size(disp_primary_weapon_name, &w, &h, &aw );
		gr_string(grd_curcanv->cv_bitmap.bm_w-w-FSPACX(1), y-(LINE_SPACING*2), disp_primary_weapon_name);//originally y-8

		snprintf(weapon_str, sizeof(weapon_str), "%s %d",SECONDARY_WEAPON_NAMES_VERY_SHORT(Players[pnum].secondary_weapon),Players[pnum].secondary_ammo[Players[pnum].secondary_weapon]);
		gr_get_string_size(weapon_str, &w, &h, &aw );
		gr_string(grd_curcanv->cv_bitmap.bm_w-w-FSPACX(1), y-LINE_SPACING, weapon_str);

		show_bomb_count(grd_curcanv->cv_bitmap.bm_w-FSPACX(1), y-(LINE_SPACING*3),-1,1, 1);
	}

	if (Players[pnum].primary_weapon == VULCAN_INDEX)
		if (Newdemo_state == ND_STATE_RECORDING)
			newdemo_record_primary_ammo(Players[pnum].primary_ammo[Players[pnum].primary_weapon]);

	if (Newdemo_state == ND_STATE_RECORDING)
		newdemo_record_secondary_ammo(Players[pnum].secondary_ammo[Players[pnum].secondary_weapon]);
}

void hud_show_cloak_invuln(void)
{
	int pnum = get_pnum_for_hud();

	gr_set_fontcolor(BM_XRGB(0,31,0),-1 );

	if (Players[pnum].flags & PLAYER_FLAGS_CLOAKED) {
		int	y = grd_curcanv->cv_bitmap.bm_h;

		if (Game_mode & GM_MULTI)
			y -= LINE_SPACING*8;
		else
			y -= LINE_SPACING*4;

		if (Players[pnum].cloak_time+CLOAK_TIME_MAX-GameTime64 > F1_0*3 || GameTime64 & 0x8000)
		{
			gr_printf(FSPACX(1), y, "%s", TXT_CLOAKED);
		}
	}

	if (Players[pnum].invulnerable_time+INVULNERABLE_TIME_MAX-GameTime64 > 0) {
		if (Players[pnum].flags & PLAYER_FLAGS_INVULNERABLE) {
			int	y = grd_curcanv->cv_bitmap.bm_h;

			if (Game_mode & GM_MULTI)
				y -= LINE_SPACING*9;
			else
				y -= LINE_SPACING*5;

			if (Players[pnum].invulnerable_time+INVULNERABLE_TIME_MAX-GameTime64 > F1_0*4 || GameTime64 & 0x8000)
			{
				gr_printf(FSPACX(1), y, "%s", TXT_INVULNERABLE);
			}
		}
	}
}

void hud_show_shield(void)
{
	int pnum = get_pnum_for_hud();

	if (PlayerCfg.HudMode<2) {
		gr_set_curfont( GAME_FONT );
		gr_set_fontcolor(BM_XRGB(0,31,0),-1 );
		if ( Players[pnum].shields >= 0 )	{
			if (Game_mode & GM_MULTI)
				gr_printf(FSPACX(1), (grd_curcanv->cv_bitmap.bm_h-(LINE_SPACING*6)),"%s: %i", TXT_SHIELD, f2ir(Players[pnum].shields));
			else
				gr_printf(FSPACX(1), (grd_curcanv->cv_bitmap.bm_h-(LINE_SPACING*2)),"%s: %i", TXT_SHIELD, f2ir(Players[pnum].shields));
		} else {
			if (Game_mode & GM_MULTI)
				gr_printf(FSPACX(1), (grd_curcanv->cv_bitmap.bm_h-(LINE_SPACING*6)),"%s: 0", TXT_SHIELD );
			else
				gr_printf(FSPACX(1), (grd_curcanv->cv_bitmap.bm_h-(LINE_SPACING*2)),"%s: 0", TXT_SHIELD );
		}
	}

	if (Newdemo_state==ND_STATE_RECORDING )
		newdemo_record_player_shields(f2ir(Players[pnum].shields));
}

//draw the icons for number of lives
void hud_show_lives()
{
	int x;

	int pnum = get_pnum_for_hud();

	if (HUD_toolong)
		return;

	if (PlayerCfg.CockpitMode[1] == CM_FULL_COCKPIT)
		x = HUD_SCALE_X(7);
	else
		x = FSPACX(2);

	if (Game_mode & GM_MULTI) {
		gr_set_curfont( GAME_FONT );
		gr_set_fontcolor(BM_XRGB(0,31,0),-1 );
		gr_printf(x, FSPACY(1), "%s: %d", TXT_DEATHS, Players[pnum].net_killed_total);
	}
	else if (Players[pnum].lives > 1)  {
		grs_bitmap *bm;
		PIGGY_PAGE_IN(Gauges[GAUGE_LIVES]);
		bm=&GameBitmaps[Gauges[GAUGE_LIVES].index];
		gr_set_curfont( GAME_FONT );
		gr_set_fontcolor(BM_XRGB(0,20,0),-1 );
		hud_bitblt_free(x,FSPACY(1),HUD_SCALE_X_AR(bm->bm_w),HUD_SCALE_Y_AR(bm->bm_h),bm);
		gr_printf(HUD_SCALE_X_AR(bm->bm_w)+x, FSPACY(1), " x %d", Players[pnum].lives-1);
	}
}

void sb_show_lives()
{
	int x,y;

	int pnum = get_pnum_for_hud();

	grs_bitmap * bm = &GameBitmaps[Gauges[GAUGE_LIVES].index];
	x = SB_LIVES_X;
	y = SB_LIVES_Y;

	gr_set_curfont( GAME_FONT );
	gr_set_fontcolor(BM_XRGB(0,20,0),-1 );
	if (Game_mode & GM_MULTI)
		gr_printf(HUD_SCALE_X(SB_LIVES_LABEL_X),HUD_SCALE_Y(y),"%s:", TXT_DEATHS);
	else
		gr_printf(HUD_SCALE_X(SB_LIVES_LABEL_X),HUD_SCALE_Y(y),"%s:", TXT_LIVES);

	if (Game_mode & GM_MULTI)
	{
		char killed_str[20];
		int w, h, aw;
		static int last_x[4] = {SB_SCORE_RIGHT_L,SB_SCORE_RIGHT_L,SB_SCORE_RIGHT_H,SB_SCORE_RIGHT_H};
		int x;

		sprintf(killed_str, "%5d", Players[pnum].net_killed_total);
		gr_get_string_size(killed_str, &w, &h, &aw);
		gr_setcolor(BM_XRGB(0,0,0));
		gr_rect(last_x[HIRESMODE], HUD_SCALE_Y(y), HUD_SCALE_X(SB_SCORE_RIGHT), HUD_SCALE_Y(y)+LINE_SPACING);
		gr_set_fontcolor(BM_XRGB(0,20,0),-1);
		x = HUD_SCALE_X(SB_SCORE_RIGHT)-w-FSPACX(1);
		gr_string(x, HUD_SCALE_Y(y), killed_str);
		last_x[HIRESMODE] = x;
		return;
	}

	//erase old icons
	gr_setcolor(BM_XRGB(0,0,0));
	gr_rect(HUD_SCALE_X(x), HUD_SCALE_Y(y), HUD_SCALE_X(SB_SCORE_RIGHT), HUD_SCALE_Y(y+bm->bm_h));

	if (Players[pnum].lives-1 > 0) {
		gr_set_curfont( GAME_FONT );
		gr_set_fontcolor(BM_XRGB(0,20,0),-1 );
		PIGGY_PAGE_IN(Gauges[GAUGE_LIVES]);
		hud_bitblt_free(HUD_SCALE_X(x),HUD_SCALE_Y(y),HUD_SCALE_X_AR(bm->bm_w),HUD_SCALE_Y_AR(bm->bm_h),bm);
		gr_printf(HUD_SCALE_X(x)+HUD_SCALE_X_AR(bm->bm_w), HUD_SCALE_Y(y), " x %d", Players[pnum].lives-1);
	}
}

#ifndef RELEASE

extern int Piggy_bitmap_cache_next;

void show_time()
{
	int secs = f2i(Players[Player_num].time_level) % 60;
	int mins = f2i(Players[Player_num].time_level) / 60;

	gr_set_curfont( GAME_FONT );

	if (Color_0_31_0 == -1)
		Color_0_31_0 = BM_XRGB(0,31,0);
	gr_set_fontcolor(Color_0_31_0, -1 );

	gr_printf(SWIDTH-FSPACX(30),GHEIGHT-(LINE_SPACING*11),"%d:%02d", mins, secs);
}
#endif

#define EXTRA_SHIP_SCORE	50000		//get new ship every this many points

void add_points_to_score(int points)
{
	int prev_score;

	score_time += f1_0*2;
	score_display += points;
	if (score_time > f1_0*4) score_time = f1_0*4;

	if (points == 0 || cheats.enabled)
		return;

	if ((Game_mode & GM_MULTI) && !( (Game_mode & GM_MULTI_COOP) || (Game_mode & GM_MULTI_ROBOTS) ))
		return;

	prev_score=Players[Player_num].score;

	Players[Player_num].score += points;

#ifndef SHAREWARE
	if (Newdemo_state == ND_STATE_RECORDING)
		newdemo_record_player_score(points);
#endif

#ifndef SHAREWARE
#ifdef NETWORK
	if ((Game_mode & GM_MULTI_COOP) || (Game_mode & GM_MULTI_ROBOTS))
		multi_send_score();
#endif
#endif

	if (Game_mode & GM_MULTI)
		return;

	if (Players[Player_num].score/EXTRA_SHIP_SCORE != prev_score/EXTRA_SHIP_SCORE) {
		int snd;
		Players[Player_num].lives += Players[Player_num].score/EXTRA_SHIP_SCORE - prev_score/EXTRA_SHIP_SCORE;
		powerup_basic(20, 20, 20, 0, TXT_EXTRA_LIFE);
		if ((snd=Powerup_info[POW_EXTRA_LIFE].hit_sound) > -1 )
			digi_play_sample( snd, F1_0 );
	}
}

void add_bonus_points_to_score(int points)
{
	int prev_score;

	if (points == 0 || cheats.enabled)
		return;

	prev_score=Players[Player_num].score;

	Players[Player_num].score += points;


#ifndef SHAREWARE
	if (Newdemo_state == ND_STATE_RECORDING)
		newdemo_record_player_score(points);
#endif

	if (Game_mode & GM_MULTI)
		return;

	if (Players[Player_num].score/EXTRA_SHIP_SCORE != prev_score/EXTRA_SHIP_SCORE) {
		int snd;
		Players[Player_num].lives += Players[Player_num].score/EXTRA_SHIP_SCORE - prev_score/EXTRA_SHIP_SCORE;
		if ((snd=Powerup_info[POW_EXTRA_LIFE].hit_sound) > -1 )
			digi_play_sample( snd, F1_0 );
	}
}

// Decode cockpit bitmap to deccpt and add alpha fields to weapon boxes (as it should have always been) so we later can render sub bitmaps over the window canvases
void cockpit_decode_alpha(grs_bitmap *bm)
{

	int i=0,x=0,y=0;
	static unsigned char *cur=NULL;
	static short cur_w=0, cur_h=0;
	static unsigned char cockpitbuf[1024*1024];

	// check if we processed this bitmap already
	if (cur==bm->bm_data && cur_w == bm->bm_w && cur_h == bm->bm_h)
		return;

	memset(cockpitbuf,0,1024*1024);

	// decode the bitmap
	if (bm->bm_flags & BM_FLAG_RLE){
		unsigned char * dbits;
		unsigned char * sbits;
		int i, data_offset;

		data_offset = 1;
		if (bm->bm_flags & BM_FLAG_RLE_BIG)
			data_offset = 2;

		sbits = &bm->bm_data[4 + (bm->bm_h * data_offset)];
		dbits = cockpitbuf;

		for (i=0; i < bm->bm_h; i++ )    {
			gr_rle_decode(sbits,dbits);
			if ( bm->bm_flags & BM_FLAG_RLE_BIG )
				sbits += (int)INTEL_SHORT(*((short *)&(bm->bm_data[4+(i*data_offset)])));
			else
				sbits += (int)bm->bm_data[4+i];
			dbits += bm->bm_w;
		}
	}
	else
	{
		memcpy(&cockpitbuf, bm->bm_data, sizeof(unsigned char)*(bm->bm_w*bm->bm_h));
	}

	// add alpha color to the pixels which are inside the window box spans
	for (y=0;y<bm->bm_h;y++)
	{
		for (x=0;x<bm->bm_w;x++)
		{
			if (y >= (HIRESMODE?364:151) && y <= (HIRESMODE?469:193) && ((x >= WinBoxLeft[y-(HIRESMODE?364:151)].l && x <= WinBoxLeft[y-(HIRESMODE?364:151)].r) ||  (x >=WinBoxRight[y-(HIRESMODE?364:151)].l && x <= WinBoxRight[y-(HIRESMODE?364:151)].r)))
				cockpitbuf[i]=TRANSPARENCY_COLOR;
			i++;
		}
	}
#ifdef OGL
	ogl_freebmtexture(bm);
#endif
	gr_init_bitmap (&deccpt, 0, 0, 0, bm->bm_w, bm->bm_h, bm->bm_w, cockpitbuf);
	gr_set_transparent(&deccpt,1);
#ifdef OGL
	ogl_ubitmapm_cs (0, 0, -1, -1, &deccpt, 255, F1_0); // render one time to init the texture
#endif
	if (WinBoxOverlay[0] != NULL)
		gr_free_sub_bitmap(WinBoxOverlay[0]);
	if (WinBoxOverlay[1] != NULL)
		gr_free_sub_bitmap(WinBoxOverlay[1]);
	WinBoxOverlay[0] = gr_create_sub_bitmap(&deccpt,(PRIMARY_W_BOX_LEFT)-2,(PRIMARY_W_BOX_TOP)-2,(PRIMARY_W_BOX_RIGHT-PRIMARY_W_BOX_LEFT+4),(PRIMARY_W_BOX_BOT-PRIMARY_W_BOX_TOP+4));
	WinBoxOverlay[1] = gr_create_sub_bitmap(&deccpt,(SECONDARY_W_BOX_LEFT)-2,(SECONDARY_W_BOX_TOP)-2,(SECONDARY_W_BOX_RIGHT-SECONDARY_W_BOX_LEFT)+4,(SECONDARY_W_BOX_BOT-SECONDARY_W_BOX_TOP)+4);

	cur = bm->bm_data;
	cur_w = bm->bm_w;
	cur_h = bm->bm_h;
}

void draw_wbu_overlay()
{
	grs_bitmap * bm = &GameBitmaps[cockpit_bitmap[PlayerCfg.CockpitMode[1]].index];

	PIGGY_PAGE_IN(cockpit_bitmap[PlayerCfg.CockpitMode[1]]);
	cockpit_decode_alpha(bm);

	if (WinBoxOverlay[0] != NULL)
		hud_bitblt(HUD_SCALE_X(PRIMARY_W_BOX_LEFT-2),HUD_SCALE_Y(PRIMARY_W_BOX_TOP-2),WinBoxOverlay[0]);
	if (WinBoxOverlay[1] != NULL)
		hud_bitblt(HUD_SCALE_X(SECONDARY_W_BOX_LEFT-2),HUD_SCALE_Y(SECONDARY_W_BOX_TOP-2),WinBoxOverlay[1]);
}

void close_gauges()
{
	if (WinBoxOverlay[0] != NULL)
		gr_free_sub_bitmap(WinBoxOverlay[0]);
	if (WinBoxOverlay[1] != NULL)
		gr_free_sub_bitmap(WinBoxOverlay[1]);
	WinBoxOverlay[0] = NULL;
	WinBoxOverlay[1] = NULL;
}

void init_gauges()
{
	old_weapon[0] = old_weapon[1] = -1;
}

void draw_energy_bar(int energy)
{
	int x1, x2, y;
	int not_energy = (HIRESMODE?(HUD_SCALE_X(125 - (energy*125)/100)):(HUD_SCALE_X(63 - (energy*63)/100)));
	double aplitscale=((double)(HUD_SCALE_X(65)/HUD_SCALE_Y(8))/(65/8)); //scale aplitude of energy bar to current resolution aspect

	// Draw left energy bar
	PIGGY_PAGE_IN(Gauges[GAUGE_ENERGY_LEFT]);
	hud_bitblt (HUD_SCALE_X(LEFT_ENERGY_GAUGE_X), HUD_SCALE_Y(LEFT_ENERGY_GAUGE_Y), &GameBitmaps[Gauges[GAUGE_ENERGY_LEFT].index]);

	gr_setcolor(BM_XRGB(0,0,0));

	if (energy < 100)
		for (y=0; y < HUD_SCALE_Y(LEFT_ENERGY_GAUGE_H); y++) {
			x1 = HUD_SCALE_X(LEFT_ENERGY_GAUGE_H - 2) - y*(aplitscale);
			x2 = HUD_SCALE_X(LEFT_ENERGY_GAUGE_H - 2) - y*(aplitscale) + not_energy;

			if (x2 > HUD_SCALE_X(LEFT_ENERGY_GAUGE_W) - (y*aplitscale)/3)
				x2 = HUD_SCALE_X(LEFT_ENERGY_GAUGE_W) - (y*aplitscale)/3;

			if (x2 > x1) gr_uline( i2f(x1+HUD_SCALE_X(LEFT_ENERGY_GAUGE_X)), i2f(y+HUD_SCALE_Y(LEFT_ENERGY_GAUGE_Y)), i2f(x2+HUD_SCALE_X(LEFT_ENERGY_GAUGE_X)), i2f(y+HUD_SCALE_Y(LEFT_ENERGY_GAUGE_Y)) );
		}

	gr_set_current_canvas( NULL );

	// Draw right energy bar
	PIGGY_PAGE_IN(Gauges[GAUGE_ENERGY_RIGHT]);
	hud_bitblt (HUD_SCALE_X(RIGHT_ENERGY_GAUGE_X), HUD_SCALE_Y(RIGHT_ENERGY_GAUGE_Y), &GameBitmaps[Gauges[GAUGE_ENERGY_RIGHT].index]);

	if (energy < 100)
		for (y=0; y < HUD_SCALE_Y(RIGHT_ENERGY_GAUGE_H); y++) {
			x1 = HUD_SCALE_X(RIGHT_ENERGY_GAUGE_W - RIGHT_ENERGY_GAUGE_H + 2 ) + y*(aplitscale) - not_energy;
			x2 = HUD_SCALE_X(RIGHT_ENERGY_GAUGE_W - RIGHT_ENERGY_GAUGE_H + 2 ) + y*(aplitscale);

			if (x1 < (y*aplitscale)/3)
				x1 = (y*aplitscale)/3;

			if (x2 > x1) gr_uline( i2f(x1+HUD_SCALE_X(RIGHT_ENERGY_GAUGE_X)), i2f(y+HUD_SCALE_Y(RIGHT_ENERGY_GAUGE_Y)), i2f(x2+HUD_SCALE_X(RIGHT_ENERGY_GAUGE_X)), i2f(y+HUD_SCALE_Y(RIGHT_ENERGY_GAUGE_Y)) );
		}

	gr_set_current_canvas( NULL );
}

void draw_shield_bar(int shield)
{
	int bm_num = shield>=100?9:(shield / 10);

	PIGGY_PAGE_IN(Gauges[GAUGE_SHIELDS+9-bm_num]);
	hud_bitblt (HUD_SCALE_X(SHIELD_GAUGE_X), HUD_SCALE_Y(SHIELD_GAUGE_Y), &GameBitmaps[Gauges[GAUGE_SHIELDS+9-bm_num].index]);
}

#define CLOAK_FADE_WAIT_TIME  0x400

void draw_player_ship(int cloak_state,int x, int y)
{
	static fix cloak_fade_timer=0;
	static int cloak_fade_value=GR_FADE_LEVELS-1;

	int pnum = get_pnum_for_hud();

	grs_bitmap *bm = NULL;

#ifdef NETWORK
	if (Game_mode & GM_TEAM)
	{
		PIGGY_PAGE_IN(Gauges[GAUGE_SHIPS+get_team(pnum)]);
		bm = &GameBitmaps[Gauges[GAUGE_SHIPS+get_team(pnum)].index];
	}
	else
#endif
	{
		int color = Netgame.players[pnum].color;

		PIGGY_PAGE_IN(Gauges[GAUGE_SHIPS+color]);
		bm = &GameBitmaps[Gauges[GAUGE_SHIPS+color].index];
	}

	//for(int i = 0; i < 8; i++) {
	//	con_printf(CON_NORMAL, "Player ship %d %d\n", i, Gauges[GAUGE_SHIPS+i].index);
	//}

	if (cloak_state)
	{
		static int step = 0;

		if (GameTime64-Players[pnum].cloak_time < F1_0)
		{
			step = -2;
		}
		else if (Players[pnum].cloak_time+CLOAK_TIME_MAX-GameTime64 <= F1_0*3)
		{
			if (cloak_fade_value >= (GR_FADE_LEVELS-1))
			{
				step = -2;
			}
			else if (cloak_fade_value <= 0)
			{
				step = 2;
			}
		}
		else
		{
			step = 0;
			cloak_fade_value = 0;
		}

		cloak_fade_timer -= FrameTime;

		while (cloak_fade_timer < 0)
		{
			cloak_fade_timer += CLOAK_FADE_WAIT_TIME;
			cloak_fade_value += step;
		}

		if (cloak_fade_value > (GR_FADE_LEVELS-1))
			cloak_fade_value = (GR_FADE_LEVELS-1);
		if (cloak_fade_value <= 0)
			cloak_fade_value = 0;
	}
	else
	{
		cloak_fade_timer = 0;
		cloak_fade_value = GR_FADE_LEVELS-1;
	}

	gr_set_current_canvas(NULL);
	hud_bitblt( HUD_SCALE_X(x), HUD_SCALE_Y(y), bm);
	gr_settransblend(cloak_fade_value, GR_BLEND_NORMAL);
	gr_rect(HUD_SCALE_X(x-3), HUD_SCALE_Y(y-3), HUD_SCALE_X(x+bm->bm_w+3), HUD_SCALE_Y(y+bm->bm_h+3));
	gr_settransblend(GR_FADE_OFF, GR_BLEND_NORMAL);
	gr_set_current_canvas( NULL );
}

#define INV_FRAME_TIME	(f1_0/10)		//how long for each frame

void draw_numerical_display(int shield, int energy)
{
	int sw,sh,saw,ew,eh,eaw;

	gr_set_curfont( GAME_FONT );
#ifndef OGL
	PIGGY_PAGE_IN(Gauges[GAUGE_NUMERICAL]);
	hud_bitblt (HUD_SCALE_X(NUMERICAL_GAUGE_X), HUD_SCALE_Y(NUMERICAL_GAUGE_Y), &GameBitmaps[Gauges[GAUGE_NUMERICAL].index]);
#endif
	// cockpit is not 100% geometric so we need to divide shield and energy X position by 1.951 which should be most accurate
	// gr_get_string_size is used so we can get the numbers finally in the correct position with sw and ew
	gr_set_fontcolor(BM_XRGB(14,14,23),-1 );
	gr_get_string_size((shield>199)?"200":(shield>99)?"100":(shield>9)?"00":"0",&sw,&sh,&saw);
	gr_printf(	(grd_curscreen->sc_w/1.951)-(sw/2),
			(grd_curscreen->sc_h/1.365),"%d",shield);

	gr_set_fontcolor(BM_XRGB(25,18,6),-1 );
	gr_get_string_size((energy>199)?"200":(energy>99)?"100":(energy>9)?"00":"0",&ew,&eh,&eaw);
	gr_printf(	(grd_curscreen->sc_w/1.951)-(ew/2),
			(grd_curscreen->sc_h/1.5),"%d",energy);

	gr_set_current_canvas( NULL );
}


void draw_keys()
{
	gr_set_current_canvas( NULL );

	int pnum = get_pnum_for_hud();

	if (Players[pnum].flags & PLAYER_FLAGS_BLUE_KEY )	{
		PIGGY_PAGE_IN(Gauges[GAUGE_BLUE_KEY]);
                hud_bitblt( HUD_SCALE_X(GAUGE_BLUE_KEY_X), HUD_SCALE_Y(GAUGE_BLUE_KEY_Y), &GameBitmaps[Gauges[GAUGE_BLUE_KEY].index]);
	} else {
		PIGGY_PAGE_IN(Gauges[GAUGE_BLUE_KEY_OFF]);
		hud_bitblt( HUD_SCALE_X(GAUGE_BLUE_KEY_X), HUD_SCALE_Y(GAUGE_BLUE_KEY_Y), &GameBitmaps[Gauges[GAUGE_BLUE_KEY_OFF].index]);
	}

	if (Players[pnum].flags & PLAYER_FLAGS_GOLD_KEY)	{
		PIGGY_PAGE_IN(Gauges[GAUGE_GOLD_KEY]);
		hud_bitblt( HUD_SCALE_X(GAUGE_GOLD_KEY_X), HUD_SCALE_Y(GAUGE_GOLD_KEY_Y), &GameBitmaps[Gauges[GAUGE_GOLD_KEY].index]);
	} else {
		PIGGY_PAGE_IN(Gauges[GAUGE_GOLD_KEY_OFF]);
		hud_bitblt( HUD_SCALE_X(GAUGE_GOLD_KEY_X), HUD_SCALE_Y(GAUGE_GOLD_KEY_Y), &GameBitmaps[Gauges[GAUGE_GOLD_KEY_OFF].index]);
	}

	if (Players[pnum].flags & PLAYER_FLAGS_RED_KEY)	{
		PIGGY_PAGE_IN( Gauges[GAUGE_RED_KEY] );
		hud_bitblt( HUD_SCALE_X(GAUGE_RED_KEY_X), HUD_SCALE_Y(GAUGE_RED_KEY_Y), &GameBitmaps[Gauges[GAUGE_RED_KEY].index]);
	} else {
		PIGGY_PAGE_IN(Gauges[GAUGE_RED_KEY_OFF]);
		hud_bitblt( HUD_SCALE_X(GAUGE_RED_KEY_X), HUD_SCALE_Y(GAUGE_RED_KEY_Y), &GameBitmaps[Gauges[GAUGE_RED_KEY_OFF].index]);
	}
}


void draw_weapon_info_sub(int info_index,gauge_box *box,int pic_x,int pic_y,char *name,int text_x,int text_y)
{
	grs_bitmap *bm;

	int pnum = get_pnum_for_hud();

	//clear the window
	gr_setcolor(BM_XRGB(0,0,0));
	gr_rect(HUD_SCALE_X(box->left),HUD_SCALE_Y(box->top),HUD_SCALE_X(box->right),HUD_SCALE_Y(box->bot+1));

	bm=&GameBitmaps[Weapon_info[info_index].picture.index];
	Assert(bm != NULL);

	PIGGY_PAGE_IN( Weapon_info[info_index].picture );
	hud_bitblt(HUD_SCALE_X(pic_x), HUD_SCALE_Y(pic_y), bm);

	if (PlayerCfg.HudMode == 0)
	{
		gr_set_fontcolor(BM_XRGB(0,20,0),-1 );

		gr_string(text_x,text_y,name);

		//	For laser, show level and quadness
		if (info_index == LASER_INDEX)
		{
			gr_printf(text_x,text_y+LINE_SPACING, "%s: %i", TXT_LVL, Players[pnum].laser_level+1);
			if (Players[pnum].flags & PLAYER_FLAGS_QUAD_LASERS)
				gr_string(text_x,text_y+(LINE_SPACING*2), TXT_QUAD);
		}
	}
}

void draw_weapon_info(int weapon_type,int weapon_num)
{
	int x,y;

#ifdef SHAREWARE
	if (Newdemo_state==ND_STATE_RECORDING )
		newdemo_record_player_weapon(weapon_type, weapon_num);
#endif

	if (weapon_type == 0)
	{
		if (PlayerCfg.CockpitMode[1] == CM_STATUS_BAR)
		{
			draw_weapon_info_sub(Primary_weapon_to_weapon_info[weapon_num],&gauge_boxes[SB_PRIMARY_BOX],SB_PRIMARY_W_PIC_X,SB_PRIMARY_W_PIC_Y,PRIMARY_WEAPON_NAMES_SHORT(weapon_num),SB_PRIMARY_W_TEXT_X,SB_PRIMARY_W_TEXT_Y);
			x=SB_PRIMARY_AMMO_X;
			y=SB_PRIMARY_AMMO_Y;
		}
		else
		{
			draw_weapon_info_sub(Primary_weapon_to_weapon_info[weapon_num],&gauge_boxes[COCKPIT_PRIMARY_BOX],PRIMARY_W_PIC_X,PRIMARY_W_PIC_Y, PRIMARY_WEAPON_NAMES_SHORT(weapon_num),PRIMARY_W_TEXT_X,PRIMARY_W_TEXT_Y);
			x=PRIMARY_AMMO_X;
			y=PRIMARY_AMMO_Y;
		}
	}
	else
	{
		if (PlayerCfg.CockpitMode[1] == CM_STATUS_BAR)
		{
			draw_weapon_info_sub(Secondary_weapon_to_weapon_info[weapon_num],&gauge_boxes[SB_SECONDARY_BOX],SB_SECONDARY_W_PIC_X,SB_SECONDARY_W_PIC_Y,SECONDARY_WEAPON_NAMES_SHORT(weapon_num),SB_SECONDARY_W_TEXT_X,SB_SECONDARY_W_TEXT_Y);
			x=SB_SECONDARY_AMMO_X;
			y=SB_SECONDARY_AMMO_Y;
		}
		else
		{
			draw_weapon_info_sub(Secondary_weapon_to_weapon_info[weapon_num],&gauge_boxes[COCKPIT_SECONDARY_BOX],SECONDARY_W_PIC_X,SECONDARY_W_PIC_Y,SECONDARY_WEAPON_NAMES_SHORT(weapon_num),SECONDARY_W_TEXT_X,SECONDARY_W_TEXT_Y);
			x=SECONDARY_AMMO_X;
			y=SECONDARY_AMMO_Y;
		}
	}

	if (PlayerCfg.HudMode!=0)
		hud_show_weapons_mode(weapon_type,1,x,y);
}

void draw_ammo_info(int x,int y,int ammo_count,int primary)
{
	if (PlayerCfg.HudMode!=0)
		hud_show_weapons_mode(!primary,1,x,y);
	else
	{
		gr_setcolor(BM_XRGB(0,0,0));
		gr_set_fontcolor(BM_XRGB(20,0,0),-1 );
		gr_printf(x,y,"%03d",ammo_count);
	}
}

void draw_primary_ammo_info(int ammo_count)
{
	if (PlayerCfg.CockpitMode[1] == CM_STATUS_BAR)
		draw_ammo_info(SB_PRIMARY_AMMO_X,SB_PRIMARY_AMMO_Y,ammo_count,1);
	else
		draw_ammo_info(PRIMARY_AMMO_X,PRIMARY_AMMO_Y,ammo_count,1);
}

void draw_secondary_ammo_info(int ammo_count)
{
	if (PlayerCfg.CockpitMode[1] == CM_STATUS_BAR)
		draw_ammo_info(SB_SECONDARY_AMMO_X,SB_SECONDARY_AMMO_Y,ammo_count,0);
	else
		draw_ammo_info(SECONDARY_AMMO_X,SECONDARY_AMMO_Y,ammo_count,0);
}

void draw_weapon_box(int weapon_type,int weapon_num)
{
	gr_set_current_canvas(NULL);

	gr_set_curfont( GAME_FONT );

	if (weapon_num != old_weapon[weapon_type] && weapon_box_states[weapon_type] == WS_SET && (old_weapon[weapon_type] != -1) && !PlayerCfg.HudMode)
	{
		weapon_box_states[weapon_type] = WS_FADING_OUT;
		weapon_box_fade_values[weapon_type]=i2f(GR_FADE_LEVELS-1);
	}

	if (old_weapon[weapon_type] == -1)
	{
		draw_weapon_info(weapon_type,weapon_num);
		old_weapon[weapon_type] = weapon_num;
		weapon_box_states[weapon_type] = WS_SET;
	}

	if (weapon_box_states[weapon_type] == WS_FADING_OUT) {
		draw_weapon_info(weapon_type,old_weapon[weapon_type]);
		weapon_box_fade_values[weapon_type] -= FrameTime * FADE_SCALE;
		if (weapon_box_fade_values[weapon_type] <= 0)
		{
			weapon_box_states[weapon_type] = WS_FADING_IN;
			old_weapon[weapon_type] = weapon_num;
			old_weapon[weapon_type] = weapon_num;
			weapon_box_fade_values[weapon_type] = 0;
		}
	}
	else if (weapon_box_states[weapon_type] == WS_FADING_IN)
	{
		if (weapon_num != old_weapon[weapon_type])
		{
			weapon_box_states[weapon_type] = WS_FADING_OUT;
		}
		else
		{
			draw_weapon_info(weapon_type,weapon_num);
			weapon_box_fade_values[weapon_type] += FrameTime * FADE_SCALE;
			if (weapon_box_fade_values[weapon_type] >= i2f(GR_FADE_LEVELS-1)) {
				weapon_box_states[weapon_type] = WS_SET;
				old_weapon[weapon_type] = -1;
			}
		}
	}
	else
	{
		draw_weapon_info(weapon_type, weapon_num);
		old_weapon[weapon_type] = weapon_num;
	}


	if (weapon_box_states[weapon_type] != WS_SET)         //fade gauge
	{
		int fade_value = f2i(weapon_box_fade_values[weapon_type]);
		int boxofs = (PlayerCfg.CockpitMode[1]==CM_STATUS_BAR)?SB_PRIMARY_BOX:COCKPIT_PRIMARY_BOX;

		gr_settransblend(fade_value, GR_BLEND_NORMAL);
		gr_rect(HUD_SCALE_X(gauge_boxes[boxofs+weapon_type].left),HUD_SCALE_Y(gauge_boxes[boxofs+weapon_type].top),HUD_SCALE_X(gauge_boxes[boxofs+weapon_type].right),HUD_SCALE_Y(gauge_boxes[boxofs+weapon_type].bot));

		gr_settransblend(GR_FADE_OFF, GR_BLEND_NORMAL);
	}

	gr_set_current_canvas(NULL);

}

void draw_weapon_boxes()
{
	int pnum = get_pnum_for_hud();

	draw_weapon_box(0,Players[pnum].primary_weapon);

	if (weapon_box_states[0] == WS_SET)
		if (Players[pnum].primary_weapon == VULCAN_INDEX)
		{
			if (Newdemo_state == ND_STATE_RECORDING)
				newdemo_record_primary_ammo(Players[pnum].primary_ammo[Players[pnum].primary_weapon]);
			draw_primary_ammo_info(f2i(VULCAN_AMMO_SCALE * Players[pnum].primary_ammo[Players[pnum].primary_weapon]));
		}

	draw_weapon_box(1,Players[pnum].secondary_weapon);

	if (weapon_box_states[1] == WS_SET)
		if (Newdemo_state == ND_STATE_RECORDING)
			newdemo_record_secondary_ammo(Players[pnum].secondary_ammo[Players[pnum].secondary_weapon]);

	draw_secondary_ammo_info(Players[pnum].secondary_ammo[Players[pnum].secondary_weapon]);

	if(PlayerCfg.HudMode!=0)
	{
		draw_primary_ammo_info(f2i(VULCAN_AMMO_SCALE * Players[pnum].primary_ammo[Players[pnum].primary_weapon]));
		draw_secondary_ammo_info(Players[pnum].secondary_ammo[Players[pnum].secondary_weapon]);
	}
}

void sb_draw_energy_bar(int energy)
{
	int erase_height;
	int ew, eh, eaw;

	PIGGY_PAGE_IN(Gauges[SB_GAUGE_ENERGY]);
	hud_bitblt( HUD_SCALE_X(SB_ENERGY_GAUGE_X), HUD_SCALE_Y(SB_ENERGY_GAUGE_Y), &GameBitmaps[Gauges[SB_GAUGE_ENERGY].index]);

	erase_height = HUD_SCALE_Y((100 - energy) * SB_ENERGY_GAUGE_H / 100);

	if (erase_height > 0) {
		gr_setcolor( 0 );
		gr_urect(HUD_SCALE_X(SB_ENERGY_GAUGE_X), HUD_SCALE_Y(SB_ENERGY_GAUGE_Y), HUD_SCALE_X(SB_ENERGY_GAUGE_X) + HUD_SCALE_X(SB_ENERGY_GAUGE_W) - 1, HUD_SCALE_Y(SB_ENERGY_GAUGE_Y) + erase_height - 1);
	}

	gr_set_current_canvas( NULL );

	//draw numbers
	gr_set_fontcolor(BM_XRGB(25,18,6),-1 );
	gr_get_string_size((energy>199)?"200":(energy>99)?"100":(energy>9)?"00":"0",&ew,&eh,&eaw);
	gr_printf((grd_curscreen->sc_w/3)-(ew/2),HUD_SCALE_Y(SB_ENERGY_NUM_Y),"%d",energy);
}

void sb_draw_shield_num(int shield)
{
	grs_bitmap *bm = &GameBitmaps[cockpit_bitmap[PlayerCfg.CockpitMode[1]].index];
	int sw, sh, saw;

	//draw numbers
	gr_set_curfont( GAME_FONT );
	gr_set_fontcolor(BM_XRGB(14,14,23),-1 );

	//erase old one
	PIGGY_PAGE_IN( cockpit_bitmap[PlayerCfg.CockpitMode[1]] );
	gr_setcolor(gr_gpixel(bm,SB_SHIELD_NUM_X,SB_SHIELD_NUM_Y-(SM_H(Game_screen_mode)-bm->bm_h)));
	gr_get_string_size((shield>199)?"200":(shield>99)?"100":(shield>9)?"00":"0",&sw,&sh,&saw);
	gr_printf((grd_curscreen->sc_w/2.267)-(sw/2),HUD_SCALE_Y(SB_SHIELD_NUM_Y),"%d",shield);
}

void sb_draw_shield_bar(int shield)
{
	int bm_num = shield>=100?9:(shield / 10);

	gr_set_current_canvas(NULL);

	PIGGY_PAGE_IN( Gauges[GAUGE_SHIELDS+9-bm_num] );
	hud_bitblt( HUD_SCALE_X(SB_SHIELD_GAUGE_X), HUD_SCALE_Y(SB_SHIELD_GAUGE_Y), &GameBitmaps[Gauges[GAUGE_SHIELDS+9-bm_num].index]);
}

void sb_draw_keys()
{
	int pnum = get_pnum_for_hud();

	grs_bitmap * bm;
	int flags = Players[pnum].flags;

	gr_set_current_canvas(NULL);
	bm = &GameBitmaps[Gauges[(flags&PLAYER_FLAGS_BLUE_KEY)?SB_GAUGE_BLUE_KEY:SB_GAUGE_BLUE_KEY_OFF].index];
	PIGGY_PAGE_IN(Gauges[(flags&PLAYER_FLAGS_BLUE_KEY)?SB_GAUGE_BLUE_KEY:SB_GAUGE_BLUE_KEY_OFF]);
	hud_bitblt( HUD_SCALE_X(SB_GAUGE_KEYS_X), HUD_SCALE_Y(SB_GAUGE_BLUE_KEY_Y), bm);
	bm = &GameBitmaps[Gauges[(flags&PLAYER_FLAGS_GOLD_KEY)?SB_GAUGE_GOLD_KEY:SB_GAUGE_GOLD_KEY_OFF].index];
	PIGGY_PAGE_IN(Gauges[(flags&PLAYER_FLAGS_GOLD_KEY)?SB_GAUGE_GOLD_KEY:SB_GAUGE_GOLD_KEY_OFF]);
	hud_bitblt( HUD_SCALE_X(SB_GAUGE_KEYS_X), HUD_SCALE_Y(SB_GAUGE_GOLD_KEY_Y), bm);
	bm = &GameBitmaps[Gauges[(flags&PLAYER_FLAGS_RED_KEY)?SB_GAUGE_RED_KEY:SB_GAUGE_RED_KEY_OFF].index];
	PIGGY_PAGE_IN(Gauges[(flags&PLAYER_FLAGS_RED_KEY)?SB_GAUGE_RED_KEY:SB_GAUGE_RED_KEY_OFF]);
	hud_bitblt( HUD_SCALE_X(SB_GAUGE_KEYS_X), HUD_SCALE_Y(SB_GAUGE_RED_KEY_Y), bm);
}

//	Draws invulnerable ship, or maybe the flashing ship, depending on invulnerability time left.
void draw_invulnerable_ship()
{
	static fix time=0;

	int pnum = get_pnum_for_hud();

	gr_set_current_canvas(NULL);

	if (Players[pnum].invulnerable_time+INVULNERABLE_TIME_MAX-GameTime64 > 0 && (Players[pnum].invulnerable_time+INVULNERABLE_TIME_MAX-GameTime64 > F1_0*4 || GameTime64 & 0x8000))
	{

		if (PlayerCfg.CockpitMode[1] == CM_STATUS_BAR)	{
			PIGGY_PAGE_IN(Gauges[GAUGE_INVULNERABLE+invulnerable_frame]);
			hud_bitblt( HUD_SCALE_X(SB_SHIELD_GAUGE_X), HUD_SCALE_Y(SB_SHIELD_GAUGE_Y), &GameBitmaps[Gauges[GAUGE_INVULNERABLE+invulnerable_frame].index]);
		} else {
			PIGGY_PAGE_IN(Gauges[GAUGE_INVULNERABLE+invulnerable_frame]);
			hud_bitblt( HUD_SCALE_X(SHIELD_GAUGE_X), HUD_SCALE_Y(SHIELD_GAUGE_Y), &GameBitmaps[Gauges[GAUGE_INVULNERABLE+invulnerable_frame].index]);
		}

		time += FrameTime;

		while (time > INV_FRAME_TIME) {
			time -= INV_FRAME_TIME;
			if (++invulnerable_frame == N_INVULNERABLE_FRAMES)
				invulnerable_frame=0;
		}
	} else if (PlayerCfg.CockpitMode[1] == CM_STATUS_BAR)
		sb_draw_shield_bar(f2ir(Players[pnum].shields));
	else
		draw_shield_bar(f2ir(Players[pnum].shields));
}

extern int Missile_gun;
extern int allowed_to_fire_laser(void);
extern int allowed_to_fire_missile(void);

// CED -- shipcolor fix
const rgb player_rgb[] = {	//{0,0,0}, // black
							//{4,4,12}, // indigo
							{15,15,23},    // 0x7878B8 ---> blue
							//{23, 23, 23}, // white
							{27,0,0},      // 0xD80000 ---> red
							{0,23,0},      // 0x00B800 ---> green
							{30,11,31},    // 0xF058F8 ---> PINK
							{31,16,0},     // 0xF88000 ---> orange
							//{0,0,0}, // black
							//{23, 23, 23}, // white
							{24,17,6},     // 0xC08830 ---> light orange (PROBLEM)
							{14,21,12},    // 0x70A860 ---> light green (PROBLEM)
			                {29,29,0}, };  // 0xE8E800 ---> YELLOW

const rgb player_rgb_alt[] = {

							{15,15,23},    // 0x7878B8 ---> blue
							{27,0,0},      // 0xD80000 ---> red
							{0,23,0},      // 0x00B800 ---> green
							{30,11,31},    // 0xF058F8 ---> PINK
							{31,16,0},     // 0xF88000 ---> orange
							//{4,4,12}, 		// indigo
							{12,4,20}, 		// purple
							{23, 23, 23}, 	// white
							{29,29,0}, };  // 0xE8E800 ---> YELLOW

const rgb player_rgb_all_blue[] = {

							{15,15,23},    // 0x7878B8 ---> blue
							{15,15,23},
							{15,15,23},
							{15,15,23},
							{15,15,23},
							{15,15,23},
							{15,15,23},
							{15,15,23}, };

const rgb* selected_player_rgb;

typedef struct {
	sbyte x, y;
} xy;

//offsets for reticle parts: high-big  high-sml  low-big  low-sml
static const xy cross_offsets[4] = 		{ {-8,-5},	{-4,-2},	{-4,-2}, {-2,-1} };
static const xy primary_offsets[4] = 	{ {-30,14}, {-16,6},	{-15,6}, {-8, 2} };
static const xy secondary_offsets[4] =	{ {-24,2},	{-12,0}, {-12,1}, {-6,-2} };

//draw the reticle
void show_reticle(int reticle_type, int secondary_display)
{
	int pnum = get_pnum_for_hud();

	int x,y,size;
	int laser_ready,missile_ready,laser_ammo,missile_ammo;
	int cross_bm_num,primary_bm_num,secondary_bm_num;
	int use_hires_reticle,ofs;

	x = grd_curcanv->cv_bitmap.bm_w/2;
	y = grd_curcanv->cv_bitmap.bm_h/2;
	size = (grd_curcanv->cv_bitmap.bm_h / (32-(PlayerCfg.ReticleSize*4)));

	laser_ready = allowed_to_fire_laser();
	missile_ready = allowed_to_fire_missile();

	laser_ammo = player_has_weapon(pnum, Players[pnum].primary_weapon, 0);
	missile_ammo = player_has_weapon(pnum, Players[pnum].secondary_weapon, 1);

	primary_bm_num = (laser_ready && laser_ammo==HAS_ALL);
	secondary_bm_num = (missile_ready && missile_ammo==HAS_ALL);

	if (primary_bm_num && Players[pnum].primary_weapon==LASER_INDEX && (Players[pnum].flags & PLAYER_FLAGS_QUAD_LASERS))
		primary_bm_num++;

	if (Players[pnum].secondary_weapon!=CONCUSSION_INDEX && Players[pnum].secondary_weapon!=HOMING_INDEX)
		secondary_bm_num += 3;
	else if (secondary_bm_num && !(Missile_gun&1))
			secondary_bm_num++;

	cross_bm_num = ((primary_bm_num > 0) || (secondary_bm_num > 0));

	Assert(primary_bm_num <= 2);
	Assert(secondary_bm_num <= 4);
	Assert(cross_bm_num <= 1);

	gr_setcolor(BM_XRGB(PlayerCfg.ReticleRGBA[0],PlayerCfg.ReticleRGBA[1],PlayerCfg.ReticleRGBA[2]));
	gr_settransblend(PlayerCfg.ReticleRGBA[3], GR_BLEND_NORMAL);

	switch (reticle_type)
	{
		case RET_TYPE_CLASSIC:
		{
			grs_bitmap *cross, *primary, *secondary;

			use_hires_reticle = (HIRESMODE != 0);
			ofs = (use_hires_reticle?0:2);

			PIGGY_PAGE_IN(Gauges[RETICLE_CROSS + cross_bm_num]);
			cross = &GameBitmaps[Gauges[RETICLE_CROSS + cross_bm_num].index];
			hud_bitblt_free(x+HUD_SCALE_X_AR(cross_offsets[ofs].x),y+HUD_SCALE_Y_AR(cross_offsets[ofs].y), HUD_SCALE_X_AR(cross->bm_w), HUD_SCALE_Y_AR(cross->bm_h), cross);

			PIGGY_PAGE_IN(Gauges[RETICLE_PRIMARY + primary_bm_num]);
			primary = &GameBitmaps[Gauges[RETICLE_PRIMARY + primary_bm_num].index];
			hud_bitblt_free(x+HUD_SCALE_X_AR(primary_offsets[ofs].x),y+HUD_SCALE_Y_AR(primary_offsets[ofs].y), HUD_SCALE_X_AR(primary->bm_w), HUD_SCALE_Y_AR(primary->bm_h), primary);

			PIGGY_PAGE_IN(Gauges[RETICLE_SECONDARY + secondary_bm_num]);
			secondary = &GameBitmaps[Gauges[RETICLE_SECONDARY + secondary_bm_num].index];
			hud_bitblt_free(x+HUD_SCALE_X_AR(secondary_offsets[ofs].x),y+HUD_SCALE_Y_AR(secondary_offsets[ofs].y), HUD_SCALE_X_AR(secondary->bm_w), HUD_SCALE_Y_AR(secondary->bm_h), secondary);
			break;
		}
		case RET_TYPE_CLASSIC_REBOOT:
		{
#ifdef OGL
			ogl_draw_vertex_reticle(cross_bm_num,primary_bm_num,secondary_bm_num,BM_XRGB(PlayerCfg.ReticleRGBA[0],PlayerCfg.ReticleRGBA[1],PlayerCfg.ReticleRGBA[2]),PlayerCfg.ReticleRGBA[3],PlayerCfg.ReticleSize);
#endif
			break;

		}
		case RET_TYPE_X:
			gr_uline(i2f(x-(size/2)), i2f(y-(size/2)), i2f(x-(size/5)), i2f(y-(size/5))); // top-left
			gr_uline(i2f(x+(size/2)), i2f(y-(size/2)), i2f(x+(size/5)), i2f(y-(size/5))); // top-right
			gr_uline(i2f(x-(size/2)), i2f(y+(size/2)), i2f(x-(size/5)), i2f(y+(size/5))); // bottom-left
			gr_uline(i2f(x+(size/2)), i2f(y+(size/2)), i2f(x+(size/5)), i2f(y+(size/5))); // bottom-right
			if (secondary_display && secondary_bm_num == 1)
				gr_uline(i2f(x-(size/2)-(size/5)), i2f(y-(size/2)), i2f(x-(size/5)-(size/5)), i2f(y-(size/5)));
			else if (secondary_display && secondary_bm_num == 2)
				gr_uline(i2f(x+(size/2)+(size/5)), i2f(y-(size/2)), i2f(x+(size/5)+(size/5)), i2f(y-(size/5)));
			else if (secondary_display && secondary_bm_num == 4)
				gr_uline(i2f(x+(size/2)), i2f(y+(size/2)+(size/5)), i2f(x+(size/5)), i2f(y+(size/5)+(size/5)));
			break;
		case RET_TYPE_DOT:
			gr_disk(i2f(x),i2f(y),i2f(size/5));
			if (secondary_display && secondary_bm_num == 1)
				gr_uline(i2f(x-(size/2)-(size/5)), i2f(y-(size/2)), i2f(x-(size/5)-(size/5)), i2f(y-(size/5)));
			else if (secondary_display && secondary_bm_num == 2)
				gr_uline(i2f(x+(size/2)+(size/5)), i2f(y-(size/2)), i2f(x+(size/5)+(size/5)), i2f(y-(size/5)));
			else if (secondary_display && secondary_bm_num == 4)
				gr_uline(i2f(x), i2f(y+(size/2)+(size/5)), i2f(x), i2f(y+(size/5)+(size/5)));
			break;
		case RET_TYPE_CIRCLE:
			// Hack!  Something is going wrong in OGL-land with these numbers (???)
			if(size == 33 && x == 960 && y == 540) { size = 24; }

			gr_ucircle(i2f(x),i2f(y),i2f(size/4));
			if (secondary_display && secondary_bm_num == 1)
				gr_uline(i2f(x-(size/2)-(size/5)), i2f(y-(size/2)), i2f(x-(size/5)-(size/5)), i2f(y-(size/5)));
			else if (secondary_display && secondary_bm_num == 2)
				gr_uline(i2f(x+(size/2)+(size/5)), i2f(y-(size/2)), i2f(x+(size/5)+(size/5)), i2f(y-(size/5)));
			else if (secondary_display && secondary_bm_num == 4)
				gr_uline(i2f(x), i2f(y+(size/2)+(size/5)), i2f(x), i2f(y+(size/5)+(size/5)));
			break;
		case RET_TYPE_CROSS_V1:
			gr_uline(i2f(x),i2f(y-(size/2)),i2f(x),i2f(y+(size/2)+1)); // horiz
			gr_uline(i2f(x-(size/2)),i2f(y),i2f(x+(size/2)+1),i2f(y)); // vert
			if (secondary_display && secondary_bm_num == 1)
				gr_uline(i2f(x-(size/2)), i2f(y-(size/2)), i2f(x-(size/5)), i2f(y-(size/5)));
			else if (secondary_display && secondary_bm_num == 2)
				gr_uline(i2f(x+(size/2)), i2f(y-(size/2)), i2f(x+(size/5)), i2f(y-(size/5)));
			else if (secondary_display && secondary_bm_num == 4)
				gr_uline(i2f(x-(size/2)), i2f(y+(size/2)), i2f(x-(size/5)), i2f(y+(size/5)));
			break;
		case RET_TYPE_CROSS_V2:
			gr_uline(i2f(x), i2f(y-(size/2)), i2f(x), i2f(y-(size/6))); // vert-top
			gr_uline(i2f(x), i2f(y+(size/2)), i2f(x), i2f(y+(size/6))); // vert-bottom
			gr_uline(i2f(x-(size/2)), i2f(y), i2f(x-(size/6)), i2f(y)); // horiz-left
			gr_uline(i2f(x+(size/2)), i2f(y), i2f(x+(size/6)), i2f(y)); // horiz-right
			if (secondary_display && secondary_bm_num == 1)
				gr_uline(i2f(x-(size/2)), i2f(y-(size/2)), i2f(x-(size/5)), i2f(y-(size/5)));
			else if (secondary_display && secondary_bm_num == 2)
				gr_uline(i2f(x+(size/2)), i2f(y-(size/2)), i2f(x+(size/5)), i2f(y-(size/5)));
			else if (secondary_display && secondary_bm_num == 4)
				gr_uline(i2f(x-(size/2)), i2f(y+(size/2)), i2f(x-(size/5)), i2f(y+(size/5)));
			break;
		case RET_TYPE_ANGLE:
			gr_uline(i2f(x),i2f(y),i2f(x),i2f(y+(size/2))); // vert
			gr_uline(i2f(x),i2f(y),i2f(x+(size/2)),i2f(y)); // horiz
			if (secondary_display && secondary_bm_num == 1)
				gr_uline(i2f(x-(size/2)), i2f(y-(size/2)), i2f(x-(size/5)), i2f(y-(size/5)));
			else if (secondary_display && secondary_bm_num == 2)
				gr_uline(i2f(x+(size/2)), i2f(y-(size/2)), i2f(x+(size/5)), i2f(y-(size/5)));
			else if (secondary_display && secondary_bm_num == 4)
				gr_uline(i2f(x-(size/2)), i2f(y+(size/2)), i2f(x-(size/5)), i2f(y+(size/5)));
			break;
		case RET_TYPE_NONE:
			break;
		default:
			break;
	}
	gr_settransblend(GR_FADE_OFF, GR_BLEND_NORMAL);
}

void show_mousefs_indicator(int mx, int my, int mz, int x, int y, int size)
{
	int axscale = (MOUSEFS_DELTA_RANGE*2)/size, xaxpos = x+(mx/axscale), yaxpos = y+(my/axscale), zaxpos = y+(mz/axscale);

	gr_setcolor(BM_XRGB(PlayerCfg.ReticleRGBA[0],PlayerCfg.ReticleRGBA[1],PlayerCfg.ReticleRGBA[2]));
	gr_settransblend(PlayerCfg.ReticleRGBA[3], GR_BLEND_NORMAL);
	gr_uline(i2f(xaxpos), i2f(y-(size/2)), i2f(xaxpos), i2f(y-(size/4)));
	gr_uline(i2f(xaxpos), i2f(y+(size/2)), i2f(xaxpos), i2f(y+(size/4)));
	gr_uline(i2f(x-(size/2)), i2f(yaxpos), i2f(x-(size/4)), i2f(yaxpos));
	gr_uline(i2f(x+(size/2)), i2f(yaxpos), i2f(x+(size/4)), i2f(yaxpos));
	gr_uline(i2f(x+(size/2)+HUD_SCALE_X_AR(2)), i2f(y), i2f(x+(size/2)+HUD_SCALE_X_AR(2)), i2f(zaxpos));
	gr_settransblend(GR_FADE_OFF, GR_BLEND_NORMAL);
}

extern struct connection_status connection_statuses[];

void fontcolor_bad() {
	gr_set_fontcolor(BM_XRGB(25, 0, 0), -1);
}

void fontcolor_good() {
	gr_set_fontcolor(BM_XRGB(0, 18, 0), -1);
}

int n_players,player_list[MAX_PLAYERS];

#ifdef NETWORK
void hud_show_kill_list()
{
	int n_left,i,x0,x1,y,save_y;

	if (Show_kill_list_timer > 0)
	{
		Show_kill_list_timer -= FrameTime;
		if (Show_kill_list_timer < 0)
			Show_kill_list = 0;
	}

	gr_set_curfont( GAME_FONT );

	int players = n_players;

	if (Show_kill_list == 3)
		players = 2;

	if (players <= 4 + (Netgame.host_is_obs ? 1 : 0))
		n_left = players;
	else
		n_left = (players + (Netgame.host_is_obs ? 0 : 1)) / 2;

    if(Netgame.BlackAndWhitePyros)
		selected_player_rgb = player_rgb_alt;
	else
		selected_player_rgb = player_rgb;

	x0 = FSPACX(1); x1 = FSPACX(43);

	if ((Game_mode & GM_MULTI_COOP) || (Game_mode & GM_MULTI_ROBOTS))
		x1 = FSPACX(31);

	save_y = y = grd_curcanv->cv_bitmap.bm_h - n_left*(LINE_SPACING);

	if (PlayerCfg.CockpitMode[1] == CM_FULL_COCKPIT) {
		save_y = y -= FSPACX(6);
		if ((Game_mode & GM_MULTI_COOP) || (Game_mode & GM_MULTI_ROBOTS))
			x1 = FSPACX(33);
		else
			x1 = FSPACX(43);
	}

	int ox1 = x1;
	for (i=0;i<players;i++) {
		int player_num;

		if (Show_kill_list == 3) {
			player_num = i;
		} else {
			player_num = player_list[i];
			if (Netgame.host_is_obs && player_num == 0) {
				continue;
			}
		}


		char name[9];
		int sw,sh,aw;

		if (i>=n_left) {
			if (PlayerCfg.CockpitMode[1] == CM_FULL_COCKPIT)
				x0 = grd_curcanv->cv_bitmap.bm_w - FSPACX(53);
			else
				x0 = grd_curcanv->cv_bitmap.bm_w - FSPACX(60);
			if ((Game_mode & GM_MULTI_COOP) || (Game_mode & GM_MULTI_ROBOTS))
				x1 = grd_curcanv->cv_bitmap.bm_w - FSPACX(27);
			else
				x1 = grd_curcanv->cv_bitmap.bm_w - FSPACX(15);  // Right edge of name, change this for width problems
			if (i==n_left)
				y = save_y;

			if (Netgame.KillGoal || Netgame.PlayTimeAllowed)
				x1-=FSPACX(18);

			if(! ((Game_mode & GM_MULTI_COOP) || (Game_mode & GM_MULTI_ROBOTS)) ) {
				x1 -= FSPACX(18);
				x0 -= FSPACX(18);
			}

			if(Show_network_stats) {
				x1 -= FSPACX(45);
				x0 -= FSPACX(45);

				if(Netgame.RetroProtocol) {
					x1 -= FSPACX(25);
					x0 -= FSPACX(25);
				}
			}

			if(Netgame.AllowPreferredColors) {
				x1 -= FSPACX(5);
				x0 -= FSPACX(5);
			}

			ox1 = x1;
		}
		else  if (Netgame.KillGoal || Netgame.PlayTimeAllowed)
		{
			x1 = FSPACX(43);
			ox1 = x1;
		}

		int lagx, loss_upx, loss_downx, cnxx;

		lagx = x1 + FSPACX(15);
		if (Netgame.KillGoal || Netgame.PlayTimeAllowed)
				lagx+=FSPACX(18);

		loss_upx = lagx + FSPACX(15);
		if(Netgame.RetroProtocol) {
			loss_downx = loss_upx + FSPACX(25);
		} else {
			loss_downx = loss_upx;
		}

		cnxx = loss_downx + FSPACX(25);

		int color;

		if (Players[player_num].connected != CONNECT_PLAYING) {
			gr_set_fontcolor(BM_XRGB(12, 12, 12), -1);
		} else if (Game_mode & GM_TEAM) {
			color = get_color_for_team(player_num, 0);
			gr_set_fontcolor(BM_XRGB(selected_player_rgb[color].r,selected_player_rgb[color].g,selected_player_rgb[color].b),-1 );
		} else {
			color = get_color_for_player(player_num, 0);
			gr_set_fontcolor(BM_XRGB(selected_player_rgb[color].r,selected_player_rgb[color].g,selected_player_rgb[color].b),-1 );
		}

		if (Show_kill_list == 3)
			strcpy(name, Netgame.team_name[i]);
		else if (Game_mode & GM_BOUNTY && player_num == Bounty_target && GameTime64&0x10000)
			strcpy(name,"[TARGET]");
		else
			strcpy(name,Players[player_num].callsign);	// Note link to above if!!
		gr_get_string_size(name,&sw,&sh,&aw);
		while (sw > (x1-x0-FSPACX(2))) {
			name[strlen(name)-1]=0;
			gr_get_string_size(name,&sw,&sh,&aw);
		}
		gr_printf(x0,y,"%s",name);


		if (Players[player_num].connected == CONNECT_PLAYING) {
			if (Game_mode & GM_TEAM) {
				color = get_color_for_team(player_num, 1);
				gr_set_fontcolor(BM_XRGB(selected_player_rgb[color].r,selected_player_rgb[color].g,selected_player_rgb[color].b),-1 );
			} else {
				color = get_color_for_player(player_num, 1);
				gr_set_fontcolor(BM_XRGB(selected_player_rgb[color].r,selected_player_rgb[color].g,selected_player_rgb[color].b),-1 );
			}
		}

		if (Show_kill_list==2)
		{
			if (Players[player_num].net_killed_total+Players[player_num].net_kills_total==0)
				gr_string (x1,y,"NA");
			else
				gr_printf (x1,y,"%d%%",(int)((float)((float)Players[player_num].net_kills_total/((float)Players[player_num].net_killed_total+(float)Players[player_num].net_kills_total))*100.0));
		}
		else if (Show_kill_list == 3) {
			if (Netgame.PlayTimeAllowed || Netgame.KillGoal)
				gr_printf(x1,y,"%3d(%d)",team_kills[i],Netgame.TeamKillGoalCount[i]);
			else
				gr_printf(x1,y,"%3d",team_kills[i]);


		} else if ((Game_mode & GM_MULTI_COOP) || (Game_mode & GM_MULTI_ROBOTS) )
			gr_printf(x1,y,"%-6d",Players[player_num].score);

		else {


			if (Netgame.PlayTimeAllowed || Netgame.KillGoal)
				gr_printf(x1,y,"%3d(%d)",Players[player_num].net_kills_total,Players[player_num].KillGoalCount);
			else
				gr_printf(x1,y,"%3d",Players[player_num].net_kills_total);

		}


		if((Show_network_stats && !is_observer()) && player_num != Player_num && Players[player_num].connected && Show_kill_list != 3) {
			int lag = -1;

			if(Netgame.RetroProtocol) {
				lag = Netgame.players[player_num].ping;
			} else {
				if(multi_i_am_master()) {
					lag = Netgame.players[player_num].ping;
				} else if (player_num == multi_who_is_master()) {
					lag = Netgame.players[Player_num].ping;
				} else {
					lag = Netgame.players[Player_num].ping + Netgame.players[player_num].ping;
				}
			}

			if(lag != -1) {
				if(lag > 100) {
					fontcolor_bad();
				} else {
					fontcolor_good();
				}

				gr_printf(lagx,y,"% 3d", lag);
			}

			int loss_down = Netgame.players[i].loss;
			int loss_up = Netgame.players[i].rx_loss;

			if(Netgame.RetroProtocol) {
				if(loss_up) {
					fontcolor_bad();
				} else {
					fontcolor_good();
				}

				if(loss_up > 0 || loss_down > 0) {
					gr_printf(loss_upx,y,"% 3d%%", loss_up);
				}
			}

			if(loss_down) {
				fontcolor_bad();
			} else {
				fontcolor_good();
			}

			if(loss_up > 0 || loss_down > 0) {
				gr_printf(loss_downx,y,"% 3d%%", loss_down);
			}

			if(connection_statuses[player_num].type == CONNT_PROXY) {
				fontcolor_bad();

				if(loss_up > 0 || loss_down > 0) {
					gr_printf(cnxx + FSPACX(5), y,"P");
				} else {
					gr_printf(loss_upx,y,"P");
				}
			}


		}

		y += LINE_SPACING;
	}
}

void observer_show_time() {
	char time_str[8];
	int sw, sh, saw;
	int x;
	int y = 5;

	if (GameTime64 < 3600 * F1_0)
		sprintf(time_str, "%02i:%02i", (int)(f2i(GameTime64) / 60 % 60), (int)(f2i(GameTime64) % 60));
	else
		sprintf(time_str, "%i:%02i:%02i", (int)(f2i(GameTime64) / 3600), (int)(f2i(GameTime64) / 60 % 60), (int)(f2i(GameTime64) % 60));

	gr_set_curfont( MEDIUM3_FONT );
	gr_get_string_size( time_str, &sw, &sh, &saw );

	if ((Game_mode & GM_MULTI) && (Game_mode & GM_MULTI_COOP)) {
		// For co-op, we show 0.01-second precision.
		char decimal_str[3];
		char* t;
		int w = sw;
		int h = sh;

		sprintf(decimal_str, ".%02i", (int)(f2i(GameTime64 * 100) % 100));
		while ((t = strchr(decimal_str, '1')) != NULL)
			*t = '\x84';	//convert to wide '1'

		gr_set_curfont( GAME_FONT );
		gr_get_string_size( decimal_str, &sw, &sh, &saw );

		w += sw - 5;

		x = (grd_curcanv->cv_bitmap.bm_w - w ) / 2;

		gr_settransblend(14, GR_BLEND_NORMAL);
		gr_setcolor( BM_XRGB(0,0,0) );
		gr_rect( x - 10, y - 5, x + w + 10, y + h - 5);
		gr_settransblend(GR_FADE_OFF, GR_BLEND_NORMAL);

		gr_set_curfont( MEDIUM3_FONT );
		gr_string(x, y, time_str);

		gr_set_curfont( GAME_FONT );
		gr_set_fontcolor( BM_XRGB(15,15,23), -1 );
		gr_string(x + 5 + w - sw, y - 15 + h - sh, decimal_str);
	} else {
		// For all other modes, we show 1-second precision.
		x = (grd_curcanv->cv_bitmap.bm_w - sw ) / 2;

		gr_settransblend(14, GR_BLEND_NORMAL);
		gr_setcolor( BM_XRGB(0,0,0) );
		gr_rect( x - 10, y - 5, x + sw + 10, y + sh);
		gr_settransblend(GR_FADE_OFF, GR_BLEND_NORMAL);

		gr_string(0x8000, y, time_str );
	}
}

#define OBS_PLAYER_CARD_WIDTH 212
#define OBS_TIME_WIDTH 224

int observer_draw_player_card(int pnum, int color, int x, int y) {
	glLineWidth(1);

	int sw, sh, saw;
	int starty = y;

	y += 5;

	// Name
	gr_set_curfont( GAME_FONT );
	gr_set_fontcolor(color, -1);

	gr_get_string_size(Players[pnum].callsign, &sw, &sh, &saw);

	gr_printf(x + OBS_PLAYER_CARD_WIDTH / 2 - sw / 2, y, "%s", Players[pnum].callsign);

	y += 27;

	// Score
	char score[12];
	
	if ((Game_mode & GM_MULTI_COOP) || (Game_mode & GM_MULTI_ROBOTS)) {
		int_to_string(Players[pnum].score, score);

		gr_set_curfont( GAME_FONT );
		gr_set_fontcolor(color, -1);

		gr_get_string_size(score, &sw, &sh, &saw);

		gr_printf(x + OBS_PLAYER_CARD_WIDTH / 2 - sw / 2, y, "%s", score);

		y += 27;
	} else {
		sprintf(score, "%d", Players[pnum].net_kills_total);

		gr_set_curfont( MEDIUM1_FONT );

		gr_get_string_size(score, &sw, &sh, &saw);

		gr_printf(x + OBS_PLAYER_CARD_WIDTH - sw - 3, y, "%s", score);

		y += 17;
	}

	if (!Netgame.obs_min) {
		if (PlayerCfg.ObsShowScoreboardShieldText) {
			// Shields
			char shields[7];

			sprintf(shields, "%0.1f%s", f2db(Players[pnum].shields), Players[pnum].shields_certain ? "" : "?" );

			gr_set_curfont( GAME_FONT );
			gr_set_fontcolor(color, -1);

			gr_get_string_size(shields, &sw, &sh, &saw);

			if ((Game_mode & GM_MULTI_COOP) || (Game_mode & GM_MULTI_ROBOTS)) {
				gr_printf(x + OBS_PLAYER_CARD_WIDTH / 2 - sw / 2, y, "%s", shields);
			} else {
				gr_printf(x + 3, y, "%s", shields);
			}

			y += 27;
		}

		if (PlayerCfg.ObsShowScoreboardShieldBar) {
			// Shield display
			double shield_count = f2db(Players[pnum].shields);
			double delta = f2db(Players[pnum].shields_delta);
			fix pulse = GameTime64 & 0x1ffff;
			double pulse_strength = 0;

			if (pulse > 0x10000) {
				pulse_strength = 1;
			} else {
				pulse_strength = 0;
			}

			// Fill bar with color
			if (shield_count > 0) {
				gr_setcolor(color);
				gr_urect(x + 2, y, x + OBS_PLAYER_CARD_WIDTH - 2, y + 9);
			}

			// Replace empty with dark grey, or pulsing red if under 30 shields.
			if (shield_count < 100.0) {
				int grey_color = (shield_count > 0 && shield_count < 30 ? BM_XRGB(6 + (int)(10 * pulse_strength), 6, 6) : BM_XRGB(6, 6, 6));
				gr_setcolor(grey_color);
				gr_urect(x + 2 + (int)(shield_count * ((double)OBS_PLAYER_CARD_WIDTH - 4.0) / 100.0), y, x + OBS_PLAYER_CARD_WIDTH - 2, y + 9);
			}

			if (Players[pnum].shields_delta != 0 && (Players[Player_num].hours_total - Players[pnum].shields_time_hours == 1 && i2f(3600) + Players[Player_num].time_total - Players[pnum].shields_time < i2f(2) || Players[Player_num].time_total - Players[pnum].shields_time < i2f(2))) {
				if (shield_count > 0 && shield_count < 100 && delta < 0) {
					// Replace recent damage with red, unless at 0.
					gr_setcolor(BM_XRGB(31, 6, 6));
					gr_urect(x + 2 + (int)(shield_count * ((double)OBS_PLAYER_CARD_WIDTH - 4.0) / 100.0), y, min(x + 2 + (int)((shield_count - delta) * ((double)OBS_PLAYER_CARD_WIDTH - 4.0) / 100.0), x + OBS_PLAYER_CARD_WIDTH - 2), y + 9);
				} else if (shield_count < 100 - delta && delta > 0) {
					// Replace recent healing with green.
					gr_setcolor(BM_XRGB(6, 6, 31));
					gr_urect(x + 2 + (int)((shield_count - delta) * ((double)OBS_PLAYER_CARD_WIDTH - 4.0) / 100.0), y, min(x + 2 + (int)(shield_count * ((double)OBS_PLAYER_CARD_WIDTH - 4.0) / 100.0), x + OBS_PLAYER_CARD_WIDTH - 2), y + 9);
				}
			}

			// Divide bar into segments
			for (int seg = 1; seg < 10; seg++) {
				gr_setcolor(BM_XRGB(0, 0, 0));
				gr_uline(i2f(x + 1 + 21 * seg), i2f(y), i2f(x + 1 + 21 * seg), i2f(y + 10));
			}

			y += 11;
		}

		double energy = f2db(Players[pnum].energy);
		double ammo = f2db(Players[pnum].primary_ammo[1] * VULCAN_AMMO_SCALE);

		if (PlayerCfg.ObsShowAmmoBars) {
			// Energy display
			if (energy > 0) {
				gr_setcolor(BM_XRGB(25, 18, 6));
				gr_urect(x + 2, y, min(x + 2 + (int)(energy * ((double)OBS_PLAYER_CARD_WIDTH - 4.0) / 100.0), x + OBS_PLAYER_CARD_WIDTH - 2), y + 3);
			}

			y += 5;

			// Ammo display
			if (ammo > 0) {
				gr_setcolor(BM_XRGB(25, 25, 25));
				gr_urect(x + 2, y, min(x + 2 + (int)(ammo * ((double)OBS_PLAYER_CARD_WIDTH - 4.0) / 10000.0), x + OBS_PLAYER_CARD_WIDTH - 2), y + 2);
			}

			y += 4;
		}

		if (PlayerCfg.ObsShowPrimary) {
			// Selected primary
			char primary[7];
			switch (Players[pnum].primary_weapon) {
				case 0:
					sprintf(primary, "%s %i", (Players[pnum].flags & PLAYER_FLAGS_QUAD_LASERS) ? "QUAD" : "LASER", Players[pnum].laser_level + 1);
					break;
				case 1:
					sprintf(primary, "VUL");
					break;
				case 2:
					sprintf(primary, "SPREAD");
					break;
				case 3:
					sprintf(primary, "PLASMA");
					break;
				case 4:
					sprintf(primary, "FUSION");
					break;
			}

			// Primary ammo
			char primary_ammo[7];
			gr_set_fontcolor(color, -1);
			gr_printf(x + 3, y, "%s", primary);

			if (Players[pnum].primary_weapon == 1) {
				gr_set_fontcolor(BM_XRGB(25, 25, 25), -1);
				int_to_string((int)ammo, primary_ammo);
			} else {
				gr_set_fontcolor(BM_XRGB(25, 18, 6), -1);
				sprintf(primary_ammo, "%i", (int)energy);
			}

			gr_get_string_size(primary_ammo, &sw, &sh, &saw);
			gr_printf(x + OBS_PLAYER_CARD_WIDTH - 1 - sw, y, primary_ammo);

			y += 27;
		}

		if (PlayerCfg.ObsShowSecondary) {
			// Selected secondary
			char secondary[7];
			switch (Players[pnum].secondary_weapon) {
				case 0:
					sprintf(secondary, "CONC");
					break;
				case 1:
					sprintf(secondary, "HOMING");
					break;
				case 2:
					sprintf(secondary, "PROX");
					break;
				case 3:
					sprintf(secondary, "SMART");
					break;
				case 4:
					sprintf(secondary, "MEGA");
					break;
			}

			gr_set_fontcolor(color, -1);
			gr_printf(x + 3, y, "%s", secondary);

			// Secondary ammo
			char secondary_ammo[3];
			sprintf(secondary_ammo, "%i", Players[pnum].secondary_ammo[Players[pnum].secondary_weapon]);
			
			gr_get_string_size(secondary_ammo, &sw, &sh, &saw);
			gr_printf(x + OBS_PLAYER_CARD_WIDTH - 1 - sw, y, secondary_ammo);

			y += 27;
		}
	}

	glLineWidth(linedotscale);

	return y - starty;
}

int observer_show_player_cards() {
    if(Netgame.BlackAndWhitePyros)
		selected_player_rgb = player_rgb_alt;
	else
		selected_player_rgb = player_rgb;

	if ((Game_mode & GM_MULTI) && (Game_mode & GM_TEAM)) {
		// Show team one to the left, team two to the right when possible.
		// TODO
	} else {
		int pnum;
		int color;
		int x, y = 0;
		int obs_player_card_height = 0;
		int drawn_players = n_players - (Netgame.host_is_obs ? 1 : 0);
		bool found_host_as_obs = FALSE;

		// Show players in order of score.
		for (int i = 0; i < n_players; i++) {
			pnum = player_list[i];

			if (Netgame.host_is_obs && pnum == 0) {
				found_host_as_obs = TRUE;
				continue;
			}

			if (Players[pnum].connected != CONNECT_PLAYING) {
				color = BM_XRGB(12, 12, 12);
			} else {
				int color_for_player = get_color_for_player(pnum, 0);
				color = BM_XRGB(selected_player_rgb[color_for_player].r, selected_player_rgb[color_for_player].g, selected_player_rgb[color_for_player].b);
			}

			int position = i - (found_host_as_obs ? 1 : 0);

			if (grd_curcanv->cv_bitmap.bm_w < 2 * OBS_PLAYER_CARD_WIDTH + OBS_TIME_WIDTH) {
				x = grd_curcanv->cv_bitmap.bm_w / 2 - OBS_PLAYER_CARD_WIDTH + OBS_PLAYER_CARD_WIDTH * (position % 2);
				y = 53 + obs_player_card_height * (position / 2);
			} else if (grd_curcanv->cv_bitmap.bm_w < 4 * OBS_PLAYER_CARD_WIDTH + OBS_TIME_WIDTH) {
				x = grd_curcanv->cv_bitmap.bm_w / 2 - OBS_TIME_WIDTH / 2 - OBS_PLAYER_CARD_WIDTH + (OBS_PLAYER_CARD_WIDTH + OBS_TIME_WIDTH) * (position % 2);
				y = 0 + obs_player_card_height * (position / 2);
			} else if (grd_curcanv->cv_bitmap.bm_w < 6 * OBS_PLAYER_CARD_WIDTH + OBS_TIME_WIDTH) {
				if (drawn_players < 3) {
					position += 1;
				}
				x = grd_curcanv->cv_bitmap.bm_w / 2 - OBS_TIME_WIDTH / 2 - 2 * OBS_PLAYER_CARD_WIDTH + OBS_PLAYER_CARD_WIDTH * (position % 4) + OBS_TIME_WIDTH * ((position % 4) / 2);
				y = 0 + obs_player_card_height * (position / 4);
			} else if (grd_curcanv->cv_bitmap.bm_w < 8 * OBS_PLAYER_CARD_WIDTH + OBS_TIME_WIDTH) {
				if (drawn_players < 3) {
					position += 2;
				} else if (drawn_players < 5) {
					position += 1;
				}
				x = grd_curcanv->cv_bitmap.bm_w / 2 - OBS_TIME_WIDTH / 2 - 3 * OBS_PLAYER_CARD_WIDTH + OBS_PLAYER_CARD_WIDTH * (position % 6) + OBS_TIME_WIDTH * ((position % 6) / 3);
				y = 0 + obs_player_card_height * (position / 6);
			} else {
				if (drawn_players < 3) {
					position += 3;
				} else if (drawn_players < 5) {
					position += 2;
				} else if (drawn_players < 7) {
					position += 1;
				}
				x = grd_curcanv->cv_bitmap.bm_w / 2 - OBS_TIME_WIDTH / 2 - 4 * OBS_PLAYER_CARD_WIDTH + OBS_PLAYER_CARD_WIDTH * (position % 8) + OBS_TIME_WIDTH * ((position % 8) / 4);
				y = 0 + obs_player_card_height * (position / 8);
			}

			obs_player_card_height = observer_draw_player_card(pnum, color, x, y);
		}

		return y + obs_player_card_height;
	}

	return 0;
}

void observer_maybe_show_team_score() {

}

void observer_maybe_show_kill_graph() {
	int pnum;
	kill_event *ev;
	int minscore = 0;
	int maxscore = 0;
	int x, y;
	int scorescale = 0;
	int timescale = 0;
	int gridminy, gridmaxy;
	char score[10];
	int sw, sh, aw;
	int gridminx, gridminx2, gridmaxx;
	char time[10];
	int color;
	kill_event *last_ev;
	int old_x, old_y;
	
	if (GameTime64 < Show_graph_until) {
		glLineWidth(1);

		for (int i = 0; i < n_players; i++) {
			pnum = player_list[i];

			if (Netgame.host_is_obs && pnum == 0) {
				continue;
			}

			if ((ev = First_event[pnum]) != NULL) {
				while(ev != NULL) {
					if (ev->score < minscore)
						minscore = ev->score;
					if (ev->score > maxscore)
						maxscore = ev->score;
					ev = ev->next;
				}
			}
		}

		if (minscore != maxscore) {
			x = (grd_curcanv->cv_bitmap.bm_w - 1000) / 2;
			y = grd_curcanv->cv_bitmap.bm_h - 205;
			gr_settransblend(14, GR_BLEND_NORMAL);
			gr_setcolor( BM_XRGB(0, 0, 0) );
			gr_rect(x,y - FSPACY(4),x+1000 + FSPACX(7),y+200);
			gr_settransblend(GR_FADE_OFF, GR_BLEND_NORMAL);

			gr_set_curfont( GAME_FONT );
			gr_set_fontcolor(BM_XRGB(31, 31, 31), -1);

			// Determine the numbers to use on the axis.  We want a maximum of 6 vertically (including 0) and 12 horizontally (not including 0).
			scorescale = 1;
			while (trunc((float)maxscore / (float)scorescale) - trunc((float)minscore / (float)scorescale) > 5) {
				switch (scorescale) {
					case 1:
						scorescale = 2;
						break;
					case 2:
						scorescale = 5;
						break;
					case 5:
						scorescale = 10;
						break;
					case 10:
						scorescale = 20;
						break;
					case 20:
						scorescale = 25;
						break;
					case 25:
						scorescale = 50;
						break;
					case 50:
						scorescale = 100;
						break;
					case 100:
						scorescale = 200;
						break;
					case 200:
						scorescale = 250;
						break;
					case 250:
						scorescale = 500;
						break;
					case 500:
						scorescale = 1000;
						break;
					case 1000:
						scorescale = 2000;
						break;
					case 2000:
						scorescale = 2500;
						break;
					case 2500:
						scorescale = 5000;
						break;
					case 5000:
						scorescale = 10000;
						break;
					case 10000:
						scorescale = 20000;
						break;
					case 20000:
						scorescale = 25000;
						break;
					case 25000:
						scorescale = 50000;
						break;
					case 50000:
						scorescale = 100000;
						break;
					case 100000:
						scorescale = 200000;
						break;
					case 200000:
						scorescale = 250000;
						break;
					case 250000:
						scorescale = 500000;
						break;
					case 500000:
						scorescale = 1000000;
						break;
					case 1000000:
						scorescale = 2000000;
						break;
					case 2000000:
						scorescale = 2500000;
						break;
					case 2500000:
						scorescale = 5000000;
						break;
					case 5000000:
						scorescale = 10000000;
						break;
					case 10000000:
						scorescale = 20000000;
						break;
					case 20000000:
						scorescale = 25000000;
						break;
					case 25000000:
						scorescale = 50000000;
						break;
					case 50000000:
						scorescale = 100000000;
						break;
				}
			}

			timescale = 1;
			while ((GameTime64 / 60) / i2f(timescale) > 12) {
				switch (timescale) {
					case 1:
						timescale = 2;
						break;
					case 2:
						timescale = 5;
						break;
					case 5:
						timescale = 10;
						break;
					case 10:
						timescale = 15;
						break;
					case 15:
						timescale = 20;
						break;
					case 20:
						timescale = 30;
						break;
					case 30:
						timescale = 60;
						break;
					case 60:
						timescale = 120;
						break;
					case 120:
						timescale = 240;
						break;
				}
			}
			gridminy = grd_curcanv->cv_bitmap.bm_h - 10 - FSPACY(6);
			gridmaxy = grd_curcanv->cv_bitmap.bm_h - 200;

			sprintf(score, "%i", minscore);
			gr_get_string_size(score, &sw, &sh, &aw);
			gridminx = (grd_curcanv->cv_bitmap.bm_w - 1000) / 2 + 5 + sw;

			sprintf(score, "%i", maxscore);
			gr_get_string_size(score, &sw, &sh, &aw);
			gridminx2 = (grd_curcanv->cv_bitmap.bm_w - 1000) / 2 + 5 + sw;

			if (gridminx2 > gridminx) {
				gridminx = gridminx2;
			}

			gridmaxx = (grd_curcanv->cv_bitmap.bm_w - 1000) / 2 + 995;

			for (int i = trunc((float)minscore / (float)scorescale); i <= maxscore; i += scorescale) {
				y = gridminy - (int)((float)(gridminy - gridmaxy) * (((float)(i - minscore)) / (float)(maxscore - minscore)));
				sprintf(score, "%i", i);
				gr_get_string_size(score, &sw, &sh, &aw);
				gr_set_fontcolor(BM_XRGB(31, 31, 31), -1);
				gr_printf(gridminx - sw, y - sh / 2, "%s", score);
				gr_setcolor(BM_XRGB(12, 12, 12));
				gr_line(i2f(gridminx), i2f(y), i2f(gridmaxx), i2f(y));
			}

			for (int i = 0; i2f(i) < GameTime64; i += timescale * 60) {
				x = gridminx + (int)((float)(gridmaxx - gridminx) * (((float)i2f(i)) / (float)(GameTime64)));
				if (i > 0) {
					sprintf(time, "%i", i / 60);
					gr_get_string_size(time, &sw, &sh, &aw);
					gr_set_fontcolor(BM_XRGB(31, 31, 31), -1);
					gr_printf(x - sw / 2, gridminy + 1, "%s", time);
				}
				gr_setcolor(BM_XRGB(12, 12, 12));
				gr_line(i2f(x), i2f(gridminy), i2f(x), i2f(gridmaxy));
			}

			for (int i = n_players - 1; i >= 0; i--) {
				pnum = player_list[i];

				if ((ev = First_event[pnum]) != NULL) {
					if (Game_mode & GM_TEAM) {
						color = get_color_for_team(pnum, 0);
						gr_setcolor(BM_XRGB(selected_player_rgb[color].r,selected_player_rgb[color].g,selected_player_rgb[color].b));
					} else {
						color = get_color_for_player(pnum, 0);
						gr_setcolor(BM_XRGB(selected_player_rgb[color].r,selected_player_rgb[color].g,selected_player_rgb[color].b));
					}

					last_ev = ev;
					while (ev->next != NULL) {
						ev = ev->next;
						if (ev->score != last_ev->score) {
							old_x = gridminx + (int)((float)(gridmaxx - gridminx) * (((float)last_ev->timestamp) / (float)GameTime64));
							old_y = gridminy - (int)((float)(gridminy - gridmaxy) * (((float)(last_ev->score - minscore)) / (float)(maxscore - minscore)));
							x = gridminx + (int)((float)(gridmaxx - gridminx) * (((float)ev->timestamp) / (float)GameTime64));
							y = gridminy - (int)((float)(gridminy - gridmaxy) * (((float)(ev->score - minscore)) / (float)(maxscore - minscore)));

							gr_line(i2f(old_x), i2f(old_y), i2f(x), i2f(old_y));
							gr_line(i2f(x), i2f(old_y), i2f(x), i2f(y));

							last_ev = ev;
						}
					}

					old_x = gridminx + (int)((float)(gridmaxx - gridminx) * (((float)last_ev->timestamp) / (float)GameTime64));
					old_y = gridminy - (int)((float)(gridminy - gridmaxy) * (((float)(last_ev->score - minscore)) / (float)(maxscore - minscore)));

					gr_line(i2f(old_x), i2f(old_y), i2f(gridmaxx), i2f(old_y));
				}
			}
		}

		glLineWidth(linedotscale);
	}
}

void observer_maybe_show_streaks() {
	game_status status;
	int pnum;
	fix64 diff;
	kill_event* ev;
	kill_event* opp_ev;
	kill_event* last_ev;
	kill_event* last_opp_ev;
	fix64 diff2;

	bool is_anarchy = (Game_mode & GM_TEAM) == 0 && (Game_mode & GM_MULTI_ROBOTS) == 0 && (Game_mode & GM_BOUNTY) == 0;
	bool is_team_anarchy = (Game_mode & GM_TEAM) != 0;
	bool is_robo_anarchy = (Game_mode & GM_MULTI_ROBOTS) != 0 && (Game_mode & GM_MULTI_COOP) == 0;
	bool is_coop = (Game_mode & GM_MULTI_COOP) != 0;
	bool is_bounty = (Game_mode & GM_BOUNTY) != 0;

	for (int i = 0; i < n_players; i++) {
		pnum = player_list[i];

		if (Netgame.host_is_obs && pnum == 0) {
			continue;
		}

		// Determine last major event for player.
		if (((is_anarchy || is_team_anarchy) && Kill_streak[pnum] >= 3) || (is_bounty && pnum == Bounty_target)) {
			status.type = GST_KILL_STREAK;
			if (is_bounty) {
				sprintf(status.text, "Bounty Kill Streak: %i", Kill_streak[pnum]);
			} else {
				sprintf(status.text, "Kill Streak: %i", Kill_streak[pnum]);
			}
			add_player_status(pnum, status);
			remove_player_status(pnum, GST_LAST_KILL);
			remove_player_status(pnum, GST_LAST_DEATH);
		} else if ((is_anarchy || is_team_anarchy) && Last_kill[pnum] != NULL && ((diff = GameTime64 - Last_kill[pnum]->timestamp) >= i2f(60))) {
			status.type = GST_LAST_KILL;
			if (diff >= i2f(3600)) {
				sprintf(status.text, "Last Kill: %i:%02i:%02i", (int)(diff / i2f(3600)), (int)(diff / i2f(60)) % 60, (int)(diff / i2f(1)) % 60);
			} else {
				sprintf(status.text, "Last Kill: %02i:%02i", (int)(diff / i2f(60)) % 60, (int)(diff / i2f(1)) % 60);
			}
			add_player_status(pnum, status);
			remove_player_status(pnum, GST_KILL_STREAK);
			remove_player_status(pnum, GST_LAST_DEATH);
		} else if (n_players > 2 + (Netgame.host_is_obs ? 1 : 0) && Last_death[pnum] != NULL && ((diff = GameTime64 - Last_death[pnum]->timestamp) >= i2f(60))) {
			status.type = GST_LAST_DEATH;
			if (diff >= i2f(3600)) {
				sprintf(status.text, "Last Death: %i:%02i:%02i", (int)(diff / i2f(3600)), (int)(diff / i2f(60)) % 60, (int)(diff / i2f(1)) % 60);
			} else {
				sprintf(status.text, "Last Death: %02i:%02i", (int)(diff / i2f(60)) % 60, (int)(diff / i2f(1)) % 60);
			}
			add_player_status(pnum, status);
			remove_player_status(pnum, GST_KILL_STREAK);
			remove_player_status(pnum, GST_LAST_KILL);
		} else {
			remove_player_status(pnum, GST_KILL_STREAK);
			remove_player_status(pnum, GST_LAST_KILL);
			remove_player_status(pnum, GST_LAST_DEATH);
		}

		// Determine if there is a run.
		if (is_anarchy && n_players == 2 + (Netgame.host_is_obs ? 1 : 0)) {
			int initial_score = Players[pnum].net_kills_total;
			int initial_opp_score = Players[player_list[1 - i]].net_kills_total;
			if (initial_score >= 5) {
				ev = Last_event[pnum];
				opp_ev = Last_event[player_list[1 - i]];
				last_ev = ev;
				last_opp_ev = opp_ev;
				while (true) {
					if (ev->score != last_ev->score) {
						last_ev = ev;
						if (opp_ev->score != last_opp_ev->score)
							last_opp_ev = opp_ev;
					}
					if (opp_ev->score > last_opp_ev->score) {
						last_opp_ev = opp_ev;
					}

					if (ev->prev == NULL && opp_ev->prev == NULL) {
						// Neither player has a previous event, we're done calculating the run.
						break;
					}

					if (ev->prev != NULL && opp_ev->prev != NULL) {
						// Both players have a previous event, figure out whose is later.
						if (ev->prev->timestamp > opp_ev->prev->timestamp) {
							ev = ev->prev;
						} else {
							opp_ev = opp_ev->prev;
						}
					} else if (ev->prev == NULL) {
						// Only the opponent has a previous event.
						opp_ev = opp_ev->prev;
					} else if (opp_ev->prev == NULL) {
						// Only the player has a previous event.
						ev = ev->prev;
					}

					if (initial_opp_score - opp_ev->score <= 0 || (initial_score - ev->score < 5 && initial_opp_score - opp_ev->score < 5))
						continue;

					if (initial_score - ev->score <= 0)
						break;

					if (((float)(initial_score - ev->score)) / ((float)(initial_opp_score - opp_ev->score)) < 2)
						break;
				}

				if ((last_ev->score > 0 || last_opp_ev->score > 0) && initial_score - last_ev->score >= 5) {
					diff = GameTime64 - last_opp_ev->timestamp;
					diff2 = GameTime64 - last_ev->timestamp;
					if (diff2 > diff)
						diff = diff2;

					status.type = GST_RUN;
					if (diff >= i2f(3600)) {
						sprintf(status.text, "Run: %i-%i in %i:%02i:%02i", initial_score - last_ev->score, initial_opp_score - last_opp_ev->score, (int)(diff / i2f(3600)), (int)(diff / i2f(60)) % 60, (int)(diff / i2f(1)) % 60);
					} else {
						sprintf(status.text, "Run: %i-%i in %02i:%02i", initial_score - last_ev->score, initial_opp_score - last_opp_ev->score, (int)(diff / i2f(60)) % 60, (int)(diff / i2f(1)) % 60);
					}
					add_player_status(pnum, status);
				} else {
					remove_player_status(pnum, GST_RUN);
				}
			} else {
				remove_player_status(pnum, GST_RUN);
			}
		}
	}

	// TODO: Team runs.

	// Display all statuses in order.
	int y = 0;
	player_status* p_status = First_status;
	game_status* g_status;
	int color;

	while (p_status != NULL) {
		y += 27;

		g_status = p_status->statuses;

		while (g_status != NULL) {
			y += 27;

			g_status = g_status->next;
		}

		if (p_status->next != NULL) {
			y += 10;
		}

		p_status = p_status->next;
	}

	int w;
	int h;
	int aw;
	int x;

	if (y > 0) {
		y = grd_curcanv->cv_bitmap.bm_h - y - 5;

		p_status = First_status;

		gr_set_curfont( GAME_FONT );
		
		while (p_status != NULL) {
			pnum = p_status->pnum;

			color = get_color_for_player(pnum, 0);
			gr_set_fontcolor(BM_XRGB(selected_player_rgb[color].r,selected_player_rgb[color].g,selected_player_rgb[color].b), -1);

			gr_get_string_size(Players[pnum].callsign, &w, &h, &aw);
			x = grd_curcanv->cv_bitmap.bm_w - w - 5;

			gr_printf(x, y, "%s", Players[pnum].callsign);
			y += 27;

			color = get_color_for_player(pnum, 1);
			gr_set_fontcolor(BM_XRGB(selected_player_rgb[color].r,selected_player_rgb[color].g,selected_player_rgb[color].b), -1);

			g_status = p_status->statuses;

			while (g_status != NULL) {
				gr_get_string_size(g_status->text, &w, &h, &aw);
				x = grd_curcanv->cv_bitmap.bm_w - w - 5;

				gr_printf(x, y, "%s", g_status->text);
				y += 27;

				g_status = g_status->next;
			}

			y += 10;

			p_status = p_status->next;
		}
	}
}

void observer_maybe_show_death_log(int y) {
	kill_log_event* kle = Kill_log;

	int color;
	int x;
	int sw, sh, aw;

	char killed[9];
	int killed_color = 0;
	char killer[9];
	int killer_color = 0;
	char reason[20];
	int reason_color = BM_XRGB(12, 12, 12);

	y += 5;

	gr_set_curfont( GAME_FONT );

	while (kle != NULL && GameTime64 - kle->timestamp < i2f(5)) {
		sprintf(killed, "%s", Players[kle->killed_id].callsign);

		color = get_color_for_player(kle->killed_id, 0);
		killed_color = BM_XRGB(selected_player_rgb[color].r,selected_player_rgb[color].g,selected_player_rgb[color].b);

		switch (kle->killer_type) {
			case OBJ_WALL:
				// You can't die to a wall, but you can die to lava which is considered a wall.
				sprintf(killer, "Lava");
				killer_color = BM_XRGB(12, 12, 12);
				reason[0] = '\0';
				break;
			case OBJ_ROBOT:
				sprintf(killer, "Robot");
				killer_color = BM_XRGB(12, 12, 12);
				reason[0] = '\0';
				break;
			case OBJ_PLAYER:
				if (kle->killer_id == kle->killed_id) {
					killer[0] = '\0';
				} else {
					sprintf(killer, "%s", Players[kle->killer_id].callsign);
					color = get_color_for_player(kle->killer_id, 0);
					killer_color = BM_XRGB(selected_player_rgb[color].r,selected_player_rgb[color].g,selected_player_rgb[color].b);
				}

				switch (kle->damage_type) {
					case DAMAGE_WEAPON:
					case DAMAGE_BLAST:
						if (kle->source_id == SHIP_EXPLOSION_DAMAGE) {
							sprintf(reason, "Explosion");
						} else {
							sprintf(reason, "%s", weapon_id_to_name(kle->source_id));
						}
						break;
					case DAMAGE_COLLISION:
						sprintf(reason, "Ramming");
						break;
					case DAMAGE_LAVA:
						sprintf(reason, "Lava");
						break;
					case DAMAGE_OVERCHARGE:
						sprintf(reason, "Overcharge");
						break;
				}
				break;
			case OBJ_CNTRLCEN:
				sprintf(killer, "Reactor");
				killer_color = BM_XRGB(12, 12, 12);
				reason[0] = '\0';
				break;
		}

		x = grd_curcanv->cv_bitmap.bm_w - 5;

		// Draw killed
		gr_get_string_size(killed, &sw, &sh, &aw);
		x -= sw;
		gr_set_fontcolor(killed_color, -1);
		gr_printf(x, y, "%s", killed);
		x -= 8;

		// Draw >
		gr_get_string_size(">", &sw, &sh, &aw);
		x -= sw;
		gr_set_fontcolor(BM_XRGB(12, 12, 12), -1);
		gr_printf(x, y, ">");
		x -= 8;

		// Draw killer
		if (killer[0] != '\0') {
			gr_get_string_size(killer, &sw, &sh, &aw);
			x -= sw;
			gr_set_fontcolor(killer_color, -1);
			gr_printf(x, y, killer);
			x -= 8;
		}

		// Draw reason
		if (reason[0] != '\0') {
			gr_get_string_size(reason, &sw, &sh, &aw);
			x -= sw;
			gr_set_fontcolor(reason_color, -1);
			gr_printf(x, y, reason);
			x -= 8;
		}

		kle = kle->next;
		y += FSPACY(5);
	}
}

void observer_maybe_show_death_summaries() {

}

void observer_show_kill_list()
{
	// Show the clock at the top of the screen.
	observer_show_time();

	// Show each player's score and ship status.
	Observer_message_y_start = observer_show_player_cards();

	// Show the team's score.
	observer_maybe_show_team_score();

	// Show the kill graph, which is a line graph of kills over time for each pilot.
	if (PlayerCfg.ObsShowKillGraph) {
		observer_maybe_show_kill_graph();
	}

	// Show streaks, such as last kill, last death in non-1v1, kill streak, and runs in 1v1.
	if (PlayerCfg.ObsShowStreaks) {
		observer_maybe_show_streaks();
	}

	// Show a death log, including who killed who and with what.
	if (PlayerCfg.ObsShowKillFeed) {
		observer_maybe_show_death_log(Observer_message_y_start);
	}

	// Show death summaries, including the killing blow and a line graph of shields over time.
	observer_maybe_show_death_summaries();
}

#endif

//returns true if viewer can see object
int see_object(int objnum)
{
	fvi_query fq;
	int hit_type;
	fvi_info hit_data;

	//see if we can see this player

	fq.p0 					= &Viewer->pos;
	fq.p1 					= &Objects[objnum].pos;
	fq.rad 					= 0;
	fq.thisobjnum			= Viewer - Objects;
	fq.flags 				= FQ_TRANSWALL | FQ_CHECK_OBJS | FQ_GET_SEGLIST;
	fq.startseg				= Viewer->segnum;
	fq.ignore_obj_list	= NULL;

	hit_type = find_vector_intersection(&fq, &hit_data);

	return (hit_type == HIT_OBJECT && hit_data.hit_object == objnum);
}

void show_HUD_names()
{
	int is_friend = 0, show_friend_name = 0, show_enemy_name = 0, show_name = 0, show_shields = 0, show_typing = 0, show_indi = 0, pnum = 0, objnum = 0;

	int my_pnum = get_pnum_for_hud();

	if(Netgame.BlackAndWhitePyros)
		selected_player_rgb = player_rgb_alt;
	else
		selected_player_rgb = player_rgb;

	for (pnum=0;pnum<N_players;pnum++)
	{
		if ((pnum == my_pnum && !is_observer()) || Players[pnum].connected != CONNECT_PLAYING || (Netgame.host_is_obs && pnum == 0))
			continue;
		// ridiculusly complex to check if we want to show something... but this is readable at least.
		is_friend = (Game_mode & GM_MULTI_COOP || (Game_mode & GM_TEAM && get_team(pnum) == get_team(my_pnum)));
		show_friend_name = Show_reticle_name;
		show_enemy_name = Show_reticle_name && Netgame.ShowEnemyNames && !(Players[pnum].flags & PLAYER_FLAGS_CLOAKED);
		show_name = ((is_friend && show_friend_name) || (!is_friend && show_enemy_name)) || (is_observer() && PlayerCfg.ObsShowNames);
		show_shields = (is_observer() && PlayerCfg.ObsShowShieldText);
		show_typing = is_friend || !(Players[pnum].flags & PLAYER_FLAGS_CLOAKED);
		show_indi = ((/*(Game_mode & ( GM_CAPTURE | GM_HOARD ) && Players[pnum].flags & PLAYER_FLAGS_FLAG) || */(Game_mode & GM_BOUNTY &&  pnum == Bounty_target)) && (is_friend || !(Players[pnum].flags & PLAYER_FLAGS_CLOAKED)));

		if (Newdemo_state == ND_STATE_PLAYBACK) {
			//if this is a demo, the objnum in the player struct is wrong, so we search the object list for the objnum
			for (objnum=0;objnum<=Highest_object_index;objnum++)
				if (Objects[objnum].type==OBJ_PLAYER && Objects[objnum].id == pnum)
					break;
			if (objnum > Highest_object_index)	//not in list, thus not visible
				continue;			//..so don't show name
		}
		else
			objnum = Players[pnum].objnum;

		if ((show_name || show_typing || show_indi || show_shields) && (see_object(objnum) || is_observer()))
		{
			g3s_point player_point;
			g3_rotate_point(&player_point,&Objects[objnum].pos);

			if (player_point.p3_codes == 0) //on screen
			{
				g3_project_point(&player_point);

				if (!(player_point.p3_flags & PF_OVERFLOW))
				{
					fix x,y,dx,dy;
					char s[CALLSIGN_LEN+20];
					int w, h, aw, x1, y1, color_num;

					x = player_point.p3_sx;
					y = player_point.p3_sy;
					dy = -fixmuldiv(fixmul(Objects[objnum].size,Matrix_scale.y),i2f(grd_curcanv->cv_bitmap.bm_h)/2,player_point.p3_z);
					dx = fixmul(dy,grd_curscreen->sc_aspect);
					color_num = (Game_mode & GM_TEAM)?get_team(pnum):Netgame.players[pnum].color;//pnum;
					memset(&s, '\0', CALLSIGN_LEN+20);
					/* Set the text to show */
					if( Game_mode & GM_BOUNTY && pnum == Bounty_target )
						strncpy( s, "Target", 6 );
					else if (show_name)
					{
						if (!Netgame.obs_min && is_observer() && show_shields)
							snprintf( s, sizeof(s), "%s (%0.1f%s)", Players[pnum].callsign, f2db(Players[pnum].shields), Players[pnum].shields_certain ? "" : "?" );
						else
							snprintf( s, sizeof(s), "%s", Players[pnum].callsign );
					}
					else if (show_shields) {
						if (!Netgame.obs_min) {
							snprintf( s, sizeof(s), "(%0.1f%s)", f2db(Players[pnum].shields), Players[pnum].shields_certain ? "" : "?" );
						}
					}
					if (show_typing && multi_sending_message[pnum])
					{
						if (s[0])
							strncat( s, ", typing", 8);
						else
							strncpy( s, "Typing", 6 );
					}
					if (s[0])
					{
						gr_get_string_size(s, &w, &h, &aw);
						gr_set_fontcolor(BM_XRGB(selected_player_rgb[color_num].r,selected_player_rgb[color_num].g,selected_player_rgb[color_num].b),-1 );
						x1 = f2i(x)-w/2;
						y1 = f2i(y-dy)+FSPACY(1);
						gr_string (x1, y1, s);
					}
					if (is_observer() && PlayerCfg.ObsShowShieldBar) {
						glLineWidth(1);

						int x2 = f2i(x) - 199/2;
						int y2;
						if (s[0]) {
							y2 = f2i(y - dy) + FSPACY(6);
						} else {
							y2 = f2i(y - dy) + FSPACY(1);
						}

						// Shield display
						double shield_count = f2db(Players[pnum].shields);
						double delta = f2db(Players[pnum].shields_delta);
						fix pulse = GameTime64 & 0x1ffff;
						double pulse_strength = 0;

						if (pulse > 0x10000) {
							pulse_strength = 1;
						} else {
							pulse_strength = 0;
						}

						// Fill bar with color
						if (shield_count > 0) {
							gr_setcolor(BM_XRGB(selected_player_rgb[color_num].r,selected_player_rgb[color_num].g,selected_player_rgb[color_num].b));
							gr_urect(x2, y2, x2 + 198, y2 + 8);
						}

						// Replace empty with dark grey, or pulsing red if under 30 shields.
						if (shield_count < 100.0) {
							int grey_color = (shield_count > 0 && shield_count < 30 ? BM_XRGB(6 + (int)(10 * pulse_strength), 6, 6) : BM_XRGB(6, 6, 6));
							gr_setcolor(grey_color);
							gr_urect(x2 + (int)(shield_count * 198 / 100.0), y2, x2 + 198, y2 + 8);
						}

						if (Players[pnum].shields_delta != 0 && (Players[Player_num].hours_total - Players[pnum].shields_time_hours == 1 && i2f(3600) + Players[Player_num].time_total - Players[pnum].shields_time < i2f(2) || Players[Player_num].time_total - Players[pnum].shields_time < i2f(2))) {
							if (shield_count > 0 && shield_count < 100 && delta < 0) {
								// Replace recent damage with red, unless at 0.
								gr_setcolor(BM_XRGB(31, 6, 6));
								gr_urect(x2 + (int)(shield_count * 198 / 100.0), y2, min(x2 + (int)((shield_count - delta) * 198 / 100.0), x2 + 198), y2 + 8);
							} else if (shield_count < 100 - delta && delta > 0) {
								// Replace recent healing with green.
								gr_setcolor(BM_XRGB(6, 6, 31));
								gr_urect(x2 + (int)((shield_count - delta) * 198 / 100.0), y2, min(x2 + (int)(shield_count * 198 / 100.0), x2 + 198), y2 + 8);
							}
						}

						// Divide bar into segments
						for (int seg = 1; seg < 10; seg++) {
							gr_setcolor(BM_XRGB(0, 0, 0));
							gr_uline(i2f(x2 - 1 + 20 * seg), i2f(y2), i2f(x2 - 1 + 20 * seg), i2f(y2 + 9));
						}

						glLineWidth(linedotscale);
					}

					if (!Netgame.obs_min && is_observer() && PlayerCfg.ObsShowDamage)
					{
						if (Players[pnum].shields_delta != 0 && (Players[Player_num].hours_total - Players[pnum].shields_time_hours == 1 && i2f(3600) + Players[Player_num].time_total - Players[pnum].shields_time < i2f(2) || Players[Player_num].time_total - Players[pnum].shields_time < i2f(2)))
						{
							double diff = (double)(int)Players[pnum].shields_delta / 65536;

							memset(&s, '\0', CALLSIGN_LEN+20);
							snprintf( s, sizeof(s), "%+0.1f", diff);

							gr_get_string_size(s, &w, &h, &aw);
							gr_set_fontcolor(BM_XRGB(diff < 0 ? (diff < -50 ? 32 : (diff > -10 ? 8 : 32 - (diff + 50) * 24 / 40)) : 0, diff > 0 ? 32 : 0, 0), -1);
							x1 = f2i(x)-w/2;
							y1 = f2i(y+dy)-FSPACY(6);
							gr_string (x1, y1, s);
						}
					}

					/* Draw box on HUD */
					if (show_indi)
					{
						fix w,h;

						w = dx/4;
						h = dy/4;

// 						if (Game_mode & GM_CAPTURE)
// 							gr_setcolor((get_team(pnum) == TEAM_BLUE)?BM_XRGB(31,0,0):BM_XRGB(0,0,31));
// 						else if (Game_mode & GM_HOARD)
// 						{
// 							if (Game_mode & GM_TEAM)
// 								gr_setcolor((get_team(pnum) == TEAM_RED)?BM_XRGB(31,0,0):BM_XRGB(0,0,31));
// 							else
// 								gr_setcolor(BM_XRGB(0,31,0));
// 						}
// 						else if( Game_mode & GM_BOUNTY )
							gr_setcolor( BM_XRGB( selected_player_rgb[pnum].r, selected_player_rgb[pnum].g, selected_player_rgb[pnum].b ) );

						gr_line(x+dx-w,y-dy,x+dx,y-dy);
						gr_line(x+dx,y-dy,x+dx,y-dy+h);
						gr_line(x-dx,y-dy,x-dx+w,y-dy);
						gr_line(x-dx,y-dy,x-dx,y-dy+h);
						gr_line(x+dx-w,y+dy,x+dx,y+dy);
						gr_line(x+dx,y+dy,x+dx,y+dy-h);
						gr_line(x-dx,y+dy,x-dx+w,y+dy);
						gr_line(x-dx,y+dy,x-dx,y+dy-h);
					}
				}
			}
		}
	}
}

//draw all the things on the HUD

void draw_hud()
{
	n_players = multi_get_kill_list(player_list);

	if (is_observer()) {
		// Show HUD names
		show_HUD_names();

		// Show observer interface.
		observer_show_kill_list();

		// Show game messages
		HUD_render_message_frame();

		if (is_observing_player() && PlayerCfg.ObsShowCockpit && !Obs_at_distance) {
			if (PlayerCfg.CockpitMode[1]==CM_STATUS_BAR || PlayerCfg.CockpitMode[1]==CM_FULL_SCREEN)
				hud_show_homing_warning();

			if (PlayerCfg.CockpitMode[1]==CM_FULL_SCREEN) {
				hud_show_energy();
				hud_show_shield();
				hud_show_weapons();
				if (!PCSharePig)
					hud_show_keys();
				hud_show_cloak_invuln();

				if (Newdemo_state==ND_STATE_RECORDING)
				{
					int pnum = get_pnum_for_hud();
					newdemo_record_player_flags(Players[pnum].flags);
				}
			}

			if (PlayerCfg.CockpitMode[1] != CM_LETTERBOX)
				show_reticle(PlayerCfg.ReticleType, 1);
			if (PlayerCfg.CockpitMode[1] != CM_LETTERBOX && Newdemo_state != ND_STATE_PLAYBACK && (PlayerCfg.MouseControlStyle == MOUSE_CONTROL_FLIGHT_SIM) && PlayerCfg.MouseFSIndicator) /* Old School Mouse */
				show_mousefs_indicator(Controls.raw_mouse_axis[0], Controls.raw_mouse_axis[1], Controls.raw_mouse_axis[2], GWIDTH/2, GHEIGHT/2, GHEIGHT/4);
		}

		return;
	}

	if (PlayerCfg.HudMode==3) // no hud, "immersion mode"
		return;

	// Cruise speed
	if ( Player_num > -1 && Viewer->type==OBJ_PLAYER && Viewer->id==Player_num && PlayerCfg.CockpitMode[1] != CM_REAR_VIEW)	{
		int	x = FSPACX(1);
		int	y = grd_curcanv->cv_bitmap.bm_h;

		gr_set_curfont( GAME_FONT );
		gr_set_fontcolor( BM_XRGB(0, 31, 0), -1 );
		if (Cruise_speed > 0) {
			if (PlayerCfg.CockpitMode[1]==CM_FULL_SCREEN) {
				if (Game_mode & GM_MULTI)
					y -= LINE_SPACING * 10;
				else
					y -= LINE_SPACING * 6;
			} else if (PlayerCfg.CockpitMode[1] == CM_STATUS_BAR) {
				if (Game_mode & GM_MULTI)
					y -= LINE_SPACING * 6;
				else
					y -= LINE_SPACING * 1;
			} else {
				if (Game_mode & GM_MULTI)
					y -= LINE_SPACING * 7;
				else
					y -= LINE_SPACING * 2;
			}

			gr_printf( x, y, "%s %2d%%", TXT_CRUISE, f2i(Cruise_speed) );
		}
	}

	//	Show score so long as not in rearview
	if ( !Rear_view && PlayerCfg.CockpitMode[1]!=CM_REAR_VIEW && PlayerCfg.CockpitMode[1]!=CM_STATUS_BAR) {
		hud_show_score();
		if (score_time)
			hud_show_score_added();
	}

	if ( !Rear_view && PlayerCfg.CockpitMode[1]!=CM_REAR_VIEW)
		hud_show_timer_count();

	//	Show other stuff if not in rearview or letterbox.
	if (!Rear_view && PlayerCfg.CockpitMode[1]!=CM_REAR_VIEW)
	{
		show_HUD_names();

		if (PlayerCfg.CockpitMode[1]==CM_STATUS_BAR || PlayerCfg.CockpitMode[1]==CM_FULL_SCREEN)
			hud_show_homing_warning();

		if (PlayerCfg.CockpitMode[1]==CM_FULL_SCREEN) {
			hud_show_energy();
			hud_show_shield();
			hud_show_weapons();
			if (!PCSharePig)
				hud_show_keys();
			hud_show_cloak_invuln();

			if (Newdemo_state==ND_STATE_RECORDING)
			{
				int pnum = get_pnum_for_hud();
				newdemo_record_player_flags(Players[pnum].flags);
			}
		}

#ifndef RELEASE
		if (!(Game_mode&GM_MULTI && Show_kill_list))
			show_time();
#endif
		HUD_render_message_frame();

		if (PlayerCfg.CockpitMode[1]!=CM_STATUS_BAR)
			hud_show_lives();
		if (Game_mode&GM_MULTI && Show_kill_list)
			hud_show_kill_list();
		if (PlayerCfg.CockpitMode[1] != CM_LETTERBOX)
			show_reticle(PlayerCfg.ReticleType, 1);
		if (PlayerCfg.CockpitMode[1] != CM_LETTERBOX && Newdemo_state != ND_STATE_PLAYBACK && (PlayerCfg.MouseControlStyle == MOUSE_CONTROL_FLIGHT_SIM) && PlayerCfg.MouseFSIndicator) /* Old School Mouse */
			show_mousefs_indicator(Controls.raw_mouse_axis[0], Controls.raw_mouse_axis[1], Controls.raw_mouse_axis[2], GWIDTH/2, GHEIGHT/2, GHEIGHT/4);
	}

	if (Rear_view && PlayerCfg.CockpitMode[1]!=CM_REAR_VIEW) {
		HUD_render_message_frame();
		gr_set_curfont( GAME_FONT );
		gr_set_fontcolor(BM_XRGB(0,31,0),-1 );
		gr_string(0x8000,GHEIGHT-LINE_SPACING,TXT_REAR_VIEW);
	}

}

//print out some player statistics
void render_gauges()
{
	int pnum = get_pnum_for_hud();

	int energy = f2ir(Players[pnum].energy);
	int shields = f2ir(Players[pnum].shields);
	int cloak = ((Players[pnum].flags&PLAYER_FLAGS_CLOAKED) != 0);

	Assert(PlayerCfg.CockpitMode[1]==CM_FULL_COCKPIT || PlayerCfg.CockpitMode[1]==CM_STATUS_BAR);

	if (shields < 0 ) shields = 0;

	gr_set_current_canvas(NULL);
	gr_set_curfont( GAME_FONT );

	if (Newdemo_state == ND_STATE_RECORDING)
		if (Players[pnum].homing_object_dist >= 0)
			newdemo_record_homing_distance(Players[pnum].homing_object_dist);

	draw_weapon_boxes();

	if (PlayerCfg.CockpitMode[1] == CM_FULL_COCKPIT) {

		if (Newdemo_state == ND_STATE_RECORDING)
			newdemo_record_player_energy(energy);
		draw_energy_bar(energy);
		draw_numerical_display(shields, energy);
		if (!PlayerCfg.HudMode)
			show_bomb_count(HUD_SCALE_X(BOMB_COUNT_X), HUD_SCALE_Y(BOMB_COUNT_Y), gr_find_closest_color(0, 0, 0), 0, 0);
		draw_player_ship(cloak, SHIP_GAUGE_X, SHIP_GAUGE_Y);

		if (Players[pnum].flags & PLAYER_FLAGS_INVULNERABLE)
			draw_invulnerable_ship();
		else
			draw_shield_bar(shields);
		draw_numerical_display(shields, energy);
		if (Newdemo_state == ND_STATE_RECORDING)
		{
			newdemo_record_player_shields(shields);
			newdemo_record_player_flags(Players[pnum].flags);
		}
		draw_keys();

		show_homing_warning();
		draw_wbu_overlay();

	} else if (PlayerCfg.CockpitMode[1] == CM_STATUS_BAR) {

		if (Newdemo_state == ND_STATE_RECORDING)
			newdemo_record_player_energy(energy);
		sb_draw_energy_bar(energy);
		if (!PlayerCfg.HudMode)
			show_bomb_count(HUD_SCALE_X(SB_BOMB_COUNT_X), HUD_SCALE_Y(SB_BOMB_COUNT_Y), gr_find_closest_color(0, 0, 0), 0, 0);

		draw_player_ship(cloak, SB_SHIP_GAUGE_X, SB_SHIP_GAUGE_Y);

		if (Players[pnum].flags & PLAYER_FLAGS_INVULNERABLE)
			draw_invulnerable_ship();
		else
			sb_draw_shield_bar(shields);
		sb_draw_shield_num(shields);

		if (Newdemo_state==ND_STATE_RECORDING)
		{
			newdemo_record_player_shields(shields);
			newdemo_record_player_flags(Players[pnum].flags);
		}
		sb_draw_keys();

		sb_show_lives();

		if ((Game_mode&GM_MULTI) && !((Game_mode & GM_MULTI_COOP) || (Game_mode & GM_MULTI_ROBOTS)))
		{
			sb_show_score();
		}
		else
		{
			sb_show_score();
			sb_show_score_added();
		}
	} else
		draw_player_ship(cloak, SB_SHIP_GAUGE_X, SB_SHIP_GAUGE_Y);
}

//	---------------------------------------------------------------------------------------------------------
//	Call when picked up a laser powerup.
//	If laser is active, set old_weapon[0] to -1 to force redraw.
void update_laser_weapon_info(void)
{
	if (old_weapon[0] == 0)
		old_weapon[0] = -1;
}
