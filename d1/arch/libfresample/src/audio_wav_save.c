/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "audio.h"
#include "binary.h"

#include <string.h>

#define WAV_HEADER_SIZE 44

void
audio_wav_save(FILE *fp, const struct audio *a)
{
    char header[WAV_HEADER_SIZE];
    size_t ssz, asz;

    ssz = audio_format_size(a->fmt);
    asz = ssz * a->nchan * a->nframe;

    memcpy(header, "RIFF", 4);
    write_lu32(header + 4, (unsigned) asz + 36);
    memcpy(header + 8, "WAVE", 4);

    memcpy(header + 12, "fmt ", 4);
    write_lu32(header + 16, 16);
    write_lu16(header + 20, 1); /* 1 = PCM */
    write_lu16(header + 22, a->nchan);
    write_lu32(header + 24, a->rate);
    write_lu32(header + 28, a->rate * ssz * a->nchan);
    write_lu16(header + 32, ssz * a->nchan);
    write_lu16(header + 34, ssz * 8);

    memcpy(header + 36, "data", 4);
    write_lu32(header + 40, asz);

    fwrite(header, sizeof(header), 1, fp);
    fwrite(a->data, asz, 1, fp);
}
