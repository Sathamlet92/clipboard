using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ClipboardManager.ML.Services;

public class TesseractOcrService : IDisposable
{
    private readonly string _tessDataPath;
    private readonly LanguageDetectionService? _languageDetector;
    private bool _disposed;
    private bool _isAvailable;

    public TesseractOcrService(string tessDataPath, LanguageDetectionService? languageDetector = null)
    {
        _tessDataPath = tessDataPath;
        _languageDetector = languageDetector;
        CheckAvailability();
    }

    private void CheckAvailability()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "tesseract",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            
            _isAvailable = process.ExitCode == 0;
            
            if (_isAvailable)
            {
                Console.WriteLine($"✅ Tesseract CLI encontrado: {output.Split('\n')[0]}");
            }
            else
            {
                Console.WriteLine("❌ Tesseract CLI no disponible");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error verificando Tesseract: {ex.Message}");
            _isAvailable = false;
        }
    }

    public async Task<string> ExtractTextAsync(byte[] imageData)
    {
        if (!_isAvailable)
        {
            Console.WriteLine("⚠️  Tesseract no disponible");
            return string.Empty;
        }

        var tempImagePath = Path.GetTempFileName() + ".png";
        var tempOutputPath = Path.GetTempFileName();
        
        try
        {
            Console.WriteLine($"📸 Iniciando extracción OCR (imagen: {imageData.Length} bytes)");
            
            // Guardar imagen temporal
            using (var image = Image.Load<Rgb24>(imageData))
            {
                Console.WriteLine($"   Dimensiones: {image.Width}x{image.Height}");
                await image.SaveAsPngAsync(tempImagePath);
            }
            
            // Ejecutar tesseract con configuración optimizada
            // --psm 3: Automatic page segmentation (funciona mejor para layouts variados)
            // --oem 1: LSTM neural net mode (mejor precisión que el motor legacy)
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "tesseract",
                    Arguments = $"\"{tempImagePath}\" \"{tempOutputPath}\" -l spa+eng --psm 3 --oem 1",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            // Configurar TESSDATA_PREFIX para usar modelos custom
            if (Directory.Exists(_tessDataPath))
            {
                process.StartInfo.Environment["TESSDATA_PREFIX"] = _tessDataPath;
                Console.WriteLine($"🔄 Ejecutando: tesseract -l spa+eng --psm 3 --oem 1");
            }
            else
            {
                Console.WriteLine($"🔄 Ejecutando: tesseract -l spa+eng --psm 3 --oem 1 (sistema)");
            }
            
            process.Start();
            
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            if (process.ExitCode != 0)
            {
                Console.WriteLine($"❌ Tesseract falló: {stderr}");
                return string.Empty;
            }
            
            // Leer resultado
            var outputFile = tempOutputPath + ".txt";
            if (!File.Exists(outputFile))
            {
                Console.WriteLine("⚠️  No se generó archivo de salida");
                return string.Empty;
            }
            
            var text = await File.ReadAllTextAsync(outputFile);
            text = text.Trim();
            
            // Usar ML para detectar si es código ANTES de limpiar
            bool isCode = false;
            if (_languageDetector?.IsAvailable == true && !string.IsNullOrWhiteSpace(text))
            {
                var language = await _languageDetector.DetectLanguageAsync(text);
                isCode = !string.IsNullOrEmpty(language);
            }
            
            // Solo limpiar si NO es código
            if (!isCode && !string.IsNullOrWhiteSpace(text))
            {
                // Post-procesamiento: limpiar artefactos comunes de iconos
                var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var cleanedLines = new List<string>();
                
                foreach (var line in lines)
                {
                    var cleaned = line.Trim();
                    
                    // Saltar líneas vacías o de un solo carácter
                    if (string.IsNullOrWhiteSpace(cleaned) || cleaned.Length == 1)
                        continue;
                    
                    // Eliminar caracteres sueltos al inicio (iconos): <, >, números solos, etc.
                    cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"^[<>O\d]\s+", "");
                    
                    // Eliminar caracteres sueltos al final (iconos): números solos, <, >, etc.
                    cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+[<>O\d]$", "");
                    
                    // Si después de limpiar queda algo válido, agregarlo
                    if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 1)
                    {
                        cleanedLines.Add(cleaned);
                    }
                }
                
                text = string.Join("\n", cleanedLines).Trim();
            }
            
            if (!string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine($"✅ OCR extrajo {text.Length} caracteres");
                Console.WriteLine($"   Texto: {text.Substring(0, Math.Min(100, text.Length))}...");
            }
            else
            {
                Console.WriteLine("⚠️  No se detectó texto");
            }
            
            // Limpiar archivo de salida
            File.Delete(outputFile);
            
            return text;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error en OCR: {ex.GetType().Name}: {ex.Message}");
            return string.Empty;
        }
        finally
        {
            // Limpiar archivos temporales
            try
            {
                if (File.Exists(tempImagePath)) File.Delete(tempImagePath);
                if (File.Exists(tempOutputPath)) File.Delete(tempOutputPath);
            }
            catch { }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
