/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "param.h"
#include <stdlib.h>
#include <string.h>
#include <math.h>

struct lfr_param *
lfr_param_new(void)
{
    struct lfr_param *param;
    param = malloc(sizeof(*param));
    if (!param)
        return NULL;
    param->set = 0;
    param->current = 0;
    return param;
}

void
lfr_param_free(struct lfr_param *param)
{
    free(param);
}

struct lfr_param *
lfr_param_copy(struct lfr_param *param)
{
    struct lfr_param *nparam;
    nparam = malloc(sizeof(*nparam));
    if (!nparam)
        return NULL;
    memcpy(nparam, param, sizeof(*nparam));
    return nparam;
}

void
lfr_param_seti(struct lfr_param *param, lfr_param_t pname, int value)
{
    int n = pname;
    if (n < 0 || n >= LFR_PARAM_COUNT)
        return;
    param->set |= 1u << n;
    param->current = 0;
    param->param[n] = (double) value;
}

void
lfr_param_setf(struct lfr_param *param, lfr_param_t pname, double value)
{
    int n = pname;
    if (n < 0 || n >= LFR_PARAM_COUNT)
        return;
    param->set |= 1u << n;
    param->current = 0;
    param->param[n] = value;
}

void
lfr_param_geti(struct lfr_param *param, lfr_param_t pname, int *value)
{
    int n = pname;
    if (n < 0 || n >= LFR_PARAM_COUNT)
        return;
    if (!param->current)
        lfr_param_calculate(param);
    *value = (int) floor(param->param[n] + 0.5);
}

void
lfr_param_getf(struct lfr_param *param, lfr_param_t pname, double *value)
{
    int n = pname;
    if (n < 0 || n >= LFR_PARAM_COUNT)
        return;
    if (!param->current)
        lfr_param_calculate(param);
    *value = param->param[n];
}

#define MAX_QUALITY 10

struct lfr_quality {
    unsigned short atten;      /* dB */
    unsigned short transition; /* out of 1000 */
    unsigned short kind;       /* 0 = loose, 2 = maxfreq=0 */
    unsigned short minfpass;   /* out of 1000 */
};

static const struct lfr_quality LFR_QUALITY[MAX_QUALITY + 1] = {
    {  35, 350, 0, 500 },
    {  35, 350, 0, 500 },
    {  35, 350, 0, 500 }, /* low */
    {  35, 350, 0, 500 },
    {  50, 290, 0, 600 },
    {  60, 230, 1, 700 }, /* medium */
    {  80, 180, 1, 800 },
    {  90, 140, 1, 850 },
    {  96, 100, 1, 900 }, /* high */
    { 108,  60, 2, 915 },
    { 120,  30, 2, 930 }  /* ultra */
};

#define MIN_OUTPUT (1.0 / 128)
#define MIN_ATTEN (13)
#define MAX_ATTEN (144)
#define MIN_TRANSITION (1.0 / 256.0)
#define MAX_MINPASS (31.0 / 32.0)
#define MIN_MINPASS (8.0 / 32.0)

#define ISSET(n) ((set & (1u << (LFR_PARAM_ ## n))) != 0)
#define GETF(n) (param->param[LFR_PARAM_ ## n])
#define GETI(n) ((int) floor(GETF(n) + 0.5))

