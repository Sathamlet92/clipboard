#!/bin/bash
set -e

echo "📦 Instalando dependencias para Clipboard Manager..."
echo ""

# Detectar distribución
if [ -f /etc/os-release ]; then
    . /etc/os-release
    DISTRO=$ID
else
    echo "❌ No se pudo detectar la distribución"
    exit 1
fi

echo "🐧 Distribución detectada: $DISTRO"
echo ""

# Función para instalar en Arch/Manjaro
install_arch() {
    echo "📦 Instalando dependencias para Arch Linux..."
    sudo pacman -S --needed \
        dotnet-sdk \
        cmake \
        ninja \
        gcc \
        grpc \
        protobuf \
        wayland \
        wayland-protocols \
        wl-clipboard \
        tesseract \
        tesseract-data-eng \
        tesseract-data-spa \
        sqlite
    
    echo "📦 Instalando ONNX Runtime (opcional para embeddings)..."
    if pacman -Qi onnxruntime &> /dev/null; then
        echo "✅ ONNX Runtime ya está instalado"
    else
        echo "⚠️  ONNX Runtime no encontrado en repositorios oficiales"
        echo "   La app usará el paquete NuGet (Microsoft.ML.OnnxRuntime)"
    fi
    
    echo "✅ Dependencias instaladas (Arch)"
}

# Función para instalar en Ubuntu/Debian
install_ubuntu() {
    echo "📦 Instalando dependencias para Ubuntu/Debian..."
    
    # Agregar repositorio de .NET si no existe
    if ! command -v dotnet &> /dev/null; then
        echo "📥 Agregando repositorio de .NET..."
        wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
        sudo dpkg -i packages-microsoft-prod.deb
        rm packages-microsoft-prod.deb
        sudo apt-get update
    fi
    
    sudo apt-get install -y \
        dotnet-sdk-10.0 \
        cmake \
        ninja-build \
        g++ \
        libgrpc++-dev \
        libprotobuf-dev \
        protobuf-compiler-grpc \
        libwayland-dev \
        wayland-protocols \
        wl-clipboard \
        tesseract-ocr \
        tesseract-ocr-eng \
        tesseract-ocr-spa \
        libsqlite3-dev
    echo "✅ Dependencias instaladas (Ubuntu/Debian)"
}

# Función para instalar en Fedora
install_fedora() {
    echo "📦 Instalando dependencias para Fedora..."
    sudo dnf install -y \
        dotnet-sdk-10.0 \
        cmake \
        ninja-build \
        gcc-c++ \
        grpc-devel \
        grpc-plugins \
        protobuf-devel \
        wayland-devel \
        wayland-protocols-devel \
        wl-clipboard \
        tesseract \
        tesseract-langpack-eng \
        tesseract-langpack-spa \
        sqlite-devel
    echo "✅ Dependencias instaladas (Fedora)"
}

# Instalar según distribución
case "$DISTRO" in
    arch|manjaro)
        install_arch
        ;;
    ubuntu|debian|pop)
        install_ubuntu
        ;;
    fedora|rhel|centos)
        install_fedora
        ;;
    *)
        echo "❌ Distribución no soportada: $DISTRO"
        echo "Por favor instala manualmente:"
        echo "  - .NET SDK 10"
        echo "  - CMake + Ninja"
        echo "  - gRPC + Protobuf"
        echo "  - Wayland + wl-clipboard"
        echo "  - Tesseract OCR (eng + spa)"
        echo "  - SQLite"
        exit 1
        ;;
esac

echo ""
echo "🎉 Dependencias instaladas exitosamente!"
echo ""
echo "📋 Verificando instalación..."
echo ""

# Verificar instalaciones
check_command() {
    if command -v $1 &> /dev/null; then
        echo "✅ $1: $(command -v $1)"
    else
        echo "❌ $1: NO ENCONTRADO"
    fi
}

check_command dotnet
check_command cmake
check_command ninja
check_command g++
check_command protoc
check_command wl-copy
check_command tesseract

echo ""
echo "📊 Versiones:"
dotnet --version
cmake --version | head -1
tesseract --version | head -1

echo ""
echo "✅ Instalación completa!"
echo ""
echo "📝 Próximos pasos:"
echo "  1. Descargar modelos Tesseract: bash scripts/download-models.sh"
echo "  2. Descargar modelos ML (embeddings): bash scripts/download-ml-models.sh"
echo "  3. Compilar daemon: bash daemon/build.sh"
echo "  4. Compilar app: dotnet build"
echo "  5. Ejecutar: dotnet run --project src/ClipboardManager.App"
