using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ClipboardManager.ML.Services;

public class LanguageDetectionService : IDisposable
{
    private readonly InferenceSession? _session;
    private readonly Dictionary<string, int>? _vocab;
    private readonly List<string>? _labels;
    private readonly BpeTokenizer? _tokenizer;
    private readonly int _maxLength = 512;
    private bool _disposed;
    private bool _isAvailable;

    public LanguageDetectionService(string modelPath)
    {
        _isAvailable = false;
        
        try
        {
            var modelFile = Path.Combine(modelPath, "model.onnx");
            var vocabFile = Path.Combine(modelPath, "vocab.json");
            var mergesFile = Path.Combine(modelPath, "merges.txt");
            var labelsFile = Path.Combine(modelPath, "labels.txt");

            if (!File.Exists(modelFile))
            {
                Console.WriteLine($"⚠️  Modelo de detección de lenguajes no encontrado: {modelFile}");
                Console.WriteLine($"   Ejecuta: bash scripts/download-ml-models.sh");
                return;
            }

            if (!File.Exists(vocabFile) || !File.Exists(mergesFile))
            {
                Console.WriteLine($"⚠️  Archivos de tokenizer no encontrados");
                Console.WriteLine($"   Ejecuta: bash scripts/download-ml-models.sh");
                return;
            }

            // Cargar tokenizer BPE
            _tokenizer = new BpeTokenizer(vocabFile, mergesFile);
            _vocab = new Dictionary<string, int>(); // Placeholder, usamos el tokenizer
            Console.WriteLine($"✅ Tokenizer BPE cargado");

            // Cargar labels (lenguajes)
            if (File.Exists(labelsFile))
            {
                _labels = File.ReadAllLines(labelsFile).ToList();
                Console.WriteLine($"✅ Labels cargados: {_labels.Count} lenguajes");
            }
            else
            {
                Console.WriteLine($"⚠️  Labels no encontrados: {labelsFile}");
                return;
            }

            // Cargar modelo ONNX
            var options = new SessionOptions();
            options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            
            _session = new InferenceSession(modelFile, options);
            _isAvailable = true;
            
            Console.WriteLine($"✅ Modelo de detección de lenguajes cargado: {modelFile}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error cargando modelo de detección de lenguajes: {ex.Message}");
            Console.WriteLine($"   Detección de lenguajes deshabilitada.");
        }
    }

    public bool IsAvailable => _isAvailable;

    public async Task<string?> DetectLanguageAsync(string code)
    {
        if (!_isAvailable || string.IsNullOrWhiteSpace(code))
            return null;

        return await Task.Run(() => DetectLanguage(code));
    }

    private string? DetectLanguage(string code)
    {
        if (_session == null || _tokenizer == null || _labels == null)
            return null;

        try
        {
            // Truncar código a 2000 caracteres máximo
            var truncatedCode = code.Length > 2000 ? code.Substring(0, 2000) : code;
            
            Console.WriteLine($"🔍 Detectando lenguaje para código de {code.Length} caracteres");
            Console.WriteLine($"   Primeros 100 chars: {truncatedCode.Substring(0, Math.Min(100, truncatedCode.Length))}");
            
            // Tokenizar código usando BPE
            var tokens = _tokenizer.Encode(truncatedCode, _maxLength);
            
            Console.WriteLine($"   Tokens generados: {tokens.Count}");
            
            // Crear arrays para tensores
            var inputIdsData = new long[_maxLength];
            var attentionMaskData = new long[_maxLength];
            
            for (int i = 0; i < Math.Min(tokens.Count, _maxLength); i++)
            {
                inputIdsData[i] = tokens[i];
                attentionMaskData[i] = 1;
            }
            
            // Crear tensores [batch_size, sequence_length]
            var inputIdsTensor = new DenseTensor<long>(inputIdsData, new[] { 1, _maxLength });
            var attentionMaskTensor = new DenseTensor<long>(attentionMaskData, new[] { 1, _maxLength });
            
            // Crear inputs
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
            };
            
            // Ejecutar modelo
            using var results = _session.Run(inputs);
            
            // Obtener logits
            var logits = results.First().AsEnumerable<float>().ToArray();
            
            Console.WriteLine($"   Logits recibidos: {logits.Length}");
            
            // Encontrar clase con mayor probabilidad
            var maxIndex = 0;
            var maxValue = logits[0];
            for (int i = 1; i < logits.Length && i < _labels.Count; i++)
            {
                if (logits[i] > maxValue)
                {
                    maxValue = logits[i];
                    maxIndex = i;
                }
            }
            
            // Mostrar top 3
            var topScores = logits.Select((score, idx) => new { Score = score, Index = idx, Label = idx < _labels.Count ? _labels[idx] : "?" })
                .OrderByDescending(x => x.Score)
                .Take(3)
                .ToList();
            
            Console.WriteLine($"   Top 3: {string.Join(", ", topScores.Select(x => $"{x.Label}={x.Score:F2}"))}");
            
            // UMBRAL DE CONFIANZA: Si el score es muy bajo, no es código válido
            // Scores típicos de código real: 6.0-8.0
            // Scores típicos de texto/basura: 2.0-4.0
            if (maxValue < 4.5f)
            {
                Console.WriteLine($"   ⚠️  Score muy bajo ({maxValue:F2}), probablemente no es código");
                return null; // Reclasificar como texto
            }
            
            var detectedLanguage = _labels[maxIndex];
            
            // Mapear nombres del modelo a nombres estándar
            detectedLanguage = MapLanguageName(detectedLanguage);
            
            Console.WriteLine($"   ✅ Detectado: {detectedLanguage} (confianza: {maxValue:F2})");
            
            return detectedLanguage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error detectando lenguaje: {ex.Message}");
            Console.WriteLine($"   Stack: {ex.StackTrace}");
            return null;
        }
    }

    private string MapLanguageName(string modelName)
    {
        // Mapear nombres del modelo a nombres estándar usados en la UI
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["C#"] = "csharp",
            ["C++"] = "cpp",
            ["Visual Basic .NET"] = "vb",
            ["ARM Assembly"] = "asm",
            ["Mathematica/Wolfram Language"] = "mathematica",
            ["PowerShell"] = "powershell",
            ["AppleScript"] = "applescript"
        };

        if (mapping.TryGetValue(modelName, out var standardName))
            return standardName;

        // Para el resto, convertir a minúsculas
        return modelName.ToLower();
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _session?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
