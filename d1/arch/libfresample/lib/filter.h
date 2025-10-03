/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#ifndef FILTER_H
#define FILTER_H
#include "defs.h"

/* Bits used for interpolating between two filters.  The scalar
   implementation supports up to 16 bits, but the SIMD implementations
   generally only support 14 so the values can fit in a signed 16-bit
   word.  We bring all implementations to the same accuracy in order
   to ensure that all implementations produce the same results.  */
#define INTERP_BITS 14

/*
  Filter types
*/
typedef enum {
    LFR_FTYPE_S16,
    LFR_FTYPE_F32
} lfr_ftype_t;

/*
  Array of FIR filters.

  Let N=2^log2nfilt, M=nsamp.

  This is an array of N+1 filters, 0..N, with each filter M samples
  long.  Filter number I will be centered on the sample with offset:

  floor(M/2) + I/N

  This means that filter N will be a copy of filter 0, except shifted
  to the right by one sample.  This extra filter makes the
  interpolation code simpler.
*/
struct lfr_filter {
    /*
      FIR filter coefficient data type.
    */
    lfr_ftype_t type;

    /*
      FIR filter data.  This is not allocated separately, but is part
      of the same block of memory as this structure.
    */
    void *data;

    /*
      Length of each filter, in samples.  This is chosen to make each
      filter size a multiple of the SIMD register size.  Most SIMD
      implementations today use 16 byte registers, so this will
      usually be a multiple of 8 or 4, depending on the coefficient
      type.
    */
    int nsamp;

    /*
      Base 2 logarithm of the number of different filters.  There is
      always an additional filter at the end of the filter array to
      simplify interpolation, this extra filter is a copy of the first
      filter shifted right by one sample.
    */
    int log2nfilt;

    /*
      Filter delay.  Filters are causal, so this should be
      non-negative.
    */
    lfr_fixed_t delay;

    /*
      Design parameters of the filter.
    */
    double f_pass, f_stop, atten;
};

/*
  Create a Kaiser-windowed sinc filter.
*/
LFR_PRIVATE void
lfr_filter_new_window(
    struct lfr_filter **fpp,
    lfr_ftype_t type, int nsamp, int log2nfilt,
    double cutoff, double beta);

#endif
