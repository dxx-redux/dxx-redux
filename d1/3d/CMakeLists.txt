add_library(3d STATIC
    clipper.c
    draw.c
    globvars.c
    instance.c
    interp.c
    matrix.c
    points.c
    rod.c
    setup.c
    )

include_directories(../include ../arch/include ../main)

find_package(SDL)
find_package(PhysFS)
target_include_directories(3d PUBLIC ${SDL_INCLUDE_DIR} ${PHYSFS_INCLUDE_DIR})
