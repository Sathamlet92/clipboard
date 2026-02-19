using System.Text.Json.Serialization;

namespace ClipboardManager.Core.Models;

/// <summary>
/// Configuración de tema visual de la aplicación.
/// </summary>
public class ThemeConfig
{
    [JsonPropertyName("window")]
    public WindowConfig Window { get; set; } = new();

    [JsonPropertyName("colors")]
    public ColorConfig Colors { get; set; } = new();

    [JsonPropertyName("fonts")]
    public FontConfig Fonts { get; set; } = new();

    [JsonPropertyName("spacing")]
    public SpacingConfig Spacing { get; set; } = new();
}

public class WindowConfig
{
    [JsonPropertyName("width")]
    public int Width { get; set; } = 600;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 500;

    [JsonPropertyName("cornerRadius")]
    public int CornerRadius { get; set; } = 12;

    [JsonPropertyName("borderThickness")]
    public int BorderThickness { get; set; } = 2;

    [JsonPropertyName("opacity")]
    public double Opacity { get; set; } = 0.95;
}

public class ColorConfig
{
    [JsonPropertyName("background")]
    public string Background { get; set; } = "#1E1E1E";

    [JsonPropertyName("backgroundAlt")]
    public string BackgroundAlt { get; set; } = "#252526";

    [JsonPropertyName("border")]
    public string Border { get; set; } = "#3E3E42";

    [JsonPropertyName("accent")]
    public string Accent { get; set; } = "#007ACC";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "#CCCCCC";

    [JsonPropertyName("textSecondary")]
    public string TextSecondary { get; set; } = "#858585";

    [JsonPropertyName("searchBar")]
    public string SearchBar { get; set; } = "#2D2D30";

    [JsonPropertyName("itemHover")]
    public string ItemHover { get; set; } = "#2A2D2E";

    [JsonPropertyName("codeBackground")]
    public string CodeBackground { get; set; } = "#1E1E1E";

    [JsonPropertyName("urlBackground")]
    public string UrlBackground { get; set; } = "#1E3A5F";

    [JsonPropertyName("urlText")]
    public string UrlText { get; set; } = "#4A90E2";

    [JsonPropertyName("ocrBackground")]
    public string OcrBackground { get; set; } = "#1E3A1E";

    [JsonPropertyName("ocrText")]
    public string OcrText { get; set; } = "#4EC9B0";
}

public class FontConfig
{
    [JsonPropertyName("family")]
    public string Family { get; set; } = "Segoe UI,Arial,sans-serif";

    [JsonPropertyName("monoFamily")]
    public string MonoFamily { get; set; } = "Cascadia Code,Consolas,Courier New,monospace";

    [JsonPropertyName("size")]
    public int Size { get; set; } = 13;

    [JsonPropertyName("sizeSmall")]
    public int SizeSmall { get; set; } = 11;

    [JsonPropertyName("sizeLarge")]
    public int SizeLarge { get; set; } = 14;
}

public class SpacingConfig
{
    [JsonPropertyName("itemPadding")]
    public int ItemPadding { get; set; } = 10;

    [JsonPropertyName("itemMargin")]
    public int ItemMargin { get; set; } = 5;

    [JsonPropertyName("itemSpacing")]
    public int ItemSpacing { get; set; } = 5;
}
