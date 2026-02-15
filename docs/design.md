# Clipboard Manager - Diseño Técnico

## Arquitectura General

### Stack Tecnológico
- **Core Application**: C# (.NET 10)
- **UI Framework**: AvaloniaUI 11.x
- **Clipboard Daemon**: C++ (libX11 + libwayland)
- **ML Engine**: ONNX Runtime (sin Python)
- **Database**: SQLite 3.35+ con FTS5
- **IPC**: gRPC (daemon ↔ app)
- **Build System**: .NET SDK + CMake

### Diagrama de Arquitectura

```
┌─────────────────────────────────────────────────────┐
│   C# Main Application (.NET 10)                     │
│   ┌───────────────────────────────────────────┐     │
│   │  AvaloniaUI (Presentation Layer)          │     │
│   │  - MainWindow (overlay)                   │     │
│   │  - SearchView                             │     │
│   │  - SettingsView                           │     │
│   │  - PreviewControls                        │     │
│   └───────────────┬───────────────────────────┘     │
│                   │                                  │
│   ┌───────────────▼───────────────────────────┐     │
│   │  Business Logic Layer                     │     │
│   │  - ClipboardService                       │     │
│   │  - SearchService                          │     │
│   │  - SecurityService                        │     │
│   │  - ClassificationService                  │     │
│   └───────────────┬───────────────────────────┘     │
│                   │                                  │
│   ┌───────────────▼───────────────────────────┐     │
│   │  Data Access Layer                        │     │
│   │  - SQLite Repository                      │     │
│   │  - FTS5 Search Engine                     │     │
│   │  - Encryption Manager                     │     │
│   └───────────────┬───────────────────────────┘     │
│                   │                                  │
│   ┌───────────────▼───────────────────────────┐     │
│   │  ML Services (ONNX Runtime)               │     │
│   │  - OcrService (PaddleOCR models)          │     │
│   │  - SemanticSearchService (BERT)           │     │
│   │  - ClassificationService (ML.NET)         │     │
│   └───────────────────────────────────────────┘     │
└────────────────────┬────────────────────────────────┘
                     │ gRPC
┌────────────────────▼────────────────────────────────┐
│   C++ Clipboard Daemon                              │
│   ┌───────────────────────────────────────────┐     │
│   │  Platform Abstraction Layer               │     │
│   │  - IClipboardMonitor (interface)          │     │
│   └───────────────┬───────────────────────────┘     │
│                   │                                  │
│   ┌───────────────┴───────────────────────────┐     │
│   │                                            │     │
│   ▼                                            ▼     │
│  ┌──────────────────┐              ┌──────────────┐ │
│  │ X11Monitor       │              │WaylandMonitor│ │
│  │ - libX11         │              │ - wlr-data-  │ │
│  │ - XFixesSelect   │              │   control    │ │
│  │ - Event loop     │              │ - Event loop │ │
│  └──────────────────┘              └──────────────┘ │
│                                                      │
│   ┌───────────────────────────────────────────┐     │
│   │  gRPC Server                              │     │
│   │  - ClipboardEvents service                │     │
│   │  - Protobuf serialization                 │     │
│   └───────────────────────────────────────────┘     │
└─────────────────────────────────────────────────────┘
```

## Componentes Detallados

### 1. C# Main Application

