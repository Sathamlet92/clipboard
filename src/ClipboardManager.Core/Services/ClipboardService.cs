using ClipboardManager.Core.Models;
using ClipboardManager.Core.Interfaces;
using System.Text;
using System.Text.Json;

namespace ClipboardManager.Core.Services;

public class LanguageDetectedEventArgs : EventArgs
{
    public long ItemId { get; set; }
    public string Language { get; set; } = string.Empty;
}

public class ClipboardService
{
    private readonly IClipboardRepository _repository;
    private readonly ClassificationService _classificationService;
    private readonly SecurityService _securityService;
    private readonly OcrQueueService? _ocrQueueService;
    private readonly object? _embeddingService; // EmbeddingService (ML project)
    private readonly AppConfiguration _config;

    public event EventHandler<LanguageDetectedEventArgs>? LanguageDetected;

    public ClipboardService(
        IClipboardRepository repository,
        ClassificationService classificationService,
        SecurityService securityService,
        AppConfiguration config,
        OcrQueueService? ocrQueueService = null,
        object? embeddingService = null)
    {
        _repository = repository;
        _classificationService = classificationService;
        _securityService = securityService;
        _config = config;
        _ocrQueueService = ocrQueueService;
        _embeddingService = embeddingService;
    }

    public async Task<ClipboardItem> ProcessClipboardEventAsync(
        byte[] data,
        string sourceApp,
        string? windowTitle = null,
        string? mimeType = null)
    {
        // 0. Filtrar datos vacíos o MIME types inválidos
        if (data == null || data.Length == 0)
        {
            Console.WriteLine("⚠️  Datos vacíos, ignorando");
            throw new ArgumentException("Empty clipboard data");
        }
        
        // Filtrar MIME types de metadata
        if (mimeType == "SAVE_TARGETS" || 
            mimeType == "TARGETS" ||
            mimeType == "MULTIPLE" ||
            mimeType == "TIMESTAMP" ||
            mimeType?.StartsWith("chromium/") == true)
        {
            Console.WriteLine($"⏭️  Ignorando MIME type de metadata: {mimeType}");
            throw new ArgumentException($"Metadata MIME type: {mimeType}");
        }
        
        Console.WriteLine($"📝 Procesando: {data.Length} bytes, MIME={mimeType}, App={sourceApp}");
        
        // 1. Clasificar contenido (SIMPLIFICADO - todo texto inicialmente)
        var contentType = _classificationService.Classify(data, mimeType);
        Console.WriteLine($"🔍 Clasificación inicial: {contentType}");
        
        // Forzar a Text si no es imagen - ML decidirá si es código
        if (contentType != ClipboardType.Image)
        {
            contentType = ClipboardType.Text;
            Console.WriteLine($"🔄 Forzado a Text (ML decidirá si es código)");
        }

        // 2. Detectar si es password
        string? textContent = null;
        var isPassword = false;

        if (contentType == ClipboardType.Text || contentType == ClipboardType.Code)
        {
            textContent = Encoding.UTF8.GetString(data);
            isPassword = _securityService.IsPassword(textContent, sourceApp, windowTitle);
        }

        // 3. Manejar passwords según configuración
        if (isPassword && _config.Security.HandlePasswords == PasswordHandling.Ignore)
        {
            // No guardar passwords
            throw new InvalidOperationException("Password handling is set to Ignore");
        }

        // 4. Verificar duplicados
        var contentHash = _securityService.ComputeHash(data);
        if (await _repository.ExistsByHashAsync(contentHash))
        {
            // Ya existe, no duplicar
            throw new InvalidOperationException("Content already exists");
        }

        // 5. Encriptar si es necesario
        byte[] finalContent = data;
        var isEncrypted = false;

        if (isPassword && _config.Security.HandlePasswords == PasswordHandling.Encrypt)
        {
            finalContent = await _securityService.EncryptAsync(data);
            isEncrypted = true;
        }

        // 6. Detectar lenguaje de código si aplica (SIMPLIFICADO - siempre "text")
        string? codeLanguage = null;
        // ML decidirá en background si es código

        // 7. Crear metadata
        var metadata = new Dictionary<string, object>
        {
            ["hash"] = contentHash,
            ["mime_type"] = mimeType ?? "unknown"
        };

        if (codeLanguage != null)
        {
            metadata["language"] = codeLanguage;
        }

        // 8. Crear item
        var item = new ClipboardItem
        {
            Content = finalContent,
            ContentType = isPassword ? ClipboardType.Password : contentType,
            SourceApp = sourceApp,
            Timestamp = DateTime.UtcNow,
            IsPassword = isPassword,
            IsEncrypted = isEncrypted,
            Metadata = JsonSerializer.Serialize(metadata),
            CodeLanguage = codeLanguage
        };
        
        Console.WriteLine($"💾 Item creado: Type={item.ContentType}, IsPassword={isPassword}, CodeLanguage={codeLanguage}");

        // 9. Guardar en DB
        item.Id = await _repository.AddAsync(item);
        Console.WriteLine($"✅ Item guardado en DB con ID={item.Id}, Type={item.ContentType}");

        // 10. Detectar lenguaje con ML en BACKGROUND (analiza TODO el texto)
        if (textContent != null && contentType == ClipboardType.Text)
        {
            var itemId = item.Id;
            var text = textContent;
            _ = Task.Run(async () =>
            {
                try
                {
                    var detectedLanguage = _classificationService.DetectCodeLanguage(text);
                    if (!string.IsNullOrEmpty(detectedLanguage))
                    {
                        // ML detectó código con confianza alta - obtener item fresco de DB
                        var freshItem = await _repository.GetByIdAsync(itemId);
                        if (freshItem != null)
                        {
                            freshItem.ContentType = ClipboardType.Code;
                            freshItem.CodeLanguage = detectedLanguage;
                            await _repository.UpdateAsync(freshItem);
                            Console.WriteLine($"✅ Lenguaje detectado para item {itemId}: {detectedLanguage}");
                            
                            // Notificar evento
                            LanguageDetected?.Invoke(this, new LanguageDetectedEventArgs
                            {
                                ItemId = itemId,
                                Language = detectedLanguage
                            });
                        }
                    }
                    // Si retorna null, se queda como Text (correcto)
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  Error detectando lenguaje: {ex.Message}");
                }
            });
        }

        // 11. Generar embeddings si está habilitado (background, no bloquea)
        if (_config.Performance.SemanticSearch && _embeddingService != null && textContent != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // Usar reflexión para llamar GetEmbeddingAsync
                    var method = _embeddingService.GetType().GetMethod("GetEmbeddingAsync");
                    if (method != null)
                    {
                        var task = method.Invoke(_embeddingService, new object[] { textContent });
                        if (task is Task<float[]?> embeddingTask)
                        {
                            var embedding = await embeddingTask;
                            if (embedding != null)
                            {
                                item.Embedding = embedding;
                                await _repository.UpdateAsync(item);
                                Console.WriteLine($"✅ Embedding generado para item {item.Id}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  Error generando embedding: {ex.Message}");
                }
            });
        }

        // 11. Si es imagen, programar OCR (se hará en background)
        if (contentType == ClipboardType.Image && _config.Performance.OcrEnabled && _ocrQueueService != null)
        {
            // Encolar para OCR asíncrono (no bloquea)
            _ocrQueueService.EnqueueOcr(item.Id, data);
        }

        return item;
    }
    // Sobrecarga para aceptar ClipboardEventArgs del daemon
    public async Task<ClipboardItem> ProcessClipboardEventAsync(object clipboardEvent)
    {
        // Usar reflexión para extraer propiedades del evento
        var eventType = clipboardEvent.GetType();
        var dataProperty = eventType.GetProperty("Data");
        var sourceAppProperty = eventType.GetProperty("SourceApp");
        var windowTitleProperty = eventType.GetProperty("WindowTitle");
        var mimeTypeProperty = eventType.GetProperty("MimeType");

        var data = (byte[])(dataProperty?.GetValue(clipboardEvent) ?? Array.Empty<byte>());
        var sourceApp = (string)(sourceAppProperty?.GetValue(clipboardEvent) ?? "unknown");
        var windowTitle = (string?)(windowTitleProperty?.GetValue(clipboardEvent));
        var mimeType = (string?)(mimeTypeProperty?.GetValue(clipboardEvent));

        return await ProcessClipboardEventAsync(data, sourceApp, windowTitle, mimeType);
    }


