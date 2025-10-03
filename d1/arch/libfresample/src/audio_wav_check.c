/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "audio.h"
#include <string.h>

int
audio_wav_check(const void *data, size_t length)
{
    const unsigned char *p = data;
    if (length < 12)
        return 0;
    if (!memcmp(p, "RIFF", 4) && !memcmp(p + 8, "WAVE", 4))
        return 1;
    return 0;
}