#### 1.1 Estructura de Proyecto
```
ClipboardManager.sln
├── src/
│   ├── ClipboardManager.App/              # Aplicación principal
│   │   ├── Program.cs
│   │   ├── App.axaml
│   │   ├── Views/
│   │   │   ├── MainWindow.axaml
│   │   │   ├── SearchView.axaml
│   │   │   ├── SettingsView.axaml
│   │   │   └── PreviewControls/
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs
│   │   │   ├── SearchViewModel.cs
│   │   │   └── SettingsViewModel.cs
│   │   └── Services/
│   │       ├── HotkeyService.cs
│   │       └── ThemeService.cs
│   │
│   ├── ClipboardManager.Core/             # Lógica de negocio
│   │   ├── Services/
│   │   │   ├── ClipboardService.cs
│   │   │   ├── SearchService.cs
│   │   │   ├── SecurityService.cs
│   │   │   └── ClassificationService.cs
│   │   ├── Models/
│   │   │   ├── ClipboardItem.cs
│   │   │   ├── SearchResult.cs
│   │   │   └── Configuration.cs
│   │   └── Interfaces/
│   │       ├── IClipboardService.cs
│   │       ├── ISearchService.cs
│   │       └── ISecurityService.cs
│   │
│   ├── ClipboardManager.Data/             # Acceso a datos
│   │   ├── ClipboardDbContext.cs
│   │   ├── Repositories/
│   │   │   ├── ClipboardRepository.cs
│   │   │   └── SearchRepository.cs
│   │   ├── Migrations/
│   │   └── Encryption/
│   │       └── EncryptionManager.cs
│   │
│   ├── ClipboardManager.ML/               # Machine Learning
│   │   ├── OcrService.cs
│   │   ├── SemanticSearchService.cs
│   │   ├── Models/
│   │   │   ├── OcrModel.cs
│   │   │   └── EmbeddingModel.cs
│   │   └── Utils/
│   │       ├── ImagePreprocessor.cs
│   │       └── BertTokenizer.cs
│   │
│   └── ClipboardManager.Daemon.Client/    # Cliente gRPC
│       ├── DaemonClient.cs
│       └── Generated/
│           └── clipboard.proto
│
├── daemon/                                 # C++ Daemon
│   ├── src/
│   │   ├── main.cpp
│   │   ├── clipboard_monitor.h/cpp
│   │   ├── x11_monitor.h/cpp
│   │   ├── wayland_monitor.h/cpp
│   │   └── grpc_server.h/cpp
│   ├── proto/
│   │   └── clipboard.proto
│   └── CMakeLists.txt
│
├── models/                                 # Modelos ONNX
│   ├── paddleocr/
│   │   ├── ch_PP-OCRv4_det.onnx
│   │   └── ch_PP-OCRv4_rec.onnx
│   └── bert/
│       ├── all-MiniLM-L6-v2.onnx
│       └── vocab.txt
│
└── tests/
    ├── ClipboardManager.Tests/
    └── ClipboardManager.IntegrationTests/
```

#### 1.2 Modelos de Datos

```csharp
// ClipboardItem.cs
public class ClipboardItem
{
    public long Id { get; set; }
    public byte[] Content { get; set; }
    public ClipboardType ContentType { get; set; }
    public string? OcrText { get; set; }
    public float[]? Embedding { get; set; }
    public string SourceApp { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsPassword { get; set; }
    public bool IsEncrypted { get; set; }
    public string? Metadata { get; set; } // JSON
    public byte[]? ThumbnailData { get; set; }
}

public enum ClipboardType
{
    Text,
    RichText,
    Code,
    Image,
    Url,
    Email,
    Phone,
    FilePath,
    Password
}

// Configuration.cs
public class AppConfiguration
{
    public SecurityConfig Security { get; set; }
    public PerformanceConfig Performance { get; set; }
    public UiConfig Ui { get; set; }
    public ClipboardConfig Clipboard { get; set; }
}

public class SecurityConfig
{
    public PasswordHandling HandlePasswords { get; set; } = PasswordHandling.Encrypt;
    public bool ShowPasswords { get; set; } = false;
    public bool AutoDetectPasswords { get; set; } = true;
    public int PasswordTimeoutSeconds { get; set; } = 300;
    public bool EncryptSensitive { get; set; } = true;
}

public enum PasswordHandling
{
    Ignore,
    Encrypt,
    Allow
}
```

#### 1.3 Servicios Principales

