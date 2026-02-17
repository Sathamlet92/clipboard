using ClipboardManager.Core.Models;
using ClipboardManager.Core.Interfaces;
using Dapper;
using Microsoft.Data.Sqlite;

namespace ClipboardManager.Data.Repositories;

public class ClipboardRepository : IClipboardRepository
{
    private readonly ClipboardDbContextFactory _factory;

    public ClipboardRepository(ClipboardDbContextFactory factory)
    {
        _factory = factory;
    }

    public async Task<long> AddAsync(ClipboardItem item)
    {
        const string sql = @"
            INSERT INTO clipboard_items 
            (content, content_type, ocr_text, embedding, source_app, timestamp, 
             is_password, is_encrypted, metadata, thumbnail, code_language)
            VALUES 
            (@Content, @ContentType, @OcrText, @Embedding, @SourceApp, @Timestamp,
             @IsPassword, @IsEncrypted, @Metadata, @ThumbnailData, @CodeLanguage);
            SELECT last_insert_rowid();";

        var timestamp = new DateTimeOffset(item.Timestamp).ToUnixTimeSeconds();
        
        var connection = await _factory.GetConnectionAsync();
        try
        {
            var id = await connection.ExecuteScalarAsync<long>(sql, new
            {
                item.Content,
                ContentType = item.ContentType.ToString(),
                item.OcrText,
                Embedding = item.Embedding != null ? SerializeEmbedding(item.Embedding) : null,
                item.SourceApp,
                Timestamp = timestamp,
                item.IsPassword,
                item.IsEncrypted,
                item.Metadata,
                item.ThumbnailData,
                item.CodeLanguage
            });
            
            // Actualizar FTS manualmente
            await UpdateFtsAsync(connection, id, item);
            
            return id;
        }
        finally
        {
            _factory.ReleaseConnection();
        }
    }

    public async Task<ClipboardItem?> GetByIdAsync(long id)
    {
        const string sql = @"
            SELECT id, content, content_type, ocr_text, embedding, source_app, 
                   timestamp, is_password, is_encrypted, metadata, thumbnail, code_language
            FROM clipboard_items
            WHERE id = @Id";

        var connection = await _factory.GetConnectionAsync();
        try
        {
            var row = await connection.QuerySingleOrDefaultAsync(sql, new { Id = id });
            return row != null ? MapToClipboardItem(row) : null;
        }
        finally
        {
            _factory.ReleaseConnection();
        }
    }

    public async Task<List<ClipboardItem>> GetRecentAsync(int limit = 100)
    {
        const string sql = @"
            SELECT id, content, content_type, ocr_text, embedding, source_app, 
                   timestamp, is_password, is_encrypted, metadata, thumbnail, code_language
            FROM clipboard_items
            ORDER BY timestamp DESC
            LIMIT @Limit";

        var connection = await _factory.GetConnectionAsync();
        try
        {
            var rows = await connection.QueryAsync(sql, new { Limit = limit });
            return rows.Select(MapToClipboardItem).ToList();
        }
        finally
        {
            _factory.ReleaseConnection();
        }
    }

    public async Task<bool> UpdateAsync(ClipboardItem item)
    {
        const string sql = @"
            UPDATE clipboard_items
            SET content = @Content,
                content_type = @ContentType,
                ocr_text = @OcrText,
                embedding = @Embedding,
                source_app = @SourceApp,
                timestamp = @Timestamp,
                is_password = @IsPassword,
                is_encrypted = @IsEncrypted,
                metadata = @Metadata,
                thumbnail = @ThumbnailData,
                code_language = @CodeLanguage
            WHERE id = @Id";

        var timestamp = new DateTimeOffset(item.Timestamp).ToUnixTimeSeconds();
        
        var connection = await _factory.GetConnectionAsync();
        try
        {
            var affected = await connection.ExecuteAsync(sql, new
            {
                item.Id,
                item.Content,
                ContentType = item.ContentType.ToString(),
                item.OcrText,
                Embedding = item.Embedding != null ? SerializeEmbedding(item.Embedding) : null,
                item.SourceApp,
                Timestamp = timestamp,
                item.IsPassword,
                item.IsEncrypted,
                item.Metadata,
                item.ThumbnailData,
                item.CodeLanguage
            });

            if (affected > 0)
            {
                // Actualizar FTS
                var content = item.ContentType == ClipboardType.Image ? "" : 
                             System.Text.Encoding.UTF8.GetString(item.Content);
                
                const string ftsDeleteSql = "DELETE FROM clipboard_fts WHERE rowid = @Id";
                const string ftsInsertSql = @"
                    INSERT INTO clipboard_fts(rowid, content, ocr_text, code_language, source_app)
                    VALUES (@Id, @Content, @OcrText, @CodeLanguage, @SourceApp)";
                
                await connection.ExecuteAsync(ftsDeleteSql, new { item.Id });
                await connection.ExecuteAsync(ftsInsertSql, new
                {
                    item.Id,
                    Content = content,
                    OcrText = item.OcrText ?? "",
                    CodeLanguage = item.CodeLanguage ?? "",
                    SourceApp = item.SourceApp ?? ""
                });
            }

            return affected > 0;
        }
        finally
        {
            _factory.ReleaseConnection();
        }
    }

