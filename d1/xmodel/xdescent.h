#ifndef XDESCENT_H
#define XDESCENT_H
#include <stdarg.h>
#include <stdint.h>
#include "xcfile.h"

static inline int Max(int a, int b) { return a > b ? a : b; }

#define __xpack__
typedef union tTexCoord2f {
	float a [2];
	struct {
		float	u, v;
		} v;
	} __xpack__ tTexCoord2f;

typedef union tTexCoord3f {
	float a [3];
	struct {
		float	u, v, l;
		} v;
	} __xpack__ tTexCoord3f;


#define MAX_THRUSTERS		16
#define REAR_THRUSTER		1
#define FRONT_THRUSTER		2
#define LEFT_THRUSTER		4
#define RIGHT_THRUSTER		8
#define TOP_THRUSTER			16
#define BOTTOM_THRUSTER		32
#define FRONTAL_THRUSTER	64
#define LATERAL_THRUSTER	128

#define FR_THRUSTERS			(FRONT_THRUSTER | REAR_THRUSTER)
#define LR_THRUSTERS			(LEFT_THRUSTER | RIGHT_THRUSTER)
#define TB_THRUSTERS			(TOP_THRUSTER | BOTTOM_THRUSTER)

//#define MAX_POLYGON_MODELS 500
//#define COCKPIT_MODEL (MAX_POLYGON_MODELS - 37)
#define COCKPIT_MODEL -1

inline static void PrintLog (int lvl, const char *msg, ...) {
	va_list vp;
	va_start(vp, msg);
	vfprintf(stderr, msg, vp);
	va_end(vp);
}
inline static void PrintLog (int lvl) {
	PrintLog(lvl, "");
}
#define NEW new
#define FILENAME_LEN 255

class CBitmap {
	uint8_t *m_data;
	int m_width;
	int m_height;
	int m_rowsize;
	int m_bpp;
	int m_team;
	char *m_name;
public:
	CBitmap() : m_data(NULL), m_width(0), m_height(0), m_rowsize(0), m_bpp(0), m_team(0), m_name(NULL) {}
	~CBitmap() { Destroy(); }
	inline void SetName(const char *name) { if (m_name) free(m_name); m_name = strdup(name); }
	inline const char *Name() { return m_name; }
	inline void SetFlat(int n) { (void)n; }
	inline uint8_t *Buffer() { return m_data; }
	inline int Width() { return m_width; }
	inline int Height() { return m_height; }
	inline int RowSize() { return m_rowsize; }
	inline int BPP() { return m_bpp; }
	inline int Team() { return m_team; }
	inline void SetWidth(int n) { m_width = n; m_rowsize = n * m_bpp; }
	inline void SetHeight(int n) { m_height = n; }
	//inline void SetRowSize(int n) { m_rowsize = n; }
	inline void SetBPP(int n) { m_bpp = n; m_rowsize = m_width * n; }
	inline int Size() { return m_rowsize * m_height; }
	inline int Length() { return Size(); }
	inline void Init(int a, int b, int c, int width, int height, int bpp, void *data, bool x) {
		m_width = width;
		m_height = height;
		m_bpp = bpp;
		m_rowsize = width * bpp;
		m_data = (uint8_t*)data;
	}
	inline bool HasTransparency() { return false; }
	inline bool LoadTexture(int a, int b, int c) { return false;}
	inline void Destroy() { if (m_data) { delete[] m_data; m_data = NULL; } if (m_name) { free(m_name); m_name = NULL; } }
	inline void SetTeam(int n) { m_team = n; }
	inline void Bind(int n) {}
	inline int Frames() { return 0; }
	inline CBitmap *CurFrame() { return 0; }
	inline bool CreateBuffer() {
		if (m_data)
			m_data = NULL;
		m_data = new uint8_t[m_width * m_height * m_bpp];
		return true;
	}
	inline CBitmap *Override(int n) { return NULL; }
	inline void AddFlags(int n) {}
	inline void DelFlags(int n) {}
	inline void SetTranspType(int n) {}
	inline void SetType(int n) {}
	inline void Read(CFile& cf) { cf.Read(Buffer(), RowSize(), Height()); }
	inline static CBitmap *Create(int a, int b, int c, int d) { return new CBitmap(); }
	inline void ReleaseTexture() {}
};

