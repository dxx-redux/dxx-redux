/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "convert.h"
#include <stdint.h>

void
lfr_swap16_scalar(void *dest, const void *src, size_t count)
{
    size_t i, n;
    unsigned x, y;
    const unsigned char *s8;
    unsigned char *d8;
    const unsigned short *s16;
    unsigned short *d16;
    const unsigned *s32;
    unsigned *d32;

    if (!count)
        return;

    if ((((uintptr_t) dest | (uintptr_t) src) & 1u) != 0) {
        /* completely unaligned */
        s8 = src;
        d8 = dest;
        for (i = 0; i < count; ++i) {
            x = s8[i*2+0];
            y = s8[i*2+1];
            d8[i*2+0] = (unsigned char) y;
            d8[i*2+1] = (unsigned char) x;
        }
    } else if ((((uintptr_t) dest - (uintptr_t) src) & 3u) != 0) {
        /* 16-bit aligned */
        s16 = src;
        d16 = dest;
        for (i = 0; i < count; ++i) {
            x = s16[i];
            d16[i] = (unsigned short) ((x >> 8) | (x << 8));
        }
    } else {
        /* 16-bit aligned, with 32-bit aligned delta */
        s16 = src;
        d16 = dest;
        n = count;
        if ((uintptr_t) dest & 3u) {
            x = s16[0];
            d16[0] = (unsigned short) ((x >> 8) | (x << 8));
            n -= 1;
            s16 += 1;
            d16 += 1;
        }
        s32 = (const unsigned *) s16;
        d32 = (unsigned *) d16;
        for (i = 0; i < n/2; ++i) {
            x = s32[i];
            d32[i] = ((x >> 8) & 0xff00ffu) | ((x & 0xff00ffu) << 8);
        }
        d32 += n/2;
        s32 += n/2;
        n -= (n/2)*2;
        if (n) {
            s16 = (const unsigned short *) s32;
            d16 = (unsigned short *) d32;
            x = s16[0];
            d16[0] = (unsigned short) ((x >> 8) | (x << 8));
        }
    }
}
