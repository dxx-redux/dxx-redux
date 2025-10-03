/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "audio.h"

#include <stdlib.h>

void
audio_raw_load(struct audio *a, const void *data, size_t nframe,
               lfr_fmt_t format, int nchan, int rate)
{
    if (a->alloc)
        free(a->alloc);

    a->alloc = NULL;
    a->data = data;
    a->nframe = nframe;
    a->fmt = format;
    a->nchan = nchan;
    a->rate = rate;
}
