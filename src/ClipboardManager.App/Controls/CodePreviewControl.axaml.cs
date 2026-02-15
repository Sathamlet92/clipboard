using System;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace ClipboardManager.App.Controls;

public partial class CodePreviewControl : UserControl
{
    private RegistryOptions? _registryOptions;

    public CodePreviewControl()
    {
        InitializeComponent();
        InitializeTextMate();
    }

    private void InitializeTextMate()
    {
        try
        {
            // Inicializar TextMate con tema dark
            _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error inicializando TextMate: {ex.Message}");
        }
    }

    public void SetCode(string code, string language)
    {
        if (CodeEditor == null)
        {
            return;
        }

        try
        {
            // Establecer el texto
            CodeEditor.Text = code;

            // Si tenemos TextMate, aplicar highlighting
            if (_registryOptions != null)
            {
                var scopeName = MapLanguageToScope(language);
                
                if (!string.IsNullOrEmpty(scopeName))
                {
                    var installation = CodeEditor.InstallTextMate(_registryOptions);
                    installation.SetGrammar(scopeName);
                    
                    Console.WriteLine($"✅ Syntax highlighting aplicado: {language} -> {scopeName}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error aplicando syntax highlighting: {ex.Message}");
            // Fallback: mostrar código sin highlighting
            CodeEditor.Text = code;
        }
    }

    private string MapLanguageToScope(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "c#" or "csharp" => "source.cs",
            "python" => "source.python",
            "javascript" or "js" => "source.js",
            "typescript" or "ts" => "source.ts",
            "java" => "source.java",
            "c++" or "cpp" => "source.cpp",
            "c" => "source.c",
            "go" => "source.go",
            "rust" => "source.rust",
            "ruby" => "source.ruby",
            "php" => "source.php",
            "html" => "text.html.basic",
            "css" => "source.css",
            "json" => "source.json",
            "xml" => "text.xml",
            "yaml" or "yml" => "source.yaml",
            "sql" => "source.sql",
            "bash" or "shell" or "sh" => "source.shell",
            "powershell" or "ps1" => "source.powershell",
            "markdown" or "md" => "text.html.markdown",
            _ => string.Empty
        };
    }
}
