#!/bin/bash
set -e

echo "Building Clipboard Daemon..."

# Create build directory
mkdir -p build
cd build

# Configure with CMake using Ninja
cmake .. \
    -G Ninja \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_EXPORT_COMPILE_COMMANDS=ON

# Build with Ninja
ninja

echo ""
echo "✅ Build successful!"
echo "Binary: $(pwd)/clipboard-daemon"
echo ""
echo "To run:"
echo "  ./clipboard-daemon"
echo ""
echo "To install:"
echo "  sudo ninja install"
