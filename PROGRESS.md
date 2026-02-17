# Clipboard Manager - Progress

## ✅ Completado

### 1. Arquitectura Base
- [x] Solución .NET 10 con 5 proyectos
- [x] ClipboardManager.App (Avalonia UI)
- [x] ClipboardManager.Core (lógica de negocio)
- [x] ClipboardManager.Data (SQLite + repositorios)
- [x] ClipboardManager.ML (OCR con Tesseract + Embeddings)
- [x] ClipboardManager.Daemon.Client (gRPC client)

### 2. Base de Datos
- [x] SQLite con schema optimizado
- [x] FTS5 para búsqueda de texto completo
- [x] Thread-safe con ClipboardDbContextFactory
- [x] Soporte para embeddings (columna BLOB)
- [x] Deduplicación por hash
- [x] Manual FTS updates (sin triggers para evitar corrupción)

### 3. Daemon C++ (Wayland/X11)
- [x] Detección automática de Wayland/X11
- [x] WaylandMonitor con wlr-data-control
- [x] X11Monitor con Xlib
- [x] gRPC server con streaming
- [x] CMake + Ninja build system
- [x] Shutdown limpio con poll() timeout
- [x] **PROBADO**: Funciona en Hyprland

### 4. OCR (Tesseract CLI)
- [x] TesseractOcrService con CLI wrapper
- [x] Modelos tessdata_best (eng + spa) - 28MB
- [x] OcrQueueService para procesamiento background
- [x] Actualización automática de UI cuando OCR completa
- [x] Post-procesamiento para limpiar artefactos de iconos
- [x] PSM 3 + OEM 1 (LSTM) para mejor precisión
- [x] **PROBADO**: Reconoce ñ, acentos correctamente

### 5. UI (Avalonia)
- [x] MainWindow con lista de items
- [x] Búsqueda en tiempo real (FTS5)
- [x] Preview de texto e imágenes
- [x] Click en item para copiar
- [x] Botón "📝 Copiar texto" para OCR
- [x] Botón "🗑️ Eliminar" por item
- [x] Botón "🗑️ Limpiar todo" en barra de búsqueda
- [x] HotkeyService (Ctrl+Shift+V para mostrar/ocultar)
- [x] Texto OCR se muestra en metadata con tooltip
- [x] Texto OCR limitado a 1 línea con MaxHeight=30px
- [x] **PROBADO**: UI actualiza en tiempo real

### 6. Clipboard UTF-8 (Wayland)
- [x] Problema identificado: Avalonia no maneja UTF-8 correctamente en Wayland
- [x] Solución: Usar `wl-copy` directamente desde C#
- [x] CopyItemAsync usa wl-copy para texto
- [x] CopyOcrTextAsync usa wl-copy para texto OCR
- [x] StandardInputEncoding = UTF8 explícito
- [x] **PROBADO**: ñ, acentos se copian correctamente

### 7. Machine Learning - Embeddings Semánticos
- [x] EmbeddingService con ONNX Runtime
- [x] Modelo all-MiniLM-L6-v2 (384 dimensiones)
- [x] Generación automática de embeddings al guardar items
- [x] Búsqueda semántica por similitud coseno
- [x] Búsqueda híbrida (FTS5 30% + Semántica 70%)
- [x] SearchRepository con SemanticSearchAsync y HybridSearchAsync
- [x] Integrado en MainWindowViewModel con SearchMode
- [x] **PROBADO**: ONNX Runtime carga correctamente con Microsoft.ML.OnnxRuntime 1.20.1

### 8. Scripts de Instalación
- [x] scripts/download-models.sh (Tesseract tessdata_best)
- [x] scripts/download-ml-models.sh (Embedding models)
- [x] scripts/install-deps.sh (detecta Arch/Ubuntu/Fedora)
- [ ] scripts/setup-wayland.sh (pendiente actualizar)

## 🚧 En Progreso

### 9. Syntax Highlighting
- [ ] Agregar AvaloniaEdit o ColorCode
- [ ] Detectar lenguaje de programación automáticamente
- [ ] Mostrar código con colores en preview
- [ ] Soportar 15+ lenguajes
- [ ] Tema dark/light

### 10. Mejoras de UI
- [ ] Toggle para cambiar SearchMode (Text/Semantic/Hybrid)
- [ ] Filtros por tipo de contenido (Code, Text, Image, URL)
- [ ] Ordenamiento (fecha, tipo, tamaño)
- [ ] Exportar historial (JSON, CSV)
- [ ] Configuración de OCR (idiomas, precisión)
- [ ] Settings window

## 📋 Pendiente

### 11. Instalador y Distribución
- [ ] Script de instalación completo (`install.sh`)
- [ ] Systemd service para daemon
- [ ] Desktop entry (.desktop file)
- [ ] Empaquetado (AppImage, .deb, .rpm)
- [ ] Auto-start on login

