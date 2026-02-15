using ClipboardManager.Core.Models;
using Dapper;

namespace ClipboardManager.Data.Repositories;

public class SearchRepository
{
    private readonly ClipboardDbContextFactory _factory;

    public SearchRepository(ClipboardDbContextFactory factory)
    {
        _factory = factory;
    }

    public async Task<List<SearchResult>> FullTextSearchAsync(string query, int limit = 20)
    {
        const string sql = @"
            SELECT 
                ci.id, ci.content, ci.content_type, ci.ocr_text, ci.embedding, 
                ci.source_app, ci.timestamp, ci.is_password, ci.is_encrypted, 
                ci.metadata, ci.thumbnail,
                fts.rank as score
            FROM clipboard_fts fts
            INNER JOIN clipboard_items ci ON fts.rowid = ci.id
            WHERE clipboard_fts MATCH @Query
            ORDER BY fts.rank
            LIMIT @Limit";

        var connection = await _factory.GetConnectionAsync();
        try
        {
            var rows = await connection.QueryAsync(sql, new { Query = query, Limit = limit });
            
            return rows.Select(row => new SearchResult
            {
                Item = MapToClipboardItem(row),
                Score = (float)row.score,
                ResultType = SearchResultType.TextMatch
            }).ToList();
        }
        finally
        {
            _factory.ReleaseConnection();
        }
    }

    public async Task<List<ClipboardItem>> GetAllWithEmbeddingsAsync()
    {
        const string sql = @"
            SELECT id, content, content_type, ocr_text, embedding, source_app, 
                   timestamp, is_password, is_encrypted, metadata, thumbnail
            FROM clipboard_items
            WHERE embedding IS NOT NULL
            ORDER BY timestamp DESC";

        var connection = await _factory.GetConnectionAsync();
        try
        {
            var rows = await connection.QueryAsync(sql);
            return rows.Select(MapToClipboardItem).ToList();
        }
        finally
        {
            _factory.ReleaseConnection();
        }
    }

    public async Task<List<ClipboardItem>> FilterByTypeAsync(ClipboardType type, int limit = 100)
    {
        const string sql = @"
            SELECT id, content, content_type, ocr_text, embedding, source_app, 
                   timestamp, is_password, is_encrypted, metadata, thumbnail
            FROM clipboard_items
            WHERE content_type = @Type
            ORDER BY timestamp DESC
            LIMIT @Limit";

        var connection = await _factory.GetConnectionAsync();
        try
        {
            var rows = await connection.QueryAsync(sql, new 
            { 
                Type = type.ToString(), 
                Limit = limit 
            });
            
            return rows.Select(MapToClipboardItem).ToList();
        }
        finally
        {
            _factory.ReleaseConnection();
        }
    }

    public async Task<List<ClipboardItem>> FilterByDateRangeAsync(
        DateTime startDate, 
        DateTime endDate, 
        int limit = 100)
    {
        const string sql = @"
            SELECT id, content, content_type, ocr_text, embedding, source_app, 
                   timestamp, is_password, is_encrypted, metadata, thumbnail
            FROM clipboard_items
            WHERE timestamp BETWEEN @StartTimestamp AND @EndTimestamp
            ORDER BY timestamp DESC
            LIMIT @Limit";

        var startTimestamp = new DateTimeOffset(startDate).ToUnixTimeSeconds();
        var endTimestamp = new DateTimeOffset(endDate).ToUnixTimeSeconds();

        var connection = await _factory.GetConnectionAsync();
        try
        {
            var rows = await connection.QueryAsync(sql, new 
            { 
                StartTimestamp = startTimestamp,
                EndTimestamp = endTimestamp,
                Limit = limit 
            });
            
            return rows.Select(MapToClipboardItem).ToList();
        }
        finally
        {
            _factory.ReleaseConnection();
        }
    }

    public async Task<List<ClipboardItem>> FilterBySourceAppAsync(string sourceApp, int limit = 100)
    {
        const string sql = @"
            SELECT id, content, content_type, ocr_text, embedding, source_app, 
                   timestamp, is_password, is_encrypted, metadata, thumbnail
            FROM clipboard_items
            WHERE source_app = @SourceApp
            ORDER BY timestamp DESC
            LIMIT @Limit";

        var connection = await _factory.GetConnectionAsync();
        try
        {
            var rows = await connection.QueryAsync(sql, new 
            { 
                SourceApp = sourceApp, 
                Limit = limit 
            });
            
            return rows.Select(MapToClipboardItem).ToList();
        }
        finally
        {
            _factory.ReleaseConnection();
        }
    }

