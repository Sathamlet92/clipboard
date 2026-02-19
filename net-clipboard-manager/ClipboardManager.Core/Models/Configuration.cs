namespace ClipboardManager.Core.Models;

public class AppConfiguration
{
    public SecurityConfig Security { get; set; } = new();
    public PerformanceConfig Performance { get; set; } = new();
    public UiConfig Ui { get; set; } = new();
    public ClipboardConfig Clipboard { get; set; } = new();
}

public class SecurityConfig
{
    public PasswordHandling HandlePasswords { get; set; } = PasswordHandling.Encrypt;
    public bool ShowPasswords { get; set; } = false;
    public bool AutoDetectPasswords { get; set; } = true;
    public int PasswordTimeoutSeconds { get; set; } = 300;
    public bool EncryptSensitive { get; set; } = true;
}

public class PerformanceConfig
{
    public int MaxItems { get; set; } = 1000;
    public bool OcrEnabled { get; set; } = true;
    public bool OcrAsync { get; set; } = true;
    public bool SemanticSearch { get; set; } = true;
    public int ThumbnailSize { get; set; } = 200;
}

public class UiConfig
{
    public string Hotkey { get; set; } = "Ctrl+Shift+V";
    public string Theme { get; set; } = "dark";
    public bool ShowPreviews { get; set; } = true;
    public int ItemsPerPage { get; set; } = 20;
    public int WindowWidth { get; set; } = 800;
    public int WindowHeight { get; set; } = 600;
}

public class ClipboardConfig
{
    public bool MonitorImages { get; set; } = true;
    public bool MonitorText { get; set; } = true;
    public bool MonitorFiles { get; set; } = true;
    public List<string> IgnoreApps { get; set; } = new();
}

public enum PasswordHandling
{
    Ignore,
    Encrypt,
    Allow
}
