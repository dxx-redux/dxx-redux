/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "defs.h"
#include "fresample.h"
#include <string.h>

#define NAMELEN 12

static const char LFR_PARAM_NAME[LFR_PARAM_COUNT][NAMELEN] = {
    "quality",
    "inrate",
    "outrate",
    "ftransition",
    "maxfreq",
    "loose",
    "minfpass",
    "fpass",
    "fstop",
    "atten"
};

const char *
lfr_param_name(lfr_param_t pname)
{
    int n = pname;
    if (n < 0 || n >= LFR_PARAM_COUNT)
        return NULL;
    return LFR_PARAM_NAME[n];
}

int
lfr_param_lookup(const char *pname, size_t len)
{
    char tmp[NAMELEN], c;
    int i;
    if (len > NAMELEN)
        return -1;
    for (i = 0; i < (int) len; ++i) {
        c = pname[i];
        if (c >= 'A' && c <= 'Z')
            c += 'a' - 'A';
        tmp[i] = c;
    }
    for (; i < NAMELEN; ++i)
        tmp[i] = '\0';
    for (i = 0; i < LFR_PARAM_COUNT; ++i) {
        if (!memcmp(tmp, LFR_PARAM_NAME[i], NAMELEN))
            return i;
    }
    return -1;
}
