/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "common.h"
#include <stdio.h>
#include <stdlib.h>

void
error_func(const char *file, int line, const char *msg)
{
    fprintf(stderr, "error: %s (%s:%d)\n", msg, file, line);
    exit(1);
}

static const char MEMORY_ERROR[] = "out of memory";

void *
xmalloc(size_t size)
{
    void *ptr;
    if (!size)
        return NULL;
    ptr = malloc(size);
    if (!ptr)
        error(MEMORY_ERROR);
    return ptr;
}

void *
xcalloc(size_t nmemb, size_t size)
{
    void *ptr;
    if (!nmemb || !size)
        return NULL;
    ptr = calloc(nmemb, size);
    if (!ptr)
        error(MEMORY_ERROR);
    return ptr;
}

void *
xrealloc(void *ptr, size_t size)
{
    void *nptr;
    if (!size) {
        free(ptr);
        return NULL;
    }
    nptr = realloc(ptr, size);
    if (!nptr)
        error(MEMORY_ERROR);
    return nptr;
}
