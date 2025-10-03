/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#define LFR_ALTIVEC 1

#include "cpu.h"
#if defined(LFR_CPU_PPC)
#include "resample.h"
#include <stdint.h>
#include <string.h>

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
    acc = vec_msum(dat, fir0, acc)

#define LOOP_COMBINE \
    perm_lo64 = vec_add(perm_hi64, vec_splat_u8(8));                \
                                                                    \
    x1 = vec_add(vec_mergeh(a[0], a[1]), vec_mergel(a[0], a[1]));   \
    x2 = vec_add(vec_mergeh(a[2], a[3]), vec_mergel(a[2], a[3]));   \
    x3 = vec_add(vec_mergeh(a[4], a[5]), vec_mergel(a[4], a[5]));   \
    x4 = vec_add(vec_mergeh(a[6], a[7]), vec_mergel(a[6], a[7]));   \
                                                                    \
    y1 = vec_add(                                                   \
        vec_perm(x1, x2, perm_hi64),                                \
        vec_perm(x1, x2, perm_lo64));                               \
    y2 = vec_add(                                                   \
        vec_perm(x3, x4, perm_hi64),                                \
        vec_perm(x3, x4, perm_lo64));                               \
                                                                    \
    shift = vec_splat_u32(17 - 32);                                 \
    y1 = vec_add(y1, (vector signed int) vec_sr(dsv, shift));       \
    dsv = lfr_vecrand(dsv, lcg_a, lcg_c);                           \
    y2 = vec_add(y2, (vector signed int) vec_sr(dsv, shift));       \
    dsv = lfr_vecrand(dsv, lcg_a, lcg_c);                           \
                                                                    \
    shift = vec_splat_u32(15);                                      \
    out1 = vec_packs(vec_sra(y1, shift), vec_sra(y2, shift))

#define LOOP_STORE \
    a[i & 7] = acc;                                     \
    if ((i & 7) == 7) {                                 \
        LOOP_COMBINE;                                   \
        out0 = vec_perm(out0, out1, store_perm);        \
        if (i > 7) {                                    \
            vec_st(out0, (i - 7) * 2, (short *) out);   \
        } else {                                        \
            a0 = (int) ((uintptr_t) out & 15);          \
            u.vh[0] = out0;                             \
            memcpy(out, (char *) &u + a0, 16 - a0);     \
            u.vh[0] = vec_splat_s16(0);                 \
        }                                               \
        out0 = out1;                                    \
    }

