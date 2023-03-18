/* fix[HA] points to maths[HA] */
#ifndef _XFIX_H
#define _XFIX_H

#include "xmaths.h"

// ------------------------------------------------------------------------

//extract a fix from a quad product
static __attribute__((always_inline)) inline fix FixQuadAdjust (tQuadInt *q)
{
#ifdef MATH64
return q->q >> 16;
#else
return (q->high << 16) + (q->low >> 16);
#endif
}

// ------------------------------------------------------------------------

static inline void FixQuadNegate (tQuadInt *q)
{
#ifdef MATH64
q->q = -q->q;
#else
q->low  = (uint32_t) -((int32_t) q->low);
q->high = -q->high - (q->low != 0);
#endif
}

// ------------------------------------------------------------------------

#endif //_FIX_H
