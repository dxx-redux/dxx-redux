add_executable (d2x-redux
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

target_compile_definitions(d2x-redux PUBLIC
    DXX_VERSION_MAJORi=${PROJECT_VERSION_MAJOR}
    DXX_VERSION_MINORi=${PROJECT_VERSION_MINOR}
    DXX_VERSION_MICROi=$<IF:$<BOOL:${PROJECT_VERSION_MICRO}>,${PROJECT_VERSION_MICRO},0>
    )

include(CheckTypeSize)
check_type_size("ssize_t" SIZEOF_SSIZE_T LANGUAGE C)
if(NOT "${SIZEOF_SSIZE_T}" STREQUAL "")
    target_compile_definitions(d2x-redux PUBLIC HAVE_SSIZE_T=1)
endif()

if(OPENGL)
    target_link_libraries(d2x-redux PRIVATE arch_ogl xmodel)
endif()

if(SDLMIXER)
    target_link_libraries(d2x-redux PUBLIC ${SDL_MIXER_LIBRARIES})
endif()

if(EDITOR)
    target_sources(d2x-redux PRIVATE bmread.c)
    target_link_libraries(d2x-redux PRIVATE editor ui)
endif()

if(UDP)
    target_sources(d2x-redux PRIVATE net_udp.c)
endif()

if(WIN32)
    target_link_libraries(d2x-redux PUBLIC glu32 winmm ws2_32 dbghelp)
    target_link_libraries(d2x-redux PRIVATE arch_win32)
    target_sources(d2x-redux PRIVATE
        ${CMAKE_SOURCE_DIR}/arch/win32/d2x-rebirth.ico
        ${CMAKE_SOURCE_DIR}/arch/win32/d2x-rebirth.rc
        )
    set(CMAKE_EXE_LINKER_FLAGS_DEBUG "${CMAKE_EXE_LINKER_FLAGS_DEBUG} /NODEFAULTLIB:msvcrt.lib")
    if(CMAKE_CXX_COMPILER_ID STREQUAL "MSVC")
        # The manifest has to be included through the .rc for compatibility with MSYS. However
        # MSVC will automatically generate a manifest using this method - which will cause a
        # collision - so we need to tell it not to.
        target_link_options(d2x-redux PRIVATE "/MANIFEST:NO")
        if(CMAKE_BUILD_TYPE STREQUAL "Debug")
            # Enable Hot Reload for debug builds
            target_link_options(${PROJECT_NAME} PRIVATE $<$<CONFIG:Debug>:/INCREMENTAL>)
            target_compile_options(${PROJECT_NAME} PRIVATE $<$<CONFIG:Debug>:/ZI>)
        endif()
    endif()

    install(FILES
        ${PROJECT_BINARY_DIR}/main/SDL.dll
        ${PROJECT_BINARY_DIR}/main/physfs.dll
        DESTINATION .)
    if(CMAKE_BUILD_TYPE STREQUAL "Debug")
        install(FILES ${PROJECT_BINARY_DIR}/main/zlibd1.dll DESTINATION .)
    else()
        install(FILES ${PROJECT_BINARY_DIR}/main/zlib1.dll DESTINATION .)
    endif()
    if(SDLMIXER)
        install(FILES ${PROJECT_BINARY_DIR}/main/SDL_mixer.dll DESTINATION .)
    endif()
    if(OPENGL)
        if(CMAKE_BUILD_TYPE STREQUAL "Debug")
            install(FILES ${PROJECT_BINARY_DIR}/main/glew32d.dll DESTINATION .)
        else()
            install(FILES ${PROJECT_BINARY_DIR}/main/glew32.dll DESTINATION .)
        endif()
    endif()
    if(PNG)
        if(CMAKE_BUILD_TYPE STREQUAL "Debug")
            install(FILES ${PROJECT_BINARY_DIR}/main/libpng16d.dll DESTINATION .)
        else()
            install(FILES ${PROJECT_BINARY_DIR}/main/libpng16.dll DESTINATION .)
        endif()
    endif()
endif()

if(APPLE)
    target_link_libraries(d2x-redux PRIVATE arch_cocoa)
endif()

if(CMAKE_SYSTEM_NAME STREQUAL "Linux")
    target_link_libraries(d2x-redux PRIVATE arch_x11)
    list(APPEND EXTRA_LIBRARIES "X11")
endif()

if(NOT WIN32)
    list(APPEND EXTRA_LIBRARIES "m")
endif()

find_package(SDL 1.2 REQUIRED)
find_package(PhysFS REQUIRED)
if(OPENGL)
    find_package(OpenGL REQUIRED)
    find_package(GLEW REQUIRED)
    list(APPEND EXTRA_LIBRARIES GLEW::GLEW)
endif()
if(PNG)
    find_package(PNG REQUIRED)
endif()
target_include_directories(d2x-redux PUBLIC ${SDL_INCLUDE_DIR} ${PHYSFS_INCLUDE_DIR})
target_link_libraries(d2x-redux PRIVATE 2d 3d arch_sdl iff libmve maths mem misc texmap)
target_link_libraries(d2x-redux PUBLIC ${OPENGL_LIBRARY} ${SDL_LIBRARY} ${SDL_MIXER_LIBRARY} ${PHYSFS_LIBRARY} ${PNG_LIBRARY} ${EXTRA_LIBRARIES})
target_sources(d2x-redux PRIVATE vers_id.c)
install(TARGETS d2x-redux RUNTIME DESTINATION .)
