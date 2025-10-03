/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#define LFR_ALTIVEC 1

#include "cpu.h"
#if defined(LFR_CPU_PPC)
#include "resample.h"
#include <stdint.h>
#include <string.h>

#define LOOP_COMBINE \
    perm_lo64 = vec_add(perm_hi64, vec_splat_u8(8));    \
                                                        \
    x1 = vec_add(                                       \
        vec_perm(a[0], a[1], perm_hi64),                \
        vec_perm(a[0], a[1], perm_lo64));               \
    x2 = vec_add(                                       \
        vec_perm(a[2], a[3], perm_hi64),                \
        vec_perm(a[2], a[3], perm_lo64));               \
                                                        \
    shift = vec_splat_u32(1);                           \
    x1 = vec_add(x1, vec_ctf(vec_srl(dsv, shift), 31)); \
    z1 = vec_cts(vec_floor(x1), 0);                     \
    dsv = lfr_vecrand(dsv, lcg_a, lcg_c);               \
    x2 = vec_add(x2, vec_ctf(vec_srl(dsv, shift), 31)); \
    z2 = vec_cts(vec_floor(x2), 0);                     \
    dsv = lfr_vecrand(dsv, lcg_a, lcg_c);               \
    out1 = vec_packs(z1, z2)

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
lfr_resample_s16n2f32_altivec(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio, unsigned *dither,
    void *out, int outlen, const void *in, int inlen,
    const struct lfr_filter *filter)
{
    int i, j, log2nfilt, fn, ff0, ff1, off, flen, fidx0, fidx1, a0, a1;
    float accs0, accs1, ff0f, ff1f, f;
    vector unsigned char load_perm, store_perm;
    vector unsigned char perm_hi64 =
        { 0, 1, 2, 3, 4, 5, 6, 7, 16, 17, 18, 19, 20, 21, 22, 23 };
    vector unsigned char perm_lo64;
    vector signed short dati0, dati1, dati2, out0, out1;
    vector float ff0v, ff1v, fir0, fir1, fir2, fir3;
    vector float dat0, dat1, dat2, dat3;
    vector float acc, acc_a, acc_b, zv, a[4], x1, x2;
    vector signed int z1, z2;
    vector unsigned int dsv, lcg_a, lcg_c, shift;
    const vector float *fd;
    lfr_fixed_t x;
    unsigned ds;

    union {
        unsigned w[8];
        float f[8];
        vector signed short vh[2];
        vector unsigned int vw[2];
        vector float vf[2];
    } u;

    fd = filter->data;
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

    u.f[2] = 0.0f;
    u.f[3] = 0.0f;
    zv = (vector float) vec_splat_s32(0);
    out0 = out1 = vec_splat_s16(0);
    store_perm = vec_lvsr(0, (short *) out);
    for (i = 0; i < outlen; ++i) {
        /* fn: filter number
           ff0: filter factor for filter fn
           ff1: filter factor for filter fn+1 */
        fn = (((unsigned) x >> 1) >> (31 - log2nfilt)) &
            ((1u << log2nfilt) - 1);
        ff1 = ((unsigned) x >> (32 - log2nfilt - INTERP_BITS)) &
            ((1u << INTERP_BITS) - 1);
        ff0 = (1u << INTERP_BITS) - ff1;
        ff0f = (float) ff0 * (1.0f / (1 << INTERP_BITS));
        ff1f = (float) ff1 * (1.0f / (1 << INTERP_BITS));
        u.f[0] = ff0f;
        u.f[1] = ff1f;
        ff0v = u.vf[0];

        acc_a = zv;
        acc_b = zv;
        /* off: offset in input corresponding to first sample in filter */
        off = (int) (x >> 32);
        /* fidx0, fidx1: start, end indexes in FIR data */
        fidx0 = (-off + 7) >> 3;
        fidx1 = (inlen - off) >> 3;
        if (fidx0 > 0) {
            if (fidx0 > flen)
                goto accumulate;
            accs0 = 0.0f;
            accs1 = 0.0f;
            for (j = -off; j < fidx0 * 8; ++j) {
                f = ((const float *) fd)[(fn+0) * (flen*8) + j] * ff0f +
                    ((const float *) fd)[(fn+1) * (flen*8) + j] * ff1f;
                accs0 += ((const short *) in)[(j + off)*2 + 0] * f;
                accs1 += ((const short *) in)[(j + off)*2 + 1] * f;
            }
            u.f[0] = accs0;
            u.f[1] = accs1;
            acc_a = u.vf[0];
        } else {
            fidx0 = 0;
        }
        if (fidx1 < flen) {
            if (fidx1 < 0)
                goto accumulate;
            accs0 = 0.0f;
            accs1 = 0.0f;
            for (j = fidx1 * 8; j < inlen - off; ++j) {
                f = ((const float *) fd)[(fn+0) * (flen*8) + j] * ff0f +
                    ((const float *) fd)[(fn+1) * (flen*8) + j] * ff1f;
                accs0 += ((const short *) in)[(j + off)*2 + 0] * f;
                accs1 += ((const short *) in)[(j + off)*2 + 1] * f;
            }
            u.f[0] = accs0;
            u.f[1] = accs1;
            acc_b = u.vf[0];
        } else {
            fidx1 = flen;
        }

        ff1v = vec_splat(ff0v, 1);
        ff0v = vec_splat(ff0v, 0);
        if ((((unsigned) (uintptr_t) in + off*4) & 15) == 0) {
            for (j = fidx0; j < fidx1; ++j) {
                dati0 = vec_ld(off*4 + j*32 + 0,  (const short *) in);
                dati1 = vec_ld(off*4 + j*32 + 16, (const short *) in);
                fir0 = fd[(fn+0)*flen*2 + j*2 + 0];
                fir1 = fd[(fn+0)*flen*2 + j*2 + 1];
                fir2 = fd[(fn+1)*flen*2 + j*2 + 0];
                fir3 = fd[(fn+1)*flen*2 + j*2 + 1];
                dat0 = vec_ctf(vec_unpackh(dati0), 0);
                dat1 = vec_ctf(vec_unpackl(dati0), 0);
                dat2 = vec_ctf(vec_unpackh(dati1), 0);
                dat3 = vec_ctf(vec_unpackl(dati1), 0);
                fir0 = vec_madd(fir0, ff0v, vec_madd(fir2, ff1v, zv));
                fir1 = vec_madd(fir1, ff0v, vec_madd(fir3, ff1v, zv));
                acc_a = vec_madd(dat0, vec_mergeh(fir0, fir0), acc_a);
                acc_b = vec_madd(dat1, vec_mergel(fir0, fir0), acc_b);
                acc_a = vec_madd(dat2, vec_mergeh(fir1, fir1), acc_a);
                acc_b = vec_madd(dat3, vec_mergel(fir1, fir1), acc_b);
            }
        } else {
            load_perm = vec_lvsl(off*4, (const short *) in);
            dati2 = vec_ld(off*4 + fidx0*32, (const short *) in);
            for (j = fidx0; j < fidx1; ++j) {
                dati0 = dati2;
                dati1 = vec_ld(off*4 + j*32 + 16, (const short *) in);
                dati2 = vec_ld(off*4 + j*32 + 32, (const short *) in);
                dati0 = vec_perm(dati0, dati1, load_perm);
                dati1 = vec_perm(dati1, dati2, load_perm);
                fir0 = fd[(fn+0)*flen*2 + j*2 + 0];
                fir1 = fd[(fn+0)*flen*2 + j*2 + 1];
                fir2 = fd[(fn+1)*flen*2 + j*2 + 0];
                fir3 = fd[(fn+1)*flen*2 + j*2 + 1];
                dat0 = vec_ctf(vec_unpackh(dati0), 0);
                dat1 = vec_ctf(vec_unpackl(dati0), 0);
                dat2 = vec_ctf(vec_unpackh(dati1), 0);
                dat3 = vec_ctf(vec_unpackl(dati1), 0);
                fir0 = vec_madd(fir0, ff0v, vec_madd(fir2, ff1v, zv));
                fir1 = vec_madd(fir1, ff0v, vec_madd(fir3, ff1v, zv));
                acc_a = vec_madd(dat0, vec_mergeh(fir0, fir0), acc_a);
                acc_b = vec_madd(dat1, vec_mergel(fir0, fir0), acc_b);
                acc_a = vec_madd(dat2, vec_mergeh(fir1, fir1), acc_a);
                acc_b = vec_madd(dat3, vec_mergel(fir1, fir1), acc_b);
            }
        }

    accumulate:
        acc = vec_add(acc_a, acc_b);
        LOOP_STORE;

        x += inv_ratio;
    }

    u.vw[0] = dsv;
    ds = u.w[0];
    for (i = 0; i < (outlen & 3) * 2; ++i)
        ds = LCG_A * ds + LCG_C;
    *pos = x;
    *dither = ds;

    acc = zv;
    if ((outlen & 3) == 0) {
        out0 = vec_perm(out0, out0, store_perm);
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
