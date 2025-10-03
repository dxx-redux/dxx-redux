/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "defs.h"
#include <stdlib.h>

void
lfr_filter_free(struct lfr_filter *fp)
{
    free(fp);
}
