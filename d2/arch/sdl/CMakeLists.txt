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
endif()

target_compile_definitions(arch_sdl PUBLIC
    DXX_VERSION_MAJORi=${PROJECT_VERSION_MAJOR}
    DXX_VERSION_MINORi=${PROJECT_VERSION_MINOR}
    DXX_VERSION_MICROi=$<IF:$<BOOL:${PROJECT_VERSION_MICRO}>,${PROJECT_VERSION_MICRO},0>
    )
