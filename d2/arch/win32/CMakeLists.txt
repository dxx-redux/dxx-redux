add_library(arch_win32 STATIC
    messagebox.c
    )

include_directories(${CMAKE_SOURCE_DIR}/include ${CMAKE_SOURCE_DIR}/arch/include ${CMAKE_SOURCE_DIR}/main)

target_include_directories(arch_win32 PUBLIC ${SDL_INCLUDE_DIR})
