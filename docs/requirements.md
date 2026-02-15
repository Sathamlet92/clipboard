# Clipboard Manager - Requisitos

## Resumen del Proyecto
Clipboard manager inteligente con historial infinito, OCR automático, búsqueda semántica y seguridad para passwords. Optimizado para Linux (X11/Wayland) con alto rendimiento.

## Objetivos de Performance
- **Captura de clipboard**: < 10ms
- **Apertura de UI**: < 100ms
- **Búsqueda de texto**: < 50ms (1000 items)
- **Búsqueda semántica**: < 200ms (1000 items)
- **OCR (background)**: < 1s por imagen
- **RAM idle**: < 50MB
- **RAM con 1000 items**: < 200MB
- **CPU idle**: < 0.5%

## Features Principales

### 1. Captura Automática de Clipboard
**Prioridad**: Alta | **Fase**: MVP

- Monitoreo continuo del clipboard del sistema
- Captura automática de:
  - Texto plano
  - Texto enriquecido (RTF)
  - Imágenes (PNG, JPG, BMP)
  - Código fuente
  - URLs
  - Rutas de archivos
- Soporte para X11 y Wayland (wlr-data-control)
- Detección de aplicación de origen
- Timestamp preciso de cada captura

**Criterios de Aceptación**:
- ✅ Captura en < 10ms
- ✅ No pierde eventos de clipboard
- ✅ Funciona en X11 y Wayland (Hyprland)
- ✅ Detecta tipo de contenido automáticamente

### 2. OCR Automático
**Prioridad**: Alta | **Fase**: MVP

- Extracción automática de texto de imágenes
- Procesamiento en background (no bloquea UI)
- Soporte multi-idioma (español, inglés)
- Detección de orientación de texto
- Cache de resultados

**Criterios de Aceptación**:
- ✅ OCR completa en < 1s por imagen típica
- ✅ Precisión > 90% en texto claro
- ✅ No bloquea la UI durante procesamiento
- ✅ Texto extraído es buscable

### 3. Búsqueda Inteligente
**Prioridad**: Alta | **Fase**: MVP (texto) + Fase 2 (semántica)

#### Búsqueda por Texto (MVP)
- Full-text search con SQLite FTS5
- Búsqueda incremental (mientras escribes)
- Filtros por:
  - Tipo de contenido
  - Rango de fechas
  - Aplicación de origen
- Soporte para regex (power users)

#### Búsqueda Semántica (Fase 2)
- Búsqueda por significado, no solo palabras exactas
- Embeddings con BERT via ONNX
- Ejemplo: buscar "setup containers" encuentra "cómo instalar docker"

**Criterios de Aceptación**:
- ✅ Búsqueda texto < 50ms
- ✅ Búsqueda semántica < 200ms
- ✅ Resultados relevantes en top 5
- ✅ Búsqueda incremental sin lag

### 4. Seguridad y Passwords
**Prioridad**: Alta | **Fase**: MVP

#### Detección de Passwords
- Detección automática por contexto (campo de password)
- Heurística: analiza características del texto
  - Longitud 8-128 caracteres
  - Mezcla de mayúsculas, minúsculas, números, símbolos
  - Sin espacios
- Marcado manual por usuario

#### Manejo de Passwords
- Configuración flexible:
  - `encrypt`: Guardar encriptado (default)
  - `ignore`: No guardar passwords
  - `allow`: Guardar en texto plano (no recomendado)
- Encriptación con AES-256
- UI oculta passwords: "••••••••"
- Revelar temporalmente (5 segundos)
- Auto-delete después de X minutos (configurable)

**Criterios de Aceptación**:
- ✅ Detecta passwords con > 95% precisión
- ✅ Passwords encriptados no son legibles en DB
- ✅ UI nunca muestra passwords por defecto
- ✅ Auto-delete funciona correctamente

### 5. Interfaz de Usuario
**Prioridad**: Alta | **Fase**: MVP

- Hotkey global: `Ctrl+Shift+V` (configurable)
- Ventana overlay rápida
- Preview de contenido:
  - Texto: primeras 2-3 líneas
  - Código: syntax highlighting
  - Imágenes: thumbnail
- Navegación con teclado:
  - `↑↓`: Navegar items
  - `Enter`: Pegar seleccionado
  - `Ctrl+F`: Buscar
  - `Esc`: Cerrar
- Tema dark/light

