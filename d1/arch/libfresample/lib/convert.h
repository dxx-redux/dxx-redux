/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#ifndef CONVERT_H
#define CONVERT_H
#include "defs.h"

LFR_PRIVATE void
lfr_swap16_scalar(void *dest, const void *src, size_t count);

LFR_PRIVATE void
lfr_swap16_sse2(void *dest, const void *src, size_t count);

#endif
