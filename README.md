# Clipboard Smart Manager

Un gestor de portapapeles inteligente para Linux con historial infinito, búsqueda semántica, OCR automático y gestión segura de contraseñas.

## 🎯 Características Principales

- **Captura Automática**: Monitorea cambios del portapapeles en tiempo real
- **Historial Infinito**: Almacena todo tu historial de portapapeles de forma segura
- **Búsqueda Semántica**: Busca por significado, no solo por palabras clave (con BERT)
- **OCR Automático**: Extrae texto de imágenes automáticamente (PaddleOCR)
- **Gestión de Contraseñas**: Detección y protección de datos sensibles
- **Soporte X11/Wayland**: Funciona en cualquier entorno de escritorio Linux
- **Alto Rendimiento**: Nativamente compilado en C++ para máxima eficiencia
- **Interfaz Moderna**: UI nativa con GTK4 y gtkmm
- **Base de Datos SQLite**: Almacenamiento eficiente con búsqueda full-text (FTS5)

## 🏗️ Arquitectura

```
┌────────────────────────────────────────────┐
│  UI Application (C++ + GTK4/gtkmm)         │
│  - Historial de clipboard                  │
│  - Búsqueda y filtrado                     │
│  - Configuración                           │
└────────────┬─────────────────────────────┘
             │ gRPC
┌────────────▼─────────────────────────────┐
│  Clipboard Daemon (C++)                  │
│  - Monitor X11/Wayland                   │
│  - Captura de eventos                    │
└──────────────────────────────────────────┘
             │
┌────────────▼─────────────────────────────┐
│  ML Services (C++ + ONNX Runtime)        │
│  - OCR (Tesseract + PaddleOCR)           │
│  - Búsqueda Semántica (BERT)             │
│  - Clasificación con OpenCV              │
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
base-devel cmake pkg-config gtk4 gtkmm-4.0 sqlite tesseract opencv onnxruntime protobuf grpc
```

### Dependencias de Desarrollo
- **Compilador**: GCC/Clang con soporte C++20
- **Build**: CMake 3.20+, protobuf-compiler, grpc
- **UI**: GTK4, gtkmm-4.0
- **ML**: ONNX Runtime, Tesseract, OpenCV

## 🚀 Instalación

### Desde AUR (Arch Linux)
```bash
yay -S clipboard-smart-manager
```

### Desde Fuente (C++ 100%)
```bash
# Compilar Daemon
cd daemon
mkdir -p build && cd build
cmake -DCMAKE_BUILD_TYPE=Release ..
cmake --build .
sudo cmake --install .
cd ../..

# Compilar Aplicación UI
cd clipboard-manager
mkdir -p build && cd build
cmake -DCMAKE_BUILD_TYPE=Release ..
cmake --build .
sudo cmake --install .
```

### Scripts de Instalación
```bash
# Instalar dependencias de C++
./install-cpp.sh

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
cd clipboard-manager
./build/clipboard-manager

# Instalar sistema (después de cmake --install)
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
├── daemon/                  # Daemon backend en C++
│   ├── src/                # Código fuente
│   ├── proto/              # Definiciones protobuf
│   └── protocols/          # Protocolos Wayland
├── clipboard-manager/       # Aplicación UI en C++
│   ├── src/                # Código fuente
│   │   ├── ui/            # Componentes UI (GTK4/gtkmm)
│   │   ├── database/      # Gestión de base de datos
│   │   ├── ml/            # Servicios ML
│   │   ├── services/      # Lógica de negocio
│   │   └── grpc/          # Cliente gRPC
│   └── assets/            # Recursos gráficos
├── models/                 # Modelos ML pre-entrenados
│   ├── bert/              # Modelos BERT
│   └── paddleocr/         # Modelos OCR
└── scripts/               # Scripts de instalación
```

### Compilar en Modo Debug
```bash
# C++ Daemon
cd daemon
mkdir build && cd build
cmake -DCMAKE_BUILD_TYPE=Debug ..
make
cd ../..

# C++ Application
cd clipboard-manager
mkdir build && cd build
cmake -DCMAKE_BUILD_TYPE=Debug ..
make
```

### Tests
```bash
# Compilar con tests
cd [daemon|clipboard-manager]
mkdir build && cd build
cmake -DCMAKE_BUILD_TYPE=Debug -DENABLE_TESTS=ON ..
make test
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
