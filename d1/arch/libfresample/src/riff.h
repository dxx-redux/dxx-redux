/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#ifndef RIFF_H
#define RIFF_H
#include <stddef.h>

struct riff_tag {
    char tag[4];
    unsigned length;
    const void *data;
};

struct riff {
    char tag[4];
    unsigned atags, ntags;
    struct riff_tag *tags;
};

void
riff_init(struct riff *riff);

void
riff_destroy(struct riff *riff);

void
riff_parse(struct riff *riff, const void *data, size_t length);

struct riff_tag *
riff_get(struct riff *riff, const char tag[4]);

#endif
