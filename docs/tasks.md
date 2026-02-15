# Clipboard Manager - Plan de Implementación

## Fase 1: MVP (4-6 semanas)

### Sprint 1: Infraestructura Base (1 semana)

#### Task 1.1: Setup del Proyecto
**Estimación**: 4 horas

- [ ] Crear solución .NET 10
- [ ] Configurar proyectos:
  - ClipboardManager.App (AvaloniaUI)
  - ClipboardManager.Core (lógica)
  - ClipboardManager.Data (SQLite)
  - ClipboardManager.ML (ONNX)
  - ClipboardManager.Daemon.Client (gRPC)
- [ ] Configurar .editorconfig y análisis de código
- [ ] Setup CI/CD básico (GitHub Actions)

**Criterios de Aceptación**:
- ✅ Solución compila sin errores
- ✅ Estructura de carpetas correcta
- ✅ NuGet packages instalados

#### Task 1.2: Database Schema
**Estimación**: 6 horas

- [ ] Crear esquema SQLite
- [ ] Implementar migraciones
- [ ] Configurar FTS5
- [ ] Crear índices
- [ ] Tests unitarios para schema

**Archivos**:
- `ClipboardManager.Data/ClipboardDbContext.cs`
- `ClipboardManager.Data/Migrations/001_Initial.sql`

**Criterios de Aceptación**:
- ✅ DB se crea correctamente
- ✅ FTS5 funciona
- ✅ Índices optimizados

#### Task 1.3: Modelos de Datos
**Estimación**: 4 horas

- [ ] Implementar `ClipboardItem`
- [ ] Implementar `Configuration`
- [ ] Implementar DTOs
- [ ] Validación de modelos

**Archivos**:
- `ClipboardManager.Core/Models/ClipboardItem.cs`
- `ClipboardManager.Core/Models/Configuration.cs`

#### Task 1.4: C++ Daemon - Estructura Base
**Estimación**: 8 horas

- [ ] Setup CMake
- [ ] Implementar interfaz `IClipboardMonitor`
- [ ] Configurar gRPC
- [ ] Definir protobuf
- [ ] Compilar y probar

**Archivos**:
- `daemon/CMakeLists.txt`
- `daemon/src/clipboard_monitor.h`
- `daemon/proto/clipboard.proto`

**Criterios de Aceptación**:
- ✅ Daemon compila
- ✅ gRPC server inicia
- ✅ Protobuf funciona

---

### Sprint 2: Clipboard Monitoring (1 semana)

#### Task 2.1: X11 Monitor
**Estimación**: 12 horas

- [ ] Implementar `X11Monitor`
- [ ] Detectar cambios de clipboard
- [ ] Leer contenido (texto, imágenes)
- [ ] Detectar aplicación de origen
- [ ] Manejo de errores

**Archivos**:
- `daemon/src/x11_monitor.cpp`
- `daemon/src/x11_monitor.h`

**Criterios de Aceptación**:
- ✅ Detecta cambios en < 10ms
- ✅ Lee texto correctamente
- ✅ Lee imágenes correctamente
- ✅ Identifica app de origen

#### Task 2.2: Wayland Monitor
**Estimación**: 16 horas

- [ ] Implementar `WaylandMonitor`
- [ ] Integrar wlr-data-control protocol
- [ ] Detectar cambios de clipboard
- [ ] Leer contenido
- [ ] Manejo de errores

**Archivos**:
- `daemon/src/wayland_monitor.cpp`
- `daemon/src/wayland_monitor.h`

**Criterios de Aceptación**:
- ✅ Funciona en Hyprland
- ✅ Detecta cambios correctamente
- ✅ Lee contenido correctamente

#### Task 2.3: gRPC Integration
**Estimación**: 8 horas

- [ ] Implementar gRPC server en daemon
- [ ] Implementar cliente en C#
- [ ] Stream de eventos
- [ ] Manejo de reconexión
- [ ] Tests de integración

**Archivos**:
- `daemon/src/grpc_server.cpp`
- `ClipboardManager.Daemon.Client/DaemonClient.cs`

**Criterios de Aceptación**:
- ✅ Eventos llegan a C# app
- ✅ Reconexión automática funciona
- ✅ No hay pérdida de eventos

---

### Sprint 3: Core Services (1 semana)

#### Task 3.1: ClipboardService
**Estimación**: 12 horas

- [ ] Implementar `ClipboardService`
- [ ] Procesar eventos del daemon
- [ ] Guardar items en DB
- [ ] Deduplicación básica (hash)
- [ ] Tests unitarios

**Archivos**:
- `ClipboardManager.Core/Services/ClipboardService.cs`

**Criterios de Aceptación**:
- ✅ Guarda items correctamente
- ✅ No duplicados exactos
- ✅ Performance < 50ms por item

