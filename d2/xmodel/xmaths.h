/* Maths[HA] library header file */

#ifndef _XMATHS_H
#define _XMATHS_H

#define NO_FIX_INLINE 0
#define ALT_SQRT 2

#include <stdlib.h>

#ifdef _WIN32
#	pragma pack(push)
#	pragma pack(8)
#endif
#include <cmath>
#ifdef _WIN32
#	pragma pack(pop)
#endif

#include "xpstypes.h"

//=============================== FIXED POINT ===============================

#ifndef PI
#  define  PI    3.141592653589793240
#endif


typedef int32_t fix;		//16 bits int32_t, 16 bits frac
typedef int16_t fixang;		//angles

#define MATH64
typedef struct tQuadInt {// integer 64 bit, previously called "quad"
#ifdef MATH64
	int64_t q;
#else
	uint32_t low;
   int32_t high;
#endif
} __pack__ tQuadInt;

#define WORLDSCALE	1000	// one world unit (65536 in fixed point arithmetic) corresponds to 1000 mm (1 m) of the real world

//Convert an int32_t to a fix
#define I2X(i) ((static_cast<fix> (i)) * 65536)

//Get the int32_t part of a fix
#define X2I(_f) ((_f) >> 16) // / 65536)

//Get the int32_t part of a fix, with rounding
#define X2IR(_f) (((_f) + (I2X (1) / 2)) / 65536)

//Convert fix to double and double to fix
#define X2F(_f) (float (_f) / 65536.0f)
#define X2D(_f) (double (_f) / 65536.0)
#define F2X(_f) (fix ((_f) * 65536))

#define MM2X(_mm) (((_mm) * I2X (1)) / WORLDSCALE)	// mm to world units
#define X2MM(_i)	(int32_t (X2F (_i) * WORLDSCALE + 0.5f))

//Some handy constants
//#define F0_1	0x199a

inline float FRound (float v)	{ return (v < 0.0f) ? v - 0.5f : v + 0.5f; }
inline double DRound (double v)	{ return (v < 0.0) ? v - 0.5 : v + 0.5; }

#if 0

#	if 1
#	define XRound(_v)					(fix) (_v)
#	else
#	define XRound(_v)					(fix) (((_v) < 0.0) ? (_v) - 0.5 : (_v) + 0.5)
#endif

#define FixMul64(_a, _b)		((int64_t) XRound (double (_b) / 65536.0 * double (_a)))
#define FixMul(_a, _b)			(XRound (double (_b) / 65536.0 * double (_a)))
#define FixDiv(_a, _b)			(XRound ((_b) ? (double (_a) / double (_b) * 65536.0) : 1))
#define FixMulDiv(_a, _b, _c) (XRound ((_c) ? double (_a) / double (_c) * double (_b) : 1))

#else

#define FixMul64(_a, _b)		(static_cast<int64_t> (((static_cast<int64_t> (_a)) * (static_cast<int64_t> (_b))) >> 16))
#define FixMul(_a, _b)			(static_cast<fix> (FixMul64 (_a, _b)))
#define FixDiv(_a, _b)			(static_cast<fix> ((_b) ? (((static_cast<int64_t> (_a)) * 65536) / (static_cast<int64_t> (_b))) : 1))
#define FixMulDiv(_a, _b, _c) ((fix) ((_c) ? (((static_cast<int64_t> (_a)) * (static_cast<int64_t> (_b))) / (static_cast<int64_t> (_c))) : 1))

#endif

//divide a tQuadInt by a long
int32_t FixDivQuadLong (uint32_t qlow, uint32_t qhigh, uint32_t d);

//computes the square root of a long, returning a int16_t
uint16_t LongSqrt (int32_t a);

//computes the square root of a tQuadInt, returning a long
extern int32_t nMathFormat;
extern int32_t nDefMathFormat;

uint32_t sqrt64 (uint64_t a);

#define mul64(_a,_b)	(static_cast<int64_t> (_a) * static_cast<int64_t> (_b))

//multiply two fixes, and add 64-bit product to a tQuadInt
#ifdef MATH64
static
#ifndef _MSC_VER
__attribute__((always_inline))
#endif
inline void FixMulAccum (tQuadInt * q, fix a, fix b)
{
q->q += mul64 (a, b);
}
#else
void FixMulAccum (tQuadInt * q, fix a, fix b);
#endif

uint32_t QuadSqrt (uint32_t low, int32_t high);
//uint32_t QuadSqrt (long low, long high);

//computes the square root of a fix, returning a fix
fix FixSqrt (fix a);

//compute sine and cosine of an angle, filling in the variables
//either of the pointers can be NULL
void FixSinCos (fix a, fix * s, fix * c);	//with interpolation

void FixFastSinCos (fix a, fix * s, fix * c);	//no interpolation

//compute inverse sine & cosine
fixang FixASin (fix v);

fixang FixACos (fix v);

//given cos & sin of an angle, return that angle.
//parms need not be normalized, that is, the ratio of the parms cos/sin must
//equal the ratio of the actual cos & sin for the result angle, but the parms
//need not be the actual cos & sin.
//NOTE: this is different from the standard C atan2, since it is left-handed.
fixang FixAtan2 (fix cos, fix sin);

//for passed value a, returns 1/sqrt(a)
fix FixISqrt (fix a);

//-----------------------------------------------------------------------------

static inline int32_t RandSys (int32_t nMax)
{
return int32_t (rand ()) % nMax;
}

//-----------------------------------------------------------------------------
// return a random integer in the range [0 .. SHORT_RAND_MAX]

#define SHORT_RAND_MAX 0x7fff

int32_t RandShort (void);

static inline int32_t Rand (int32_t nMax)
{
return RandShort() % nMax;
}

static inline int32_t RandTop (int32_t nMax)
{
return (RandShort() * nMax) >> 15;
}

static inline int32_t RandShortSys (void)
{
return RandSys (SHORT_RAND_MAX + 1);
}

//-----------------------------------------------------------------------------
// return a random integer in the range [-(SHORT_RAND_MAX + 1) / 2 .. +SHORT_RAND_MAX / 2]

static inline int32_t SRandShort (void)
{
return RandShort () - (SHORT_RAND_MAX + 1) / 2;
}

static inline int32_t SRandShortSys (void)
{
return RandShortSys () - (SHORT_RAND_MAX + 1) / 2;
}

//-----------------------------------------------------------------------------
// return a random float in the range [0.0f .. 1.0f]

static inline float RandFloat (float scale = 1.0f)
{
return float (rand ()) / (float (RAND_MAX) * scale);
}

//------------------------------------------------------------------------------
// return a random double in the range [0.0 .. 1.0]

inline double RandDouble (void)
{
return double (rand ()) / double (RAND_MAX);
}

//-----------------------------------------------------------------------------

template<typename _T>
inline void Swap (_T& a, _T& b) { _T h = a; a = b; b = h; }

template<typename _T>
inline const _T Clamp (const _T v, const _T vMin, const _T vMax) { return (v < vMin) ? vMin : (v > vMax) ? vMax : v; }

template<typename _T>
inline const _T Min (const _T a, const _T b) { return (a <= b) ? a : b; }

template<typename _T>
inline const _T Max (const _T a, const _T b) { return (a >= b) ? a : b; }

//-----------------------------------------------------------------------------

#endif