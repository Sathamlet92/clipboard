# Clipboard Smart Manager

Un gestor de portapapeles inteligente para Linux con historial infinito, búsqueda semántica, OCR automático y gestión segura de contraseñas.

## 🎯 Características Principales

- **Captura Automática**: Monitorea cambios del portapapeles en tiempo real
- **Historial Infinito**: Almacena todo tu historial de portapapeles de forma segura
- **Búsqueda Semántica**: Busca por significado, no solo por palabras clave (con BERT)
- **OCR Automático**: Extrae texto de imágenes automáticamente (PaddleOCR)
- **Gestión de Contraseñas**: Detección y protección de datos sensibles
- **Soporte X11/Wayland**: Funciona en cualquier entorno de escritorio Linux
- **Alto Rendimiento**: Especialmente optimizado para Hyprland
- **Interfaz Moderna**: UI basada en AvaloniaUI
- **Base de Datos SQLite**: Almacenamiento eficiente con búsqueda full-text (FTS5)

## 🏗️ Arquitectura

```
┌──────────────────────────────────────────┐
│  UI Application (.NET 10 + AvaloniaUI)   │
│  - Historial de clipboard                │
│  - Búsqueda y filtrado                   │
│  - Configuración                         │
└────────────┬─────────────────────────────┘
             │ gRPC
┌────────────▼─────────────────────────────┐
│  Clipboard Daemon (C++)                  │
│  - Monitor X11/Wayland                   │
│  - Captura de eventos                    │
└──────────────────────────────────────────┘
             │
┌────────────▼─────────────────────────────┐
│  ML Services (ONNX Runtime)              │
│  - OCR (PaddleOCR)                       │
│  - Búsqueda Semántica (BERT)             │
│  - Clasificación                         │
└──────────────────────────────────────────┘
             │
┌────────────▼─────────────────────────────┐
│  SQLite Database                         │
│  - Historial completo                    │
│  - Índices FTS5                          │
└──────────────────────────────────────────┘
```

## 📋 Requisitos

### Dependencias de Sistema (Arch Linux)
```bash
base-devel cmake pkgconf libx11 libxfixes wayland protobuf grpc
```

### Dependencias de Desarrollo
- **C++**: CMake, protobuf-compiler, grpc, libx11-dev, libxfixes-dev, wayland-dev
- **.NET**: .NET 10 SDK, AvaloniaUI 11.x
- **ML**: ONNX Runtime, modelos PaddleOCR y BERT

## 🚀 Instalación

### Desde AUR (Arch Linux)
```bash
yay -S clipboard-smart-manager
```

### Desde Fuente

#### Daemon (C++)
```bash
cd daemon
mkdir -p build && cd build
cmake ..
cmake --build . --config Release
sudo cmake --install .
```

#### Aplicación UI (.NET)
```bash
cd net-clipboard-manager
dotnet build -c Release
```

### Scripts de Instalación
```bash
# Instalar dependencias de C++
./install-cpp.sh

# Instalar dependencias de .NET
./install-net.sh

# Configurar para Hyprland/Wayland
./scripts/setup-wayland.sh
```

## ⚙️ Configuración

### Iniciar el Daemon
```bash
# Con systemd
systemctl --user start clipboard-daemon
systemctl --user enable clipboard-daemon

# Manualmente
clipboard-daemon
```

### Iniciar la Aplicación
```bash
# Construida desde fuente
cd net-clipboard-manager
dotnet run -c Release

# O si está instalada por AUR
clipboard-manager
```

## 📊 Objetivos de Rendimiento

- **Captura de clipboard**: < 10ms
- **Apertura de UI**: < 100ms
- **Búsqueda de texto**: < 50ms (1000 items)
- **Búsqueda semántica**: < 200ms (1000 items)
- **OCR (background)**: < 1s por imagen
- **RAM idle**: < 50MB
- **RAM con 1000 items**: < 200MB

## 🔧 Desarrollo

### Estructura del Proyecto
```
clipboard-smart-manager/
├── daemon/                    # Daemon en C++
│   ├── src/                  # Código fuente
│   ├── proto/                # Definiciones protobuf
│   └── protocols/            # Protocolos Wayland
├── net-clipboard-manager/    # Aplicación .NET
│   ├── ClipboardManager.App/ # Interfaz AvaloniaUI
│   ├── ClipboardManager.Core/ # Lógica principal
│   ├── ClipboardManager.ML/  # Servicios ML
│   └── ClipboardManager.Daemon.Client/ # Cliente gRPC
├── models/                   # Modelos ML
│   ├── bert/                # Modelos BERT
│   └── paddleocr/           # Modelos OCR
└── scripts/                 # Scripts de instalación
```

### Compilar en Modo Debug
```bash
# C++ Daemon
cd daemon
mkdir build && cd build
cmake -DCMAKE_BUILD_TYPE=Debug ..
make

# .NET App
cd net-clipboard-manager
dotnet build
```

### Tests
```bash
cd net-clipboard-manager
dotnet test
```

## 📝 Licencia

Este proyecto está licenciado bajo la **Apache License 2.0**. Consulta [LICENSE](LICENSE) para más detalles.

## 🤝 Contribución

Las contribuciones son bienvenidas. Por favor:

1. Fork el proyecto
2. Crea una rama para tu feature (`git checkout -b feature/AmazingFeature`)
3. Commit tus cambios (`git commit -m 'Add some AmazingFeature'`)
4. Push a la rama (`git push origin feature/AmazingFeature`)
5. Abre un Pull Request

## 📚 Documentación Adicional

- [Diseño Técnico](docs/design.md) - Arquitectura detallada del proyecto
- [Requisitos](docs/requirements.md) - Especificación completa de features
- [Tasks](docs/tasks.md) - Roadmap y tareas pendientes

## 🐛 Issues y Reportes

Si encuentras un bug o tienes una sugerencia, por favor abre un [issue](../../issues).

## 📞 Contacto

Para preguntas o sugerencias, contacta al equipo de desarrollo.

---

**Nota**: Este proyecto está en desarrollo activo. Algunas features pueden cambiar.
