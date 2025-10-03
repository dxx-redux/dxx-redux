/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
/*
  Functions for serializing / deserializing binary data.  Each function
  is named using the following parts:

  * read/write
  * b/l: big/little endian
  * s/u: signed/unsigned integer
*/
#ifndef BINARY_H
#define BINARY_H
#include "common.h"
#include <stdint.h>

/* Read an unsigned 8 bit integer. */
INLINE uint8_t
read_u8(void const *ptr)
{
 return *(uint8_t const *) ptr;
}

/* Read an signed 8 bit integer.  */
INLINE int8_t
read_s8(void const *ptr)
{
    return *(int8_t const *) ptr;
}

/* Read a big endian unsigned 16 bit integer.  */
INLINE uint16_t
read_bu16(void const *ptr)
{
    uint8_t const *p = ptr;
    uint16_t r = ((uint16_t) p[0] << 8) | (uint16_t) p[1];
    return r;
}

/* Read a little endian unsigned 16 bit integer.  */
INLINE uint16_t
read_lu16(void const *ptr)
{
    uint8_t const *p = ptr;
    uint16_t r = ((uint16_t) p[1] << 8) | (uint16_t) p[0];
    return r;
}

/* Read a big endian signed 16 bit integer.  */
INLINE int16_t
read_bs16(void const *ptr)
{
    return (int16_t) read_bu16(ptr);
}

/* Read a little endian signed 16 bit integer.  */
INLINE int16_t
read_ls16(void const *ptr)
{
    return (int16_t) read_lu16(ptr);
}

/* Read big endian unsigned 32 bit integer.  */
INLINE uint32_t
read_bu32(void const *ptr)
{
    uint8_t const *p = ptr;
    uint32_t r = ((uint32_t) p[0] << 24)
        | ((uint32_t) p[1] << 16)
        | ((uint32_t) p[2] << 8)
        | (uint32_t) p[3];
    return r;
}

/* Read a little endian unsigned 32 bit integer.  */
INLINE uint32_t
read_lu32(void const *ptr)
{
    uint8_t const *p = ptr;
    uint32_t r = ((uint32_t) p[3] << 24)
        | ((uint32_t) p[2] << 16)
        | ((uint32_t) p[1] << 8)
        | (uint32_t) p[0];
    return r;
}

/* Read a big endian signed 32 bit integer.  */
INLINE int32_t
read_bs32(void const *ptr)
{
    return (int32_t) read_bu32(ptr);
}

/* Read a little endian signed 32 bit integer.  */
INLINE int32_t
read_ls32(void const *ptr)
{
    return (int32_t) read_lu32(ptr);
}

/* Read big endian unsigned 64 bit integer.  */
INLINE uint64_t
read_bu64(void const *ptr)
{
    uint8_t const *p = ptr;
    return ((uint64_t) read_bu32(p) << 32) | read_bu32(p + 4) ;
}

/* Read big endian unsigned 64 bit integer.  */
INLINE uint64_t
read_lu64(void const *ptr)
{
    uint8_t const *p = ptr;
    return ((uint64_t) read_lu32(p + 4) << 32) | read_lu32(p);
}

/* Read a big endian signed 64 bit integer.  */
INLINE int64_t
read_bs64(void const *ptr)
{
    return (int64_t) read_bu64(ptr);
}

/* Read a little endian signed 64 bit integer.  */
INLINE int64_t
read_ls64(void const *ptr)
{
    return (int64_t) read_lu64(ptr);
}

/* Write an unsigned 8 bit integer.  */
INLINE void
write_u8(void *ptr, uint8_t v)
{
    *(uint8_t *) ptr = v;
}

/* Write an unsigned 8 bit integer.  */
INLINE void
write_s8(void *ptr, int8_t v)
{
    *(int8_t *) ptr = v;
}

/* Write a big endian unsigned 16 bit integer.  */
INLINE void
write_bu16(void *ptr, uint16_t v)
{
    uint8_t *p = ptr;
    p[0] = v >> 8;
    p[1] = v;
}

/* Write a little endian unsigned 16 bit integer.  */
INLINE void
write_lu16(void *ptr, uint16_t v)
{
    uint8_t *p = ptr;
    p[0] = v;
    p[1] = v >> 8;
}

/* Write a big endian signed 16 bit integer.  */
INLINE void
write_bs16(void *ptr, int16_t v)
{
    write_bu16(ptr, v);
}

/* Write a little endian signed 16 bit integer.  */
INLINE void
write_ls16(void *ptr, int16_t v)
{
    write_lu16(ptr, v);
}

/* Write a big endian unsigned 32 bit integer.  */
INLINE void
write_bu32(void *ptr, uint32_t v)
{
    uint8_t *p = ptr;
    p[0] = v >> 24;
    p[1] = v >> 16;
    p[2] = v >> 8;
    p[3] = v;
}

/* Write a little endian unsigned 32 bit integer.  */
INLINE void
write_lu32(void *ptr, uint32_t v)
{
    uint8_t *p = ptr;
    p[0] = v;
    p[1] = v >> 8;
    p[2] = v >> 16;
    p[3] = v >> 24;
}

/* Write a big endian signed 32 bit integer.  */
INLINE void
write_bs32(void *ptr, int32_t v)
{
    write_bu32(ptr, v);
}

/* Write a little endian signed 32 bit integer.  */
INLINE void
write_ls32(void *ptr, int32_t v)
{
    write_lu32(ptr, v);
}

/* Write a big endian unsigned 64 bit integer.  */
INLINE void
write_bu64(void *ptr, uint64_t v)
{
    uint8_t *p = ptr;
    write_bu32(p, v >> 32);
    write_bu32(p + 4, v);
}

/* Write a little endian unsigned 64 bit integer.  */
INLINE void
write_lu64(void *ptr, uint64_t v)
{
    uint8_t *p = ptr;
    write_lu32(p, v);
    write_lu32(p + 4, v >> 32);
}

/* Write a big endian signed 64 bit integer.  */
INLINE void
write_bs64(void *ptr, int64_t v)
{
    write_bu64(ptr, v);
}

/* Write a little endian signed 64 bit integer.  */
INLINE void
write_ls64(void *ptr, int64_t v)
{
    write_lu64(ptr, v);
}
#endif
