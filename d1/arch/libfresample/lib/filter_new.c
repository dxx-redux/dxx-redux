/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "filter.h"
#include "param.h"
#include <math.h>

#if 0
#include <stdio.h>
#define debug(...) fprintf(stderr, __VA_ARGS__)
#else
#define debug(...) (void) 0
#endif

void
lfr_filter_new(struct lfr_filter **fpp, struct lfr_param *param)
{
    double f_pass, f_stop, atten, f_pass2;
    double dw, dw2, lenf, atten2, atten3, maxerror, beta;
    double ierror, rerror, error;
    double t, a, ulp;
    int len, align=8, max_oversample, oversample;
    lfr_ftype_t type;
    struct lfr_filter *fp;

    lfr_param_calculate(param);

    f_pass = param->param[LFR_PARAM_FPASS];
    f_stop = param->param[LFR_PARAM_FSTOP];
    atten  = param->param[LFR_PARAM_ATTEN];

    /* atten2 is the attenuation of the ideal (un-rounded) filter,
       maxerror is the ratio of a 0 dBFS signal to the permitted
       roundoff and interpolation error.  This divides the
       "attenuation" into error budgets with the assumption that the
       error from each source is uncorrelated.  Note that atten2 is
       measured in dB, whereas maxerror is a ratio to a full scale
       signal.  */
    atten2 = atten + 3.0;
    maxerror = exp((atten + 3.0) * (-log(10.0) / 20.0));

    /* Calculate the filter order from the attenuation.  Round up to
       the nearest multiple of the SIMD register alignment.  */
    dw = (8.0 * atan(1.0)) * (f_stop - f_pass);
    lenf = (atten2 - 8.0) / (4.57 * dw) + 1;
    len = (int) ceil(lenf / align) * align;
    if (len < align)
        len = align;
    debug("length: %d\n", len);

    /* ========================================
       Determine filter coefficient size.
       ======================================== */

    /* We can calculate expected roundoff error from filter length.
       We assume that the roundoff error at each coefficient is an
       independent uniform variable with a range of 1 ULP, and that
       there is an equal roundoff step during interpolation.  This
       gives roundoff error with a variance of N/6 ULP^2.  Roundoff
       error is a ratio relative to 1 ULP.  */
    rerror = sqrt(len * (1.0 / 6.0));
    debug("rerror: %g\n", rerror);

    /* The interpolation error can be adjusted by choosing the amount
       of oversampling.  The curvature of the sinc function is bounded
       by (2*pi*f)^2, so the interpolation error is bounded by
       (pi*f/M)^2.

       We set a maximum oversampling of 2^8 for 16-bit, and 2^12 for
       floating point.  */
    t = f_pass * (4.0 * atan(1.0));
    a = 1.0 / 256;
    ierror = (t * a) * (t * a);
    ulp = 1.0 / 32768.0;
    /* We assume, again, that interpolation and roundoff error are
       uncorrelated and normal.  If error at 16-bit exceeds our
       budget, then use floating point.  */
    error = (ierror * ierror + rerror * rerror) * (ulp * ulp);
    if (error <= maxerror * maxerror) {
        type = LFR_FTYPE_S16;
        ulp = 1.0 / 32768.0;
        max_oversample = 8;
    } else {
        type = LFR_FTYPE_F32;
        ulp = 1.0 / 16777216.0;
        max_oversample = 12;
    }
    debug("format: %s\n", type == LFR_FTYPE_S16 ? "S16" : "F32");

    /* Calculate the interpolation error budget by subtracting
       roundoff error from the total error budget, since roundoff
       error is fixed.  */
    ierror = maxerror * maxerror - (rerror * rerror) * (ulp * ulp);
    ierror = (ierror > 0) ? sqrt(ierror) : 0;
    debug("maxerror: %g (%g ulp)\n", maxerror, maxerror / ulp);
    debug("ierror: %g (%g ulp)\n", ierror, ierror / ulp);
    if (ierror < ulp)
        ierror = ulp;

    /* Calculate oversampling from the error budget.  */
    a = (f_pass * (4.0 * atan(1.0))) / sqrt(ierror);
    debug("M: %g\n", a);
    oversample = (int) ceil(log(a) * (1.0 / log(2.0)));
    if (oversample < 0)
        oversample = 0;
    else if (oversample > max_oversample)
        oversample = max_oversample;
    debug("log2nfilt: %d\n", oversample);

    /* ========================================
       Calculate window parameters
       ======================================== */

    /* Since we rounded up the filter order, we can increase the stop
       band attenuation or bandwidth without making the transition
       bandwidth exceed the design parameters.  This gives a "free"
       boost in quality.  We choose to increase both.

       If we didn't increase these parameters, we would still incur
       the additional computational cost but it would be spent
       increasing the width of the stop band, which is not useful.  */

    /* atten3 is the free stopband attenuation */
    atten3 = (len - 1) * 4.57 * dw + 8;
    t = (-20.0 / log(10.0)) * log(rerror * ulp);
    debug("t: %.3f\n", t);
    if (t < atten3)
        atten3 = t;
    debug("atten3: %.3f\n", atten3);
    if (atten2 > atten3)
        atten3 = atten2;
    else
        atten3 = 0.5 * (atten2 + atten3);
    debug("atten3: %.3f (final)\n", atten3);

    /* f_pass2 is the free pass band */
    dw2 = (atten3 - 8.0) / (4.57 * (len - 1));
    f_pass2 = f_stop - dw2 * (1.0 / (8.0 * atan(1.0)));
    debug("f_pass: %.3f -> %.3f\n", f_pass, f_pass2);

    if (atten3 > 50)
        beta = 0.1102 * (atten3 - 8.7);
    else if (atten3 > 21)
        beta = 0.5842 * pow(atten3 - 21, 0.4) + 0.07866 * (atten3 - 21);
    else
        beta = 0;
    debug("beta: %.3f\n", beta);

    lfr_filter_new_window(fpp, type, len, oversample, f_pass2, beta);
    fp = *fpp;
    fp->f_pass = f_pass;
    fp->f_stop = f_stop;
    fp->atten = atten;
}
