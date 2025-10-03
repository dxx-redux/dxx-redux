/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "defs.h"
#include "fresample.h"
#include <string.h>

#define NAMELEN 8

static const char LFR_INFO_NAME[LFR_INFO_COUNT][NAMELEN] = {
    "size",
    "delay",
    "memsize",
    "fpass",
    "fstop",
    "atten"
};

const char *
lfr_info_name(int pname)
{
    if (pname < 0 || pname >= LFR_INFO_COUNT)
        return NULL;
    return LFR_INFO_NAME[pname];
}

int
lfr_info_lookup(const char *pname, size_t len)
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
    for (i = 0; i < LFR_INFO_COUNT; ++i) {
        if (!memcmp(tmp, LFR_INFO_NAME[i], NAMELEN))
            return i;
    }
    return -1;
}
