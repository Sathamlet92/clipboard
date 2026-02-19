# Clipboard Smart Manager

Gestor de portapapeles inteligente para Linux (X11/Wayland) escrito en C++ con búsqueda semántica, OCR local y monitore en tiempo real.

## 🎯 Características Principales

- **Captura en Tiempo Real**: Monitorea cambios del portapapeles (X11 y Wayland)
- **Historial Persistente**: Almacenamiento SQLite con índices FTS5
- **Búsqueda Semántica**: Búsqueda por significado usando embeddings ONNX/BERT
- **OCR Local**: Extrae texto de imágenes con Tesseract + OpenCV
- **Detección Automática**:
  - Lenguaje natural (idioma: en, es, fr, etc.)
  - Código y lenguaje de programación (C#, Python, Java, etc.)
  - Tipo de contenido (JSON, URLs, código, etc.)
- **Syntax Highlighting**: Para fragmentos de código detectados
- **Alto Rendimiento**: 100% C++ compilado a binario nativo
- **IPC gRPC**: Comunicación eficiente entre daemon y UI
- **Interfaz GTK4**: UI nativa moderna con gtkmm-4.0
- **Zero Python**: Sin dependencias de Python ni runtime externo

## 🏗️ Arquitectura

```
┌──────────────────────────────────────────────────┐
│  Clipboard Manager (UI)                          │
│  Language: C++20                                 │
│  Framework: GTK4 + gtkmm-4.0                    │
│                                                  │
│  ┌────────────────────────────────────────────┐ │
│  │ Presentation Layer                         │ │
│  │ - main_window (historial)                  │ │
│  │ - clipboard_item_widget                    │ │
│  └────────────────────────────────────────────┘ │
│                    │                             │
│  ┌────────────────▼────────────────────────────┐ │
│  │ Business Logic Layer                       │ │
│  │ - ClipboardService (search, filter)        │ │
│  │ - SearchService (full-text + embeddings)   │ │
│  │ - LanguageDetector (fastText)              │ │
│  │ - OCRService (Tesseract + OpenCV)          │ │
│  │ - EmbeddingService (ONNX Runtime)          │ │
│  └────────────────┬────────────────────────────┘ │
│                   │ gRPC over unix socket        │
└───────────────────┼──────────────────────────────┘
                    │
┌───────────────────▼──────────────────────────────┐
│  Clipboard Daemon                                │
│  Language: C++20                                 │
│  Socket: /tmp/clipboard-daemon.sock (default)   │
│                                                  │
│  ┌────────────────────────────────────────────┐ │
│  │ Monitor Layer                              │ │
│  │ - X11Monitor (XSelectionNotify)            │ │
│  │ - WaylandMonitor (wlr-data-control v1)    │ │
│  │ - ClipboardMonitor (factory pattern)       │ │
│  └────────────────┬────────────────────────────┘ │
│                   │                              │
│  ┌────────────────▼────────────────────────────┐ │
│  │ gRPC Service Layer                         │ │
│  │ - GrpcServer (protobuf messages)           │ │
│  │ - Async event broadcasting                 │ │
│  └────────────────────────────────────────────┘ │
└────────────────────────────────────────────────┘
         │
┌────────▼────────────────────────────────┐
│  Shared Data Store                      │
│ - SQLite Database                       │
│ - FTS5 full-text index                  │
│ - Embedding vectors cache               │
└─────────────────────────────────────────┘
```

## 🔄 Flujo de Operación

1. **Captura**: Daemon monitorea clipboard (X11/Wayland)
2. **Clasificación**: Al detectar cambio, analiza el tipo:
   - **Texto**: Full-text indexable
   - **Código**: Detecta lenguaje (Python, C#, JS, etc.)
   - **Imagen**: OCR con Tesseract
   - **JSON/URL**: Patrones especializados
3. **Análisis ML**:
   - Embeddings ONNX/BERT para búsqueda semántica
   - LanguageDetector ONNX (idioma natural + lenguajes de programación)
   - Syntax highlighting para código
4. **Almacenamiento**: Guarda en SQLite con:
   - Contenido original
   - OCR extraído (si es imagen)
   - Lenguaje detectado
   - Embeddings cacheados
   - Tipo de contenido
5. **Notificación**: Notifica a UI vía gRPC
6. **Visualización**: UI recibe y muestra:
   - Vista previa con syntax highlighting si es código
   - Badge de lenguaje
   - Favicon de tipo
7. **Búsqueda**: Usuario busca por:
   - Texto literal (FTS5)
   - Significado (embeddings)
   - Lenguaje (C#, Python, etc.)
   - Tipo (código, imagen, texto)

## � Dependencias

### Runtime 
```bash
# Arch Linux
sudo pacman -S \
    libx11              # X11 clipboard support \
    libxfixes           # XFixes extension \
    wayland             # Wayland support \
    gtk4                # GTK4 library \
    sqlite              # Database \
    tesseract           # OCR engine \
    onnxruntime         # ML model inference \
    protobuf            # Message serialization \
    grpc                # RPC framework
```

### Build
```bash
# Arch Linux
sudo pacman -S \
    base-devel          # gcc, g++, make, etc \
    cmake>=3.20         # Build system \
    pkg-config          # Dependency management \
    gtkmm-4.0-devel     # GTK C++ bindings \
    opencv              # Image processing \
    grpc                # gRPC compiler \
    protobuf            # Protocol buffers
```

### C++ Language Requirements
- **Standard**: C++20 (CMakeLists.txt: `set(CMAKE_CXX_STANDARD 20)`)
- **Compiler**: GCC 11+ o Clang 13+ (con soporte C++20)
- **CMake**: 3.20+

## 📚 Stack Tecnológico

| Capa | Librería | Propósito |
|------|----------|-----------|
| **UI** | GTK4 + gtkmm-4.0 | Interfaz gráfica |
| **IPC** | gRPC + Protobuf | Comunicación daemon-UI |
| **Captura** | libX11 + libwayland | Monitoreo de clipboard |
| **Base de Datos** | SQLite 3 | Almacenamiento persistente |
| **Búsqueda** | FTS5 | Full-text search |
| **ML Inference** | ONNX Runtime | Embeddings + LanguageDetector |
| **Detección de Lenguaje** | ONNX Model | Idioma natural + Lenguajes de programación |
| **OCR** | Tesseract | Extracción de texto de imágenes |
| **Syntax Highlighting** | Custom | Resaltado para código detectado |
| **Procesamiento** | OpenCV | Procesamiento de imágenes |

## 🚀 Instalación

### Opción 1: Desde AUR (Recomendado para Arch Linux)

```bash
yay -S clipboard-smart-manager
```

O si prefieres construir desde AUR:
```bash
git clone https://aur.archlinux.org/clipboard-smart-manager.git
cd clipboard-smart-manager
makepkg -si
```

### Opción 2: Desde Fuente

**Instalación de dependencias:**
```bash
./install-cpp.sh
```

**Compilar Daemon:**
```bash
cd daemon
mkdir -p build
cd build
cmake -DCMAKE_BUILD_TYPE=Release ..
cmake --build .
sudo cmake --install .
```

**Compilar Aplicación UI:**
```bash
cd clipboard-manager
mkdir -p build
cd build
cmake -DCMAKE_BUILD_TYPE=Release ..
cmake --build .
sudo cmake --install .
```

**Descargar modelos ML (opcional para búsqueda semántica):**
```bash
./scripts/download-models.sh
```

### Opción 3: Hyprland Integration (Automático)
```bash
./scripts/setup-wayland.sh
./scripts/configure-hyprland-clipboard.sh
```

## ⚙️ Primer Uso

### 1. Iniciar el Daemon

```bash
# Con systemd (recomendado)
systemctl --user start clipboard-daemon
systemctl --user enable clipboard-daemon    # Auto-inicio

# O manualmente
clipboard-daemon &
```

Verifica que está corriendo:
```bash
lsof -i :50051
# O busca el socket:
ls -la /tmp/clipboard-daemon.sock
```

### 2. Iniciar la Aplicación UI

```bash
# Si está instalada por AUR o sudo make install
clipboard-manager

# O desde fuente
./clipboard-manager/build/clipboard-manager
```

###  🔍 Ejemplos de Detección

**Detección Automática de Contenido:**

```
Entrada: "print('Hello')"
→ Tipo: Código
→ Lenguaje: Python
→ Características: Resaltado de sintaxis, badge "py"

Entrada: "SELECT * FROM users WHERE id=1"
→ Tipo: Código
→ Lenguaje: SQL
→ Indexado para búsqueda semántica

Entrada: {"name": "John", "age": 30}
→ Tipo: Código
→ Lenguaje: JSON
→ Validación de estructura

Entrada: [Screenshot de pantalla]
→ Tipo: Imagen
→ OCR: Tesseract extrae texto
→ Embeddings: Se genera búsqueda semántica
```

**Búsqueda Inteligente:**

```
Usuario en barra: "cómo imprimir en python"
→ Busca items con código Python relevante
→ Usa embeddings para significado
→ Resultado: Fragmento "print(...)

Usuario: "select query"
→ Busca SQL o queries
→ FTS5 para coincidencia exacta
→ Embeddings para similitud
```
```json
{
  "history_limit": 10000,
  "enable_ocr": true,
  "enable_embeddings": false,
  "ocr_language": "eng+spa",
  "daemon_socket": "unix:///tmp/clipboard-daemon.sock"
}
```

**Notas:**
- `enable_embeddings`: Requiere descargar modelos con `./scripts/download-models.sh`
- `ocr_language`: Códigos Tesseract (eng, spa, fra, deu, etc.)
- Primera ejecución descarga modelos automáticamente si es necesario

## ⚡ Rendimiento

### Objetivos

| Operación | Target | Notas |
|-----------|--------|-------|
| Captura de clipboard | < 10ms | X11: ≈5ms, Wayland: ≈8ms |
| Almacenamiento en DB | < 5ms | SQLite local |
| Notificación gRPC | < 2ms | Unix socket |
| Apertura de UI | < 100ms | GTK4 + carga de historial |
| Búsqueda FTS5 | < 50ms | 1000 items |
| Búsqueda semántica | < 200ms | Incluye generación de embedding |
| OCR por imagen | < 1s | Tesseract, multithreaded |
| **RAM Idle** | < 50MB | Daemon + UI sin items |
| **RAM con 1000 items** | < 200MB | Con embeddings cacheados |

### Optimizaciones

- **Lazy Loading**: Modelos ML se cargan solo si se usan
- **Threading**: OCR y embeddings en threads separados
- **Caching**: Embeddings cachean resultados de búsquedas
- **Async gRPC**: Notificaciones no-blocking
- **FTS5 Indexes**: Búsqueda full-text optimizada
- **ONNX Runtime**: Inference GPU-compatible (CPU fallback)

## 🔧 Desarrollo

### Estructura del Proyecto

```
clipboard-smart-manager/
│
├── daemon/                          # Backend: Monitoreo y gRPC server
│   ├── src/
│   │   ├── main.cpp                # Entry point
│   │   ├── clipboard_monitor.cpp   # Factory de monitores (X11/Wayland)
│   │   ├── wayland_monitor.cpp     # Monitor Wayland (wlr-data-control)
│   │   ├── x11_monitor.cpp         # Monitor X11 (XSelection)
│   │   └── grpc_server.cpp         # Servidor gRPC asíncrono
│   ├── proto/
│   │   └── clipboard.proto         # Definiciones de mensajes protobuf
│   ├── protocols/
│   │   └── wlr-data-control...    # Protocolo Wayland externo
│   ├── CMakeLists.txt
│   ├── build.sh
│   └── systemd/
│       └── clipboard-daemon.service # Servicio systemd
│
├── clipboard-manager/               # Frontend: Interfaz GTK4 + servicios ML
│   ├── src/
│   │   ├── main.cpp                # Entry point
│   │   ├── app/
│   │   │   └── bootstrap.cpp       # Inicialización de aplicación
│   │   ├── ui/
│   │   │   ├── main_window.cpp     # Ventana principal (GTK4)
│   │   │   └── clipboard_item_widget.cpp  # Componentes de items
│   │   ├── database/
│   │   │   └── clipboard_db.cpp    # Operaciones SQLite
│   │   ├── services/
│   │   │   ├── clipboard_service.cpp  # Lógica de negocio
│   │   │   └── search_service.cpp     # Búsqueda (FTS5 + embeddings)
│   │   ├── ml/
│   │   │   ├── ocr_service.cpp     # Tesseract OCR
│   │   │   ├── embedding_service.cpp  # ONNX Runtime (BERT)
│   │   │   └── language_detector.cpp  # Detección de idioma
│   │   ├── grpc/
│   │   │   └── daemon_client.cpp   # Cliente gRPC al daemon
│   │   └── app_config.h            # Configuración global
│   ├── assets/                      # Icons, imágenes resources
│   ├── com.clipboard.manager.desktop # Entrada de aplicación
│   ├── CMakeLists.txt
│   ├── build.sh
│   └── app.manifest
│
├── models/                           # Modelos ML pre-entrenados
│   ├── bert/                        # Embedding model (ONNX)
│   └── paddleocr/                   # OCR model (por si se implementa)
│
├── scripts/
│   ├── install-cpp.sh               # Instalador de deps C++
│   ├── download-models.sh           # Descarga modelos ML
│   ├── setup-wayland.sh             # Config Wayland
│   ├── configure-hyprland-clipboard.sh  # Config Hyprland
│   └── integration/                 # Scripts adicionales
│
├── aur/
│   ├── PKGBUILD                     # Definición AUR
│   └── .SRCINFO                     # Metadatos AUR
│
├── LICENSE                          # Apache 2.0
├── README.md                        # Este archivo
├── install-cpp.sh
└── install-net.sh                   # (Deprecated: soporte .NET antiguo)
```

### Compilar en Modo Debug

```bash
# Daemon
cd daemon
mkdir build && cd build
cmake -DCMAKE_BUILD_TYPE=Debug -DENABLE_TESTS=ON ..
make
make test  # Si hay tests
cd ../..

# Application
cd clipboard-manager
mkdir build && cd build
cmake -DCMAKE_BUILD_TYPE=Debug -DENABLE_TESTS=ON ..
make
make test
```

### Compilar y Ejecutar

```bash
# Terminal 1: Daemon
cd daemon/build
./clipboard-daemon

# Terminal 2: UI
cd clipboard-manager/build
./clipboard-manager
```

### Puntos de Entrada Principales

**Daemon (`daemon/src/main.cpp`):**
- Inicializa monitor del clipboard (X11 o Wayland automático)
- Inicia servidor gRPC en socket unix
- Escucha cambios del clipboard y notifica vía gRPC

**Application (`clipboard-manager/src/main.cpp`):**
- Inicializa GTK4
- Conecta a daemon vía gRPC
- Carga servicios ML (lazy initialization)
- Renderiza UI del historial

### Variables de Entorno

```bash
CLIPBOARD_DAEMON_SOCKET=/tmp/clipboard-daemon.sock  # Default socket
HOME/.clipboard-manager/models                       # Ubicación modelos ML
HOME/.clipboard-manager/clipboard.db                 # Base de datos
```

## � Uso

### Interfaz gráfica

**Vista principal:**
- Historial de clipboard items (más recients primero)
- Vista previa de contenido (texto/imagen)
- Barra de búsqueda (FTS5 + embeddings)

**Búsqueda:**
- Escribe para búsqueda literal full-text
- Usa embeddings para búsqueda por significado
- Filtro por tipo (texto, imagen, código, URL)

**Atajos:**
- `Ctrl+C` en item: Copia al clipboard
- `Ctrl+D`: Elimina item
- `Ctrl+L`: Focus búsqueda

### Daemon

Ejecuta en background sin UI. Accesible via:
```bash
# Ver estado
systemctl --user status clipboard-daemon

# Ver logs
journalctl --user -u clipboard-daemon -f

# Detener
systemctl --user stop clipboard-daemon
```

## 🔍 Solución de Problemas

### Daemon no inicia

```bash
# Verifica dependencias
ldd /usr/bin/clipboard-daemon

# Conecta manualmente para error detallado
clipboard-daemon 2>&1
```

### UI no encuentra el daemon

```bash
# Verifica socket
ls -la /tmp/clipboard-daemon.sock

# Reinicia ambos
systemctl --user restart clipboard-daemon
pkill clipboard-manager
clipboard-manager
```

### OCR no funciona

```bash
# Verifica tesseract
tesseract --version

# Verifica datos de idioma
tessdata_best-glob

# Si falta español
sudo pacman -S tesseract-data-spa
```

### Búsqueda semántica lenta

- Primera ejecución: Genera embeddings (puede tomar minutos)
- Resultados subsecuentes: Cachean en BD
- Si sigue siendo lenta: Reduce historial limit en config.json

## ⚠️ Limitaciones Actuales

- ⚠️ **Windows/macOS**: No soportados (dependencias X11/Wayland)
- ⚠️ **Clipboard Privado**: Firefox/Chrome encrypta clipboard en Wayland
- ⚠️ **Historial 100% seguro**: Se almacena en SQLite local sin encripción
- ⚠️ **Búsqueda semántica**: Requiere descargar modelos (~500MB)
- ⚠️ **Modelos offline**: ONNX sin conexión a internet

## 🗺️ Roadmap

- [ ] Encripción de base de datos
- [ ] Sincronización entre dispositivos
- [ ] Ignorelist avanzado
- [ ] Caché de clipboard en memoria para más rapidez
- [ ] Integración con pass/KeePass
- [ ] Webhooks personalizados

## 📝 Licencia

Este proyecto está licenciado bajo **Apache License 2.0**. 

Ver [LICENSE](LICENSE) para detalles completos.

Si usas este proyecto en tu trabajo, apreciaríamos una mención.

## 🙏 Créditos

### Tecnologías Utilizadas

- [GTK4](https://www.gtk.org/) - Interfaz gráfica
- [gtkmm](https://www.gtkmm.org/) - Bindings de C++
- [gRPC](https://grpc.io/) - RPC framework
- [Protocol Buffers](https://developers.google.com/protocol-buffers) - Serialización
- [SQLite](https://www.sqlite.org/) - Base de datos
- [ONNX Runtime](https://onnxruntime.ai/) - ML inference
- [Tesseract OCR](https://github.com/UB-Mannheim/tesseract) - OCR engine
- [OpenCV](https://opencv.org/) - Visión por computadora
- [libX11](https://www.x.org/) - X11 clipboard
- [libwayland](https://wayland.freedesktop.org/) - Wayland support

## 👤 Autor

Sathamlet92

---

**Nota**: Este proyecto está bajo desarrollo activo. Algunas características pueden cambiar sin previo aviso.
