using System;
using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace ClipboardManager.App.Controls;

public partial class CodePreviewControl : UserControl
{
    public static readonly StyledProperty<string> CodeProperty =
        AvaloniaProperty.Register<CodePreviewControl, string>(nameof(Code), string.Empty);

    public static readonly StyledProperty<string> LanguageProperty =
        AvaloniaProperty.Register<CodePreviewControl, string>(nameof(Language), string.Empty);

    private TextMate.Installation? _textMateInstallation;
    private RegistryOptions? _registryOptions;

    public string Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    public string Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    public CodePreviewControl()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CodeProperty || change.Property == LanguageProperty)
        {
            UpdateCode();
        }
    }

    private void UpdateCode()
    {
        if (CodeEditor == null)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateCode(), Avalonia.Threading.DispatcherPriority.Loaded);
            return;
        }
        
        // Actualizar texto
        CodeEditor.Text = Code ?? string.Empty;
        
        // Configurar TextMate si no está inicializado
        if (_textMateInstallation == null && !string.IsNullOrEmpty(Code))
        {
            try
            {
                _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
                _textMateInstallation = CodeEditor.InstallTextMate(_registryOptions);
            }
            catch
            {
                // Silently fail
            }
        }
        
        // Aplicar grammar según el lenguaje
        if (_textMateInstallation != null && _registryOptions != null && !string.IsNullOrEmpty(Language))
        {
            try
            {
                var extension = GetFileExtension(Language);
                var language = _registryOptions.GetLanguageByExtension(extension);
                
                if (language != null)
                {
                    var scopeName = _registryOptions.GetScopeByLanguageId(language.Id);
                    _textMateInstallation.SetGrammar(scopeName);
                }
            }
            catch
            {
                // Silently fail
            }
        }
    }

    private string GetFileExtension(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "csharp" or "c#" => ".cs",
            "python" => ".py",
            "javascript" or "js" => ".js",
            "typescript" or "ts" => ".ts",
            "java" => ".java",
            "cpp" or "c++" => ".cpp",
            "c" => ".c",
            "rust" => ".rs",
            "go" => ".go",
            "kotlin" => ".kt",
            "sql" => ".sql",
            "html" => ".html",
            "css" => ".css",
            "json" => ".json",
            "xml" => ".xml",
            "bash" or "shell" => ".sh",
            "php" => ".php",
            "ruby" => ".rb",
            "swift" => ".swift",
            "powershell" => ".ps1",
            "vb" or "visualbasic" => ".vb",
            "perl" => ".pl",
            "r" => ".r",
            "scala" => ".scala",
            "lua" => ".lua",
            "dart" => ".dart",
            "yaml" or "yml" => ".yaml",
            "markdown" or "md" => ".md",
            _ => ".txt"
        };
    }
}
