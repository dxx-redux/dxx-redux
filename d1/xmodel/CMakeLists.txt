add_library(xmodel STATIC
aseread.cpp
submodel.cpp
tga.cpp
xmodel.cpp
	)

find_package(SDL2 REQUIRED)
find_package(GLEW REQUIRED)

include_directories(../include ../arch/include ../main)
target_include_directories(xmodel PUBLIC ${SDL2_INCLUDE_DIRS} ${GLEW_INCLUDE_DIR})