**Criterios de Aceptación**:
- ✅ Ventana abre en < 100ms
- ✅ Hotkey funciona globalmente
- ✅ Preview es claro y útil
- ✅ Navegación fluida con teclado

### 6. Almacenamiento
**Prioridad**: Alta | **Fase**: MVP

- SQLite con FTS5 para búsqueda full-text
- Esquema optimizado:
  - Tabla principal: items con metadata
  - FTS5: índice de búsqueda
  - Embeddings: vectores para búsqueda semántica
- Deduplicación inteligente:
  - Hash SHA256 para texto exacto
  - Perceptual hash para imágenes
- Límite configurable de items (default: 1000)
- Auto-limpieza de items antiguos

**Criterios de Aceptación**:
- ✅ Queries < 50ms para 1000 items
- ✅ No duplicados exactos
- ✅ DB size razonable (~50-100MB para 1000 items)

### 7. Clasificación Automática
**Prioridad**: Media | **Fase**: MVP (reglas) + Fase 2 (ML)

#### Fase MVP (Reglas Heurísticas)
- Texto plano
- Código (detecta lenguaje: Python, C#, JavaScript, etc.)
- URLs
- Emails
- Teléfonos
- Rutas de archivos
- Imágenes
- Passwords

#### Fase 2 (ML Opcional)
- Clasificación más precisa con ML.NET
- Detección de idioma
- Análisis de sentimiento (opcional)

**Criterios de Aceptación**:
- ✅ Clasificación correcta > 90% casos
- ✅ Detección de lenguaje de código > 85%
- ✅ Clasificación en < 10ms

## Features Opcionales (Fase 3+)

### 8. Estadísticas
- Items más copiados
- Apps más usadas
- Gráficos de uso temporal
- Exportar estadísticas

### 9. Sincronización (Futuro)
- Sync entre dispositivos
- Encriptación end-to-end
- Servidor propio o cloud

### 10. Plugins/Extensiones
- API para extensiones
- Transformaciones de texto
- Integraciones con otras apps

## Configuración

### Archivo de Configuración
```json
{
  "security": {
    "handlePasswords": "encrypt",
    "showPasswords": false,
    "autoDetectPasswords": true,
    "passwordTimeout": 300,
    "encryptSensitive": true
  },
  "performance": {
    "maxItems": 1000,
    "ocrEnabled": true,
    "ocrAsync": true,
    "semanticSearch": true,
    "thumbnailSize": 200
  },
  "ui": {
    "hotkey": "Ctrl+Shift+V",
    "theme": "dark",
    "showPreviews": true,
    "itemsPerPage": 20,
    "windowWidth": 800,
    "windowHeight": 600
  },
  "clipboard": {
    "monitorImages": true,
    "monitorText": true,
    "monitorFiles": true,
    "ignoreApps": []
  }
}
```

## Roadmap de Desarrollo

### Fase 1: MVP (4-6 semanas)
- ✅ Monitoreo básico de clipboard (X11)
- ✅ Almacenamiento en SQLite
- ✅ UI básica con AvaloniaUI
- ✅ Hotkey global
- ✅ Búsqueda por texto (FTS5)
- ✅ Detección y encriptación de passwords
- ✅ OCR con ONNX Runtime
- ✅ Clasificación básica (reglas)

### Fase 2: Inteligencia (2-3 semanas)
- ✅ Soporte Wayland completo
- ✅ Búsqueda semántica (BERT/ONNX)
- ✅ Clasificación ML avanzada
- ✅ Syntax highlighting para código
- ✅ Mejoras de UI/UX

### Fase 3: Polish (1-2 semanas)
- ✅ Configuración avanzada
- ✅ Estadísticas
- ✅ Exportar/importar
- ✅ Temas personalizados
- ✅ Optimizaciones finales

## Restricciones y Consideraciones

### Técnicas
- .NET 10 (C# 13)
- Linux only (X11 + Wayland)
- SQLite 3.35+ (para FTS5)
- ONNX Runtime 1.16+

### Seguridad
- Passwords nunca en logs
- DB encriptada para datos sensibles
- Permisos mínimos necesarios
- No telemetría sin consentimiento

### Performance
- No bloquear UI nunca
- OCR en background threads
- Búsqueda incremental optimizada
- Lazy loading de previews

### Usabilidad
- Hotkey debe funcionar siempre
- UI debe ser intuitiva sin tutorial
- Errores claros y accionables
- Configuración simple pero potente
