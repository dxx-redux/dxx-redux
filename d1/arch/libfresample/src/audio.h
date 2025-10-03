/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#ifndef AUDIO_H
#define AUDIO_H
#include <stddef.h>
#include <stdio.h>
#include "fresample.h"

struct audio {
    /* Allocated region for audio data.  If this is NULL, then the
       audio data references some region of memory elsewhere.  */
    void *alloc;

    /* Audio data */
    const void *data;

    /* Number of frames */
    size_t nframe;

    /* Audio sample format */
    lfr_fmt_t fmt;

    /* Number of channels */
    int nchan;

    /* Audio sample rate */
    int rate;
};

/* Get the name for the given audio format.  */
const char *
audio_format_name(lfr_fmt_t fmt);

/* Look up an audio format by name.  Returns -1 on failure.  */
int
audio_format_lookup(const char *name);

/* Get the size of an audio format in bytes.  */
size_t
audio_format_size(lfr_fmt_t fmt);

/* Parse an audio sample rate.  Returns -1 on failure.  */
int
audio_rate_parse(const char *rate);

/* Format an audio sample rate as a string.  */
#define AUDIO_RATE_FMTLEN 16
void
audio_rate_format(char *buf, size_t buflen, int rate);

/* Zero the fields in an audio structure.  */
void
audio_init(struct audio *a);

/* Free memory used by an audio structure.  */
void
audio_destroy(struct audio *a);

/* Set the destination to alias the source.  */
void
audio_alias(struct audio *dest, const struct audio *src);

/* Allocate audio data with the given parameters.  The audio object
   must be initialized.  */
void
audio_alloc(struct audio *a,
            size_t nframe, lfr_fmt_t format, int nchan, int rate);

/* Load raw PCM into an audio structure.  The audio structure will
   alias the raw data.  */
void
audio_raw_load(struct audio *a, const void *data, size_t nframe,
               lfr_fmt_t format, int nchan, int rate);

/* Return 1 if the file could be a WAV file, 0 otherwise.  */
int
audio_wav_check(const void *data, size_t length);

/* Load a WAV file into an audio structure.  The audio file will
   probably alias the WAV data.  */
void
audio_wav_load(struct audio *a, const void *data, size_t length);

/* Write a WAV file to disk.  */
void
audio_wav_save(FILE *fp, const struct audio *a);

/* Convert an audio buffer to the given format.  */
void
audio_convert(struct audio *a, lfr_fmt_t fmt);

#endif
