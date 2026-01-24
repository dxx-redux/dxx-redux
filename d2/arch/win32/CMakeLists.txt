add_library(arch_win32 STATIC
    crashdump.c
    messagebox.c
    )
if(SDL2)
target_sources(arch_win32 PRIVATE rbaudio.c)
endif()
