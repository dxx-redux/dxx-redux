/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "audio.h"
#include "binary.h"
#include "riff.h"

#include <string.h>

/* Wave audio fomats we understand */
enum {
    WAVE_PCM = 1,
    WAVE_FLOAT = 3
};

static const char ERROR_NOTWAV[] = "not a WAVE file";
static const char ERROR_WAV[] = "invalid WAVE file";
static const char ERROR_UNKWAV[] = "unsupported WAVE format";

void
audio_wav_load(struct audio *a, const void *data, size_t length)
{
    struct riff riff;
    struct riff_tag *tag;
    uint16_t afmt, nchan, sampbits;
    uint32_t rate, nframe;
    const unsigned char *p;
    lfr_fmt_t format;

    riff_parse(&riff, data, length);

    if (memcmp(riff.tag, "WAVE", 4))
        error(ERROR_NOTWAV);

    /* Read WAVE format information */
    tag = riff_get(&riff, "fmt ");
    if (!tag)
        error(ERROR_WAV);
    if (tag->length < 16)
        error(ERROR_WAV);
    p = tag->data;
    afmt = read_lu16(p + 0);
    nchan = read_lu16(p + 2);
    rate = read_lu32(p + 4);
    /* blkalign = read_lu16(p + 12); */
    sampbits = read_lu16(p + 14);

    /* Read WAVE data */
    tag = riff_get(&riff, "data");
    if (!tag)
        error(ERROR_WAV);
    p = tag->data;

    switch (afmt) {
    case WAVE_PCM:
        switch (sampbits) {
        case 8:
            nframe = tag->length / nchan;
            format = LFR_FMT_U8;
            break;

        case 16:
            nframe = tag->length / (2 * nchan);
            format = LFR_FMT_S16LE;
            break;

        case 24:
            nframe = tag->length / (3 * nchan);
            format = LFR_FMT_S24LE;
            break;

        default:
            error(ERROR_UNKWAV);
            return;
        }
        break;

    case WAVE_FLOAT:
        if (sampbits != 32) {
            error(ERROR_UNKWAV);
            return;
        }
        nframe = tag->length / (4 * nchan);
        format = LFR_FMT_F32LE;
        break;

    default:
        error(ERROR_UNKWAV);
        return;
    }

    riff_destroy(&riff);

    audio_raw_load(a, p, nframe, format, nchan, rate);
}
