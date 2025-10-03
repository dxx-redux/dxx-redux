/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "binary.h"
#include "common.h"
#include "riff.h"

#include <stdlib.h>
#include <string.h>

static const char RIFF_ERROR[] = "invalid RIFF data";

void
riff_parse(struct riff *riff, const void *data, size_t length)
{
    const unsigned char *p = data;
    unsigned rlength, pos;
    struct riff_tag t, *tp;
    unsigned nalloc;

    riff->atags = 0;
    riff->ntags = 0;
    riff->tags = NULL;

    if (length < 8)
        error(RIFF_ERROR);
    if (memcmp(p, "RIFF", 4))
        error(RIFF_ERROR);
    rlength = read_lu32(p + 4);
    if (rlength > length - 8 || rlength < 4)
        error(RIFF_ERROR);
    memcpy(riff->tag, p + 8, 4);

    p += 12;
    rlength -= 4;
    if (!rlength)
        return;
    if (rlength < 8)
        error(RIFF_ERROR);
    pos = 0;
    while (pos < rlength) {
        if (pos > rlength - 8)
            error(RIFF_ERROR);
        memcpy(&t.tag, p + pos, 4);
        t.length = read_lu32(p + pos + 4);
        pos += 8;
        if (t.length > rlength - pos)
            error(RIFF_ERROR);
        t.data = p + pos;
        pos += t.length;

        if (riff->ntags >= riff->atags) {
            nalloc = riff->atags ? riff->atags * 2 : 2;
            tp = xrealloc(riff->tags, sizeof(*tp) * nalloc);
            riff->tags = tp;
            riff->atags = nalloc;
        }
        memcpy(&riff->tags[riff->ntags++], &t, sizeof(t));

        if (pos & 1) {
            /* Guaranteed not to overflow,
               since we subtracted 4 from rlength */
            pos += 1;
        }
    }
}

void
riff_destroy(struct riff *riff)
{
    free(riff->tags);
}

struct riff_tag *
riff_get(struct riff *riff, const char tag[4])
{
    struct riff_tag *p = riff->tags, *e = p + riff->ntags;
    for (; p != e; ++p)
        if (!memcmp(p->tag, tag, 4))
            return p;
    return NULL;
}
