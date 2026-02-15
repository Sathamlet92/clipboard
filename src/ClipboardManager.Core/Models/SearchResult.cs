namespace ClipboardManager.Core.Models;

public class SearchResult
{
    public ClipboardItem Item { get; set; } = null!;
    public float Score { get; set; }
    public SearchResultType ResultType { get; set; }
    public string? HighlightedText { get; set; }
}

public enum SearchResultType
{
    TextMatch,
    SemanticMatch,
    OcrMatch,
    HybridMatch
}

public enum SearchMode
{
    Text,      // 0 - Texto exacto (FTS5)
    Semantic,  // 1 - Semántico puro
    Hybrid     // 2 - Híbrido (recomendado)
}
