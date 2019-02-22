add_library(misc STATIC
    args.c
    error.c
    hash.c
    hmp.c
    ignorecase.c
    physfsx.c
    strio.c
    strutil.c
    )

include_directories(../include ../arch/include ../main)

find_package(sdl)
find_package(physfs)
target_include_directories(misc PUBLIC ${SDL_INCLUDE_DIR} ${PHYSFS_INCLUDE_DIR})