```csharp
// ClipboardService.cs
public class ClipboardService : IClipboardService
{
    private readonly IClipboardRepository _repository;
    private readonly IOcrService _ocrService;
    private readonly ISecurityService _securityService;
    private readonly IClassificationService _classificationService;
    private readonly DaemonClient _daemonClient;

    public async Task ProcessClipboardEventAsync(ClipboardEvent evt)
    {
        // 1. Clasificar contenido
        var contentType = await _classificationService.ClassifyAsync(evt.Data);
        
        // 2. Detectar si es password
        var isPassword = await _securityService.IsPasswordAsync(
            evt.Data, evt.SourceApp, evt.WindowTitle);
        
        // 3. Crear item
        var item = new ClipboardItem
        {
            Content = isPassword ? 
                await _securityService.EncryptAsync(evt.Data) : evt.Data,
            ContentType = contentType,
            SourceApp = evt.SourceApp,
            Timestamp = DateTime.UtcNow,
            IsPassword = isPassword,
            IsEncrypted = isPassword
        };
        
        // 4. OCR si es imagen (async, no bloquea)
        if (contentType == ClipboardType.Image)
        {
            _ = Task.Run(async () =>
            {
                item.OcrText = await _ocrService.ExtractTextAsync(evt.Data);
                item.Embedding = await _semanticSearch.GetEmbeddingAsync(item.OcrText);
                await _repository.UpdateAsync(item);
            });
        }
        
        // 5. Guardar
        await _repository.AddAsync(item);
    }
}

// SearchService.cs
public class SearchService : ISearchService
{
    private readonly ISearchRepository _searchRepo;
    private readonly ISemanticSearchService _semanticSearch;

    public async Task<List<SearchResult>> SearchAsync(
        string query, SearchMode mode = SearchMode.Text)
    {
        return mode switch
        {
            SearchMode.Text => await _searchRepo.FullTextSearchAsync(query),
            SearchMode.Semantic => await SemanticSearchAsync(query),
            SearchMode.Hybrid => await HybridSearchAsync(query),
            _ => throw new ArgumentException(nameof(mode))
        };
    }

    private async Task<List<SearchResult>> SemanticSearchAsync(string query)
    {
        var queryEmbedding = await _semanticSearch.GetEmbeddingAsync(query);
        var items = await _searchRepo.GetAllWithEmbeddingsAsync();
        
        return items
            .Select(item => new SearchResult
            {
                Item = item,
                Score = CosineSimilarity(queryEmbedding, item.Embedding)
            })
            .OrderByDescending(r => r.Score)
            .Take(20)
            .ToList();
    }
}
```

### 2. C++ Clipboard Daemon

#### 2.1 Estructura

```cpp
// clipboard_monitor.h
class IClipboardMonitor {
public:
    virtual ~IClipboardMonitor() = default;
    virtual bool Initialize() = 0;
    virtual void Run() = 0;
    virtual void Stop() = 0;
    
    // Callback cuando cambia el clipboard
    std::function<void(const ClipboardEvent&)> OnClipboardChanged;
};

// x11_monitor.cpp
class X11Monitor : public IClipboardMonitor {
private:
    Display* display_;
    Window window_;
    Atom clipboard_atom_;
    
public:
    bool Initialize() override {
        display_ = XOpenDisplay(nullptr);
        if (!display_) return false;
        
        window_ = XCreateSimpleWindow(display_, 
            DefaultRootWindow(display_), 0, 0, 1, 1, 0, 0, 0);
        
        clipboard_atom_ = XInternAtom(display_, "CLIPBOARD", False);
        
        // Registrar para eventos de clipboard
        XFixesSelectSelectionInput(display_, window_, 
            clipboard_atom_, XFixesSetSelectionOwnerNotifyMask);
        
        return true;
    }
    
    void Run() override {
        XEvent event;
        while (running_) {
            XNextEvent(display_, &event);
            
            if (event.type == XFixesSelectionNotify) {
                HandleClipboardChange();
            }
        }
    }
    
    void HandleClipboardChange() {
        // Leer contenido del clipboard
        auto data = ReadClipboardData();
        
        ClipboardEvent evt;
        evt.set_data(data);
        evt.set_source_app(GetActiveWindowName());
        evt.set_timestamp(GetCurrentTimestamp());
        
        // Notificar via callback
        if (OnClipboardChanged) {
            OnClipboardChanged(evt);
        }
    }
};

// wayland_monitor.cpp
class WaylandMonitor : public IClipboardMonitor {
private:
    wl_display* display_;
    zwlr_data_control_manager_v1* data_control_manager_;
    zwlr_data_control_device_v1* data_device_;
    
public:
    bool Initialize() override {
        display_ = wl_display_connect(nullptr);
        if (!display_) return false;
        
        // Bind to wlr-data-control protocol
        // ... (implementación del protocolo Wayland)
        
        return true;
    }
    
    void Run() override {
        while (running_) {
            wl_display_dispatch(display_);
        }
    }
};

// grpc_server.cpp
class ClipboardServiceImpl final : public ClipboardService::Service {
private:
    std::unique_ptr<IClipboardMonitor> monitor_;
    
public:
    grpc::Status StreamClipboardEvents(
        grpc::ServerContext* context,
        const Empty* request,
        grpc::ServerWriter<ClipboardEvent>* writer) override 
    {
        monitor_->OnClipboardChanged = [writer](const ClipboardEvent& evt) {
            writer->Write(evt);
        };
        
        monitor_->Run();
        return grpc::Status::OK;
    }
};
```

