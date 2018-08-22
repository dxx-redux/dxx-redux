add_library(arch_ogl STATIC
    gr.c
    ogl.c
    )

include_directories(../../include ../include ../../main)

find_package(sdl)
find_package(physfs)
target_include_directories(arch_ogl PUBLIC ${SDL_INCLUDE_DIR} ${PHYSFS_INCLUDE_DIR})
