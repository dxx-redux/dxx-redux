/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "resample.h"

void
lfr_resample_s16n2s16_scalar(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio, unsigned *dither,
    void *out, int outlen, const void *in, int inlen,
    const struct lfr_filter *filter)
{
    int i, j, acc0, acc1, f, b, fn, ff0, ff1, off, flen, fidx0, fidx1;
    const short *fd, *inp = in;
    short *outp = out;
    lfr_fixed_t x;
    unsigned ds0, ds1;

    fd = filter->data;
    flen = filter->nsamp;
    b = filter->log2nfilt;
    x = *pos;
    ds0 = *dither;
    ds1 = LCG_A * ds0 + LCG_C;

    for (i = 0; i < outlen; ++i) {
        /* fn: filter number
           ff0: filter factor for filter fn
           ff1: filter factor for filter fn+1 */
        fn = (((unsigned) x >> 1) >> (31 - b)) & ((1u << b) - 1);
        ff1 = ((unsigned) x >> (32 - b - INTERP_BITS))
            & ((1u << INTERP_BITS) - 1);
        ff0 = (1u << INTERP_BITS) - ff1;

        /* off: offset in input corresponding to first sample in filter */
        off = (int) (x >> 32);
        /* fidx0, fidx1: start, end indexes in FIR data */
        fidx0 = -off;
        if (fidx0 < 0)
            fidx0 = 0;
        fidx1 = inlen - off;
        if (fidx1 > flen)
            fidx1 = flen;

        /* acc: FIR accumulator */
        acc0 = 0;
        acc1 = 0;
        for (j = fidx0; j < fidx1; ++j) {
            f = (fd[(fn+0) * flen + j] * ff0 +
                 fd[(fn+1) * flen + j] * ff1) >> INTERP_BITS;
            acc0 += inp[(j + off)*2+0] * f;
            acc1 += inp[(j + off)*2+1] * f;
        }
        acc0 += (int) (ds0 >> 17);
        acc1 += (int) (ds1 >> 17);
        acc0 >>= 15;
        acc1 >>= 15;
        if (acc0 > 0x7fff)
            acc0 = 0x7fff;
        else if (acc0 < -0x8000)
            acc0 = -0x8000;
        if (acc1 > 0x7fff)
            acc1 = 0x7fff;
        else if (acc1 < -0x8000)
            acc1 = -0x8000;
        outp[i*2+0] = (short) acc0;
        outp[i*2+1] = (short) acc1;

        x += inv_ratio;
        ds0 = LCG_A2 * ds0 + LCG_C2;
        ds1 = LCG_A2 * ds1 + LCG_C2;
    }

    *pos = x;
    *dither = ds0;
}