#### 2.2 Protobuf Definition

```protobuf
// clipboard.proto
syntax = "proto3";

package clipboardmanager;

service ClipboardService {
  rpc StreamClipboardEvents(Empty) returns (stream ClipboardEvent);
}

message Empty {}

message ClipboardEvent {
  bytes data = 1;
  string source_app = 2;
  string window_title = 3;
  int64 timestamp = 4;
  string mime_type = 5;
}
```

### 3. ML Services (ONNX Runtime)

#### 3.1 OCR Service

```csharp
public class OcrService : IOcrService
{
    private readonly InferenceSession _detectionModel;
    private readonly InferenceSession _recognitionModel;
    private readonly IImagePreprocessor _preprocessor;

    public OcrService(string modelsPath)
    {
        var sessionOptions = new SessionOptions();
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        
        _detectionModel = new InferenceSession(
            Path.Combine(modelsPath, "paddleocr/ch_PP-OCRv4_det.onnx"),
            sessionOptions);
            
        _recognitionModel = new InferenceSession(
            Path.Combine(modelsPath, "paddleocr/ch_PP-OCRv4_rec.onnx"),
            sessionOptions);
    }

    public async Task<string> ExtractTextAsync(byte[] imageData)
    {
        // 1. Preprocesar imagen
        var tensor = await _preprocessor.PreprocessAsync(imageData);
        
        // 2. Detectar regiones de texto
        var detInputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("x", tensor)
        };
        
        using var detResults = _detectionModel.Run(detInputs);
        var regions = ExtractTextRegions(detResults);
        
        // 3. Reconocer texto en cada región
        var texts = new List<string>();
        foreach (var region in regions)
        {
            var regionTensor = await _preprocessor.PrepareRegionAsync(region);
            var recInputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("x", regionTensor)
            };
            
            using var recResults = _recognitionModel.Run(recInputs);
            var text = DecodeText(recResults);
            texts.Add(text);
        }
        
        return string.Join("\n", texts);
    }
}
```

#### 3.2 Semantic Search Service

```csharp
public class SemanticSearchService : ISemanticSearchService
{
    private readonly InferenceSession _model;
    private readonly BertTokenizer _tokenizer;

    public SemanticSearchService(string modelPath)
    {
        var sessionOptions = new SessionOptions();
        sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        
        _model = new InferenceSession(
            Path.Combine(modelPath, "bert/all-MiniLM-L6-v2.onnx"),
            sessionOptions);
            
        _tokenizer = new BertTokenizer(
            Path.Combine(modelPath, "bert/vocab.txt"));
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        // 1. Tokenizar
        var tokens = _tokenizer.Encode(text, maxLength: 512);
        
        // 2. Crear tensors
        var inputIds = new DenseTensor<long>(
            tokens.InputIds, 
            new[] { 1, tokens.InputIds.Length });
            
        var attentionMask = new DenseTensor<long>(
            tokens.AttentionMask,
            new[] { 1, tokens.AttentionMask.Length });
        
        // 3. Inferencia
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
        };
        
        using var results = _model.Run(inputs);
        var embeddings = results.First()
            .AsEnumerable<float>()
            .ToArray();
        
        // 4. Mean pooling
        return MeanPooling(embeddings, attentionMask);
    }

    private float[] MeanPooling(float[] embeddings, DenseTensor<long> mask)
    {
        // Implementación de mean pooling
        // ...
    }
}
```

