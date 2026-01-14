add_library(arch_ogl STATIC
    gr.c
    ogl.c
    )

if(OPENGLMERGE)
   target_sources(arch_ogl PRIVATE oglprog.c)
endif()

include_directories(../../include ../include ../../main)

target_compile_definitions(arch_ogl PUBLIC
    DXX_VERSION_MAJORi=${PROJECT_VERSION_MAJOR}
    DXX_VERSION_MINORi=${PROJECT_VERSION_MINOR}
    DXX_VERSION_MICROi=$<IF:$<BOOL:${PROJECT_VERSION_MICRO}>,${PROJECT_VERSION_MICRO},0>
    )

find_package(SDL)
find_package(PhysFS)
target_include_directories(arch_ogl PUBLIC ${SDL_INCLUDE_DIR} ${PHYSFS_INCLUDE_DIR})
