add_library(libmve STATIC
    decoder8.c
    decoder16.c
    mve_audio.c
    mvelib.c
    mveplay.c
    )

include_directories(../include ../arch/include ../main)

find_package(SDL)
target_include_directories(libmve PUBLIC ${SDL_INCLUDE_DIR})

if(SDLMIXER)
    find_package(SDL_mixer)
    target_include_directories(libmve PUBLIC ${SDL_MIXER_INCLUDE_DIRS})
endif()
