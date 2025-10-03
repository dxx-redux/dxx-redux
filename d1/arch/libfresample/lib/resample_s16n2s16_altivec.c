/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#define LFR_ALTIVEC 1

#include "cpu.h"
#if defined(LFR_CPU_PPC)
#include "resample.h"
#include <stdint.h>
#include <string.h>

#include <stdio.h>

#define LOOP_FIRCOEFF \
    fn = (((unsigned) x >> 1) >> (31 - log2nfilt)) &            \
        ((1u << log2nfilt) - 1);                                \
    ff1 = ((unsigned) x >> (32 - log2nfilt - INTERP_BITS)) &    \
        ((1u << INTERP_BITS) - 1);                              \
    ff0 = (1u << INTERP_BITS) - ff1;                            \
    u.h[0] = ff0;                                               \
    u.h[1] = ff1;                                               \
    fir_interp = (vector signed short)                          \
        vec_splat((vector signed int) u.vs[0], 0)

#define LOOP_ACCUM(flen, j) \
    fir0 = fd[(fn+0)*(flen) + (j)];     \
    fir1 = fd[(fn+1)*(flen) + (j)];     \
                                        \
    fir0 = vec_pack(                    \
        vec_sra(                        \
            vec_msum(                   \
                vec_mergeh(fir0, fir1), \
                fir_interp,             \
                zero),                  \
            fir_shift),                 \
        vec_sra(                        \
            vec_msum(                   \
                vec_mergel(fir0, fir1), \
                fir_interp,             \
                zero),                  \
            fir_shift));                \
                                        \
    fir1 = vec_mergel(fir0, fir0);      \
    fir0 = vec_mergeh(fir0, fir0);      \
                                        \
    acc_a = vec_msum(                   \
        vec_mergeh(dat0, dat1),         \
        vec_mergeh(fir0, fir1),         \
        acc_a);                         \
    acc_b = vec_msum(                   \
        vec_mergel(dat0, dat1),         \
        vec_mergel(fir0, fir1),         \
        acc_b)

#define LOOP_COMBINE \
    perm_lo64 = vec_add(perm_hi64, vec_splat_u8(8));            \
                                                                \
    y1 = vec_add(                                               \
        vec_perm(a[0], a[1], perm_hi64),                        \
        vec_perm(a[0], a[1], perm_lo64));                       \
    y2 = vec_add(                                               \
        vec_perm(a[2], a[3], perm_hi64),                        \
        vec_perm(a[2], a[3], perm_lo64));                       \
                                                                \
    shift = vec_splat_u32(17 - 32);                             \
    y1 = vec_add(y1, (vector signed int) vec_sr(dsv, shift));   \
    dsv = lfr_vecrand(dsv, lcg_a, lcg_c);                       \
    y2 = vec_add(y2, (vector signed int) vec_sr(dsv, shift));   \
    dsv = lfr_vecrand(dsv, lcg_a, lcg_c);                       \
                                                                \
    shift = vec_splat_u32(15);                                  \
    out1 = vec_packs(vec_sra(y1, shift), vec_sra(y2, shift))

#define LOOP_STORE \
    a[i & 3] = acc;                                     \
    if ((i & 3) == 3) {                                 \
        LOOP_COMBINE;                                   \
        out0 = vec_perm(out0, out1, store_perm);        \
        if (i > 3) {                                    \
            vec_st(out0, (i - 3) * 4, (short *) out);   \
        } else {                                        \
            a0 = (int) ((uintptr_t) out & 15);          \
            u.vh[0] = out0;                             \
            memcpy(out, (char *) &u + a0, 16 - a0);     \
            u.vh[0] = vec_splat_s16(0);                 \
        }                                               \
        out0 = out1;                                    \
    }

