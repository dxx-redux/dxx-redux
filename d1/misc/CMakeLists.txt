add_library(misc STATIC
    args.c
    error.c
    hash.c
    hmp.c
    ignorecase.c
    physfsx.c
    strio.c
    strutil.c
    )

if(PNG)
    target_sources(misc PRIVATE pngfile.c)
endif()
