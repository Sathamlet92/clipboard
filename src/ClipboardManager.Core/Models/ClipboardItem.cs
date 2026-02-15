namespace ClipboardManager.Core.Models;

public class ClipboardItem
{
    public long Id { get; set; }
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public ClipboardType ContentType { get; set; }
    public string? OcrText { get; set; }
    public float[]? Embedding { get; set; }
    public string SourceApp { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsPassword { get; set; }
    public bool IsEncrypted { get; set; }
    public string? Metadata { get; set; }
    public byte[]? ThumbnailData { get; set; }
    public string? CodeLanguage { get; set; }
    
    // Helper properties
    public bool IsImage => ContentType == ClipboardType.Image;
    
    public string ContentAsText => IsEncrypted ? "••••••••" : System.Text.Encoding.UTF8.GetString(Content);
    
    public string ContentPreview
    {
        get
        {
            if (IsPassword)
                return "•••••••• (password oculto)";
            
            if (ContentType == ClipboardType.Image)
                return $"[Imagen - {Content.Length} bytes]";
            
            try
            {
                var text = System.Text.Encoding.UTF8.GetString(Content);
                return text.Length > 200 ? text.Substring(0, 200) + "..." : text;
            }
            catch
            {
                return $"[Contenido binario - {Content.Length} bytes]";
            }
        }
    }
    
    public bool HasOcr => !string.IsNullOrEmpty(OcrText);
    public bool HasEmbedding => Embedding != null && Embedding.Length > 0;
}