void
lfr_resample_s16n2s16_altivec(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio, unsigned *dither,
    void *out, int outlen, const void *in, int inlen,
    const struct lfr_filter *filter)
{
    const vector signed short *fd;
    int flen, log2nfilt;
    lfr_fixed_t x;

    vector unsigned char perm_hi64 =
        { 0, 1, 2, 3, 4, 5, 6, 7, 16, 17, 18, 19, 20, 21, 22, 23 };
    vector unsigned char perm_lo64, load_perm, store_perm;
    vector signed short fir0, fir1, fir_interp;
    vector signed short dat0, dat1, dat2, dat3, dat4, dat5, dat6;
    vector signed short out0, out1;
    vector unsigned int acc_shift, fir_shift;
    vector signed int acc_a, acc_b, acc, a[4], zero;
    vector signed int y1, y2;
    vector unsigned int dsv, lcg_a, lcg_c, shift;
    int fn, ff0, ff1, off, fidx0, fidx1;
    int accs0, accs1, i, j, f, a0, a1;
    unsigned ds;

    union {
        unsigned short h[16];
        int w[8];
        vector signed short vh[2];
        vector signed int vs[2];
        vector unsigned int vw[2];
    } u;

    zero = vec_splat_s32(0);
    perm_lo64 = vec_add(perm_hi64, vec_splat_u8(8));
    acc_shift = vec_splat_u32(15);
    fir_shift = vec_splat_u32(INTERP_BITS);

    fd = (const vector signed short *) filter->data;
    flen = filter->nsamp >> 3;
    log2nfilt = filter->log2nfilt;

    x = *pos;
    ds = *dither;
    for (i = 0; i < 4; ++i) {
        u.w[i] = ds;
        ds = LCG_A * ds + LCG_C;
    }
    dsv = u.vw[0];
    u.w[0] = LCG_A4;
    u.w[1] = LCG_C4;
    lcg_a = u.vw[0];
    lcg_c = vec_splat(lcg_a, 1);
    lcg_a = vec_splat(lcg_a, 0);

    u.w[2] = 0;
    u.w[3] = 0;

    out0 = out1 = vec_splat_s16(0);
    store_perm = vec_lvsr(0, (short *) out);

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
        acc_a = vec_splat_s32(0);
        acc_b = vec_splat_s32(0);
        /* off: offset in input corresponding to first sample in
           filter */
        off = (int) (x >> 32);
        /* fixd0, fidx1: start, end indexes of 8-word (16-byte) chunks
           of whole FIR data we will use */
        fidx0 = (-off + 7) >> 3;
        fidx1 = (inlen - off) >> 3;
        if (fidx0 > 0) {
            if (fidx0 > flen) {
                acc = vec_splat_s32(0);
                goto flenv_accumulate;
            }
            accs0 = 0;
            accs1 = 0;
            for (j = -off; j < fidx0 * 8; ++j) {
                f = (((const short *) fd)[(fn+0) * (flen*8) + j] * ff0 +
                     ((const short *) fd)[(fn+1) * (flen*8) + j] * ff1)
                    >> INTERP_BITS;
                accs0 += ((const short *) in)[(j + off)*2 + 0] * f;
                accs1 += ((const short *) in)[(j + off)*2 + 1] * f;
            }
            u.w[0] = accs0;
            u.w[1] = accs1;
            acc_a = u.vs[0];
        } else {
            fidx0 = 0;
        }
        if (fidx1 < flen) {
            if (fidx1 < 0) {
                acc = vec_splat_s32(0);
                goto flenv_accumulate;
            }
            accs0 = 0;
            accs1 = 0;
            for (j = fidx1 * 8; j < inlen - off; ++j) {
                f = (((const short *) fd)[(fn+0) * (flen*8) + j] * ff0 +
                     ((const short *) fd)[(fn+1) * (flen*8) + j] * ff1)
                    >> INTERP_BITS;
                accs0 += ((const short *) in)[(j + off)*2 + 0] * f;
                accs1 += ((const short *) in)[(j + off)*2 + 1] * f;
            }
            u.w[0] = accs0;
            u.w[1] = accs1;
            acc_b = u.vs[0];
        } else {
            fidx1 = flen;
        }

        if (((uintptr_t) in + off * 4) & 15) {
            load_perm = vec_lvsl(off * 4, (unsigned char *) in);
            dat2 = vec_ld(fidx0 * 32 + off * 4, (short *) in);
            for (j = fidx0; j < fidx1; ++j) {
                dat0 = dat2;
                dat1 = vec_ld(j * 32 + off * 4 + 16, (short *) in);
                dat2 = vec_ld(j * 32 + off * 4 + 32, (short *) in);
                dat0 = vec_perm(dat0, dat1, load_perm);
                dat1 = vec_perm(dat1, dat2, load_perm);
                LOOP_ACCUM(flen, j);
            }
        } else {
            for (j = fidx0; j < fidx1; ++j) {
                dat0 = vec_ld(j * 32 + off * 4 + 0, (short *) in);
                dat1 = vec_ld(j * 32 + off * 4 + 16, (short *) in);
                LOOP_ACCUM(flen, j);
            }
        }
        acc = vec_add(acc_a, acc_b);

    flenv_accumulate:
        LOOP_STORE;
        x += inv_ratio;
    }
    goto done;

