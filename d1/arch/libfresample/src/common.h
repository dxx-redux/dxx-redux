/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#ifndef COMMON_H
#define COMMON_H
#include <stddef.h>

#define error(msg) error_func(__FILE__, __LINE__, msg)

#define ATTR_NORETURN
#define ATTR_MALLOC
#define ATTR_WARN_UNUSED_RESULT
#define ATTR_ARTIFICIAL
#define INLINE_SPEC

#if defined(__clang__)
# if __has_attribute(noreturn)
#  undef ATTR_NORETURN
#  define ATTR_NORETURN __attribute__((noreturn))
# endif
# if __has_attribute(malloc)
#  undef ATTR_MALLOC
#  define ATTR_MALLOC __attribute((malloc))
# endif
# if __has_attribute(warn_unused_result)
#  undef ATTR_WARN_UNUSED_RESULT
#  define ATTR_WARN_UNUSED_RESULT __attribute__((warn_unused_result))
# endif
# if __has_attribute(artificial)
#  undef ATTR_ARTIFICIAL
#  define ATTR_ARTIFICIAL __attribute__((artificial))
# endif
# undef INLINE_SPEC
# define INLINE_SPEC __inline
#elif defined(__GNUC__)
# if __GNUC__ >= 4
#  undef ATTR_NORETURN
#  define ATTR_NORETURN __attribute__((noreturn))
#  undef ATTR_MALLOC
#  define ATTR_MALLOC __attribute__((malloc))
#  undef ATTR_WARN_UNUSED_RESULT
#  define ATTR_WARN_UNUSED_RESULT __attribute__((warn_unused_result))
# endif
# if (__GNUC__ == 4 && __GNUC_MINOR__ >= 3) || __GNUC__ > 4
#  undef ATTR_ARTIFICIAL
#  define ATTR_ARTIFICIAL __attribute__((artificial))
# endif
# undef INLINE_SPEC
# define INLINE_SPEC __inline
#endif

#define INLINE static INLINE_SPEC ATTR_ARTIFICIAL

void
error_func(const char *file, int line, const char *msg)
	ATTR_NORETURN;

void *
xmalloc(size_t size)
	ATTR_MALLOC;

void *
xcalloc(size_t nmemb, size_t size)
	ATTR_MALLOC;

void *
xrealloc(void *ptr, size_t size)
	ATTR_WARN_UNUSED_RESULT;

#endif
