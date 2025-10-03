/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "filter.h"

static void
lfr_filter_get(const struct lfr_filter *fp, int iname,
               int *vi, double *vd)
{
    int sz;
    int ivalue;
    double dvalue;

    switch (iname) {
    case LFR_INFO_SIZE:
        ivalue = fp->nsamp;
        goto ivalue;

    case LFR_INFO_DELAY:
        if (vi)
            *vi = (int) (fp->delay >> 32);
        else if (vd)
            *vd = (double) (fp->delay) * (1.0 / 4294967296.0);
        else
            LFR_UNREACHABLE;
        return;

    case LFR_INFO_MEMSIZE:
        sz = fp->nsamp * ((1 << fp->log2nfilt) + 1);
        switch (fp->type) {
        case LFR_FTYPE_S16: ivalue = sz * 2; break;
        case LFR_FTYPE_F32: ivalue = sz * 4; break;
        default: ivalue = 0; break;
        }
        goto ivalue;

    case LFR_INFO_FPASS:
        dvalue = fp->f_pass;
        goto dvalue;

    case LFR_INFO_FSTOP:
        dvalue = fp->f_stop;
        goto dvalue;

    case LFR_INFO_ATTEN:
        dvalue = fp->atten;
        goto dvalue;

    default:
        return;
    }

    return;

ivalue:
    if (vi)
        *vi = ivalue;
    else if (vd)
        *vd = (double) ivalue;
    else
        LFR_UNREACHABLE;
    return;

dvalue:
    if (vi)
        *vi = (int) dvalue;
    else if (vd)
        *vd = dvalue;
    else
        LFR_UNREACHABLE;
    return;
}

void
lfr_filter_geti(const struct lfr_filter *fp, int iname, int *value)
{
    lfr_filter_get(fp, iname, value, NULL);
}

void
lfr_filter_getf(const struct lfr_filter *fp, int iname, double *value)
{
    lfr_filter_get(fp, iname, NULL, value);
}

