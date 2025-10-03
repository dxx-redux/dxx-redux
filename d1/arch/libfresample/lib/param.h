/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#ifndef PARAM_H
#define PARAM_H
#include "defs.h"

struct lfr_param {
    /*
      Bit set of which parameters were set by the user.
    */
    unsigned set;

    /*
      Set to 1 if the derived parameters are up to date.
    */
    int current;

    /*
      Parameter values.
    */
    double param[LFR_PARAM_COUNT];
};

LFR_PRIVATE void
lfr_param_calculate(struct lfr_param *param);

#endif
