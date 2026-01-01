add_library(iff STATIC
    iff.c
    )

include_directories(../include ../arch/include ../main)

find_package(SDL2 REQUIRED)
find_package(PhysFS)
target_include_directories(iff PUBLIC ${SDL2_INCLUDE_DIRS} ${PHYSFS_INCLUDE_DIR})
