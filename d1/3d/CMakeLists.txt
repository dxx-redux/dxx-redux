add_library(3d STATIC
    clipper.c
    draw.c
    globvars.c
    instance.c
    interp.c
    matrix.c
    points.c
    rod.c
    setup.c
    )

if(APPLE)
    # Needed to define the globals in function-less globvars.c
    target_compile_options(3d PRIVATE -fno-common)
endif()
