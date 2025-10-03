/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "audio.h"

#include <limits.h>
#include <math.h>
#include <stdio.h>
#include <stdlib.h>

int
audio_rate_parse(const char *rate)
{
    double v;
    char *ep;

    v = strtod(rate, &ep);
    while (*ep == ' ')
        ep++;
    if (*ep == 'k') {
        v *= 1e3;
        ep++;
    }
    if ((ep[0] == 'h' || ep[0] == 'H') &&
        (ep[1] == 'z' || ep[1] == 'Z'))
        ep += 2;
    while (*ep == ' ')
        ep++;
    if (*ep != '\0')
        return -1;

    v = floor(v + 0.5);
    if (v < 1 || v > INT_MAX)
        return -1;
    return (int) v;
}

void
audio_rate_format(char *buf, size_t buflen, int rate)
{
    int k, r;
    k = rate / 1000;
    r = rate % 1000;
    if (!k) {
        snprintf(buf, buflen, "%d Hz", r);
    } else if (!r) {
        snprintf(buf, buflen, "%d kHz", k);
    } else {
        if (!(r % 10)) {
            r /= 10;
            if (!(r % 10))
                r /= 10;
        }
        snprintf(buf, buflen, "%d.%d kHz", k, r);
    }
}
