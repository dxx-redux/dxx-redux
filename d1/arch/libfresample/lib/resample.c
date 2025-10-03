/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "defs.h"
#include "cpu.h"
#include "filter.h"
#include "resample.h"

LFR_PUBLIC void
lfr_resample(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio,
    unsigned *dither, int nchan,
    void *out, lfr_fmt_t outfmt, int outlen,
    const void *in, lfr_fmt_t infmt, int inlen,
    const struct lfr_filter *filter)
{
    lfr_resample_func_t func;
    if (outfmt == LFR_FMT_S16_NATIVE && infmt == LFR_FMT_S16_NATIVE) {
        func = lfr_resample_s16func(nchan, filter);
        if (!func)
            return;
        func(pos, inv_ratio, dither, out, outlen, in, inlen, filter);
    }
}
