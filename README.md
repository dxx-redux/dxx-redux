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

- Install the required packages

  - msys2 (use the 'MSYS2 MINGW64' entry in the Start menu)

    `pacman -S git mingw-w64-x86_64-gcc mingw-w64-x86_64-cmake
    mingw-w64-x86_64-physfs mingw-w64-x86_64-SDL mingw-w64-x86_64-SDL_mixer
    mingw-w64-x86_64-libpng mingw-w64-x86_64-glew`

  - Debian/Ubuntu

    `apt install build-essential git cmake libphysfs-dev libsdl1.2-dev libsdl-mixer1.2-dev libpng-dev
    libglew-dev`

  - Fedora

    `dnf install make gcc-c++ git cmake physfs-devel sdl12-compat-devel SDL_mixer-devel libpng-devel
    glew-devel`

  - Arch Linux

    `pacman -S base-devel git cmake physfs sdl12-compat sdl_mixer libpng glew`

- Get the source code from github

  `git clone https://github.com/dxx-redux/dxx-redux`

- Enter the dxx-redux directory

  `cd dxx-redux`

- Enter the the d1 or d2 directory

  `cd d1`

- Set the build options

  `cmake -B build -DCMAKE_BUILD_TYPE=RelWithDebInfo`

  (see `cmake -B build -L` for more options)

- Build the code

  `cmake --build build`

  (add `-j4` to use 4 cores)
