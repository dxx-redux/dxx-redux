#!/bin/bash
set -x

GIT_HASH=$(git rev-parse --short HEAD)

build_app() {
    builddir="$1"
    name="$2"
    prettyname="$3"
    
    zipfilename="${prettyname}-win-${GIT_HASH}.zip"
    outdir="${prettyname}"
    tmpdir="packagetemp"
    inipath="${name:0:2}/${name:0:3}.ini"
    
    # Have to bundle in separate directory because of case-insensitivity clashes
    mkdir ${tmpdir}
    cd ${tmpdir}
    
    mkdir ${outdir}
    mkdir ${outdir}/demos
    mkdir ${outdir}/missions
    mkdir ${outdir}/screenshots

    # Copy executable and libraris
    cp ../${builddir}/${name}.exe ${outdir}/
    
    # Copy in .dlls the old fashioned way. This assumes all the needed DLLs are 
    # ones from the mingw64 installation
    ldd ${outdir}/${name}.exe |grep mingw64 |sort |cut -d' ' -f3 |while read dll; do cp "${dll}" ${outdir}/; done

    # Copy in other resources
    cp ../COPYING.txt ${outdir}/
    cp ../${inipath} ${outdir}/
        
    # zip up and output to top level dir
    zip -r -X ../${zipfilename} ${outdir}
    
    cd ..
    
    rm -rf ${tmpdir}
}

build_app "buildd1/main" "d1x-redux" "D1X-Redux"
build_app "buildd2/main" "d2x-redux" "D2X-Redux"

# Clean up

