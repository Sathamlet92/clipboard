#!/bin/bash
set -e

echo "Building Clipboard Manager C++ (Native Wayland)"

# Create build directory
mkdir -p build
cd build

# Configure with CMake using Ninja
cmake -G Ninja \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_INSTALL_PREFIX=/usr/local \
    ..

# Build with Ninja
ninja -j$(nproc)

echo "✅ Build successful!"
echo "Binary: $(pwd)/clipboard-manager"
echo ""
echo "To install:"
echo "  sudo ninja install"
