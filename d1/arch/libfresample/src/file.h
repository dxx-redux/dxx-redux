/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#ifndef FILE_H
#define FILE_H
#include <stddef.h>

struct file_data {
    int is_mapped;
    void *data;
    size_t length;
};

void
file_read(struct file_data *fp, const char *path);

void
file_destroy(struct file_data *fp);

#endif
