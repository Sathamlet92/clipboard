#!/bin/bash
set -euo pipefail

SKIP_DEPS=0
SKIP_MODELS=0
SKIP_HYPR=0

for arg in "$@"; do
    case "$arg" in
        --skip-deps) SKIP_DEPS=1 ;;
        --skip-models) SKIP_MODELS=1 ;;
        --skip-hyprland) SKIP_HYPR=1 ;;
        --help)
            echo "Uso: $0 [--skip-deps] [--skip-models] [--skip-hyprland]"
            exit 0
            ;;
        *)
            echo "❌ Opción no soportada: $arg"
            exit 1
            ;;
    esac
done

if ! command -v pacman >/dev/null 2>&1; then
    echo "❌ Este instalador es solo para Arch Linux"
    exit 1
fi

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
BIN_DIR="$HOME/.local/bin"
SYSTEMD_USER_DIR="$HOME/.config/systemd/user"
SERVICE_FILE="$SYSTEMD_USER_DIR/clipboard-daemon.service"

echo "📦 Instalando Clipboard Manager C++ para Arch + Hyprland"
echo ""

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
cd "$ROOT_DIR/cpp-app"
./build.sh

echo "🔨 Compilando daemon C++..."
cd "$ROOT_DIR/daemon"
./build.sh

mkdir -p "$BIN_DIR"
cp "$ROOT_DIR/cpp-app/build/clipboard-manager" "$BIN_DIR/clipboard-manager"
cp "$ROOT_DIR/daemon/build/clipboard-daemon" "$BIN_DIR/clipboard-daemon"
chmod +x "$BIN_DIR/clipboard-manager" "$BIN_DIR/clipboard-daemon"

if [ $SKIP_MODELS -eq 0 ]; then
    echo "📥 Descargando modelos ML..."
    bash "$ROOT_DIR/scripts/download-ml-models.sh"
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