#### Task 3.2: ClassificationService
**Estimación**: 10 horas

- [ ] Implementar clasificación por reglas
- [ ] Detectar tipo de contenido:
  - Texto plano
  - Código (lenguaje)
  - URL
  - Email
  - Teléfono
  - Ruta de archivo
- [ ] Tests con casos reales

**Archivos**:
- `ClipboardManager.Core/Services/ClassificationService.cs`

**Criterios de Aceptación**:
- ✅ Precisión > 90%
- ✅ Clasificación < 10ms
- ✅ Detecta lenguajes de código

#### Task 3.3: SecurityService
**Estimación**: 12 horas

- [ ] Implementar detección de passwords
- [ ] Heurística de passwords
- [ ] Encriptación AES-256-GCM
- [ ] Key management (keyring)
- [ ] Tests de seguridad

**Archivos**:
- `ClipboardManager.Core/Services/SecurityService.cs`
- `ClipboardManager.Data/Encryption/EncryptionManager.cs`

**Criterios de Aceptación**:
- ✅ Detecta passwords > 95%
- ✅ Encriptación segura
- ✅ Keys en keyring del sistema

#### Task 3.4: SearchService (Text Only)
**Estimación**: 8 horas

- [ ] Implementar búsqueda FTS5
- [ ] Búsqueda incremental
- [ ] Filtros (tipo, fecha, app)
- [ ] Paginación
- [ ] Tests de performance

**Archivos**:
- `ClipboardManager.Core/Services/SearchService.cs`
- `ClipboardManager.Data/Repositories/SearchRepository.cs`

**Criterios de Aceptación**:
- ✅ Búsqueda < 50ms (1000 items)
- ✅ Resultados relevantes
- ✅ Filtros funcionan

---

### Sprint 4: UI Básica (1.5 semanas)

#### Task 4.1: MainWindow
**Estimación**: 12 horas

- [ ] Diseñar layout en AXAML
- [ ] Implementar ViewModel
- [ ] Lista de items con virtual scrolling
- [ ] Preview básico
- [ ] Navegación con teclado

**Archivos**:
- `ClipboardManager.App/Views/MainWindow.axaml`
- `ClipboardManager.App/ViewModels/MainViewModel.cs`

**Criterios de Aceptación**:
- ✅ Ventana abre en < 100ms
- ✅ Lista fluida con 1000+ items
- ✅ Navegación con ↑↓ funciona

#### Task 4.2: SearchView
**Estimación**: 10 horas

- [ ] Barra de búsqueda
- [ ] Búsqueda incremental
- [ ] Filtros UI
- [ ] Highlighting de resultados
- [ ] Shortcuts (Ctrl+F)

**Archivos**:
- `ClipboardManager.App/Views/SearchView.axaml`
- `ClipboardManager.App/ViewModels/SearchViewModel.cs`

**Criterios de Aceptación**:
- ✅ Búsqueda sin lag
- ✅ Resultados actualizan mientras escribes
- ✅ Filtros funcionan

#### Task 4.3: PreviewControls
**Estimación**: 12 horas

- [ ] Preview de texto
- [ ] Preview de código (syntax highlighting)
- [ ] Preview de imágenes (thumbnail)
- [ ] Preview de URLs
- [ ] Manejo de passwords (ocultos)

**Archivos**:
- `ClipboardManager.App/Views/PreviewControls/TextPreview.axaml`
- `ClipboardManager.App/Views/PreviewControls/CodePreview.axaml`
- `ClipboardManager.App/Views/PreviewControls/ImagePreview.axaml`

**Criterios de Aceptación**:
- ✅ Previews claros y útiles
- ✅ Syntax highlighting funciona
- ✅ Passwords ocultos por defecto

#### Task 4.4: HotkeyService
**Estimación**: 8 hours

- [ ] Implementar hotkey global (Ctrl+Shift+V)
- [ ] Registrar/desregistrar hotkey
- [ ] Mostrar/ocultar ventana
- [ ] Configuración de hotkey

**Archivos**:
- `ClipboardManager.App/Services/HotkeyService.cs`

**Criterios de Aceptación**:
- ✅ Hotkey funciona globalmente
- ✅ Ventana aparece instantáneamente
- ✅ Hotkey configurable

#### Task 4.5: SettingsView
**Estimación**: 10 horas

- [ ] UI de configuración
- [ ] Tabs: General, Security, Performance, UI
- [ ] Guardar/cargar configuración
- [ ] Validación de settings

**Archivos**:
- `ClipboardManager.App/Views/SettingsView.axaml`
- `ClipboardManager.App/ViewModels/SettingsViewModel.cs`

