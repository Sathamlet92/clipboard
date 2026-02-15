using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ClipboardManager.ML.Services;

public class EmbeddingService : IDisposable
{
    private readonly InferenceSession? _session;
    private readonly Dictionary<string, int>? _vocab;
    private readonly int _maxLength = 128;
    private bool _disposed;
    private bool _isAvailable;

    public EmbeddingService(string modelPath)
    {
        _isAvailable = false;
        
        try
        {
            var modelFile = Path.Combine(modelPath, "embedding-model.onnx");
            var vocabFile = Path.Combine(modelPath, "vocab.txt");

            if (!File.Exists(modelFile))
            {
                Console.WriteLine($"⚠️  Modelo de embeddings no encontrado: {modelFile}");
                Console.WriteLine($"   Ejecuta: bash scripts/download-ml-models.sh");
                return;
            }

            // El modelo multilingüe puede no tener vocab.txt (usa tokenizer.json)
            if (File.Exists(vocabFile))
            {
                _vocab = LoadVocabulary(vocabFile);
                Console.WriteLine($"✅ Vocabulario cargado: {_vocab.Count} tokens");
            }
            else
            {
                // Crear vocabulario básico para el modelo multilingüe
                _vocab = CreateBasicVocabulary();
                Console.WriteLine($"✅ Vocabulario básico creado: {_vocab.Count} tokens");
            }

            // Intentar cargar modelo ONNX
            var options = new SessionOptions();
            options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            
            _session = new InferenceSession(modelFile, options);
            _isAvailable = true;
            
            Console.WriteLine($"✅ Modelo de embeddings cargado: {modelFile}");
            Console.WriteLine($"   Dimensiones: 384");
        }
        catch (TypeInitializationException ex)
        {
            Console.WriteLine($"⚠️  ONNX Runtime no disponible: {ex.InnerException?.Message ?? ex.Message}");
            Console.WriteLine($"   Instala: sudo pacman -S onnxruntime");
            Console.WriteLine($"   O usa: dotnet add package Microsoft.ML.OnnxRuntime.Managed");
            Console.WriteLine($"   Embeddings semánticos deshabilitados.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error cargando modelo de embeddings: {ex.Message}");
            Console.WriteLine($"   Embeddings semánticos deshabilitados.");
        }
    }

    public bool IsAvailable => _isAvailable;

    public async Task<float[]?> GetEmbeddingAsync(string text)
    {
        if (!_isAvailable || string.IsNullOrWhiteSpace(text))
            return null;

        return await Task.Run(() => GetEmbedding(text));
    }

    private float[]? GetEmbedding(string text)
    {
        if (_session == null || _vocab == null)
            return null;

        try
        {
            // Tokenizar texto
            var tokens = Tokenize(text);
            
            // Crear arrays para tensores
            var inputIdsData = new long[_maxLength];
            var attentionMaskData = new long[_maxLength];
            var tokenTypeIdsData = new long[_maxLength]; // Todos 0s para sentence-transformers
            
            for (int i = 0; i < Math.Min(tokens.Count, _maxLength); i++)
            {
                inputIdsData[i] = tokens[i];
                attentionMaskData[i] = 1;
                tokenTypeIdsData[i] = 0;
            }
            
            // Crear tensores con las dimensiones correctas [batch_size, sequence_length]
            var inputIdsTensor = new DenseTensor<long>(inputIdsData, new[] { 1, _maxLength });
            var attentionMaskTensor = new DenseTensor<long>(attentionMaskData, new[] { 1, _maxLength });
            var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIdsData, new[] { 1, _maxLength });
            
            // Crear inputs
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
                NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
            };
            
            // Ejecutar modelo
            using var results = _session.Run(inputs);
            
            // Obtener embeddings (último hidden state)
            var output = results.First().AsEnumerable<float>().ToArray();
            
            // Mean pooling: promediar todos los tokens
            var embedding = new float[384];
            int validTokens = tokens.Count;
            
            for (int i = 0; i < validTokens && i < _maxLength; i++)
            {
                for (int j = 0; j < 384; j++)
                {
                    embedding[j] += output[i * 384 + j];
                }
            }
            
            // Promediar
            for (int i = 0; i < 384; i++)
            {
                embedding[i] /= validTokens;
            }
            
            // Normalizar (L2 norm)
            var norm = Math.Sqrt(embedding.Sum(x => x * x));
            for (int i = 0; i < 384; i++)
            {
                embedding[i] /= (float)norm;
            }
            
            return embedding;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error generando embedding: {ex.Message}");
            return null;
        }
    }

    private List<int> Tokenize(string text)
    {
        if (_vocab == null)
            return new List<int>();

        // Tokenización simple basada en vocabulario
        // [CLS] token + palabras + [SEP] token
        var tokens = new List<int> { 101 }; // [CLS]
        
        // Convertir a minúsculas y dividir por espacios
        var words = text.ToLower().Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var word in words)
        {
            if (tokens.Count >= _maxLength - 1)
                break;
            
            // Buscar palabra en vocabulario
            if (_vocab.TryGetValue(word, out int tokenId))
            {
                tokens.Add(tokenId);
            }
            else
            {
                // Si no existe, usar [UNK] token
                tokens.Add(100);
            }
        }
        
        // Agregar [SEP] token
        tokens.Add(102);
        
        return tokens;
    }

    private Dictionary<string, int> LoadVocabulary(string vocabFile)
    {
        var vocab = new Dictionary<string, int>();
        var lines = File.ReadAllLines(vocabFile);
        
        for (int i = 0; i < lines.Length; i++)
        {
            var token = lines[i].Trim();
            if (!string.IsNullOrEmpty(token))
            {
                vocab[token] = i;
            }
        }
        
        return vocab;
    }

    private Dictionary<string, int> CreateBasicVocabulary()
    {
        // Vocabulario básico para modelo multilingüe
        // Incluye tokens especiales y caracteres comunes
        var vocab = new Dictionary<string, int>
        {
            ["[PAD]"] = 0,
            ["[UNK]"] = 100,
            ["[CLS]"] = 101,
            ["[SEP]"] = 102,
            ["[MASK]"] = 103
        };
        
        // Agregar letras y números básicos
        int id = 1000;
        for (char c = 'a'; c <= 'z'; c++)
            vocab[c.ToString()] = id++;
        for (char c = '0'; c <= '9'; c++)
            vocab[c.ToString()] = id++;
        
        return vocab;
    }

    public static float CosineSimilarity(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length != b.Length)
            return 0f;
        
        float dotProduct = 0f;
        float normA = 0f;
        float normB = 0f;
        
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        
        if (normA == 0 || normB == 0)
            return 0f;
        
        return dotProduct / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _session?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
