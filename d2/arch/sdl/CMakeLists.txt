add_library(arch_sdl STATIC
    event.c
    init.c
    joy.c
    key.c
    mouse.c
    rbaudio.c
    timer.c
    window.c
    digi.c
    digi_audio.c
    )
if(NOT OPENGL)
    target_sources(arch_sdl PRIVATE gr.c)
endif()
if(SDLMIXER)
    target_sources(arch_sdl PRIVATE
        digi_mixer.c
        digi_mixer_music.c
        jukebox.c
        )
    find_package(sdl_mixer)
    target_include_directories(arch_sdl PUBLIC ${SDL_MIXER_INCLUDE_DIRS})
endif()

include_directories(${CMAKE_SOURCE_DIR}/include ${CMAKE_SOURCE_DIR}/arch/include ${CMAKE_SOURCE_DIR}/main)

find_package(sdl)
find_package(physfs)
target_include_directories(arch_sdl PUBLIC ${SDL_INCLUDE_DIR} ${PHYSFS_INCLUDE_DIR})
