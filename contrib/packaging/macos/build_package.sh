#!/bin/bash
set -x

export MACOSX_DEPLOYMENT_TARGET=10.14

version=$(git tag --points-at HEAD)
if [ -n "$version" ]; then
	if [ "${version:0:1}" = "v" ]; then
		version=${version:1}
	fi
else
    version=$(git rev-parse --short HEAD)
fi

find_lib() {
    exe="$1"
    lib="$2"
    dirlist=$(otool -l "$exe"|awk '/^ *cmd LC_RPATH/{rp=1} /^Load command/{rp=0} rp&&/^ *path /{print $2}')
    for dir in $dirlist; do
        for file in "$dir"/$lib; do 
            if [ -f "$file" ]; then echo $file; fi
        done
     done
}

build_app() {
    builddir="$1"
    name="$2"
    prettyname="$3"
    appltag="$4"
    srcdir="${name:0:2}"
    contents="${prettyname}.app/Contents"

    zipfilename="${name}-${version}-mac.zip"

    mkdir -p $contents/MacOS
    mkdir -p $contents/Resources
    mkdir -p $contents/libs
    cp -p $builddir/$name $contents/MacOS
    cp -p $srcdir/arch/cocoa/Info.plist $contents
    cp -p $srcdir/arch/cocoa/${name}.icns $contents/Resources
    echo -n "APPL${appltag}" > $contents/PkgInfo

    dylibbundler -ns -od -b -x $contents/MacOS/$name -d $contents/libs \
        -s $builddir/../_deps/sdl_mixer-1.2-cmake-build

    # SDL2 is loaded dynamically by sdl12-compat
    sdl2=libSDL2-2.0.0.dylib
    cp -p $(find_lib $builddir/$name $sdl2) $contents/libs
    dylibbundler -ns -of -b -x $contents/libs/$sdl2 -d $contents/libs

    find $contents/libs -name '*.dylib' -exec codesign -f -s - '{}' \;
    codesign -f -s - $contents/MacOS/$name

    # zip up and output to top level dir
    zip -r -X ${zipfilename} ${prettyname}.app
}

build_app "buildd1/main" "d1x-redux" "D1X-Redux" "DCNT"
build_app "buildd2/main" "d2x-redux" "D2X-Redux" "DCT2"

# Clean up
