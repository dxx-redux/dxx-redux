add_library(maths STATIC
    fixc.c
    rand.c
    tables.c
    vecmat.c
    )

include_directories(../include ../arch/include ../main)

find_package(sdl)
target_include_directories(maths PUBLIC ${SDL_INCLUDE_DIR})
