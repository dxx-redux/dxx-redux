#!/bin/bash
# Build script for d1x-redux with DLL bundling
# Run from: C:\Programs\msys64\msys2_shell.cmd -mingw64
# Usage: ./build.sh [Debug|Release]

set -e

BUILD_TYPE="${1:-Release}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/build"
EXE_PATH="$BUILD_DIR/main/d1x-redux.exe"
DIST_DIR="$SCRIPT_DIR/dist"
MINGW_BIN="/mingw64/bin"

echo "=== Building d1x-redux ($BUILD_TYPE) ==="

# Clean and recreate build directory
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure with CMake
CC=/c/Programs/msys64/mingw64/bin/gcc \
CXX=/c/Programs/msys64/mingw64/bin/g++ \
cmake -DCMAKE_BUILD_TYPE="$BUILD_TYPE" ..

# Build
cmake --build . -j4

echo ""
echo "=== Build complete ==="
echo ""

# Create dist directory
rm -rf "$DIST_DIR"
mkdir -p "$DIST_DIR"

# Copy the executable
cp "$EXE_PATH" "$DIST_DIR/"

echo "=== Copying required DLLs ==="

# Use ldd to find dependencies and copy only mingw64 DLLs
ldd "$EXE_PATH" | grep -i '/mingw64/' | awk '{print $3}' | while read dll; do
    if [ -f "$dll" ]; then
        dll_name=$(basename "$dll")
        echo "  Copying: $dll_name"
        cp "$dll" "$DIST_DIR/"
    fi
done

echo ""
echo "=== Distribution package created at: $DIST_DIR ==="
ls -la "$DIST_DIR/"
