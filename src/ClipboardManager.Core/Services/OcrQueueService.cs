using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ClipboardManager.Core.Interfaces;
using ClipboardManager.Core.Models;
using ClipboardManager.ML.Services;

namespace ClipboardManager.Core.Services;

public class OcrQueueService : IDisposable
{
    private readonly ConcurrentQueue<OcrTask> _queue;
    private readonly TesseractOcrService _ocrService;
    private readonly IClipboardRepository _repository;
    private readonly LanguageDetectionService? _languageDetector;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _processingTask;
    private bool _disposed;

    public event EventHandler<OcrCompletedEventArgs>? OcrCompleted;

    public OcrQueueService(
        TesseractOcrService ocrService,
        IClipboardRepository repository,
        LanguageDetectionService? languageDetector = null)
    {
        _queue = new ConcurrentQueue<OcrTask>();
        _ocrService = ocrService;
        _repository = repository;
        _languageDetector = languageDetector;
        _cancellationTokenSource = new CancellationTokenSource();
        
        Console.WriteLine("✅ Usando Tesseract como motor OCR");
        
        // Iniciar procesamiento en background
        _processingTask = Task.Run(ProcessQueueAsync);
    }

    public void EnqueueOcr(long itemId, byte[] imageData)
    {
        _queue.Enqueue(new OcrTask
        {
            ItemId = itemId,
            ImageData = imageData,
            EnqueuedAt = DateTime.UtcNow
        });
        
        Console.WriteLine($"📝 OCR encolado para item {itemId}. Cola: {_queue.Count}");
    }

    private async Task ProcessQueueAsync()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                if (_queue.TryDequeue(out var task))
                {
                    await ProcessOcrTaskAsync(task);
                }
                else
                {
                    // Esperar un poco si la cola está vacía
                    await Task.Delay(100, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error procesando OCR: {ex.Message}");
                await Task.Delay(1000); // Esperar antes de reintentar
            }
        }
    }

    private async Task ProcessOcrTaskAsync(OcrTask task)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            Console.WriteLine($"🔄 Procesando OCR para item {task.ItemId}...");
            
            // Extraer texto con Tesseract
            var ocrText = await _ocrService.ExtractTextAsync(task.ImageData);
            
            if (!string.IsNullOrWhiteSpace(ocrText))
            {
                // Actualizar OCR en la base de datos
                await _repository.UpdateOcrTextAsync(task.ItemId, ocrText);
                
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                Console.WriteLine($"✅ OCR completado para item {task.ItemId} en {elapsed:F0}ms: {ocrText.Substring(0, Math.Min(50, ocrText.Length))}...");
                
                // Detectar si el texto OCR es código
                string? detectedLanguage = null;
                bool isCode = false;
                
                if (_languageDetector?.IsAvailable == true)
                {
                    detectedLanguage = await _languageDetector.DetectLanguageAsync(ocrText);
                    isCode = !string.IsNullOrEmpty(detectedLanguage);
                    
                    if (isCode)
                    {
                        // Es código! Actualizar el item
                        var item = await _repository.GetByIdAsync(task.ItemId);
                        if (item != null)
                        {
                            item.ContentType = ClipboardType.Code;
                            item.CodeLanguage = detectedLanguage;
                            await _repository.UpdateAsync(item);
                            Console.WriteLine($"🔍 OCR detectó código {detectedLanguage} en imagen {task.ItemId}");
                        }
                    }
                }
                
                // Notificar que el OCR se completó (con info de código si aplica)
                OcrCompleted?.Invoke(this, new OcrCompletedEventArgs(task.ItemId, ocrText, isCode, detectedLanguage));
            }
            else
            {
                Console.WriteLine($"⚠️  No se detectó texto en item {task.ItemId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error en OCR para item {task.ItemId}: {ex.Message}");
        }
    }

    public int GetQueueSize() => _queue.Count;

    public void Dispose()
    {
        if (_disposed) return;
        
        _cancellationTokenSource.Cancel();
        
        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Ignorar errores de cancelación
        }
        
        _cancellationTokenSource.Dispose();
        _disposed = true;
    }

    private class OcrTask
    {
        public long ItemId { get; set; }
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        public DateTime EnqueuedAt { get; set; }
    }
}

public class OcrCompletedEventArgs : EventArgs
{
    public long ItemId { get; }
    public string OcrText { get; }
    public bool IsCode { get; }
    public string? CodeLanguage { get; }

    public OcrCompletedEventArgs(long itemId, string ocrText, bool isCode = false, string? codeLanguage = null)
    {
        ItemId = itemId;
        OcrText = ocrText;
        IsCode = isCode;
        CodeLanguage = codeLanguage;
    }
}
