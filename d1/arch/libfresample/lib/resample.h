/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#ifndef RESAMPLE_H
#define RESAMPLE_H
#include "defs.h"
#include "filter.h"

/*
  Resampling function naming convention:

  lfr_resample_<atype><nchan><ftype>_<feature>

  atype: audio data type, s16 or f32
  nchan: number of channels, n1 or n2
  ftype: filter coefficient type, s16 or f32

  feature: CPU feature set required
*/

/* ======================================== */

LFR_PRIVATE void
lfr_resample_s16n1s16_scalar(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio, unsigned *dither,
    void *out, int outlen, const void *in, int inlen,
    const struct lfr_filter *filter);

LFR_PRIVATE void
lfr_resample_s16n1s16_sse2(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio, unsigned *dither,
    void *out, int outlen, const void *in, int inlen,
    const struct lfr_filter *filter);

LFR_PRIVATE void
lfr_resample_s16n1s16_altivec(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio, unsigned *dither,
    void *out, int outlen, const void *in, int inlen,
    const struct lfr_filter *filter);

/* ======================================== */

LFR_PRIVATE void
lfr_resample_s16n2s16_scalar(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio, unsigned *dither,
    void *out, int outlen, const void *in, int inlen,
    const struct lfr_filter *filter);

LFR_PRIVATE void
lfr_resample_s16n2s16_sse2(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio, unsigned *dither,
    void *out, int outlen, const void *in, int inlen,
    const struct lfr_filter *filter);

LFR_PRIVATE void
lfr_resample_s16n2s16_altivec(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio, unsigned *dither,
    void *out, int outlen, const void *in, int inlen,
    const struct lfr_filter *filter);

/* ======================================== */

LFR_PRIVATE void
lfr_resample_s16n1f32_scalar(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio, unsigned *dither,
    void *out, int outlen, const void *in, int inlen,
    const struct lfr_filter *filter);

LFR_PRIVATE void
lfr_resample_s16n1f32_sse2(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio, unsigned *dither,
    void *out, int outlen, const void *in, int inlen,
    const struct lfr_filter *filter);

LFR_PRIVATE void
lfr_resample_s16n1f32_altivec(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio, unsigned *dither,
    void *out, int outlen, const void *in, int inlen,
    const struct lfr_filter *filter);

/* ======================================== */

LFR_PRIVATE void
lfr_resample_s16n2f32_scalar(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio, unsigned *dither,
    void *out, int outlen, const void *in, int inlen,
    const struct lfr_filter *filter);

LFR_PRIVATE void
lfr_resample_s16n2f32_sse2(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio, unsigned *dither,
    void *out, int outlen, const void *in, int inlen,
    const struct lfr_filter *filter);

LFR_PRIVATE void
lfr_resample_s16n2f32_altivec(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio, unsigned *dither,
    void *out, int outlen, const void *in, int inlen,
    const struct lfr_filter *filter);

#endif
