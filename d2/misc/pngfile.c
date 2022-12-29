#include <stdio.h>
#include <string.h>
#include <png.h>
#include <SDL/SDL.h>
#include <physfs.h>

#include "pngfile.h"
#include "u_mem.h"
#include "pstypes.h"

static void PNGCBAPI png_read_data(png_structp png_ptr, png_bytep data, size_t length)
{
	SDL_RWops *rw = (SDL_RWops *)png_get_io_ptr(png_ptr);
	if (SDL_RWread(rw, data, 1, length) != length)
		png_error(png_ptr, "Read Error");
}

int read_png(const char *filename, png_data *pdata)
{
	//ubyte header[8];
	png_structp png_ptr = NULL;
	png_infop info_ptr = NULL;
	png_bytepp row_pointers = NULL;
	png_uint_32 width, height;
	int depth, color_type;
	int i;
	PHYSFS_file *fp = NULL;
	char *fbuf = NULL;
	ssize_t fsize;
	SDL_RWops *rw;

	if (!filename || !pdata)
		return 0;

	if ((fp = PHYSFS_openRead(filename)) == NULL)
		return 0;

	if ((fsize = PHYSFS_fileLength(fp)) == -1 ||
		!(fbuf = malloc(fsize))) {
		PHYSFS_close(fp);
		return 0;
	}

	if (PHYSFS_readBytes(fp, fbuf, fsize) != fsize ||
		//!png_check_sig(fbuf, 8) ||
		!(rw = SDL_RWFromConstMem(fbuf, fsize))) {
		free(fbuf);
		PHYSFS_close(fp);
		return 0;
	}

	PHYSFS_close(fp);

	//SDL_RWseek(rw, 8, RW_SEEK_SET);

	png_ptr = png_create_read_struct(PNG_LIBPNG_VER_STRING, NULL, NULL, NULL);
	info_ptr = png_create_info_struct(png_ptr);

	pdata->data = pdata->palette = NULL;
	pdata->num_palette = 0;
	if (setjmp(png_jmpbuf(png_ptr)))
	{
		png_destroy_read_struct(&png_ptr, &info_ptr, NULL);
		if (pdata->data)
			free(pdata->data);
		if (pdata->palette)
			free(pdata->palette);
		if (row_pointers)
			free(row_pointers);
		SDL_RWclose(rw);
		free(fbuf);
		return 0;
	}

	//png_init_io(png_ptr, fp);
	png_set_read_fn(png_ptr, (png_voidp)rw, png_read_data);
	//png_set_sig_bytes(png_ptr, 8);
	png_read_info(png_ptr, info_ptr);
	png_get_IHDR(png_ptr, info_ptr, &width, &height, &depth, &color_type, NULL, NULL, NULL);

	pdata->width = width;
	pdata->height = height;
	pdata->depth = depth;

	pdata->data = (ubyte*)malloc(png_get_rowbytes(png_ptr, info_ptr) * height);
	row_pointers = (png_bytep *)malloc(sizeof(png_bytep) * height);
	for (i = 0; i < height; i++)
		row_pointers[i] = &pdata->data[png_get_rowbytes(png_ptr, info_ptr) * i];

	png_read_image(png_ptr, row_pointers);
	free(row_pointers);
	row_pointers=NULL;
	png_read_end(png_ptr, info_ptr);

	if (color_type == PNG_COLOR_TYPE_PALETTE)
	{
		png_colorp palette;

		if (png_get_PLTE(png_ptr, info_ptr, &palette, &pdata->num_palette))
		{
			pdata->palette = (ubyte*)malloc(pdata->num_palette * 3);
			memcpy(pdata->palette, palette, pdata->num_palette * 3);
		}
	}

	pdata->paletted = (color_type & PNG_COLOR_MASK_PALETTE) > 0;
	pdata->color = (color_type & PNG_COLOR_MASK_COLOR) > 0;
	pdata->alpha = (color_type & PNG_COLOR_MASK_ALPHA) > 0;
	if (pdata->color && pdata->alpha)
		pdata->channels = 4;
	else if (pdata->color && !pdata->alpha)
		pdata->channels = 3;
	else if (!pdata->color && pdata->alpha)
		pdata->channels = 2;
	else //if (!pdata->color && !pdata->alpha)
		pdata->channels = 1;

	png_destroy_read_struct(&png_ptr, &info_ptr, NULL);

	SDL_RWclose(rw);
	free(fbuf);

	return 1;
}

static void PNGCBAPI png_write_data(png_structp png_ptr, png_bytep data, size_t length)
{
	PHYSFS_file *fp = (PHYSFS_File *)png_get_io_ptr(png_ptr);
	if (PHYSFS_writeBytes(fp, data, length) != length)
		png_error(png_ptr, "Write Error");
}

static void PNGCBAPI png_flush_data(png_structp png_ptr)
{
	PHYSFS_file *fp = (PHYSFS_File *)png_get_io_ptr(png_ptr);
	if (!PHYSFS_flush(fp))
		png_error(png_ptr, "Flush Error");
}

int write_png(const char *file_name, png_data *pdata)
{
	PHYSFS_file *fp;
	png_structp png_ptr;
	png_infop info_ptr;
	png_colorp palette = NULL;
	png_bytepp row_pointers = NULL;
	int i, height = pdata->height;

	fp = PHYSFS_openWrite(file_name);
	if (fp == NULL)
		return 0;

	png_ptr = png_create_write_struct(PNG_LIBPNG_VER_STRING, NULL, NULL, NULL);
	info_ptr = png_ptr ? png_create_info_struct(png_ptr) : NULL;

	if (info_ptr == NULL)
	{
		PHYSFS_close(fp);
		if (png_ptr)
			png_destroy_write_struct(&png_ptr, NULL);
		return 0;
	}

	if (setjmp(png_jmpbuf(png_ptr)))
	{
		if (row_pointers)
			free(row_pointers);
		if (palette)
			png_free(png_ptr, palette);
		PHYSFS_close(fp);
		png_destroy_write_struct(&png_ptr, &info_ptr);
		return 0;
	}

	png_set_write_fn(png_ptr, fp, png_write_data, png_flush_data);

	png_set_IHDR(png_ptr, info_ptr, pdata->width, pdata->height, pdata->depth, PNG_COLOR_TYPE_RGB,
		PNG_INTERLACE_NONE, PNG_COMPRESSION_TYPE_BASE, PNG_FILTER_TYPE_BASE);

	if (pdata->paletted) {
		palette = (png_colorp)png_malloc(png_ptr, 256 * sizeof (png_color));
		png_set_PLTE(png_ptr, info_ptr, palette, 256);
	}

	png_write_info(png_ptr, info_ptr);

	row_pointers = (png_bytep *)malloc(sizeof(png_bytep) * height);
	for (i = 0; i < height; i++)
		row_pointers[i] = &pdata->data[png_get_rowbytes(png_ptr, info_ptr) * i];

	png_write_image(png_ptr, row_pointers);

	free(row_pointers);

	png_write_end(png_ptr, info_ptr);

	if (palette)
		png_free(png_ptr, palette);

	png_destroy_write_struct(&png_ptr, &info_ptr);

	PHYSFS_close(fp);

	return 1;
}
