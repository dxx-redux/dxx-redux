add_library(maths STATIC
    fixc.c
    rand.c
    tables.c
    vecmat.c
    )

include_directories(../include ../arch/include ../main)

find_package(SDL2 REQUIRED)
target_include_directories(maths PUBLIC ${SDL2_INCLUDE_DIRS})