#define BM_FLAG_TRANSPARENT         1
#define BM_FLAG_SUPER_TRANSPARENT   2
#define BM_FLAG_NO_LIGHTING         4
#define BM_FLAG_RLE                 8   // A run-length encoded bitmap.
#define BM_FLAG_PAGED_OUT           16  // This bitmap's data is paged out.
#define BM_FLAG_RLE_BIG             32  // for bitmaps that RLE to > 255 per row (i.e. cockpits)
#define BM_FLAG_SEE_THRU				64  // door or other texture containing see-through areas
#define BM_FLAG_TGA						128
#define BM_FLAG_OPAQUE					256
#define BM_TYPE_STD		0
#define BM_TYPE_ALT		1
#define BM_TYPE_FRAME	2
#define BM_TYPE_MASK		4

typedef struct tBGR {
	uint8_t	b,g,r;
} __xpack__ tBGR;

typedef struct tBGRA {
	uint8_t	b,g,r,a;
} __xpack__ tBGRA;

typedef struct tRGB {
	uint8_t	r,g,b;
} __xpack__ tRGB;

typedef struct tRGBA {
	uint8_t	r,g,b,a;
} __xpack__ tRGBA;
class CRGBColor {
	public:
		uint8_t	r, g, b;

	inline uint8_t& Red (void) { return r; }
	inline uint8_t& Green (void) { return g; }
	inline uint8_t& Blue (void) { return b; }

	inline void Set (uint8_t red, uint8_t green, uint8_t blue) {
		r = red, g = green, b = blue;
		}

		/*
	inline void ToGrayScale (int32_t bWeighted = 0) {
		if (bWeighted)
			r = g = b = (uint8_t) FRound (((float) r + (float) g + (float) b) / 3.0f);
		else
			r = g = b = (uint8_t) FRound ((float) r * 0.30f + (float) g * 0.584f + (float) b * 0.116f);
		}
	*/

	/*
	inline uint8_t Posterize (int32_t nColor, int32_t nSteps) {
		return Max (0, ((nColor + nSteps / 2) / nSteps) * nSteps - nSteps);
		}

	inline void Posterize (int32_t nSteps = 15) {
		r = Posterize (r, nSteps);
		g = Posterize (g, nSteps);
		b = Posterize (b, nSteps);
		}
	*/

	/*
	inline void Saturate (int32_t nMethod = 1) {
		uint8_t m = Max (r, Max (g, b));
		if (nMethod) {
			float s = 255.0f / float (m);
			r = uint8_t (float (r) * s);
			g = uint8_t (float (g) * s);
			b = uint8_t (float (b) * s);
			}
		else {
			if ((m = 255 - m)) {
				r += m;
				g += m;
				b += m;
				}
			}
		}
	*/

	inline void Assign (CRGBColor& other) { r = other.r, g = other.g, b = other.b;	}
	};

class CRGBAColor {
	public:
		uint8_t	r, g, b, a;

	inline void Set (uint8_t red, uint8_t green, uint8_t blue, uint8_t alpha = 255) {
		r = red, g = green, b = blue, a = alpha;
		}

	inline uint8_t& Red (void) { return r; }
	inline uint8_t& Green (void) { return g; }
	inline uint8_t& Blue (void) { return b; }
	inline uint8_t& Alpha (void) { return a; }

	#if 0
	inline void ToGrayScale (int32_t bWeighted = 0) {
		if (bWeighted)
			r = g = b = (uint8_t) FRound (((float) r + (float) g + (float) b) / 3.0f);
		else
			r = g = b = (uint8_t) FRound ((float) r * 0.30f + (float) g * 0.584f + (float) b * 0.116f);
		}

	inline uint8_t Posterize (int32_t nColor, int32_t nSteps) {
		return Max (0, ((nColor + nSteps / 2) / nSteps) * nSteps - nSteps);
		}

	inline void Posterize (int32_t nSteps = 15) {
		r = Posterize (r, nSteps);
		g = Posterize (g, nSteps);
		b = Posterize (b, nSteps);
		}

	inline void Saturate (int32_t nMethod = 1) {
		uint8_t m = Max (r, Max (g, b));
		if (nMethod) {
			float s = 255.0f / float (m);
			r = uint8_t (float (r) * s);
			g = uint8_t (float (g) * s);
			b = uint8_t (float (b) * s);
			}
		else {
			if ((m = 255 - m)) {
				r += m;
				g += m;
				b += m;
				}
			}
		}
		#endif
	inline void Assign (CRGBAColor& other) { r = other.r, g = other.g, b = other.b, a = other.a;	}
	};

extern struct CGameFolders {
	struct {
		char *szModels[2];
	} var;
	struct {
		char *szModels;
		char *szData[2];
	} game;
	struct {
		char *szModels[2];
	} mods;
} gameFolders;

extern struct CGameStates {
	struct {
		int nModelQuality;
	} render;
} gameStates;

#endif
