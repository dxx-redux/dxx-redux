add_library(libmve STATIC
    decoder8.c
    decoder16.c
    mve_audio.c
    mvelib.c
    mveplay.c
    )

include_directories(../include ../arch/include ../main)

find_package(sdl)
target_include_directories(libmve PUBLIC ${SDL_INCLUDE_DIR})

if(SDLMIXER)
    find_package(sdl_mixer)
    target_include_directories(libmve PUBLIC ${SDL_MIXER_INCLUDE_DIRS})
endif()
