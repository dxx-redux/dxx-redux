add_library(iff STATIC
    iff.c
    )

include_directories(../include ../arch/include ../main)

find_package(sdl)
find_package(physfs)
target_include_directories(iff PUBLIC ${SDL_INCLUDE_DIR} ${PHYSFS_INCLUDE_DIR})