    public async Task<List<SearchResult>> SemanticSearchAsync(float[] queryEmbedding, int limit = 20)
    {
        const string sql = @"
            SELECT id, content, content_type, ocr_text, embedding, source_app, 
                   timestamp, is_password, is_encrypted, metadata, thumbnail
            FROM clipboard_items
            WHERE embedding IS NOT NULL
            ORDER BY timestamp DESC
            LIMIT 500";

        var connection = await _factory.GetConnectionAsync();
        try
        {
            var rows = await connection.QueryAsync(sql);
            var items = rows.Select(MapToClipboardItem).ToList();
            
            // Calcular similitud coseno con cada item
            var results = new List<SearchResult>();
            foreach (var item in items)
            {
                if (item.Embedding == null) continue;
                
                var similarity = CosineSimilarity(queryEmbedding, item.Embedding);
                results.Add(new SearchResult
                {
                    Item = item,
                    Score = similarity,
                    ResultType = SearchResultType.SemanticMatch
                });
            }
            
            // Ordenar por similitud y tomar top N
            return results
                .OrderByDescending(r => r.Score)
                .Take(limit)
                .ToList();
        }
        finally
        {
            _factory.ReleaseConnection();
        }
    }

    public async Task<List<SearchResult>> HybridSearchAsync(
        string textQuery, 
        float[] queryEmbedding, 
        int limit = 20,
        float textWeight = 0.3f,
        float semanticWeight = 0.7f,
        bool excludeCode = false)
    {
        // Búsqueda FTS5
        var textResults = await FullTextSearchAsync(textQuery, limit * 2);
        
        // Búsqueda semántica
        var semanticResults = await SemanticSearchAsync(queryEmbedding, limit * 2);
        
        // Filtrar código si se solicita
        if (excludeCode)
        {
            textResults = textResults
                .Where(r => r.Item.ContentType != ClipboardType.Code)
                .ToList();
            semanticResults = semanticResults
                .Where(r => r.Item.ContentType != ClipboardType.Code)
                .ToList();
        }
        
        // Combinar resultados con pesos
        var combinedScores = new Dictionary<long, (ClipboardItem Item, float Score, float TextScore, float SemanticScore)>();
        
        foreach (var result in textResults)
        {
            // FTS5 rank: valores negativos, más cercano a 0 = mejor match
            // Invertir para que mayor score = mejor
            var normalizedScore = Math.Abs(result.Score); // Convertir a positivo
            normalizedScore = 1.0f / (1.0f + normalizedScore); // Normalizar: mejor match → 1.0, peor → 0.0
            var weightedScore = normalizedScore * textWeight;
            combinedScores[result.Item.Id] = (result.Item, weightedScore, normalizedScore, 0f);
        }
        
        foreach (var result in semanticResults)
        {
            var normalizedScore = result.Score; // Ya está entre 0-1
            var weightedScore = normalizedScore * semanticWeight;
            if (combinedScores.ContainsKey(result.Item.Id))
            {
                var existing = combinedScores[result.Item.Id];
                combinedScores[result.Item.Id] = (
                    existing.Item, 
                    existing.Score + weightedScore,
                    existing.TextScore,
                    normalizedScore
                );
            }
            else
            {
                combinedScores[result.Item.Id] = (result.Item, weightedScore, 0f, normalizedScore);
            }
        }
        
        // Ordenar por score combinado y mostrar top 5 con debug
        var sortedResults = combinedScores.Values
            .OrderByDescending(x => x.Score)
            .ToList();
        
        // Debug: mostrar top 5 scores
        Console.WriteLine($"🔍 Top 5 resultados híbridos para '{textQuery}':");
        foreach (var result in sortedResults.Take(5))
        {
            var preview = System.Text.Encoding.UTF8.GetString(result.Item.Content);
            preview = preview.Length > 30 ? preview.Substring(0, 30) + "..." : preview;
            Console.WriteLine($"   Score: {result.Score:F3} (Text: {result.TextScore:F3}, Semantic: {result.SemanticScore:F3}) - {preview}");
        }
        
        // Ordenar por score combinado
        return sortedResults
            .Take(limit)
            .Select(x => new SearchResult
            {
                Item = x.Item,
                Score = x.Score,
                ResultType = SearchResultType.HybridMatch
            })
            .ToList();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0f;
        
        float dotProduct = 0f;
        float normA = 0f;
        float normB = 0f;
        
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        
        if (normA == 0 || normB == 0) return 0f;
        
        return dotProduct / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    private static float NormalizeScore(float score, float min, float max)
    {
        if (max == min) return 0f;
        return Math.Clamp((score - min) / (max - min), 0f, 1f);
    }

    private static ClipboardItem MapToClipboardItem(dynamic row)
    {
        return new ClipboardItem
        {
            Id = row.id,
            Content = row.content,
            ContentType = Enum.Parse<ClipboardType>(row.content_type),
            OcrText = row.ocr_text,
            Embedding = row.embedding != null ? DeserializeEmbedding(row.embedding) : null,
            SourceApp = row.source_app ?? string.Empty,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)row.timestamp).DateTime,
            IsPassword = row.is_password == 1,
            IsEncrypted = row.is_encrypted == 1,
            Metadata = row.metadata,
            ThumbnailData = row.thumbnail,
            CodeLanguage = row.code_language
        };
    }

    private static float[] DeserializeEmbedding(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }
}