### 12. Documentación
- [ ] README.md completo con screenshots
- [ ] Guía de instalación (Arch, Ubuntu, Fedora)
- [ ] Guía de uso
- [ ] Arquitectura técnica
- [ ] Troubleshooting

### 13. Testing
- [ ] Tests unitarios (Core, Data)
- [ ] Tests de integración (Daemon, OCR)
- [ ] Tests de UI (Avalonia)
- [ ] Coverage > 70%

## 🐛 Bugs Conocidos

### 1. Modelo ML no detecta SQL, JSON, XML
- **Problema**: El modelo actual (philomath-1209/programming-language-identification) solo soporta 26 lenguajes y no incluye SQL, JSON, XML, YAML
- **Impacto**: Estos lenguajes muy comunes no se detectan correctamente
- **Solución propuesta**: 
  - Opción 1: Entrenar modelo custom con dataset que incluya SQL, JSON, XML, YAML, TOML, etc.
  - Opción 2: Usar modelo pre-entrenado más completo (buscar en HuggingFace)
  - Opción 3: Fine-tune el modelo actual con ejemplos adicionales
- **Prioridad**: ALTA (afecta experiencia de usuario)

## 📊 Estadísticas

- **Líneas de código**: ~3500+ (C# + C++)
- **Proyectos**: 5 (.NET) + 1 (C++ daemon)
- **Dependencias principales**: 
  - Avalonia 11.x
  - SQLite + Dapper
  - gRPC
  - Tesseract 5.5.2
  - wl-clipboard (Wayland)
  - SixLabors.ImageSharp

## 🎯 Próximos Pasos (Prioridad)

### Sprint 4: Syntax Highlighting (Estimado: 6-8 horas)
**Objetivo**: Mostrar código con colores

**Tareas**:
1. Agregar paquete AvaloniaEdit
2. Crear `CodePreviewControl.axaml`
3. Detectar lenguaje automáticamente usando ClassificationService
4. Aplicar highlighting con AvaloniaEdit
5. Integrar en MainWindow
6. Tema dark consistente con UI

**Archivos**:
- `src/ClipboardManager.App/Controls/CodePreviewControl.axaml` (nuevo)
- `src/ClipboardManager.App/Controls/CodePreviewControl.axaml.cs` (nuevo)
- `src/ClipboardManager.App/Views/MainWindow.axaml` (modificar)
- `src/ClipboardManager.App/ClipboardManager.App.csproj` (agregar paquete)

### Sprint 5: UI Toggle para SearchMode (Estimado: 2-3 horas)
**Objetivo**: Permitir al usuario elegir tipo de búsqueda

**Tareas**:
1. Agregar ComboBox o RadioButtons en MainWindow
2. Bind a SearchMode property
3. Mostrar indicador visual del modo activo
4. Deshabilitar modos si embeddings no disponibles

**Archivos**:
- `src/ClipboardManager.App/Views/MainWindow.axaml` (modificar)

### Sprint 6: Instalador y Distribución (Estimado: 12-16 horas)

#### 1. Script de Instalación Completo
**Tareas**:
- Detectar distro (Arch, Ubuntu, Fedora)
- Instalar dependencias automáticamente
- Compilar daemon
- Descargar modelos
- Configurar systemd service
- Crear desktop entry

**Archivos**:
- `install.sh` (nuevo)
- `scripts/install-deps.sh` (actualizar)
- `clipboard-manager.service` (nuevo)
- `clipboard-manager.desktop` (nuevo)

#### 2. Empaquetado
**Tareas**:
- Crear AppImage
- Crear .deb package
- Crear .rpm package
- CI/CD con GitHub Actions

## 📝 Notas Técnicas

### Decisiones de Arquitectura
1. **Tesseract CLI** en lugar de ONNX para OCR (más simple, mejor soporte UTF-8)
2. **wl-copy** para clipboard en Wayland (Avalonia tiene bugs UTF-8)
3. **Manual FTS updates** en lugar de triggers (evita corrupción DB)
4. **Background OCR** con OcrQueueService (no bloquea UI)
5. **ClipboardDbContextFactory** con semaphore (thread-safe)
6. **ONNX Runtime 1.20.1** para embeddings (versión 1.24.1 tenía problemas de carga)
7. **Búsqueda híbrida** por defecto (30% FTS5 + 70% semántica)

### Optimizaciones Implementadas
1. SQLite WAL mode + cache 64MB
2. FTS5 para búsqueda full-text
3. Índices en timestamp, type, source_app
4. Lazy loading de imágenes
5. ObservableCollection para UI reactiva

### Seguridad
1. AES-256-GCM para passwords
2. SHA256 para deduplicación
3. Master key en archivo con permisos 600

---

**Última actualización**: 2026-02-15 00:00
**Estado general**: ✅ **APLICACIÓN FUNCIONAL CON OCR Y BÚSQUEDA SEMÁNTICA**
