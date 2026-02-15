#!/bin/bash
set -e

echo "Setting up Wayland support..."
echo ""

# Install wlr-protocols
echo "Installing wlr-protocols..."
if command -v pacman &> /dev/null; then
    # On Arch, the protocols are in wlroots package or separate
    if pacman -Qi wlroots &> /dev/null; then
        echo "wlroots already installed"
    else
        # Try installing wlroots or just use system protocols
        echo "Checking for wlr-protocols..."
    fi
elif command -v apt &> /dev/null; then
    sudo apt install -y wlroots-dev
else
    echo "Please install wlr-protocols manually"
    exit 1
fi

# Create protocols directory
PROTO_DIR="daemon/protocols"
mkdir -p "$PROTO_DIR"

# Find wlr-data-control protocol
echo "Searching for wlr-data-control protocol..."

# Possible locations
POSSIBLE_PATHS=(
    "/usr/share/wlr-protocols/unstable/wlr-data-control-unstable-v1.xml"
    "/usr/share/wayland-protocols/wlr-protocols/unstable/wlr-data-control-unstable-v1.xml"
    "/usr/local/share/wlr-protocols/unstable/wlr-data-control-unstable-v1.xml"
)

WLR_PROTO_PATH=""
for path in "${POSSIBLE_PATHS[@]}"; do
    if [ -f "$path" ]; then
        WLR_PROTO_PATH="$path"
        echo "✅ Found at: $WLR_PROTO_PATH"
        break
    fi
done

if [ -z "$WLR_PROTO_PATH" ]; then
    echo "⚠️  wlr-data-control protocol not found in standard locations"
    echo ""
    echo "Downloading from wlroots repository..."
    
    # Download directly from GitLab
    WLR_PROTO_URL="https://gitlab.freedesktop.org/wlroots/wlr-protocols/-/raw/master/unstable/wlr-data-control-unstable-v1.xml"
    
    mkdir -p "$PROTO_DIR"
    if command -v curl &> /dev/null; then
        curl -L -o "$PROTO_DIR/wlr-data-control-unstable-v1.xml" "$WLR_PROTO_URL"
    elif command -v wget &> /dev/null; then
        wget -O "$PROTO_DIR/wlr-data-control-unstable-v1.xml" "$WLR_PROTO_URL"
    else
        echo "❌ Neither curl nor wget found. Cannot download protocol."
        exit 1
    fi
    
    WLR_PROTO_PATH="$PROTO_DIR/wlr-data-control-unstable-v1.xml"
    
    if [ -f "$WLR_PROTO_PATH" ]; then
        echo "✅ Downloaded protocol successfully"
    else
        echo "❌ Failed to download protocol"
        exit 1
    fi
fi

# Generate C bindings using wayland-scanner
echo "Generating Wayland protocol bindings..."

wayland-scanner client-header \
    "$WLR_PROTO_PATH" \
    "$PROTO_DIR/wlr-data-control-unstable-v1-client-protocol.h"

wayland-scanner private-code \
    "$WLR_PROTO_PATH" \
    "$PROTO_DIR/wlr-data-control-unstable-v1-protocol.c"

echo "✅ Protocol bindings generated"
echo ""
echo "Files created:"
echo "  - $PROTO_DIR/wlr-data-control-unstable-v1-client-protocol.h"
echo "  - $PROTO_DIR/wlr-data-control-unstable-v1-protocol.c"
echo ""
echo "Now rebuild the daemon with Wayland support"
