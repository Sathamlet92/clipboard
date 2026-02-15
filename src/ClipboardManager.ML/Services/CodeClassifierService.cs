using System;
using System.Linq;

namespace ClipboardManager.ML.Services;

/// <summary>
/// Clasificador de código usando embeddings semánticos
/// </summary>
public class CodeClassifierService
{
    private readonly EmbeddingService? _embeddingService;
    
    // Ejemplos de código para comparación semántica
    private static readonly string[] CodeExamples = new[]
    {
        "def calculate_sum(a, b): return a + b",
        "function getData() { return fetch('/api/data'); }",
        "public class Program { static void Main() { } }",
        "import numpy as np\narray = np.zeros((10, 10))",
        "const result = items.map(x => x * 2);",
        "SELECT * FROM users WHERE age > 18;",
        "for i in range(10): print(i)",
        "if (condition) { doSomething(); }",
        "async function fetchData() { await api.get(); }",
        "class MyClass: def __init__(self): pass"
    };
    
    // Ejemplos de texto natural para comparación
    private static readonly string[] TextExamples = new[]
    {
        "El perro corre por el parque",
        "The quick brown fox jumps over the lazy dog",
        "Hoy es un día soleado y hermoso",
        "I need to buy groceries at the store",
        "La programación es una habilidad importante",
        "Please send me the report by tomorrow",
        "El gato egipcio es una raza antigua",
        "Weather forecast shows rain this weekend",
        "Me gusta leer libros de ciencia ficción",
        "The meeting is scheduled for 3 PM"
    };
    
    private float[]? _codePrototype;
    private float[]? _textPrototype;
    private bool _isInitialized;

    public CodeClassifierService(EmbeddingService? embeddingService = null)
    {
        _embeddingService = embeddingService;
    }

    public bool IsAvailable => _isInitialized && _embeddingService?.IsAvailable == true;

    /// <summary>
    /// Inicializa los prototipos de código y texto
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_embeddingService == null || !_embeddingService.IsAvailable)
        {
            Console.WriteLine("⚠️  CodeClassifier: EmbeddingService no disponible");
            return;
        }

        try
        {
            // Generar embeddings para ejemplos de código
            var codeEmbeddings = new List<float[]>();
            foreach (var example in CodeExamples.Take(5)) // Solo 5 para ser rápido
            {
                var embedding = await _embeddingService.GetEmbeddingAsync(example);
                if (embedding != null)
                    codeEmbeddings.Add(embedding);
            }

            // Generar embeddings para ejemplos de texto
            var textEmbeddings = new List<float[]>();
            foreach (var example in TextExamples.Take(5))
            {
                var embedding = await _embeddingService.GetEmbeddingAsync(example);
                if (embedding != null)
                    textEmbeddings.Add(embedding);
            }

            // Calcular prototipos (promedio de embeddings)
            if (codeEmbeddings.Count > 0)
                _codePrototype = AverageEmbeddings(codeEmbeddings);
            
            if (textEmbeddings.Count > 0)
                _textPrototype = AverageEmbeddings(textEmbeddings);

            _isInitialized = true;
            Console.WriteLine($"✅ CodeClassifier inicializado con {codeEmbeddings.Count} ejemplos de código y {textEmbeddings.Count} de texto");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error inicializando CodeClassifier: {ex.Message}");
        }
    }

    /// <summary>
    /// Clasifica si el texto es código o no usando ML
    /// </summary>
    /// <returns>Probabilidad de que sea código (0.0 - 1.0)</returns>
    public async Task<float> GetCodeProbabilityAsync(string text)
    {
        if (!_isInitialized || _embeddingService == null || _codePrototype == null || _textPrototype == null)
        {
            return 0.5f; // No sabemos, 50/50
        }

        try
        {
            // Generar embedding del texto
            var embedding = await _embeddingService.GetEmbeddingAsync(text);
            if (embedding == null)
                return 0.5f;

            // Calcular similitud con prototipos
            var codeSimil = CosineSimilarity(embedding, _codePrototype);
            var textSimilarity = CosineSimilarity(embedding, _textPrototype);

            // Normalizar a probabilidad (0-1)
            // Si es más similar a código que a texto, probabilidad alta
            var totalSimilarity = codeSimil + textSimilarity;
            if (totalSimilarity == 0)
                return 0.5f;

            var probability = codeSimil / totalSimilarity;
            
            Console.WriteLine($"🤖 ML Classifier: code={codeSimil:F3}, text={textSimilarity:F3}, prob={probability:F3}");
            
            return probability;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error en clasificación ML: {ex.Message}");
            return 0.5f;
        }
    }

    /// <summary>
    /// Clasifica si es código (threshold 0.6)
    /// </summary>
    public async Task<bool> IsCodeAsync(string text)
    {
        var probability = await GetCodeProbabilityAsync(text);
        return probability > 0.6f; // 60% de confianza
    }

    private static float[] AverageEmbeddings(List<float[]> embeddings)
    {
        if (embeddings.Count == 0)
            throw new ArgumentException("No embeddings to average");

        var dimension = embeddings[0].Length;
        var average = new float[dimension];

        foreach (var embedding in embeddings)
        {
            for (int i = 0; i < dimension; i++)
            {
                average[i] += embedding[i];
            }
        }

        for (int i = 0; i < dimension; i++)
        {
            average[i] /= embeddings.Count;
        }

        return average;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have same length");

        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = MathF.Sqrt(magnitudeA);
        magnitudeB = MathF.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct / (magnitudeA * magnitudeB);
    }
}