**Criterios de Aceptación**:
- ✅ Todas las opciones configurables
- ✅ Cambios se aplican inmediatamente
- ✅ Validación funciona

---

### Sprint 5: ML Integration (1.5 semanas)

#### Task 5.1: Download ONNX Models
**Estimación**: 4 horas

- [ ] Script para descargar modelos
- [ ] PaddleOCR models (det + rec)
- [ ] BERT model (all-MiniLM-L6-v2)
- [ ] Verificar checksums
- [ ] Documentar proceso

**Archivos**:
- `scripts/download-models.sh`
- `docs/models.md`

#### Task 5.2: OcrService
**Estimación**: 16 horas

- [ ] Implementar `OcrService`
- [ ] Cargar modelos ONNX
- [ ] Preprocesamiento de imágenes
- [ ] Detección de texto
- [ ] Reconocimiento de texto
- [ ] Post-procesamiento
- [ ] Tests con imágenes reales

**Archivos**:
- `ClipboardManager.ML/OcrService.cs`
- `ClipboardManager.ML/Utils/ImagePreprocessor.cs`

**Criterios de Aceptación**:
- ✅ OCR < 1s por imagen típica
- ✅ Precisión > 90%
- ✅ Funciona con múltiples idiomas
- ✅ No bloquea UI

#### Task 5.3: Async OCR Processing
**Estimación**: 8 horas

- [ ] Background processing de OCR
- [ ] Queue de imágenes
- [ ] Progress tracking
- [ ] Actualizar DB con resultados
- [ ] Manejo de errores

**Archivos**:
- `ClipboardManager.Core/Services/OcrQueueService.cs`

**Criterios de Aceptación**:
- ✅ OCR no bloquea captura
- ✅ Queue procesa en orden
- ✅ Resultados se guardan correctamente

---

## Fase 2: Inteligencia (2-3 semanas)

### Sprint 6: Semantic Search (1 semana)

#### Task 6.1: SemanticSearchService
**Estimación**: 12 horas

- [ ] Implementar `SemanticSearchService`
- [ ] Cargar modelo BERT ONNX
- [ ] Implementar tokenizer
- [ ] Generar embeddings
- [ ] Cosine similarity
- [ ] Tests de precisión

**Archivos**:
- `ClipboardManager.ML/SemanticSearchService.cs`
- `ClipboardManager.ML/Utils/BertTokenizer.cs`

**Criterios de Aceptación**:
- ✅ Embeddings < 50ms
- ✅ Búsqueda semántica < 200ms
- ✅ Resultados relevantes

#### Task 6.2: Hybrid Search
**Estimación**: 8 horas

- [ ] Combinar búsqueda texto + semántica
- [ ] Ranking de resultados
- [ ] Configuración de pesos
- [ ] A/B testing

**Archivos**:
- `ClipboardManager.Core/Services/SearchService.cs` (update)

**Criterios de Aceptación**:
- ✅ Mejores resultados que solo texto
- ✅ Performance aceptable

#### Task 6.3: Background Embedding Generation
**Estimación**: 8 horas

- [ ] Generar embeddings al guardar
- [ ] Queue de procesamiento
- [ ] Actualizar DB
- [ ] Progress tracking

**Archivos**:
- `ClipboardManager.Core/Services/EmbeddingQueueService.cs`

---

### Sprint 7: Advanced Features (1 semana)

#### Task 7.1: Syntax Highlighting
**Estimación**: 10 horas

- [ ] Integrar librería de syntax highlighting
- [ ] Detectar lenguaje automáticamente
- [ ] Temas (dark/light)
- [ ] Preview mejorado

**Archivos**:
- `ClipboardManager.App/Views/PreviewControls/CodePreview.axaml` (update)

#### Task 7.2: Advanced Classification (ML.NET)
**Estimación**: 12 horas

- [ ] Entrenar modelo de clasificación
- [ ] Integrar ML.NET
- [ ] Mejorar detección de lenguaje
- [ ] Detección de idioma
- [ ] Tests de precisión

**Archivos**:
- `ClipboardManager.ML/ClassificationModel.cs`

#### Task 7.3: Smart Deduplication
**Estimación**: 8 horas

- [ ] Deduplicación semántica
- [ ] Perceptual hashing para imágenes
- [ ] Configuración de threshold
- [ ] Tests

**Archivos**:
- `ClipboardManager.Core/Services/DeduplicationService.cs`

---

### Sprint 8: Polish & Optimization (1 semana)

#### Task 8.1: Performance Optimization
**Estimación**: 12 horas

- [ ] Profiling de performance
- [ ] Optimizar queries SQL
- [ ] Optimizar UI rendering
- [ ] Reducir consumo de RAM
- [ ] Benchmarks

#### Task 8.2: UI/UX Improvements
**Estimación**: 10 horas

