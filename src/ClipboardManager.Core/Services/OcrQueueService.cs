using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ClipboardManager.Core.Interfaces;
using ClipboardManager.ML.Services;

namespace ClipboardManager.Core.Services;

public class OcrQueueService : IDisposable
{
    private readonly ConcurrentQueue<OcrTask> _queue;
    private readonly TesseractOcrService _ocrService;
    private readonly IClipboardRepository _repository;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _processingTask;
    private bool _disposed;

    public event EventHandler<OcrCompletedEventArgs>? OcrCompleted;

    public OcrQueueService(TesseractOcrService ocrService, IClipboardRepository repository)
    {
        _queue = new ConcurrentQueue<OcrTask>();
        _ocrService = ocrService;
        _repository = repository;
        _cancellationTokenSource = new CancellationTokenSource();
        
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
            
            // Extraer texto de la imagen
            var ocrText = await _ocrService.ExtractTextAsync(task.ImageData);
            
            if (!string.IsNullOrWhiteSpace(ocrText))
            {
                // Actualizar en la base de datos
                await _repository.UpdateOcrTextAsync(task.ItemId, ocrText);
                
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                Console.WriteLine($"✅ OCR completado para item {task.ItemId} en {elapsed:F0}ms: {ocrText.Substring(0, Math.Min(50, ocrText.Length))}...");
                
                // Notificar que el OCR se completó
                OcrCompleted?.Invoke(this, new OcrCompletedEventArgs(task.ItemId, ocrText));
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

    public OcrCompletedEventArgs(long itemId, string ocrText)
    {
        ItemId = itemId;
        OcrText = ocrText;
    }
}
