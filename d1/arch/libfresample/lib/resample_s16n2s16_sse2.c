/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#define LFR_SSE2 1

#include "cpu.h"
#if defined(LFR_CPU_X86)
#include "resample.h"
#include <string.h>

/* uninitialized local 'u' */
#pragma warning( disable : 4701 )

#define LOOP_FIRCOEFF_SCALAR \
    fn = (((unsigned) x >> 1) >> (31 - log2nfilt)) &            \
        ((1u << log2nfilt) - 1);                                \
    ff1 = ((unsigned) x >> (32 - log2nfilt - INTERP_BITS)) &    \
        ((1u << INTERP_BITS) - 1);                              \
    ff0 = (1u << INTERP_BITS) - ff1

#define LOOP_FIRCOEFF \
    LOOP_FIRCOEFF_SCALAR; \
    fir_interp = _mm_set1_epi32(ff0 | (ff1 << 16));

#define LOOP_COMBINE \
    x1 = _mm_add_epi32(                                 \
        _mm_unpacklo_epi64(a[0], a[1]),                 \
        _mm_unpackhi_epi64(a[0], a[1]));                \
    x2 = _mm_add_epi32(                                 \
        _mm_unpacklo_epi64(a[2], a[3]),                 \
        _mm_unpackhi_epi64(a[2], a[3]));                \
                                                        \
    x1 = _mm_add_epi32(x1, _mm_srli_epi32(dsv, 17));    \
    dsv = lfr_rand_epu32(dsv, lcg_a, lcg_c);            \
    x2 = _mm_add_epi32(x2, _mm_srli_epi32(dsv, 17));    \
    dsv = lfr_rand_epu32(dsv, lcg_a, lcg_c);            \
                                                        \
    acc = _mm_packs_epi32(                              \
        _mm_srai_epi32(x1, 15),                         \
        _mm_srai_epi32(x2, 15))

#define LOOP_STORE \
    a[i & 3] = acc;                                     \
    if ((i & 3) == 3) {                                 \
        LOOP_COMBINE;                                   \
        _mm_storeu_si128(                               \
            (__m128i *) ((short *) out + (i - 3) * 2),  \
            acc);                                       \
    }

#define LOOP_COMPUTE(flen, j) \
    dat0 = _mm_loadu_si128(                         \
        (const __m128i *)                           \
        ((const short *) in + off*2 + (j)*16));     \
    dat1 = _mm_loadu_si128(                         \
        (const __m128i *)                           \
        ((const short *) in + off*2 + (j)*16+8));   \
    fir0 = fd[(fn+0)*(flen) + (j)];                 \
    fir1 = fd[(fn+1)*(flen) + (j)];                 \
                                                    \
    fir0 = _mm_packs_epi32(                         \
        _mm_srai_epi32(                             \
            _mm_madd_epi16(                         \
                _mm_unpacklo_epi16(fir0, fir1),     \
                fir_interp),                        \
            INTERP_BITS),                           \
        _mm_srai_epi32(                             \
            _mm_madd_epi16(                         \
                _mm_unpackhi_epi16(fir0, fir1),     \
                fir_interp),                        \
            INTERP_BITS));                          \
                                                    \
    fir1 = _mm_unpackhi_epi16(fir0, fir0);          \
    fir0 = _mm_unpacklo_epi16(fir0, fir0);          \
                                                    \
    x1 = _mm_madd_epi16(                            \
        _mm_unpacklo_epi16(dat0, dat1),             \
        _mm_unpacklo_epi16(fir0, fir1));            \
    x2 = _mm_madd_epi16(                            \
        _mm_unpackhi_epi16(dat0, dat1),             \
        _mm_unpackhi_epi16(fir0, fir1))

