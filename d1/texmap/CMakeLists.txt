add_library(texmap STATIC
    ntmap.c
    scanline.c
    )

include_directories(../include ../arch/include ../main)

if(NOT OPENGL)
    target_sources(texmap PRIVATE tmapflat.c)
    if(ASM)
        if(NOT CMAKE_ASM_${ASM_DIALECT}_COMPILE_OBJECT)
            set(CMAKE_ASM_${ASM_DIALECT}_COMPILE_OBJECT "<CMAKE_ASM_${ASM_DIALECT}_COMPILER> -o <OBJECT> <SOURCE>")
        endif()
        target_sources(texmap PRIVATE
            tmap_ll.asm
            tmap_flt.asm
            tmapfade.asm
            tmap_lin.asm
            tmap_per.asm
            )
    else()
        target_compile_definitions(texmap PUBLIC NO_ASM)
    endif()
else()
    target_compile_definitions(texmap PUBLIC NO_ASM)
endif()

find_package(sdl)
find_package(physfs)
target_include_directories(texmap PUBLIC ${SDL_INCLUDE_DIR} ${PHYSFS_INCLUDE_DIR})
