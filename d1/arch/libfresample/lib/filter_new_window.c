/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "filter.h"
#include <math.h>
#include <stdint.h>
#include <stdlib.h>

/*
  Simple sinc function implementation.  Note that this is the
  unnormalized sinc function, with zeroes at nonzero multiples of pi.
*/
static double
sinc(double x)
{
    return fabs(x) < 1e-8 ? 1.0 : sin(x) / x;
}

/*
  Modified Bessel function of the first kind of order 0, i0.  Only
  considered valid over the domain [0,18].

  This is the naive algorithm.

  In "Computation of Special Functions" by Shanjie Zhang and Jianming
  Jin, this technique is only used for 0<X<18.  However, we only need
  it for the Kaiser window, and a beta of 18 is fairly ridiculous --
  it gives a side lobe height of -180 dB, which is not only beyond the
  actual SNR of high end recording systems, it is beyond the SNR of
  theoretically perfect 24-bit systems.
*/
static double
bessel_i0(double x)
{
    double a = 1.0, y = 1.0, x2 = x*x;
    int i;
    for (i = 1; i <= 50; ++i) {
        a *= 0.25 * x2 / (i * i);
        y += a;
        if (a < y * 1e-15)
            break;
    }
    return y;
}

static void
lfr_filter_calculate_s16(short *data, int nsamp, int nfilt,
                         double offset, double cutoff, double beta)
{
    double x, x0, t, y, z, xscale, yscale, err;
    int i, j;
    double *a, sum, fac, scale;

    a = malloc(nsamp * sizeof(double));
    if (!a)
        abort(); /* FIXME: report an error */

    if (nsamp <= 8)
        scale = 31500;
    else
        scale = 32767;

    yscale = 2.0 * cutoff / bessel_i0(beta);
    xscale = (8.0 * atan(1.0)) * cutoff;
    for (i = 0; i < nfilt; ++i) {
        x0 = (nsamp - 1) / 2 + offset * i;
        sum = 0.0;
        for (j = 0; j < nsamp; ++j) {
            x = j - x0;
            t = x * (2.0 / (nsamp - 2));
            if (t <= -1.0 || t >= 1.0) {
                y = 0.0;
            } else {
                y = yscale *
                    bessel_i0(beta * sqrt(1.0 - t * t)) *
                    sinc(xscale * x);
            }
            a[j] = y;
            sum += y;
        }
        fac = scale / sum;
        err = 0.0;
        for (j = 0; j < nsamp; ++j) {
            y = a[j] * fac + err;
            z = floor(y + 0.5);
            err = z - y;
            data[i * nsamp + j] = (short) z;
        }
    }

    free(a);
}

static void
lfr_filter_calculate_f32(float *data, int nsamp, int nfilt,
                         double offset, double cutoff, double beta)
{
    double x, x0, t, y, xscale, yscale;
    int i, j;
    yscale = 2.0 * cutoff / bessel_i0(beta);
    xscale = (8.0 * atan(1.0)) * cutoff;
    for (i = 0; i < nfilt; ++i) {
        x0 = (nsamp - 1) / 2 + offset * i;
        for (j = 0; j < nsamp; ++j) {
            x = j - x0;
            t = x * (2.0 / (nsamp - 2));
            if (t <= -1.0 || t >= 1.0) {
                y = 0.0;
            } else {
                y = yscale *
                    bessel_i0(beta * sqrt(1.0 - t * t)) *
                    sinc(xscale * x);
            }
            data[i * nsamp + j] = (float) y;
        }
    }
}

void
lfr_filter_new_window(
    struct lfr_filter **fpp,
    lfr_ftype_t type, int nsamp, int log2nfilt,
    double cutoff, double beta)
{
    struct lfr_filter *fp;
    size_t esz = 1, align = 16, nfilt;
    int maxf = 0;

    if (nsamp < 1 || log2nfilt < 0)
        goto error;

    switch (type) {
    case LFR_FTYPE_S16:
        maxf = 8;
        esz = sizeof(short);
        break;

    case LFR_FTYPE_F32:
        maxf = 12;
        esz = sizeof(float);
        break;
    }
    if (log2nfilt > maxf)
        log2nfilt = maxf;
    nfilt = (1u << log2nfilt) + 1;
    if ((size_t) nsamp > (size_t) -1 / esz)
        goto error;
    if (((size_t) -1 - (align - 1) - sizeof(*fp)) / nfilt < nsamp * esz)
        goto error;
    fp = malloc(sizeof(*fp) + nsamp * nfilt * esz + align - 1);
    if (!fp)
        goto error;

    fp->type = type;
    fp->data = (void *)
        (((uintptr_t) (void *) (fp + 1) + align - 1) &
         ~((uintptr_t) align - 1));
    fp->nsamp = nsamp;
    fp->log2nfilt = log2nfilt;
    fp->delay = (lfr_fixed_t) ((nsamp - 1) / 2) << 32;

    *fpp = fp;

    switch (type) {
    case LFR_FTYPE_S16:
        lfr_filter_calculate_s16(
            fp->data, nsamp, (int) nfilt,
            1.0 / (double) (1 << log2nfilt), cutoff, beta);
        break;

    case LFR_FTYPE_F32:
        lfr_filter_calculate_f32(
            fp->data, nsamp, (int) nfilt,
            1.0 / (double) (1 << log2nfilt), cutoff, beta);
        break;
    }
    return;

error:
    *fpp = NULL;
    return;
}
