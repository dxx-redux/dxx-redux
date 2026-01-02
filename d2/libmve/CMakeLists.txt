add_library(libmve STATIC
    decoder8.c
    decoder16.c
    mve_audio.c
    mvelib.c
    mveplay.c
    )

include_directories(../include ../arch/include ../main)

find_package(SDL2 REQUIRED)
target_include_directories(libmve PUBLIC ${SDL2_INCLUDE_DIRS})

if(SDLMIXER)
    #find_package(SDL_mixer)
    #target_include_directories(libmve PUBLIC ${SDL2_MIXER_INCLUDE_DIRS})
    if(SDL2_MIXER_TARGET)
        target_link_libraries(libmve PUBLIC ${SDL2_MIXER_TARGET})
    elseif(SDL2_MIXER_LIBRARIES)
        target_link_libraries(libmve PUBLIC ${SDL2_MIXER_LIBRARIES})
    endif()
endif()

include(CheckStructHasMember)
check_struct_has_member("struct timeval" tv_sec sys/time.h HAVE_STRUCT_TIMEVAL LANGUAGE C)
if(HAVE_STRUCT_TIMEVAL)
    target_compile_definitions(libmve PUBLIC HAVE_STRUCT_TIMEVAL=1)
endif()
check_struct_has_member("struct timespec" tv_sec sys/time.h HAVE_STRUCT_TIMESPEC LANGUAGE C)
if(HAVE_STRUCT_TIMESPEC)
    target_compile_definitions(libmve PUBLIC HAVE_STRUCT_TIMESPEC=1)
endif()
