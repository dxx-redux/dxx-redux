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

find_package(PhysFS REQUIRED)
find_package(SDL2 REQUIRED)
target_include_directories(2d PUBLIC ${SDL2_INCLUDE_DIRS} ${PHYSFS_INCLUDE_DIR})