### 4. Database Schema

```sql
-- Tabla principal de items
CREATE TABLE clipboard_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    content BLOB NOT NULL,
    content_type TEXT NOT NULL,
    ocr_text TEXT,
    embedding BLOB,
    source_app TEXT,
    timestamp INTEGER NOT NULL,
    is_password BOOLEAN NOT NULL DEFAULT 0,
    is_encrypted BOOLEAN NOT NULL DEFAULT 0,
    metadata TEXT,
    thumbnail BLOB
);

-- Índices para performance
CREATE INDEX idx_timestamp ON clipboard_items(timestamp DESC);
CREATE INDEX idx_type ON clipboard_items(content_type);
CREATE INDEX idx_password ON clipboard_items(is_password);
CREATE INDEX idx_source_app ON clipboard_items(source_app);

-- FTS5 para búsqueda full-text
CREATE VIRTUAL TABLE clipboard_fts USING fts5(
    content,
    ocr_text,
    source_app,
    content='clipboard_items',
    content_rowid='id',
    tokenize='porter unicode61'
);

-- Triggers para mantener FTS5 sincronizado
CREATE TRIGGER clipboard_ai AFTER INSERT ON clipboard_items BEGIN
    INSERT INTO clipboard_fts(rowid, content, ocr_text, source_app)
    VALUES (new.id, new.content, new.ocr_text, new.source_app);
END;

CREATE TRIGGER clipboard_ad AFTER DELETE ON clipboard_items BEGIN
    DELETE FROM clipboard_fts WHERE rowid = old.id;
END;

CREATE TRIGGER clipboard_au AFTER UPDATE ON clipboard_items BEGIN
    UPDATE clipboard_fts 
    SET content = new.content,
        ocr_text = new.ocr_text,
        source_app = new.source_app
    WHERE rowid = new.id;
END;

-- Configuración
CREATE TABLE config (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
```

## Performance Optimizations

### 1. Clipboard Monitoring
- Event-driven (no polling)
- Overhead < 0.5% CPU
- Detección inmediata de cambios

### 2. Database
- Prepared statements
- Connection pooling
- Índices optimizados
- PRAGMA optimizations:
  ```sql
  PRAGMA journal_mode = WAL;
  PRAGMA synchronous = NORMAL;
  PRAGMA cache_size = -64000;  -- 64MB cache
  PRAGMA temp_store = MEMORY;
  ```

### 3. ML Inference
- ONNX Runtime optimizations
- Batch processing cuando sea posible
- GPU acceleration (opcional)
- Model quantization (INT8)

### 4. UI
- Virtual scrolling para listas grandes
- Lazy loading de thumbnails
- Debouncing en búsqueda incremental
- Async/await para operaciones I/O

## Security Considerations

### 1. Password Encryption
- AES-256-GCM
- Key derivation: PBKDF2 (100,000 iterations)
- Master key almacenada en keyring del sistema
- Salt único por password

### 2. Database Security
- Passwords nunca en plaintext
- Sensitive data encriptada
- Permisos restrictivos en archivos (600)

### 3. IPC Security
- gRPC con TLS (opcional)
- Unix domain sockets (más seguro que TCP)
- Validación de permisos

## Deployment

### Build Process
```bash
# Build C++ daemon
cd daemon
mkdir build && cd build
cmake ..
make -j$(nproc)

# Build C# app
cd ../..
dotnet build -c Release

# Download ONNX models
./scripts/download-models.sh
```

### Installation
```bash
# Install daemon
sudo cp daemon/build/clipboard-daemon /usr/local/bin/
sudo cp daemon/clipboard-daemon.service /etc/systemd/system/

# Install app
sudo cp -r src/ClipboardManager.App/bin/Release/net10.0/ /opt/clipboard-manager/

# Create desktop entry
cp clipboard-manager.desktop ~/.local/share/applications/
```

### Runtime Dependencies
- .NET 10 Runtime
- AvaloniaUI native libraries
- ONNX Runtime
- SQLite 3.35+
- libX11 (X11)
- libwayland-client (Wayland)
- gRPC libraries
