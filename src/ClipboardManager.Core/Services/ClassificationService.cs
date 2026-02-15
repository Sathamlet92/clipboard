using ClipboardManager.Core.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace ClipboardManager.Core.Services;

public class ClassificationService
{
    private static readonly Dictionary<string, string[]> CodePatterns = new()
    {
        ["csharp"] = new[] { "using ", "namespace ", "class ", "public ", "private ", "void ", "async ", "await " },
        ["python"] = new[] { "def ", "import ", "from ", "class ", "if __name__", "print(", "self." },
        ["javascript"] = new[] { "function ", "const ", "let ", "var ", "=>", "console.log", "require(" },
        ["typescript"] = new[] { "interface ", "type ", "const ", "let ", ": string", ": number", "export " },
        ["java"] = new[] { "public class", "private ", "protected ", "import java.", "System.out" },
        ["cpp"] = new[] { "#include", "std::", "cout", "cin", "namespace ", "template<" },
        ["rust"] = new[] { "fn ", "let ", "mut ", "impl ", "use ", "pub ", "match " },
        ["go"] = new[] { "package ", "func ", "import ", "type ", "var ", "defer ", "go " },
        ["sql"] = new[] { "SELECT ", "FROM ", "WHERE ", "INSERT ", "UPDATE ", "DELETE ", "CREATE TABLE" },
        ["html"] = new[] { "<!DOCTYPE", "<html", "<head", "<body", "<div", "<span", "<script" },
        ["css"] = new[] { "{", "}", ":", ";", "px", "color:", "background:", "margin:" },
        ["json"] = new[] { "{", "}", "[", "]", "\":", "\",", "null" },
        ["xml"] = new[] { "<?xml", "<", "/>", "</", "xmlns" },
        ["bash"] = new[] { "#!/bin/bash", "echo ", "if [", "then", "fi", "for ", "done" }
    };

    public ClipboardType Classify(byte[] content, string? mimeType = null)
    {
        // Si es binario y grande, probablemente es imagen
        if (IsLikelyImage(content, mimeType))
            return ClipboardType.Image;

        // Intentar decodificar como texto
        string text;
        try
        {
            text = Encoding.UTF8.GetString(content);
        }
        catch
        {
            return ClipboardType.Text; // Default fallback
        }

        // Clasificar por contenido
        if (IsUrl(text))
            return ClipboardType.Url;

        if (IsEmail(text))
            return ClipboardType.Email;

        if (IsPhone(text))
            return ClipboardType.Phone;

        if (IsFilePath(text))
            return ClipboardType.FilePath;

        if (IsTerminalCommand(text))
            return ClipboardType.Code; // Comandos se clasifican como Code

        if (IsCode(text))
            return ClipboardType.Code;

        if (IsRichText(text, mimeType))
            return ClipboardType.RichText;

        return ClipboardType.Text;
    }

    public string? DetectCodeLanguage(string text)
    {
        if (!IsCode(text))
            return null;

        var scores = new Dictionary<string, int>();

        foreach (var (language, patterns) in CodePatterns)
        {
            var score = patterns.Count(pattern => 
                text.Contains(pattern, StringComparison.OrdinalIgnoreCase));
            
            if (score > 0)
                scores[language] = score;
        }

        if (scores.Count == 0)
            return null;

        return scores.OrderByDescending(x => x.Value).First().Key;
    }