    public async Task<ClipboardItem?> GetItemAsync(long id)
    {
        var item = await _repository.GetByIdAsync(id);
        
        if (item == null)
            return null;

        // Desencriptar si es necesario
        if (item.IsEncrypted)
        {
            item.Content = await _securityService.DecryptAsync(item.Content);
        }

        return item;
    }

    public async Task<List<ClipboardItem>> GetRecentItemsAsync(int limit = 100)
    {
        var items = await _repository.GetRecentAsync(limit);

        // No desencriptar passwords automáticamente por seguridad
        return items;
    }

    public async Task<bool> DeleteItemAsync(long id)
    {
        return await _repository.DeleteAsync(id);
    }

    public async Task CleanupOldItemsAsync()
    {
        var maxItems = _config.Performance.MaxItems;
        var currentCount = await _repository.GetCountAsync();

        if (currentCount > maxItems)
        {
            // Calcular fecha de corte para mantener solo los últimos N items
            var itemsToDelete = currentCount - maxItems;
            var cutoffDate = DateTime.UtcNow.AddDays(-30); // Fallback: 30 días
            
            await _repository.DeleteOlderThanAsync(cutoffDate);
        }

        // Limpiar passwords expirados
        if (_config.Security.PasswordTimeoutSeconds > 0)
        {
            var passwordCutoff = DateTime.UtcNow.AddSeconds(-_config.Security.PasswordTimeoutSeconds);
            // TODO: Implementar limpieza específica de passwords
        }
    }

    private async Task ProcessOcrAsync(long itemId, byte[] imageData)
    {
        try
        {
            // TODO: Implementar OCR con ONNX Runtime
            // Por ahora es un placeholder
            await Task.Delay(100);
            
            // var ocrText = await _ocrService.ExtractTextAsync(imageData);
            // var item = await _repository.GetByIdAsync(itemId);
            // if (item != null)
            // {
            //     item.OcrText = ocrText;
            //     await _repository.UpdateAsync(item);
            // }
        }
        catch (Exception ex)
        {
            // Log error pero no fallar
            Console.WriteLine($"OCR failed for item {itemId}: {ex.Message}");
        }
    }
}
