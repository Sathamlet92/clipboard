#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "$SCRIPT_DIR/scripts/lib/common.sh"

SKIP_DEPS=0
SKIP_MODELS=0
SKIP_OCR_MODELS=0
SKIP_HYPR=0
CLEAN_FIRST=0
FULL_CLEAN=0

for arg in "$@"; do
    case "$arg" in
        --skip-deps) SKIP_DEPS=1 ;;
        --skip-models) SKIP_MODELS=1 ;;
        --skip-ocr-models) SKIP_OCR_MODELS=1 ;;
        --skip-hyprland) SKIP_HYPR=1 ;;
        --clean) CLEAN_FIRST=1 ;;
        --full-clean) FULL_CLEAN=1 ;;
        --help)
            echo "Uso: $0 [--clean] [--full-clean] [--skip-deps] [--skip-models] [--skip-ocr-models] [--skip-hyprland]"
            exit 0
            ;;
        *)
            echo "❌ Opción no soportada: $arg"
            exit 1
            ;;
    esac
done

require_arch_linux

ROOT_DIR="$(project_root_dir)"
BIN_DIR="$HOME/.local/bin"
SYSTEMD_USER_DIR="$HOME/.config/systemd/user"
SERVICE_FILE="$SYSTEMD_USER_DIR/clipboard-daemon.service"

echo "📦 Instalando Clipboard Manager C++ para Arch + Hyprland"
echo ""

# Always stop running instances before replacing binaries
systemctl --user stop clipboard-daemon.service 2>/dev/null || true
pkill -f "$HOME/.local/bin/clipboard-manager" 2>/dev/null || true
pkill -f "$HOME/.local/bin/clipboard-daemon" 2>/dev/null || true
killall clipboard-manager 2>/dev/null || true
killall clipboard-daemon 2>/dev/null || true

if [ $FULL_CLEAN -eq 1 ]; then
    echo "🧹 Limpieza total de instalación previa..."

    systemctl --user disable clipboard-daemon.service 2>/dev/null || true
    rm -f "$HOME/.config/systemd/user/clipboard-daemon.service"
    systemctl --user daemon-reload 2>/dev/null || true

    rm -f "$BIN_DIR/clipboard-manager" "$BIN_DIR/clipboard-daemon"
    rm -f "$HOME/.local/share/applications/clipboard-manager.desktop"
    rm -f "$HOME/.local/share/applications/com.clipboard.manager.desktop"
    rm -rf "$HOME/.local/share/clipboard-manager"
    rm -rf "$HOME/.clipboard-manager/models"
    rm -f "$HOME/.local/share/icons/hicolor/256x256/apps/clipboard-manager.png"
    rm -f "$HOME/.local/share/icons/hicolor/scalable/apps/clipboard-manager.svg"

    echo "✅ Limpieza total completada"
    echo ""
fi

if [ $CLEAN_FIRST -eq 1 ]; then
    echo "🧹 Limpieza previa de binarios y builds..."

    rm -f "$BIN_DIR/clipboard-manager" "$BIN_DIR/clipboard-daemon"
    rm -rf "$ROOT_DIR/clipboard-manager/build" "$ROOT_DIR/daemon/build"

    echo "✅ Limpieza completada"
    echo ""
fi

if [ $SKIP_DEPS -eq 0 ]; then
    echo "📥 Instalando dependencias C++..."
    sudo pacman -S --needed --noconfirm \
        gtk4 \
        gtkmm-4.0 \
        sqlite \
        tesseract \
        tesseract-data-eng \
        tesseract-data-spa \
        opencv \
        wl-clipboard \
        grpc \
        protobuf \
        onnxruntime \
        nlohmann-json \
        cmake \
        ninja \
        jq
fi

echo "🔨 Compilando app C++..."
cd "$ROOT_DIR/clipboard-manager"
./build.sh

echo "🔨 Compilando daemon C++..."
cd "$ROOT_DIR/daemon"
./build.sh

mkdir -p "$BIN_DIR"
cp "$ROOT_DIR/clipboard-manager/build/clipboard-manager" "$BIN_DIR/clipboard-manager"
cp "$ROOT_DIR/daemon/build/clipboard-daemon" "$BIN_DIR/clipboard-daemon"
chmod +x "$BIN_DIR/clipboard-manager" "$BIN_DIR/clipboard-daemon"

DESKTOP_SRC="$ROOT_DIR/clipboard-manager/com.clipboard.manager.desktop"
DESKTOP_DIR="$HOME/.local/share/applications"
mkdir -p "$DESKTOP_DIR"
if [ -f "$DESKTOP_SRC" ]; then
    cp "$DESKTOP_SRC" "$DESKTOP_DIR/com.clipboard.manager.desktop"
fi

ICON_PNG_SRC="$ROOT_DIR/clipboard-manager/assets/icon.png"
ICON_SVG_SRC="$ROOT_DIR/clipboard-manager/assets/icon.svg"
ICON_PNG_DIR="$HOME/.local/share/icons/hicolor/256x256/apps"
ICON_SVG_DIR="$HOME/.local/share/icons/hicolor/scalable/apps"

mkdir -p "$ICON_PNG_DIR" "$ICON_SVG_DIR"
if [ -f "$ICON_PNG_SRC" ]; then
    cp "$ICON_PNG_SRC" "$ICON_PNG_DIR/clipboard-manager.png"
fi
if [ -f "$ICON_SVG_SRC" ]; then
    cp "$ICON_SVG_SRC" "$ICON_SVG_DIR/clipboard-manager.svg"
fi

if [ $SKIP_MODELS -eq 0 ]; then
    echo "📥 Descargando modelos ML..."
    bash "$ROOT_DIR/scripts/download-ml-models.sh"
fi

if [ $SKIP_OCR_MODELS -eq 0 ]; then
    echo "📥 Descargando modelos OCR (Tesseract)..."
    bash "$ROOT_DIR/scripts/download-models.sh"
fi

mkdir -p "$SYSTEMD_USER_DIR"
cat > "$SERVICE_FILE" << EOF
[Unit]
Description=Clipboard Manager Daemon (C++)
After=graphical-session.target

[Service]
Type=simple
ExecStart=$HOME/.local/bin/clipboard-daemon
Restart=on-failure
RestartSec=5

[Install]
WantedBy=default.target
EOF

systemctl --user daemon-reload
systemctl --user enable --now clipboard-daemon.service

if [ $SKIP_HYPR -eq 0 ]; then
    if [ -f "$ROOT_DIR/scripts/configure-hyprland-clipboard.sh" ]; then
        echo "⚙️  Configurando Hyprland..."
        bash "$ROOT_DIR/scripts/configure-hyprland-clipboard.sh" --apply
        if command -v hyprctl >/dev/null 2>&1; then
            hyprctl reload || true
        fi
    fi
fi

echo ""
echo "✅ Instalación C++ completada"
echo ""
echo "📍 Binarios:"
echo "   $BIN_DIR/clipboard-manager"
echo "   $BIN_DIR/clipboard-daemon"
echo ""
echo "🔎 Verificación rápida:"
echo "   pgrep -af 'clipboard-manager|clipboard-daemon'"
echo "   systemctl --user status clipboard-daemon.service"
echo ""