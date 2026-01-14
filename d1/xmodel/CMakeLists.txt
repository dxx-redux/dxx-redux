add_library(xmodel STATIC
aseread.cpp
submodel.cpp
tga.cpp
xmodel.cpp
	)
find_package(GLEW REQUIRED)
target_include_directories(xmodel PUBLIC ${GLEW_INCLUDE_DIR})
