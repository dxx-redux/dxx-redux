DXX-Redux
=========

Descent 1&2 source port based on [DXX-Retro](https://github.com/CDarrow/DXX-Retro).

Building in Visual Studio
-------------------------

- [Install vcpkg](https://vcpkg.io/en/getting-started.html)

- Set the VCPKG_ROOT environment variable to the vcpkg folder with Start > search Environment >
  Environment Variables > New...

- Open the d1 or d2 folder in Visual Studio with 'Open a local folder' or Open > CMake...

- Wait until CMake generation finishes

- Select Startup Item main/d1x-redux.exe

- Optionally select Debug > Debug and Launch Settings for d1x-redux and add
  below the line starting with "name":
  `"args": [ "-hogdir", "c:\game-data-folder" ]`

Building in msys2 / Linux
-------------------------

- msys2: `pacman -Ss mingw-w64-x86_64-cmake mingw-w64-x86_64-physfs mingw-w64-x86_64-SDL mingw-w64-x86_64-SDL_mixer
  mingw-w64-x86_64-libpng mingw-w64-x86_64-glew`

- Debian/Ubuntu: `apt install cmake libphysfs-dev libsdl1.2-dev libsdl-mixer1.2-dev libpng-dev
  libglew-dev`

- Use `cd` to go to the d1 or d2 directory

- `cmake -B build`

- `cmake --build build`