void
lfr_resample_s16n2s16_sse2(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio, unsigned *dither,
    void *out, int outlen, const void *in, int inlen,
    const struct lfr_filter *filter)
{
    const __m128i *fd;
    int flen, log2nfilt;
    lfr_fixed_t x;

    __m128i acc, fir0, fir1, fir_interp, dat0, dat1;
    __m128i a[4], x1, x2;
    __m128i dsv, lcg_a, lcg_c;
    int fn, ff0, ff1, off, fidx0, fidx1;
    int accs0, accs1, i, j, f;
    unsigned ds;

    union {
        unsigned d[4];
        __m128i x;
    } u;

    /* fd: Pointer to beginning of filter coefficients, aligned.  */
    fd = (const __m128i *) filter->data;
    /* flen: Length of filter, measured in 128-bit words.  */
    flen = filter->nsamp >> 3;
    /* log2nfilt: Base 2 logarithm of the number of filters.  */
    log2nfilt = filter->log2nfilt;
    x = *pos;

    ds = *dither;
    for (i = 0; i < 4; ++i) {
        u.d[i] = ds;
        ds = ds * LCG_A + LCG_C;
    }
    dsv = u.x;
    lcg_a = _mm_set1_epi32(LCG_A4);
    lcg_c = _mm_set1_epi32(LCG_C4);

    if (flen >= 1 && flen <= 3 && inv_ratio >= 0 && inlen >= flen * 8)
        goto fast;
    else
        goto flex;

flex:
    for (i = 0; i < outlen; ++i) {
        LOOP_FIRCOEFF;

        /* acc: FIR accumulator, used for accumulating 32-bit values
           in the format L R L R.  This corresponds to one frame of
           output, so the pair of values for left and the pair for
           right have to be summed later.  */
        acc = _mm_set1_epi32(0);
        /* off: offset in input corresponding to first sample in
           filter */
        off = (int) (x >> 32);
        /* fixd0, fidx1: start, end indexes of 8-word (16-byte) chunks
           of whole FIR data we will use */
        fidx0 = (-off + 7) >> 3;
        fidx1 = (inlen - off) >> 3;
        if (fidx0 > 0) {
            if (fidx0 > flen)
                goto flenv_accumulate;
            accs0 = 0;
            accs1 = 0;
            for (j = -off; j < fidx0 * 8; ++j) {
                f = (((const short *) fd)[(fn+0) * (flen*8) + j] * ff0 +
                     ((const short *) fd)[(fn+1) * (flen*8) + j] * ff1)
                    >> INTERP_BITS;
                accs0 += ((const short *) in)[(j + off)*2 + 0] * f;
                accs1 += ((const short *) in)[(j + off)*2 + 1] * f;
            }
            acc = _mm_set_epi32(0, 0, accs1, accs0);
        } else {
            fidx0 = 0;
        }
        if (fidx1 < flen) {
            if (fidx1 < 0)
                goto flenv_accumulate;
            accs0 = 0;
            accs1 = 0;
            for (j = fidx1 * 8; j < inlen - off; ++j) {
                f = (((const short *) fd)[(fn+0) * (flen*8) + j] * ff0 +
                     ((const short *) fd)[(fn+1) * (flen*8) + j] * ff1)
                    >> INTERP_BITS;
                accs0 += ((const short *) in)[(j + off)*2 + 0] * f;
                accs1 += ((const short *) in)[(j + off)*2 + 1] * f;
            }
            acc = _mm_add_epi32(acc, _mm_set_epi32(0, 0, accs1, accs0));
        } else {
            fidx1 = flen;
        }

        for (j = fidx0; j < fidx1; ++j) {
            LOOP_COMPUTE(flen, j);
            acc = _mm_add_epi32(acc, _mm_add_epi32(x1, x2));
        }

    flenv_accumulate:
        LOOP_STORE;

        x += inv_ratio;
    }
    goto final;

fast:
    i = 0;
    for (; i < outlen; ++i) {
        LOOP_FIRCOEFF_SCALAR;

        off = (int) (x >> 32);
        if (off >= 0)
            break;

        fidx0 = -off;
        fidx1 = flen * 8;

        accs0 = 0;
        accs1 = 0;
        for (j = fidx0; j < fidx1; ++j) {
            f = (((const short *) fd)[(fn+0) * (flen*8) + j] * ff0 +
                 ((const short *) fd)[(fn+1) * (flen*8) + j] * ff1)
                >> INTERP_BITS;
            accs0 += ((const short *) in)[(j + off)*2 + 0] * f;
            accs1 += ((const short *) in)[(j + off)*2 + 1] * f;
        }
        acc = _mm_set_epi32(0, 0, accs1, accs0);

        LOOP_STORE;
        x += inv_ratio;
    }

    switch (flen) {
    case 1:
        for (; i < outlen; ++i) {
            LOOP_FIRCOEFF;
            off = (int) (x >> 32);
            if (off + 8 > inlen)
                break;

            LOOP_COMPUTE(1, 0);
            acc = _mm_add_epi32(x1, x2);
            LOOP_STORE;
            x += inv_ratio;
        }
        break;

    case 2:
        for (; i < outlen; ++i) {
            LOOP_FIRCOEFF;
            off = (int) (x >> 32);
            if (off + 16 > inlen)
                break;

            LOOP_COMPUTE(2, 0);
            acc = _mm_add_epi32(x1, x2);
            LOOP_COMPUTE(2, 1);
            acc = _mm_add_epi32(acc, _mm_add_epi32(x1, x2));
            LOOP_STORE;

            x += inv_ratio;
        }
        break;

    case 3:
        for (; i < outlen; ++i) {
            LOOP_FIRCOEFF;
            off = (int) (x >> 32);
            if (off + 24 > inlen)
                break;

            LOOP_COMPUTE(3, 0);
            acc = _mm_add_epi32(x1, x2);
            LOOP_COMPUTE(3, 1);
            acc = _mm_add_epi32(acc, _mm_add_epi32(x1, x2));
            LOOP_COMPUTE(3, 2);
            acc = _mm_add_epi32(acc, _mm_add_epi32(x1, x2));
            LOOP_STORE;

            x += inv_ratio;
        }
        break;
    }

    for (; i < outlen; ++i) {
        LOOP_FIRCOEFF_SCALAR;

        off = (int) (x >> 32);

        fidx0 = 0;
        fidx1 = inlen - off;

        accs0 = 0;
        accs1 = 0;
        for (j = fidx0; j < fidx1; ++j) {
            f = (((const short *) fd)[(fn+0) * (flen*8) + j] * ff0 +
                 ((const short *) fd)[(fn+1) * (flen*8) + j] * ff1)
                >> INTERP_BITS;
            accs0 += ((const short *) in)[(j + off)*2 + 0] * f;
            accs1 += ((const short *) in)[(j + off)*2 + 1] * f;
        }
        acc = _mm_set_epi32(0, 0, accs1, accs0);

        LOOP_STORE;
        x += inv_ratio;
    }

    goto final;

final:
    ds = _mm_cvtsi128_si32(dsv);
    for (i = 0; i < (outlen & 3) * 2; ++i)
        ds = LCG_A * ds + LCG_C;
    *pos = x;
    *dither = ds;

    /* Store remaing bytes */
    if ((outlen & 3) != 0) {
        LOOP_COMBINE;
        u.x = acc;
        memcpy((short *) out + (outlen & ~3) * 2, &u, 4 * (outlen & 3));
    }
}

#endif
