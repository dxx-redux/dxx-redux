/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#ifndef DEFS_H
#define DEFS_H

#define LFR_IMPLEMENTATION 1

#include "fresample.h"

#define ATTR_ARTIFICIAL
#define INLINE_SPEC

#define LFR_UNREACHABLE (void) 0

#if defined(__clang__)
# if __has_attribute(artificial)
#  undef ATTR_ARTIFICIAL
#  define ATTR_ARTIFICIAL __attribute__((artificial))
# endif
# if __has_builtin(__builtin_unreachable)
#  undef LFR_UNREACHABLE
#  define LFR_UNREACHABLE __builtin_unreachable()
# endif
# undef INLINE_SPEC
# define INLINE_SPEC __inline
#elif defined(__GNUC__)
# if (__GNUC__ >= 4 && __GNUC_MINOR__ >= 3) || __GNUC__ > 4
#  undef ATTR_ARTIFICIAL
#  define ATTR_ARTIFICIAL __attribute__((artificial))
# endif
# if (__GNUC__ >= 4 && __GNU_MINOR__ >= 5) || __GNUC__ > 4
#  undef LFR_UNREACHABLE
#  define LFR_UNREACHABLE __builtin_unreachable()
# endif
# undef INLINE_SPEC
# define INLINE_SPEC __inline
#endif

#define INLINE static INLINE_SPEC ATTR_ARTIFICIAL

/*
  Constants used by dithering algorithm.  We usa a simple linear
  congruential generator to generate a uniform signal for dithering,
  taking the high order bits:

  x_{n+1} = (A * x_n + C) mod 2^32

  The derived constants, AN/CN, are used for stepping the LCG forward
  by N steps.  AI is the inverse of A.
*/

#define LCG_A  1103515245u
#define LCG_A2 3265436265u
#define LCG_A4 3993403153u

#define LCG_C       12345u
#define LCG_C2 3554416254u
#define LCG_C4 3596950572u

#define LCG_AI 4005161829u
#define LCG_CI 4235699843u

/* ====================
   Utility functions
   ==================== */

#if defined(LFR_SSE2) && defined(LFR_CPU_X86)
#include <emmintrin.h>

/*
  Store 16-bit words [i0,i1) in the given location.
*/
INLINE void
lfr_storepartial_epi16(__m128i *dest, __m128i x, int i0, int i1)
{
    union {
        unsigned short h[8];
        __m128i x;
    } u;
    int i;
    u.x = x;
    for (i = i0; i < i1; ++i)
        ((unsigned short *) dest)[i] = u.h[i];
}

/*
  Advance four linear congruential generators.  The four generators
  should use the same A and C constants.

  The 32-bit multiply we want requires SSE 4.1.  We construct it out of
  two 32 to 64 bit multiply operations.
*/
INLINE __m128i
lfr_rand_epu32(__m128i x, __m128i a, __m128i c)
{
    return _mm_add_epi32(
        _mm_unpacklo_epi32(
            _mm_shuffle_epi32(
                _mm_mul_epu32(x, a),
                _MM_SHUFFLE(0, 0, 2, 0)),
            _mm_shuffle_epi32(
                _mm_mul_epu32(_mm_srli_si128(x, 4), a),
                _MM_SHUFFLE(0, 0, 2, 0))),
        c);
}

#endif

#if defined(LFR_ALTIVEC) && defined(LFR_CPU_PPC)
#if !defined(__APPLE_ALTIVEC__)
#include <altivec.h>
#endif

/*
  Advance four linear congruential generators.  The four generators
  should use the same A and C constants.

  The 32-bit multiply we want does not exist.  We construct it out of
  16-bit multiply operations.
*/
INLINE vector unsigned int
lfr_vecrand(vector unsigned int x, vector unsigned int a,
			vector unsigned int c)
{
	vector unsigned int s = vec_splat_u32(-16);
	return vec_add(
		vec_add(
			vec_mulo(
				(vector unsigned short) x,
				(vector unsigned short) a),
			c),
		vec_sl(
			vec_msum(
				(vector unsigned short) x,
				(vector unsigned short) vec_rl(a, s),
				vec_splat_u32(0)),
			s));
}

#endif

#endif
