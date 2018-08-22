add_library(editor STATIC
    centers.c
    curves.c
    autosave.c
    eglobal.c
    elight.c
    eobject.c
    eswitch.c
    fixseg.c
    func.c
    group.c
    info.c
    kbuild.c
    kcurve.c
    kfuncs.c
    kgame.c
    khelp.c
    kmine.c
    ksegmove.c
    ksegsel.c
    ksegsize.c
    ktmap.c
    kview.c
    med.c
    meddraw.c
    medmisc.c
    medrobot.c
    medsel.c
    medwall.c
    mine.c
    objpage.c
    segment.c
    seguvs.c
    texpage.c
    texture.c
    )

include_directories(../include ../include/editor ../arch/include ../main)

find_package(sdl)
find_package(physfs)
target_include_directories(editor PUBLIC ${SDL_INCLUDE_DIR} ${PHYSFS_INCLUDE_DIR})
