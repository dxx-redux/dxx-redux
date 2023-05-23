#!/bin/bash
set -x

version=$(git tag --points-at HEAD)
if [ -n "$version" ]; then
	if [ "${version:0:1}" = "v" ]; then
		version=${version:1}
	fi
else
    version=$(git rev-parse --short HEAD)
fi

#ARCH=x86_64


# Grab latest AppImage package
curl -s -L -O https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage || exit 3
chmod a+x appimagetool-x86_64.AppImage

# And the AppRun
curl -s -L -O https://github.com/AppImage/AppImageKit/releases/download/continuous/AppRun-x86_64 || exit 3
chmod a+x AppRun-x86_64


build_appimage() {
    name="$1"
    prettyname="$2"
    dir="${name:0:2}"

    appdir="${name}.appdir"
    appimagename="${prettyname}-${version}.AppImage"

    ## Install
    # Copy resources into package dir
    mkdir "${appdir}"

    # Executable
    mkdir -p ${appdir}/usr/bin
    cp build${dir}/main/${name} ${appdir}/usr/bin

    # Icons
    mkdir -p ${appdir}/usr/share/pixmaps
    cp ${dir}/${name}.xpm ${appdir}/usr/share/pixmaps
    cp ${dir}/${name}.xpm ${appdir}/

    mkdir -p ${appdir}/usr/share/icons/hicolor/128x128/apps/
    cp ${dir}/${name}.png ${appdir}/usr/share/icons/hicolor/128x128/apps/
    cp ${dir}/${name}.png ${appdir}/

    # Menu item
    mkdir -p ${appdir}/usr/share/applications
    cp ${dir}/${name}.desktop ${appdir}/usr/share/applications
    cp ${dir}/${name}.desktop ${appdir}/

    ## Package
    cp AppRun-x86_64 ${appdir}/AppRun

    # Package!
    ./appimagetool-x86_64.AppImage --no-appstream --verbose "${appdir}" "${appimagename}"

    #rm -rf ${appdir}
}

# Build each subunit
build_appimage "d1x-redux" "d1x-redux"
build_appimage "d2x-redux" "d2x-redux"

# Clean
#rm -f appimagetool* AppRun*
