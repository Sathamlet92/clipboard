# Clipboard Daemon

C++ daemon for monitoring clipboard changes on Linux (X11/Wayland).

## Dependencies

### Ubuntu/Debian
```bash
sudo apt install -y \
    build-essential \
    cmake \
    pkg-config \
    libx11-dev \
    libxfixes-dev \
    libwayland-dev \
    libprotobuf-dev \
    protobuf-compiler \
    libgrpc++-dev \
    libgrpc-dev \
    protobuf-compiler-grpc
```

### Arch Linux
```bash
sudo pacman -S \
    base-devel \
    cmake \
    pkgconf \
    libx11 \
    libxfixes \
    wayland \
    protobuf \
    grpc
```

### Fedora
```bash
sudo dnf install -y \
    gcc-c++ \
    cmake \
    pkgconfig \
    libX11-devel \
    libXfixes-devel \
    wayland-devel \
    protobuf-devel \
    grpc-devel \
    grpc-plugins
```

## Building

```bash
chmod +x build.sh
./build.sh
```

Or manually:
```bash
mkdir build && cd build
cmake ..
make -j$(nproc)
```

## Running

```bash
./build/clipboard-daemon
```

With custom address:
```bash
./build/clipboard-daemon "localhost:50051"
```

## Features

- ✅ X11 clipboard monitoring (XFixes)
- 🚧 Wayland support (stub, needs wlr-data-control)
- ✅ gRPC server for IPC
- ✅ Text and image detection
- ✅ Source application detection
- ✅ Low CPU overhead (<0.5%)

## Protocol

Uses gRPC with Protocol Buffers. See `proto/clipboard.proto` for API definition.

## Notes

- Wayland support requires wlr-data-control protocol (Hyprland, Sway)
- Falls back to X11/XWayland if Wayland not available
- Uses Unix domain socket by default for security