void
lfr_param_calculate(struct lfr_param *param)
{
    unsigned set = param->set;
    int qual;
    double rate;
    double f_output, f_transition, f_maxfreq, f_minpass;
    double atten, f_stop, f_pass;
    double t;
    int loose;

    /* Quality */
    if (ISSET(QUALITY)) {
        qual = GETI(QUALITY);
        if (qual < 0)
            qual = 0;
        else if (qual > MAX_QUALITY)
            qual = MAX_QUALITY;
    } else {
        qual = 8;
    }
    GETF(QUALITY) = qual;

    /* Input rate */
    if (ISSET(INRATE)) {
        rate = GETF(INRATE);
        if (rate <= 0)
            rate = -1.0;
    } else {
        rate = -1.0;
    }
    GETF(INRATE) = rate;

    /* Output rate */
    if (ISSET(OUTRATE)) {
        t = GETF(OUTRATE);
        if (rate > 0) {
            if (t > rate) {
                f_output = 1.0;
                GETF(OUTRATE) = rate;
            } else {
                f_output = t / rate;
                if (f_output < MIN_OUTPUT) {
                    f_output = MIN_OUTPUT;
                    GETF(OUTRATE) = MIN_OUTPUT * rate;
                }
            }
        } else {
            if (t >= 1.0) {
                f_output = 1.0;
                GETF(OUTRATE) = 1.0;
            } else {
                f_output = t;
                if (f_output < MIN_OUTPUT) {
                    f_output = MIN_OUTPUT;
                    GETF(OUTRATE) = MIN_OUTPUT;
                }
            }
        }
    } else {
        f_output = 1.0;
        if (rate > 0)
            GETF(OUTRATE) = 1.0;
        else
            GETF(OUTRATE) = rate;
    }

    /* Transition bandwidth */
    if (ISSET(FTRANSITION)) {
        f_transition = GETF(FTRANSITION);
        if (f_transition < MIN_TRANSITION)
            f_transition = 1.0/32;
        if (f_transition > 1.0)
            f_transition = 1.0;
    } else {
        f_transition = (0.5/1000) * (double) LFR_QUALITY[qual].transition;
    }
    GETF(FTRANSITION) = f_transition;

    /* Maximum audible frequency */
    if (ISSET(MAXFREQ)) {
        f_maxfreq = GETF(MAXFREQ);
    } else {
        if (LFR_QUALITY[qual].kind < 2)
            f_maxfreq = 20000.0;
        else
            f_maxfreq = -1;
        GETF(MAXFREQ) = f_maxfreq;
    }
    if (rate > 0)
        f_maxfreq = f_maxfreq / rate;
    else
        f_maxfreq = 1.0;

    /* "Loose" flag */
    if (ISSET(LOOSE)) {
        loose = GETI(LOOSE) > 0;
    } else {
        loose = LFR_QUALITY[qual].kind < 1;
    }
    GETF(LOOSE) = (double) loose;

    /* Minimum output bandwidth */
    if (ISSET(MINFPASS)) {
        f_minpass = GETF(MINFPASS);
        if (f_minpass < MIN_MINPASS)
            f_minpass = MIN_MINPASS;
        else if (f_minpass > MAX_MINPASS)
            f_minpass = MAX_MINPASS;
    } else {
        f_minpass = 0.001 * LFR_QUALITY[qual].minfpass;
    }
    GETF(MINFPASS) = f_minpass;

    /* Stop band attenuation */
    if (ISSET(ATTEN)) {
        atten = GETF(ATTEN);
        if (atten < MIN_ATTEN)
            atten = MIN_ATTEN;
        else if (atten > MAX_ATTEN)
            atten = MAX_ATTEN;
    } else {
        atten = (double) LFR_QUALITY[qual].atten;
    }
    GETF(ATTEN) = atten;

    /* Stop band frequency */
    if (ISSET(FSTOP)) {
        f_stop = GETF(FSTOP);
        if (f_stop > 1.0)
            f_stop = 1.0;
        else if (f_stop < MIN_TRANSITION)
            f_stop = MIN_TRANSITION;
    } else {
        f_stop = 0.5 * f_output;
        if (loose && f_maxfreq > 0) {
            t = f_output - f_maxfreq;
            if (t > f_stop)
                f_stop = t;
        }
    }
    GETF(FSTOP) = f_stop;

    /* Pass band frequency */
    if (ISSET(ATTEN)) {
        f_pass = GETF(FPASS);
    } else {
        f_pass = f_stop - f_transition;
        t = 0.5 * f_output * f_minpass;
        if (t > f_pass)
            f_pass = t;
        if (f_maxfreq > 0 && f_pass > f_maxfreq)
            f_pass = f_maxfreq;
    }
    t = f_stop - MIN_TRANSITION;
    if (t < 0)
        t = 0;
    if (t < f_pass)
        f_pass = t;
    GETF(FPASS) = f_pass;
}
