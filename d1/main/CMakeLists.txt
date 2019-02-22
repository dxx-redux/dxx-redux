add_executable (d1x-retro
    ai.c
    aipath.c
    automap.c
    bm.c
    bmread.c
    cntrlcen.c
    collide.c
    config.c
    console.c
    controls.c
    credits.c
    custom.c
    digiobj.c
    dumpmine.c
    effects.c
    endlevel.c
    fireball.c
    fuelcen.c
    fvi.c
    game.c
    gamecntl.c
    gamefont.c
    gamemine.c
    gamerend.c
    gamesave.c
    gameseg.c
    gameseq.c
    gauges.c
    hostage.c
    hud.c
    inferno.c
    kconfig.c
    kmatrix.c
    laser.c
    lighting.c
    menu.c
    mglobal.c
    mission.c
    morph.c
    multi.c
    multibot.c
    newdemo.c
    newmenu.c
    object.c
    paging.c
    physics.c
    piggy.c
    player.c
    playsave.c
    polyobj.c
    powerup.c
    render.c
    robot.c
    scores.c
    slew.c
    snddecom.c
    songs.c
    state.c
    switch.c
    terrain.c
    texmerge.c
    text.c
    titles.c
    vclip.c
    wall.c
    weapon.c
    )

include_directories(${CMAKE_SOURCE_DIR}/include ${CMAKE_SOURCE_DIR}/arch/include)

target_compile_definitions(d1x-retro PUBLIC
    DXX_VERSION_MAJORi=0
    DXX_VERSION_MINORi=58
    DXX_VERSION_MICROi=1
    )

if(WIN32)
    target_link_libraries(d1x-retro PUBLIC glu32 winmm ws2_32)
    target_link_libraries(d1x-retro PRIVATE arch_win32)
    target_sources(d1x-retro PRIVATE
        ${CMAKE_SOURCE_DIR}/arch/win32/d1x-rebirth.ico
        ${CMAKE_SOURCE_DIR}/arch/win32/d1x-rebirth.rc
        )
    set(CMAKE_EXE_LINKER_FLAGS_DEBUG "${CMAKE_EXE_LINKER_FLAGS_DEBUG} /NODEFAULTLIB:msvcrt.lib")
endif()

if(__APPLE__)
    target_link_libraries(d1x-retro PRIVATE arch_carbon arch_cocoa)
endif()

if(__LINUX__)
    target_include_directories(d1x-retro PUBLIC ${CMAKE_SOURCE_DIR}/arch/linux/include)
endif()

if(OPENGL)
    target_link_libraries(d1x-retro PRIVATE arch_ogl)
endif()

if(SDLMIXER)
    find_package(sdl_mixer REQUIRED)
    target_link_libraries(d1x-retro PUBLIC ${SDL_MIXER_LIBRARIES})
endif()

if(EDITOR)
    target_sources(d1x-retro PRIVATE bmread.c)
    target_link_libraries(d1x-retro PRIVATE editor ui)
endif()

if(UDP)
    target_sources(d1x-retro PRIVATE net_udp.c)
endif()

find_package(sdl 1.2 REQUIRED)
find_package(physfs REQUIRED)
target_include_directories(d1x-retro PUBLIC ${SDL_INCLUDE_DIR} ${PHYSFS_INCLUDE_DIR})
target_link_libraries(d1x-retro PUBLIC ${SDL_LIBRARY} ${PHYSFS_LIBRARY})
target_link_libraries(d1x-retro PRIVATE 2d 3d arch_sdl iff maths mem misc texmap)
target_sources(d1x-retro PRIVATE vers_id.c)
install(TARGETS d1x-retro RUNTIME DESTINATION .)
