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

if(APPLE)
    # Needed to define the globals in function-less globvars.c
    target_compile_options(3d PRIVATE -fno-common)
endif()

find_package(SDL)
find_package(PhysFS)
target_include_directories(3d PUBLIC ${SDL_INCLUDE_DIR} ${PHYSFS_INCLUDE_DIR})
