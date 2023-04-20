add_library(xmodel STATIC
aseread.cpp
submodel.cpp
tga.cpp
xmodel.cpp
	)

find_package(SDL)
find_package(GLEW REQUIRED)

include_directories(../include ../arch/include ../main)
target_include_directories(xmodel PUBLIC ${SDL_INCLUDE_DIR} ${GLEW_INCLUDE_DIR})