    private static bool IsLikelyImage(byte[] content, string? mimeType)
    {
        if (mimeType?.StartsWith("image/") == true)
            return true;

        // Verificar magic numbers comunes
        if (content.Length < 4)
            return false;

        // PNG: 89 50 4E 47
        if (content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47)
            return true;

        // JPEG: FF D8 FF
        if (content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
            return true;

        // GIF: 47 49 46
        if (content[0] == 0x47 && content[1] == 0x49 && content[2] == 0x46)
            return true;

        // BMP: 42 4D
        if (content[0] == 0x42 && content[1] == 0x4D)
            return true;

        return false;
    }

    private static bool IsUrl(string text)
    {
        text = text.Trim();
        
        if (text.Length > 2048) // URLs muy largas probablemente no son URLs
            return false;

        if (text.Contains('\n') || text.Contains('\r'))
            return false;

        return Regex.IsMatch(text, 
            @"^https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)$",
            RegexOptions.IgnoreCase);
    }

    private static bool IsEmail(string text)
    {
        text = text.Trim();
        
        if (text.Length > 254) // RFC 5321
            return false;

        if (text.Contains('\n') || text.Contains('\r'))
            return false;

        return Regex.IsMatch(text,
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");
    }

    private static bool IsPhone(string text)
    {
        text = text.Trim();
        
        if (text.Length > 20)
            return false;

        if (text.Contains('\n') || text.Contains('\r'))
            return false;

        // Patrones comunes de teléfono
        return Regex.IsMatch(text,
            @"^[\+]?[(]?[0-9]{1,4}[)]?[-\s\.]?[(]?[0-9]{1,4}[)]?[-\s\.]?[0-9]{1,9}$");
    }

    private static bool IsFilePath(string text)
    {
        text = text.Trim();
        
        if (text.Length > 4096) // Paths muy largos
            return false;

        if (text.Contains('\n') || text.Contains('\r'))
            return false;

        // Unix path
        if (text.StartsWith('/') && text.Contains('/'))
            return true;

        // Windows path
        if (Regex.IsMatch(text, @"^[a-zA-Z]:\\"))
            return true;

        // Relative path con extensión
        if (text.Contains('/') && text.Contains('.') && 
            Regex.IsMatch(text, @"\.[a-zA-Z0-9]{1,10}$"))
            return true;

        return false;
    }

    private static bool IsTerminalCommand(string text)
    {
        text = text.Trim();
        
        // Comandos muy largos probablemente no son comandos simples
        if (text.Length > 500)
            return false;

        // Comandos comunes de terminal
        var commonCommands = new[]
        {
            "cd ", "ls ", "pwd", "mkdir ", "rm ", "cp ", "mv ", "cat ", "grep ",
            "find ", "chmod ", "chown ", "sudo ", "apt ", "pacman ", "dnf ",
            "git ", "docker ", "npm ", "yarn ", "dotnet ", "python ", "node ",
            "cargo ", "go ", "make ", "cmake ", "ninja ", "gcc ", "g++",
            "echo ", "export ", "source ", "bash ", "sh ", "zsh "
        };

        // Verificar si empieza con comando común
        var startsWithCommand = commonCommands.Any(cmd => 
            text.StartsWith(cmd, StringComparison.OrdinalIgnoreCase));

        if (startsWithCommand)
            return true;

        // Verificar si tiene estructura de comando (comando + flags)
        // Ejemplo: "dotnet build --no-restore"
        if (Regex.IsMatch(text, @"^[a-z0-9\-_]+\s+(--?[a-z0-9\-]+|\S+)"))
            return true;

        // Verificar si tiene prompt de terminal al inicio
        // Ejemplo: "~/Documents/projects❯ dotnet build"
        if (Regex.IsMatch(text, @"^[~\/\w\-\.]+[\$\#\>❯]\s+"))
            return true;

        return false;
    }

    private static bool IsCode(string text)
    {
        if (text.Length < 10)
            return false;

        // Heurísticas para detectar código
        var codeIndicators = 0;

        // Tiene llaves o corchetes balanceados
        if (text.Contains('{') && text.Contains('}'))
            codeIndicators++;

        if (text.Contains('[') && text.Contains(']'))
            codeIndicators++;

        // Tiene punto y coma al final de líneas
        if (Regex.Matches(text, @";\s*$", RegexOptions.Multiline).Count > 2)
            codeIndicators++;

        // Tiene indentación consistente
        var lines = text.Split('\n');
        var indentedLines = lines.Count(l => l.StartsWith("    ") || l.StartsWith("\t"));
        if (indentedLines > lines.Length * 0.3)
            codeIndicators++;

        // Tiene palabras clave de programación
        var hasKeywords = CodePatterns.Values
            .SelectMany(patterns => patterns)
            .Any(pattern => text.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        
        if (hasKeywords)
            codeIndicators += 2;

        return codeIndicators >= 3;
    }

    private static bool IsRichText(string text, string? mimeType)
    {
        if (mimeType?.Contains("html") == true || mimeType?.Contains("rtf") == true)
            return true;

        // Detectar HTML
        if (text.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
            return true;

        // Detectar RTF
        if (text.StartsWith("{\\rtf", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
