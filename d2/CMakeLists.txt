# CMakeList.txt : CMake project for d2x-redux, include source and define
# project specific logic here.
#
cmake_minimum_required (VERSION 3.21)
project(
    d2x-redux
    VERSION 1.1
    LANGUAGES C CXX
    )

option(OPENGL "Build with OpenGL support [default: ON]" ON)
option(SDLMIXER "Build with SDL_Mixer support for sound and music (includes external music support) [default: ON]" ON)
option(ASM "Build with ASSEMBLER code (only with OpenGL OFF, requires NASM and x86) [default: OFF]" OFF)
option(EDITOR "Include editor into build (experimental) [default: OFF]" OFF)
option(IPV6 "Enable IPv6 compability [default: OFF]" OFF)
option(UDP "Enable UDP support [default: ON]" ON)
option(TRACKER "Enable Tracker support (requires UDP) [default: ON]" ON)
option(PNG "Build with PNG support for screenshots and textures [default: ON]" ON)
option(OPENGLMERGE "Use an OpenGL shader for texmerge [default: ON]" ON)

set(CMAKE_C_FLAGS_MINSIZEREL "${CMAKE_C_FLAGS_MINSIZEREL} -DRELEASE")
set(CMAKE_C_FLAGS_RELEASE "${CMAKE_C_FLAGS_RELEASE} -DRELEASE")
set(CMAKE_C_FLAGS_RELWITHDEBINFO "${CMAKE_C_FLAGS_RELWITHDEBINFO} -DRELEASE")

if(NOT CMAKE_CXX_COMPILER_ID STREQUAL "MSVC")
    set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -Wno-address-of-packed-member -Wno-deprecated-declarations")
endif()

# Global compile flags (have to be processed before subdirectories by some
#  CMake implementations - even though the docs say they don't)
add_definitions(/DNETWORK)
if(OPENGL)
    add_definitions(/DOGL)
elseif(ASM)
    if(WIN32)
        enable_language(ASM_MASM)
        set(ASM_DIALECT "MASM")
    elseif(__LINUX__)
        enable_language(ASM_NASM)
        set(ASM_DIALECT "NASM")
    endif()
endif()
if(SDLMIXER)
    add_definitions(/DUSE_SDLMIXER)
    if (APPLE)
        include(FetchContent)
        FetchContent_Declare(
            SDL_mixer-1.2-cmake
            GIT_REPOSITORY https://github.com/arbruijn/SDL_mixer-1.2-cmake
            GIT_TAG master
        )
        FetchContent_MakeAvailable(SDL_mixer-1.2-cmake)
        set(SDL_MIXER_LIBRARY SDL_mixer-1.2-cmake::SDL_mixer-1.2-cmake)
    else()
        find_package(SDL_mixer REQUIRED)
    endif()
endif()
if(EDITOR)
    add_definitions(/DEDITOR)
endif()
if(IPV6)
    add_definitions(/DIPv6)
endif()
if(UDP)
    add_definitions(/DUSE_UDP)
    if(TRACKER)
        add_definitions(/DUSE_TRACKER)
    endif()
endif()
if(PNG)
    add_definitions(/DHAVE_LIBPNG)
endif()
if(OPENGLMERGE)
    add_definitions(/DOGL_MERGE)
endif()

if(WIN32)
    # WINDOWS_IGNORE_PACKING_MISMATCH is required to suppress asserts in winnt.h that warn about
    # memory corruption when Windows headers are compiled with non-default packing (set in
    # include/pstypes.h). This may actually be a problem that warrants fixing; keep an eye on that.
    add_definitions(/DWINDOWS /DHAVE_STRUCT_TIMEVAL /DWINDOWS_IGNORE_PACKING_MISMATCH)
    if(CMAKE_CXX_COMPILER_ID STREQUAL "Clang")
        add_compile_options(-gcodeview -fms-extensions -fseh-exceptions)
        add_link_options(-fuse-ld=lld -Wl,--pdb=)
    endif()
elseif(APPLE)
    add_definitions(/DHAVE_STRUCT_TIMESPEC /DHAVE_STRUCT_TIMEVAL /D__unix__)
elseif(__LINUX__)
    add_definitions(/D__LINUX__ /DHAVE_STRUCT_TIMESPEC /DHAVE_STRUCT_TIMEVAL)
endif()

if(NOT WIN32)
    add_compile_definitions(SHAREPATH="/usr/share/games/d2x-redux")
endif()

add_subdirectory(2d)
add_subdirectory(3d)
add_subdirectory(arch/sdl)
add_subdirectory(iff)
add_subdirectory(libmve)
add_subdirectory(main)
add_subdirectory(maths)
add_subdirectory(mem)
add_subdirectory(misc)
add_subdirectory(texmap)

add_dependencies(d2x-redux 2d 3d arch_sdl iff libmve maths mem misc texmap)

if(WIN32)
    add_subdirectory(arch/win32)
    add_dependencies(d2x-redux arch_win32)
endif()

if(APPLE)
    add_subdirectory(arch/cocoa)
    add_dependencies(d2x-redux arch_cocoa)
endif()

if(CMAKE_SYSTEM_NAME STREQUAL "Linux")
    add_subdirectory(arch/x11)
    add_dependencies(d2x-redux arch_x11)
endif()

if(OPENGL)
    add_subdirectory(arch/ogl)
    add_subdirectory(xmodel)
    add_dependencies(d2x-redux arch_ogl xmodel)
endif()

if(EDITOR)
    add_subdirectory(editor)
    add_subdirectory(ui)
    add_dependencies(d2x-redux editor ui)
endif()
