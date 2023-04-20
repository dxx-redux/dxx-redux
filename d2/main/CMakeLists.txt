add_executable (d2x-retro
    ai.c
    ai2.c
    aipath.c
    automap.c
    bm.c
    cntrlcen.c
    collide.c
    config.c
    console.c
    controls.c
    credits.c
    digiobj.c
    dumpmine.c
    effects.c
    endlevel.c
    escort.c
    fireball.c
    fuelcen.c
    fvi.c
    game.c
    gamecntl.c
    gamefont.c
    gamemine.c
    gamepal.c
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
    movie.c
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
    segment.c
    slew.c
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

target_compile_definitions(d2x-retro PUBLIC
    DXX_VERSION_MAJORi=0
    DXX_VERSION_MINORi=58
    DXX_VERSION_MICROi=1
    )

include(CheckTypeSize)
check_type_size("ssize_t" SIZEOF_SSIZE_T LANGUAGE C)
if(NOT "${SIZEOF_SSIZE_T}" STREQUAL "")
    target_compile_definitions(d2x-retro PUBLIC HAVE_SSIZE_T=1)
endif()

if(WIN32)
    target_link_libraries(d2x-retro PUBLIC glu32 winmm ws2_32)
    target_link_libraries(d2x-retro PRIVATE arch_win32)
    target_sources(d2x-retro PRIVATE
        ${CMAKE_SOURCE_DIR}/arch/win32/d2x-rebirth.ico
        ${CMAKE_SOURCE_DIR}/arch/win32/d2x-rebirth.rc
        )
    set(CMAKE_EXE_LINKER_FLAGS_DEBUG "${CMAKE_EXE_LINKER_FLAGS_DEBUG} /NODEFAULTLIB:msvcrt.lib")
    install(FILES
        ${PROJECT_BINARY_DIR}/main/SDL.dll
        ${PROJECT_BINARY_DIR}/main/physfs.dll
        DESTINATION .)
endif()

if(__APPLE__)
    target_link_libraries(d2x-retro PRIVATE arch_carbon arch_cocoa)
endif()

if(__LINUX__)
    target_include_directories(d2x-retro PUBLIC ${CMAKE_SOURCE_DIR}/arch/linux/include)
endif()

if(OPENGL)
    target_link_libraries(d2x-retro PRIVATE arch_ogl xmodel)
endif()

if(SDLMIXER)
    find_package(SDL_mixer REQUIRED)
    target_link_libraries(d2x-retro PUBLIC ${SDL_MIXER_LIBRARIES})
    install(FILES ${SDL_MIXER_LIBRARIES}/../SDL_mixer.dll DESTINATION .)
endif()

if(EDITOR)
    target_sources(d2x-retro PRIVATE bmread.c)
    target_link_libraries(d2x-retro PRIVATE editor ui)
endif()

if(UDP)
    target_sources(d2x-retro PRIVATE net_udp.c)
endif()

if(NOT WIN32)
    set(EXTRA_LIBRARIES m)
endif()

find_package(SDL 1.2 REQUIRED)
find_package(PhysFS REQUIRED)
if(OPENGL)
    find_package(OpenGL REQUIRED)
endif()
if(SDLMIXER)
    find_package(SDL_mixer REQUIRED)
endif()
if(PNG)
    find_package(PNG REQUIRED)
endif()
target_include_directories(d2x-retro PUBLIC ${SDL_INCLUDE_DIR} ${PHYSFS_INCLUDE_DIR})
target_link_libraries(d2x-retro PRIVATE 2d 3d arch_sdl iff libmve maths mem misc texmap)
target_link_libraries(d2x-retro PUBLIC ${OPENGL_LIBRARY} ${SDL_LIBRARY} ${SDL_MIXER_LIBRARY} ${PHYSFS_LIBRARY} ${PNG_LIBRARY} ${EXTRA_LIBRARIES})
target_sources(d2x-retro PRIVATE vers_id.c)
install(TARGETS d2x-retro RUNTIME DESTINATION .)
