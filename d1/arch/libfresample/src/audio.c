/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "audio.h"
#include "common.h"

#include <stdlib.h>

void
audio_init(struct audio *a)
{
    a->alloc = NULL;
    a->data = NULL;
    a->nframe = 0;
    a->fmt = LFR_FMT_U8;
    a->nchan = 0;
    a->rate = 0;
}

void
audio_destroy(struct audio *a)
{
    if (a->alloc)
        free(a->alloc);
    a->alloc = NULL;
}

void
audio_alloc(struct audio *a,
            size_t nframe, lfr_fmt_t format, int nchan, int rate)
{
    size_t ssz, fsz;

    ssz = audio_format_size(format);
    if ((size_t) nchan > (size_t) -1 / ssz)
        error("audio too large");
    fsz = ssz * nchan;
    if (nframe > (size_t) -1 / fsz)
        error("audio too large");

    if (a->alloc)
        free(a->alloc);
    a->alloc = NULL;
    a->alloc = xmalloc(fsz * nframe);
    a->data = a->alloc;
    a->nframe = nframe;
    a->fmt = format;
    a->nchan = nchan;
    a->rate = rate;
}

void
audio_alias(struct audio *dest, const struct audio *src)
{
    if (dest == src)
        return;
    if (dest->alloc) {
        free(dest->alloc);
        dest->alloc = NULL;
    }
    dest->data = src->data;
    dest->nframe = src->nframe;
    dest->fmt = src->fmt;
    dest->nchan = src->nchan;
    dest->rate = src->rate;
}

void
audio_convert(struct audio *a, lfr_fmt_t fmt)
{
    lfr_fmt_t dfmt = fmt, sfmt = a->fmt;
    size_t ssz, dsz, n;
    const void *sptr;
    void *dptr;

    if (sfmt == dfmt)
        return;

    n = a->nframe * a->nchan;
    dsz = audio_format_size(dfmt);
    ssz = audio_format_size(sfmt);
    if (a->alloc && dsz == ssz)
        dptr = a->alloc;
    else
        dptr = xmalloc(dsz * n);
    sptr = a->data;

    switch (sfmt) {
    case LFR_FMT_U8:
    case LFR_FMT_S24BE:
    case LFR_FMT_S24LE:
    case LFR_FMT_F32BE:
    case LFR_FMT_F32LE:
        error("unsupported format");
        break;

    case LFR_FMT_S16BE:
    case LFR_FMT_S16LE:
        switch (dfmt) {
        case LFR_FMT_S16BE:
        case LFR_FMT_S16LE:
            lfr_swap16(dptr, sptr, n);
            break;

        default:
            error("unspported format");
        }
        break;
    }

    if (a->alloc && a->alloc != dptr)
        free(a->alloc);
    a->alloc = dptr;
    a->data = dptr;
    a->fmt = dfmt;
}
