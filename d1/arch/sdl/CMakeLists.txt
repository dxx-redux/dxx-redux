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
    #polyphase_resampler.cpp
    )
#STRING (REGEX REPLACE "/RTC(su|[1su])" "" CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG}")
#STRING (REGEX REPLACE "/RTC(su|[1su])" "" CMAKE_C_FLAGS_DEBUG "${CMAKE_C_FLAGS_DEBUG}")
#if(CMAKE_BUILD_TYPE STREQUAL "Debug")
#set_source_files_properties(polyphase_resampler.cpp PROPERTIES COMPILE_FLAGS "/fp:fast /Ox")
#else()
#set_source_files_properties(polyphase_resampler.cpp PROPERTIES COMPILE_FLAGS "/fp:fast /Ox")
#endif()

if(NOT OPENGL)
    target_sources(arch_sdl PRIVATE gr.c)
endif()
if(SDLMIXER)
    target_sources(arch_sdl PRIVATE
        digi_mixer.c
        digi_mixer_music.c
        jukebox.c
        )
    target_include_directories(arch_sdl PUBLIC ${SDL_MIXER_INCLUDE_DIRS})
    target_link_libraries(arch_sdl ${SDL_MIXER_LIBRARY})
endif()

#find_package(SampleRate REQUIRED)
#target_link_libraries(arch_sdl SampleRate::samplerate)
target_link_libraries(arch_sdl libfresample)

include_directories(${CMAKE_SOURCE_DIR}/include ${CMAKE_SOURCE_DIR}/arch/include ${CMAKE_SOURCE_DIR}/main)

target_compile_definitions(arch_sdl PUBLIC
    DXX_VERSION_MAJORi=${PROJECT_VERSION_MAJOR}
    DXX_VERSION_MINORi=${PROJECT_VERSION_MINOR}
    DXX_VERSION_MICROi=$<IF:$<BOOL:${PROJECT_VERSION_MICRO}>,${PROJECT_VERSION_MICRO},0>
    )

find_package(SDL)
find_package(PhysFS)
target_include_directories(arch_sdl PUBLIC ${SDL_INCLUDE_DIR} ${PHYSFS_INCLUDE_DIR})
