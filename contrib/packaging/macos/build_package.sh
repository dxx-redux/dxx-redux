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

    dylibbundler -od -b -x $contents/MacOS/$name -d $contents/libs

    # zip up and output to top level dir
    zip -r -X ${zipfilename} ${prettyname}.app
}

build_app "buildd1/main" "d1x-redux" "D1X-Redux" "DCNT"
build_app "buildd2/main" "d2x-redux" "D2X-Redux" "DCT2"

# Clean up