void
lfr_resample_s16n1s16_altivec(
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
    vector signed short dat, dat0, dat1, dat2, dat3;
    vector signed short out0, out1;
    vector unsigned int acc_shift, fir_shift;
    vector signed int acc, a[8], zero;
    vector signed int x1, x2, x3, x4, y1, y2;
    vector unsigned int dsv, lcg_a, lcg_c, shift;
    int fn, ff0, ff1, off, fidx0, fidx1;
    int accs, i, j, f, a0, a1;
    unsigned ds;

    union {
        unsigned short h[16];
        int w[8];
        vector signed short vh[2];
        vector signed int vs[2];
        vector unsigned int vw[2];
    } u;

    if (inlen < 16 && 0) {
        lfr_resample_s16n1s16_scalar(
            pos, inv_ratio, dither,
            out, outlen, in, inlen,
            filter);
        return;
    }

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
        ds = ds * LCG_A + LCG_C;
    }
    dsv = u.vw[0];
    u.w[0] = LCG_A4;
    u.w[1] = LCG_C4;
    lcg_a = u.vw[0];
    lcg_c = vec_splat(lcg_a, 1);
    lcg_a = vec_splat(lcg_a, 0);

    u.w[1] = 0;
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
        acc = vec_splat_s32(0);
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
            accs = 0;
            for (j = -off; j < fidx0 * 8; ++j) {
                f = (((const short *) fd)[(fn+0) * (flen*8) + j] * ff0 +
                     ((const short *) fd)[(fn+1) * (flen*8) + j] * ff1)
                    >> INTERP_BITS;
                accs += ((const short *) in)[j + off] * f;
            }
            u.w[0] = accs;
            acc = u.vs[0];
        } else {
            fidx0 = 0;
        }
        if (fidx1 < flen) {
            if (fidx1 < 0)
                goto flenv_accumulate;
            accs = 0;
            for (j = fidx1 * 8; j < inlen - off; ++j) {
                f = (((const short *) fd)[(fn+0) * (flen*8) + j] * ff0 +
                     ((const short *) fd)[(fn+1) * (flen*8) + j] * ff1)
                    >> INTERP_BITS;
                accs += ((const short *) in)[j + off] * f;
            }
            u.w[0] = accs;
            acc = vec_add(acc, u.vs[0]);
        } else {
            fidx1 = flen;
        }

        if (((uintptr_t) in + off * 2) & 15) {
            load_perm = vec_lvsl(off * 2, (unsigned char *) 0);
            dat1 = vec_ld(fidx0 * 16 + off * 2, (short *) in);
            for (j = fidx0; j < fidx1; ++j) {
                dat0 = dat1;
                dat1 = vec_ld((j + 1) * 16 + off * 2, (short *) in);
                dat = vec_perm(dat0, dat1, load_perm);
                LOOP_ACCUM(flen, j);
            }
        } else {
            for (j = fidx0; j < fidx1; ++j) {
                dat = vec_ld(j * 16 + off * 2, (short *) in);
                LOOP_ACCUM(flen, j);
            }
        }

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
        accs = 0;
        for (j = fidx0; j < fidx1; ++j) {
            f = (((const short *) fd)[(fn+0) * (flen*8) + j] * ff0 +
                 ((const short *) fd)[(fn+1) * (flen*8) + j] * ff1)
                >> INTERP_BITS;
            accs += ((const short *) in)[j + off] * f;
        }
        u.w[0] = accs;
        acc = u.vs[0];

        LOOP_STORE;
        x += inv_ratio;
    }

    switch (flen) {
    case 1:
        for (; i < outlen; ++i) {
            off = (int) (x >> 32);
            if (off + 8 > inlen)
                break;

            LOOP_FIRCOEFF;
            load_perm = vec_lvsl(off * 2, (const short *) in);
            dat0 = vec_ld(off * 2 +  0, (const short *) in);
            dat1 = vec_ld(off * 2 + 15, (const short *) in);
            acc = vec_splat_s32(0);
            dat = vec_perm(dat0, dat1, load_perm);
            LOOP_ACCUM(1, 0);
            LOOP_STORE;
            x += inv_ratio;
        }
        break;

    case 2:
        for (; i < outlen; ++i) {
            off = (int) (x >> 32);
            if (off + 16 > inlen)
                break;

            LOOP_FIRCOEFF;
            load_perm = vec_lvsl(off * 2, (const short *) in);
            dat0 = vec_ld(off * 2 +  0, (const short *) in);
            dat1 = vec_ld(off * 2 + 16, (const short *) in);
            dat2 = vec_ld(off * 2 + 31, (const short *) in);
            acc = vec_splat_s32(0);
            dat = vec_perm(dat0, dat1, load_perm);
            LOOP_ACCUM(2, 0);
            dat = vec_perm(dat1, dat2, load_perm);
            LOOP_ACCUM(2, 1);
            LOOP_STORE;
            x += inv_ratio;
        }
        break;

    case 3:
        for (; i < outlen; ++i) {
            off = (int) (x >> 32);
            if (off + 24 > inlen)
                break;

            LOOP_FIRCOEFF;
            load_perm = vec_lvsl(off * 2, (const short *) in);
            dat0 = vec_ld(off * 2 +  0, (const short *) in);
            dat1 = vec_ld(off * 2 + 16, (const short *) in);
            dat2 = vec_ld(off * 2 + 32, (const short *) in);
            dat3 = vec_ld(off * 2 + 47, (const short *) in);
            acc = vec_splat_s32(0);
            dat = vec_perm(dat0, dat1, load_perm);
            LOOP_ACCUM(3, 0);
            dat = vec_perm(dat1, dat2, load_perm);
            LOOP_ACCUM(3, 1);
            dat = vec_perm(dat2, dat3, load_perm);
            LOOP_ACCUM(3, 2);
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
        accs = 0;
        for (j = fidx0; j < fidx1; ++j) {
            f = (((const short *) fd)[(fn+0) * (flen*8) + j] * ff0 +
                 ((const short *) fd)[(fn+1) * (flen*8) + j] * ff1)
                >> INTERP_BITS;
            accs += ((const short *) in)[j + off] * f;
        }
        u.w[0] = accs;
        acc = u.vs[0];

        LOOP_STORE;
        x += inv_ratio;
    }
    goto done;

done:
    u.vw[0] = dsv;
    ds = u.w[0];
    for (i = 0; i < (outlen & 7); ++i)
        ds = LCG_A * ds + LCG_C;
    *pos = x;
    *dither = ds;

    /* Store remaing bytes */
    if ((outlen & 7) == 0) {
        out0 = vec_perm(out0, out1, store_perm);
    } else {
        LOOP_COMBINE;
        out0 = vec_perm(out0, out1, store_perm);
        out1 = vec_perm(out1, out1, store_perm);
    }

    u.vh[0] = out0;
    u.vh[1] = out1;
    i = outlen & ~7;
    /* byte offset of where out0 belongs relative to out */
    off = 2*i - (((int) (uintptr_t) out + 2*i) & 15);
    a0 = off < 0 ? -off : 0;
    a1 = outlen * 2 - off;
    if (a1 > 32)
        a1 = 32;
    if (a1 > a0)
        memcpy((char *) out + off + a0, (char *) &u + a0, a1 - a0);
}

#endif
