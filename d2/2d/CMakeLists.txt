add_library(2d STATIC
	2dsline.c
	bitblt.c
	bitmap.c
	box.c
	canvas.c
	circle.c
	disc.c
	font.c
	gpixel.c
	line.c
	palette.c
	pcx.c
	pixel.c
#	poly.c
	rect.c
	rle.c
	scalec.c
	)

include_directories(../include ../arch/include ../main)

find_package(physfs REQUIRED)
find_package(sdl REQUIRED)
target_include_directories(2d PUBLIC ${SDL_INCLUDE_DIR} ${PHYSFS_INCLUDE_DIR})
