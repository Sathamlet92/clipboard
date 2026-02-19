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
                ci.metadata, ci.thumbnail, ci.code_language,
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
                   timestamp, is_password, is_encrypted, metadata, thumbnail, code_language
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
                   timestamp, is_password, is_encrypted, metadata, thumbnail, code_language
            FROM clipboard_items
            WHERE embedding IS NOT NULL
            ORDER BY timestamp DESC
            LIMIT 100";  // Reducido de 200 a 100 para mejor performance

        var connection = await _factory.GetConnectionAsync();
        try
        {
            var rows = await connection.QueryAsync(sql);
            var items = rows.Select(MapToClipboardItem).ToList();
            
            // Calcular similitud coseno en paralelo
            var results = items
                .AsParallel()
                .Where(item => item.Embedding != null)
                .Select(item => new SearchResult
                {
                    Item = item,
                    Score = CosineSimilarity(queryEmbedding, item.Embedding!),
                    ResultType = SearchResultType.SemanticMatch
                })
                .OrderByDescending(r => r.Score)
                .Take(limit)
                .ToList();
            
            return results;
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
        float textWeight = 0.7f,  // Priorizar coincidencias exactas
        float semanticWeight = 0.3f,
        bool excludeCode = false)
    {
        // Ejecutar búsquedas en paralelo para mejor performance
        var textTask = FullTextSearchAsync(textQuery, limit * 2);
        var semanticTask = SemanticSearchAsync(queryEmbedding, limit * 2);
        
        await Task.WhenAll(textTask, semanticTask);
        
        var textResults = await textTask;
        var semanticResults = await semanticTask;
        
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
        
        // Combinar resultados: PRIORIZAR EXACTOS
        var combinedScores = new Dictionary<long, (ClipboardItem Item, float Score, bool HasExactMatch)>();
        
        // Procesar resultados de texto (exactos) - MAYOR PRIORIDAD
        foreach (var result in textResults)
        {
            var normalizedScore = Math.Abs(result.Score);
            normalizedScore = 1.0f / (1.0f + normalizedScore);
            var weightedScore = normalizedScore * textWeight;
            combinedScores[result.Item.Id] = (result.Item, weightedScore, true);
        }
        
        // Procesar resultados semánticos - MENOR PRIORIDAD
        foreach (var result in semanticResults)
        {
            var normalizedScore = result.Score;
            var weightedScore = normalizedScore * semanticWeight;
            
            if (combinedScores.ContainsKey(result.Item.Id))
            {
                var existing = combinedScores[result.Item.Id];
                combinedScores[result.Item.Id] = (
                    existing.Item, 
                    existing.Score + weightedScore,
                    existing.HasExactMatch
                );
            }
            else
            {
                combinedScores[result.Item.Id] = (result.Item, weightedScore, false);
            }
        }
        
        // Ordenar: PRIMERO los que tienen match exacto, LUEGO por score
        return combinedScores.Values
            .OrderByDescending(x => x.HasExactMatch)
            .ThenByDescending(x => x.Score)
            .Take(limit)
            .Select(x => new SearchResult
            {
                Item = x.Item,
                Score = x.Score,
                ResultType = x.HasExactMatch ? SearchResultType.TextMatch : SearchResultType.SemanticMatch
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