fast:
    i = 0;
    for (; i < outlen; ++i) {
        off = (int) (x >> 32);
        if (off >= 0)
            break;

        fidx0 = -off;
        fidx1 = flen * 8;

        LOOP_FIRCOEFF;
        accs0 = 0;
        accs1 = 0;
        for (j = fidx0; j < fidx1; ++j) {
            f = (((const short *) fd)[(fn+0) * (flen*8) + j] * ff0 +
                 ((const short *) fd)[(fn+1) * (flen*8) + j] * ff1)
                >> INTERP_BITS;
            accs0 += ((const short *) in)[2*(j + off) + 0] * f;
            accs1 += ((const short *) in)[2*(j + off) + 1] * f;
        }
        u.w[0] = accs0;
        u.w[1] = accs1;
        acc = u.vs[0];

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

            acc_a = vec_splat_s32(0);
            acc_b = vec_splat_s32(0);
            load_perm = vec_lvsl(off * 4, (short *) in);
            dat0 = vec_ld( 0 + off * 4, (short *) in);
            dat1 = vec_ld(16 + off * 4, (short *) in);
            dat2 = vec_ld(31 + off * 4, (short *) in);

            dat0 = vec_perm(dat0, dat1, load_perm);
            dat1 = vec_perm(dat1, dat2, load_perm);
            LOOP_ACCUM(1, 0);

            acc = vec_add(acc_a, acc_b);
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

            acc_a = vec_splat_s32(0);
            acc_b = vec_splat_s32(0);
            load_perm = vec_lvsl(off * 4, (short *) in);
            dat0 = vec_ld( 0 + off * 4, (short *) in);
            dat1 = vec_ld(16 + off * 4, (short *) in);
            dat2 = vec_ld(32 + off * 4, (short *) in);
            dat3 = vec_ld(48 + off * 4, (short *) in);
            dat4 = vec_ld(63 + off * 4, (short *) in);

            dat0 = vec_perm(dat0, dat1, load_perm);
            dat1 = vec_perm(dat1, dat2, load_perm);
            LOOP_ACCUM(2, 0);
            dat0 = vec_perm(dat2, dat3, load_perm);
            dat1 = vec_perm(dat3, dat4, load_perm);
            LOOP_ACCUM(2, 1);

            acc = vec_add(acc_a, acc_b);
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

            acc_a = vec_splat_s32(0);
            acc_b = vec_splat_s32(0);
            load_perm = vec_lvsl(off * 4, (short *) in);
            dat0 = vec_ld( 0 + off * 4, (short *) in);
            dat1 = vec_ld(16 + off * 4, (short *) in);
            dat2 = vec_ld(32 + off * 4, (short *) in);
            dat3 = vec_ld(48 + off * 4, (short *) in);
            dat4 = vec_ld(64 + off * 4, (short *) in);
            dat5 = vec_ld(80 + off * 4, (short *) in);
            dat6 = vec_ld(95 + off * 4, (short *) in);

            dat0 = vec_perm(dat0, dat1, load_perm);
            dat1 = vec_perm(dat1, dat2, load_perm);
            LOOP_ACCUM(3, 0);
            dat0 = vec_perm(dat2, dat3, load_perm);
            dat1 = vec_perm(dat3, dat4, load_perm);
            LOOP_ACCUM(3, 1);
            dat0 = vec_perm(dat4, dat5, load_perm);
            dat1 = vec_perm(dat5, dat6, load_perm);
            LOOP_ACCUM(3, 2);

            acc = vec_add(acc_a, acc_b);
            LOOP_STORE;
            x += inv_ratio;
        }
        break;
    }

    for (; i < outlen; ++i) {
        off = (int) (x >> 32);

        fidx0 = 0;
        fidx1 = inlen - off;

        LOOP_FIRCOEFF;
        accs0 = 0;
        accs1 = 0;
        for (j = fidx0; j < fidx1; ++j) {
            f = (((const short *) fd)[(fn+0) * (flen*8) + j] * ff0 +
                 ((const short *) fd)[(fn+1) * (flen*8) + j] * ff1)
                >> INTERP_BITS;
            accs0 += ((const short *) in)[2*(j + off) + 0] * f;
            accs1 += ((const short *) in)[2*(j + off) + 1] * f;
        }
        u.w[0] = accs0;
        u.w[1] = accs1;
        acc = u.vs[0];

        LOOP_STORE;
        x += inv_ratio;
    }
    goto done;

done:
    u.vw[0] = dsv;
    ds = u.w[0];
    for (i = 0; i < (outlen & 3) * 2; ++i)
        ds = LCG_A * ds + LCG_C;
    *pos = x;
    *dither = ds;

    /* Store remaing bytes */
    if ((outlen & 3) == 0) {
        out1 = vec_splat_s16(0);
        out0 = vec_perm(out0, out1, store_perm);
    } else {
        LOOP_COMBINE;
        out0 = vec_perm(out0, out1, store_perm);
        out1 = vec_perm(out1, out1, store_perm);
    }

    u.vh[0] = out0;
    u.vh[1] = out1;
    i = outlen & ~3;
    /* byte offset of data point i in (out0, out1) */
    off = 4*i - (((int) (uintptr_t) out + 4*i) & 15);
    a0 = off < 0 ? -off : 0;
    a1 = outlen * 4 - off;
    if (a1 > 32)
        a1 = 32;
    if (a1 > a0)
        memcpy((char *) out + off + a0, (char *) &u + a0, a1 - a0);
}

#endif