- [ ] Animaciones suaves
- [ ] Feedback visual
- [ ] Tooltips
- [ ] Keyboard shortcuts mejorados
- [ ] Accessibility

#### Task 8.3: Error Handling
**Estimación**: 8 horas

- [ ] Manejo global de errores
- [ ] Logging estructurado
- [ ] Error reporting UI
- [ ] Recovery automático

---

## Fase 3: Production Ready (1-2 semanas)

### Sprint 9: Testing & Documentation (1 semana)

#### Task 9.1: Unit Tests
**Estimación**: 16 horas

- [ ] Tests para todos los servicios
- [ ] Coverage > 80%
- [ ] Tests de performance
- [ ] Tests de seguridad

#### Task 9.2: Integration Tests
**Estimación**: 12 horas

- [ ] Tests end-to-end
- [ ] Tests de UI
- [ ] Tests de daemon
- [ ] Tests de gRPC

#### Task 9.3: Documentation
**Estimación**: 12 horas

- [ ] README completo
- [ ] Guía de instalación
- [ ] Guía de uso
- [ ] API documentation
- [ ] Troubleshooting guide

---

### Sprint 10: Deployment & Release (1 semana)

#### Task 10.1: Packaging
**Estimación**: 12 horas

- [ ] .deb package
- [ ] .rpm package
- [ ] AppImage
- [ ] Flatpak (opcional)

#### Task 10.2: Installation Scripts
**Estimación**: 8 horas

- [ ] Install script
- [ ] Uninstall script
- [ ] Systemd service
- [ ] Desktop entry

#### Task 10.3: CI/CD
**Estimación**: 10 horas

- [ ] GitHub Actions workflows
- [ ] Automated builds
- [ ] Automated tests
- [ ] Release automation

#### Task 10.4: Release v1.0
**Estimación**: 6 horas

- [ ] Release notes
- [ ] Changelog
- [ ] Tag version
- [ ] Publish packages
- [ ] Announce release

---

## Resumen de Estimaciones

### Fase 1: MVP
- Sprint 1: 22 horas (3 días)
- Sprint 2: 36 horas (4.5 días)
- Sprint 3: 42 horas (5 días)
- Sprint 4: 52 horas (6.5 días)
- Sprint 5: 28 horas (3.5 días)
**Total Fase 1**: 180 horas (~4.5 semanas a 40h/semana)

### Fase 2: Inteligencia
- Sprint 6: 28 horas (3.5 días)
- Sprint 7: 30 horas (3.75 días)
- Sprint 8: 30 horas (3.75 días)
**Total Fase 2**: 88 horas (~2 semanas)

### Fase 3: Production
- Sprint 9: 40 horas (5 días)
- Sprint 10: 36 horas (4.5 días)
**Total Fase 3**: 76 horas (~2 semanas)

**TOTAL PROYECTO**: 344 horas (~8.5 semanas a 40h/semana)

---

## Dependencias Críticas

### Bloqueantes
- Task 1.4 → Task 2.1, 2.2 (daemon base antes de monitors)
- Task 2.3 → Task 3.1 (gRPC antes de ClipboardService)
- Task 3.1 → Task 4.1 (service antes de UI)
- Task 5.1 → Task 5.2 (modelos antes de OCR)

### Parallelizables
- Task 2.1 y 2.2 (X11 y Wayland en paralelo)
- Task 3.2, 3.3, 3.4 (servicios independientes)
- Task 4.1, 4.2, 4.3 (vistas independientes)

---

## Riesgos y Mitigaciones

### Riesgo 1: Wayland wlr-data-control no funciona
**Probabilidad**: Media
**Impacto**: Alto
**Mitigación**: 
- Implementar X11 primero (funciona seguro)
- Fallback a polling en Wayland si es necesario
- Documentar limitaciones

### Riesgo 2: ONNX models muy lentos
**Probabilidad**: Baja
**Impacto**: Alto
**Mitigación**:
- Benchmarking temprano
- Model quantization (INT8)
- GPU acceleration si es necesario

### Riesgo 3: Hotkey global no funciona en Wayland
**Probabilidad**: Alta
**Impacto**: Medio
**Mitigación**:
- Usar D-Bus para hotkeys en Wayland
- Integración con compositor (Hyprland)
- Fallback a tray icon

### Riesgo 4: Performance no cumple targets
**Probabilidad**: Media
**Impacto**: Alto
**Mitigación**:
- Profiling continuo
- Optimizaciones incrementales
- Ajustar targets si es necesario

---

## Próximos Pasos

1. **Crear carpeta del proyecto**
2. **Ejecutar Task 1.1**: Setup del proyecto
3. **Configurar entorno de desarrollo**
4. **Comenzar Sprint 1**

¿Listo para empezar? 🚀