    public async Task<bool> DeleteAsync(long id)
    {
        const string sql = "DELETE FROM clipboard_items WHERE id = @Id";
        const string ftsSql = "DELETE FROM clipboard_fts WHERE rowid = @Id";
        
        var connection = await _factory.GetConnectionAsync();
        try
        {
            await connection.ExecuteAsync(ftsSql, new { Id = id });
            var affected = await connection.ExecuteAsync(sql, new { Id = id });
            return affected > 0;
        }
        finally
        {
            _factory.ReleaseConnection();
        }
    }

    public async Task<int> DeleteAllAsync()
    {
        const string sql = "DELETE FROM clipboard_items";
        const string ftsSql = "DELETE FROM clipboard_fts";
        
        var connection = await _factory.GetConnectionAsync();
        try
        {
            await connection.ExecuteAsync(ftsSql);
            return await connection.ExecuteAsync(sql);
        }
        finally
        {
            _factory.ReleaseConnection();
        }
    }

    public async Task<bool> UpdateOcrTextAsync(long id, string ocrText)
    {
        const string sql = @"
            UPDATE clipboard_items
            SET ocr_text = @OcrText
            WHERE id = @Id";
        
        var connection = await _factory.GetConnectionAsync();
        try
        {
            var affected = await connection.ExecuteAsync(sql, new { Id = id, OcrText = ocrText });
            
            if (affected > 0)
            {
                // Obtener el item completo para actualizar FTS correctamente
                const string getItemSql = @"
                    SELECT content, content_type, code_language, source_app
                    FROM clipboard_items
                    WHERE id = @Id";
                
                var item = await connection.QuerySingleOrDefaultAsync(getItemSql, new { Id = id });
                if (item != null)
                {
                    var content = item.content_type == "Image" ? "" : 
                                 System.Text.Encoding.UTF8.GetString((byte[])item.content);
                    
                    // Actualizar FTS - DELETE + INSERT
                    const string ftsDeleteSql = "DELETE FROM clipboard_fts WHERE rowid = @Id";
                    const string ftsInsertSql = @"
                        INSERT INTO clipboard_fts(rowid, content, ocr_text, code_language, source_app)
                        VALUES (@Id, @Content, @OcrText, @CodeLanguage, @SourceApp)";
                    
                    await connection.ExecuteAsync(ftsDeleteSql, new { Id = id });
                    await connection.ExecuteAsync(ftsInsertSql, new
                    {
                        Id = id,
                        Content = content,
                        OcrText = ocrText,
                        CodeLanguage = item.code_language ?? "",
                        SourceApp = item.source_app ?? ""
                    });
                }
            }
            
            return affected > 0;
        }
        finally
        {
            _factory.ReleaseConnection();
        }
    }
    
    private async Task UpdateFtsAsync(SqliteConnection connection, long id, ClipboardItem item)
    {
        const string sql = @"
            INSERT INTO clipboard_fts(rowid, content, ocr_text, code_language, source_app)
            VALUES (@Id, @Content, @OcrText, @CodeLanguage, @SourceApp)";
        
        var content = item.ContentType == ClipboardType.Image ? "" : 
                     System.Text.Encoding.UTF8.GetString(item.Content);
        
        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            Content = content,
            OcrText = item.OcrText ?? "",
            CodeLanguage = item.CodeLanguage ?? "",
            SourceApp = item.SourceApp ?? ""
        });
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffDate)
    {
        const string sql = "DELETE FROM clipboard_items WHERE timestamp < @Timestamp";
        var timestamp = new DateTimeOffset(cutoffDate).ToUnixTimeSeconds();
        var connection = await _factory.GetConnectionAsync();
        try
        {
            return await connection.ExecuteAsync(sql, new { Timestamp = timestamp });
        }
        finally
        {
            _factory.ReleaseConnection();
        }
    }

    public async Task<int> GetCountAsync()
    {
        const string sql = "SELECT COUNT(*) FROM clipboard_items";
        var connection = await _factory.GetConnectionAsync();
        try
        {
            return await connection.ExecuteScalarAsync<int>(sql);
        }
        finally
        {
            _factory.ReleaseConnection();
        }
    }

    public async Task<bool> ExistsByHashAsync(string contentHash)
    {
        const string sql = @"
            SELECT COUNT(*) 
            FROM clipboard_items 
            WHERE json_extract(metadata, '$.hash') = @Hash";
        
        var connection = await _factory.GetConnectionAsync();
        try
        {
            var count = await connection.ExecuteScalarAsync<int>(sql, new { Hash = contentHash });
            return count > 0;
        }
        finally
        {
            _factory.ReleaseConnection();
        }
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

    private static byte[] SerializeEmbedding(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] DeserializeEmbedding(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }
}
