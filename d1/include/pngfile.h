#ifndef PNGFILE_H
#define PNGFILE_H
#include "pstypes.h" // for consistent struct packing

typedef struct _png_data {
	unsigned int width;
	unsigned int height;
	unsigned int depth;
	unsigned int channels;
	unsigned paletted:1;
	unsigned color:1;
	unsigned alpha:1;

	unsigned char *data;
	unsigned char *palette;
	int num_palette;
} png_data;

extern int read_png(const char *filename, png_data *pdata);
extern int write_png(const char *filename, png_data *pdata);

#endif
