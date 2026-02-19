#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$SCRIPT_DIR"

SKIP_DEPS=0
SKIP_ML_MODELS=0
SKIP_OCR_MODELS=0
SKIP_BUILD=0
CLEAN_FIRST=0
FULL_CLEAN=0

for arg in "$@"; do
    case "$arg" in
        --skip-deps) SKIP_DEPS=1 ;;
        --skip-ml-models) SKIP_ML_MODELS=1 ;;
        --skip-ocr-models) SKIP_OCR_MODELS=1 ;;
        --skip-build) SKIP_BUILD=1 ;;
        --clean) CLEAN_FIRST=1 ;;
        --full-clean) FULL_CLEAN=1 ;;
        --help)
            echo "Uso: $0 [--clean] [--full-clean] [--skip-deps] [--skip-ml-models] [--skip-ocr-models] [--skip-build]"
            exit 0
            ;;
        *)
            echo "❌ Opcion no soportada: $arg"
            exit 1
            ;;
    esac
done

APP_NAME="clipboard-manager-net"
PUBLISH_DIR="$ROOT_DIR/net-clipboard-manager/publish/ClipboardManager.App"
INSTALL_DIR="$HOME/.local/share/$APP_NAME"
BIN_DIR="$HOME/.local/bin"
LAUNCHER="$BIN_DIR/$APP_NAME"

if [ $FULL_CLEAN -eq 1 ]; then
    echo "🧹 Limpieza total de instalacion previa (.NET)..."
    rm -rf "$INSTALL_DIR"
    rm -f "$LAUNCHER"
    echo "✅ Limpieza total completada"
    echo ""
fi

if [ $CLEAN_FIRST -eq 1 ]; then
    echo "🧹 Limpieza previa de build/publish..."
    rm -rf "$PUBLISH_DIR"
    echo "✅ Limpieza completada"
    echo ""
fi

if [ $SKIP_DEPS -eq 0 ]; then
    echo "📦 Instalando dependencias .NET..."

    if [ -f /etc/os-release ]; then
        . /etc/os-release
        DISTRO=$ID
    else
        echo "❌ No se pudo detectar la distribucion"
        exit 1
    fi

    case "$DISTRO" in
        arch|manjaro)
            sudo pacman -S --needed --noconfirm dotnet-sdk
            ;;
        ubuntu|debian|pop)
            if ! command -v dotnet >/dev/null 2>&1; then
                echo "📥 Agregando repositorio de .NET..."
                wget -q https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
                sudo dpkg -i packages-microsoft-prod.deb
                rm -f packages-microsoft-prod.deb
                sudo apt-get update
            fi
            sudo apt-get install -y dotnet-sdk-10.0
            ;;
        fedora|rhel|centos)
            sudo dnf install -y dotnet-sdk-10.0
            ;;
        *)
            echo "❌ Distribucion no soportada: $DISTRO"
            echo "Instala manualmente: dotnet-sdk 10"
            exit 1
            ;;
    esac
fi

if [ $SKIP_ML_MODELS -eq 0 ]; then
    echo "📥 Descargando modelos ML..."
    bash "$ROOT_DIR/scripts/download-ml-models.sh"
fi

if [ $SKIP_OCR_MODELS -eq 0 ]; then
    echo "📥 Descargando modelos OCR (Tesseract)..."
    bash "$ROOT_DIR/scripts/download-models.sh"
fi

if [ $SKIP_BUILD -eq 0 ]; then
    echo "🔨 Publicando app .NET..."
    dotnet publish "$ROOT_DIR/net-clipboard-manager/ClipboardManager.App/ClipboardManager.App.csproj" \
        -c Release \
        -o "$PUBLISH_DIR"
fi

mkdir -p "$INSTALL_DIR" "$BIN_DIR"
if [ -d "$PUBLISH_DIR" ]; then
    rm -rf "$INSTALL_DIR"
    cp -r "$PUBLISH_DIR" "$INSTALL_DIR"
fi

cat > "$LAUNCHER" << 'EOF'
#!/bin/bash
set -euo pipefail
APP_DIR="$HOME/.local/share/clipboard-manager-net"
APP_BIN="$APP_DIR/ClipboardManager.App"
APP_DLL="$APP_DIR/ClipboardManager.App.dll"

if [ -x "$APP_BIN" ]; then
    exec "$APP_BIN" "$@"
fi

exec dotnet "$APP_DLL" "$@"
EOF

chmod +x "$LAUNCHER"

echo ""
echo "✅ Instalacion .NET completada"
echo ""
echo "📍 Ejecutable: $LAUNCHER"
echo ""
echo "🔎 Verificacion rapida:"
echo "   $LAUNCHER"
