/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "common.h"
#include "file.h"

#include <sys/mman.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <errno.h>
#include <fcntl.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

#define MIN_MMAP (8*1024)
#define MIN_ALLOC (256)

static void
file_read_mmap(struct file_data *fp, int fd, size_t sz)
{
    void *p;
    p = mmap(NULL, sz, PROT_READ, MAP_SHARED, fd, 0);
    if (p == MAP_FAILED)
        error("mmap failed");
    fp->is_mapped = 1;
    fp->data = p;
    fp->length = sz;
}

static void
file_read_std(struct file_data *fp, int fd, size_t sz)
{
    unsigned char *data;
    size_t pos, alloc, nalloc;
    ssize_t amt;

    alloc = sz + 1;
    if (alloc < MIN_ALLOC)
        alloc = MIN_ALLOC;
    data = xmalloc(alloc);
    pos = 0;

    while (1) {
        if (pos >= alloc) {
            nalloc = alloc * 2;
            if (nalloc < alloc)
                error("file too large");
            data = xrealloc(data, nalloc);
            alloc = nalloc;
        }
        amt = read(fd, data + pos, alloc - pos);
        if (amt > 0) {
            pos += amt;
        } else if (amt == 0) {
            break;
        } else {
            error("read failed");
        }
    }

    fp->is_mapped = 0;
    fp->data = data;
    fp->length = pos;
}

static void
file_read_fdes(struct file_data *fp, int fd)
{
    struct stat st;
    int r;
    r = fstat(fd, &st);
    if (r)
        error("stat failed");
    if (!S_ISREG(st.st_mode)) {
        file_read_std(fp, fd, 0);
        return;
    }
    if ((uint64_t) st.st_size > (size_t) -1)
        error("file too large");
    if (st.st_size >= MIN_MMAP) {
        file_read_mmap(fp, fd, (size_t) st.st_size);
    } else {
        file_read_std(fp, fd, (size_t) st.st_size);
    }
}

void
file_read(struct file_data *fp, const char *path)
{
    int fd;
    if (!strcmp(path, "-")) {
        file_read_fdes(fp, STDIN_FILENO);
    } else {
        fd = open(path, O_RDONLY);
        if (fd < 0)
            error("open failed");
        file_read_fdes(fp, fd);
        close(fd);
    }
}

void
file_destroy(struct file_data *fp)
{
    if (!fp->data)
        return;
    if (fp->is_mapped)
        munmap(fp->data, fp->length);
    else
        free(fp->data);
    fp->data = NULL;
}
