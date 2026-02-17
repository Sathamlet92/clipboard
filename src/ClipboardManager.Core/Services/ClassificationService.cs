using ClipboardManager.Core.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace ClipboardManager.Core.Services;

public class ClassificationService
{
    private readonly object? _codeClassifier; // CodeClassifierService (opcional)
    private readonly object? _languageDetector; // LanguageDetectionService (opcional)
    
    public ClassificationService(object? codeClassifier = null, object? languageDetector = null)
    {
        _codeClassifier = codeClassifier;
        _languageDetector = languageDetector;
    }
    private static readonly Dictionary<string, string[]> CodePatterns = new()
    {
        ["csharp"] = new[] { 
            "using System", "namespace ", "public class", "private ", "protected ", 
            "void ", "async ", "await ", "var ", "string ", "int ", "Console.WriteLine",
            "public static void Main", "get; set;", "=> "
        },
        ["python"] = new[] { 
            "def ", "import ", "from ", "class ", "if __name__", "print(", "self.",
            "elif ", "range(", "len(", "str(", "int(", "list(", "dict(", "True", "False", "None"
        },
        ["javascript"] = new[] { 
            "function ", "const ", "let ", "var ", "=>", "console.log", "require(",
            "module.exports", "export ", "import ", "document.", "window.", "async function"
        },
        ["typescript"] = new[] { 
            "interface ", "type ", ": string", ": number", ": boolean", "export ", 
            "import ", "const ", "let ", "function ", "async ", "Promise<"
        },
        ["java"] = new[] { 
            "public class", "private ", "protected ", "import java.", "System.out",
            "public static void main", "String[] args", "new ", "extends ", "implements ",
            "ArrayList", "HashMap"
        },
        ["cpp"] = new[] { 
            "#include", "std::", "cout", "cin", "namespace ", "template<",
            "vector<", "map<", "using namespace", "endl", "::", "nullptr"
        },
        ["c"] = new[] {
            "#include <stdio.h>", "#include <stdlib.h>", "printf(", "scanf(", 
            "malloc(", "free(", "int main(", "void ", "struct ", "typedef "
        },
        ["rust"] = new[] { 
            "fn ", "let ", "mut ", "impl ", "use ", "pub ", "match ", 
            "println!(", "Vec<", "String::", "Option<", "Result<", "&str", "-> "
        },
        ["go"] = new[] { 
            "package ", "func ", "import ", "type ", "var ", "defer ", "go ",
            "fmt.Print", "make(", "chan ", "interface{}", ":= "
        },
        ["kotlin"] = new[] {
            "fun ", "val ", "var ", "data class", "sealed class", "object ",
            "companion object", "when ", "?.let", "listOf(", "mutableListOf("
        },
        ["sql"] = new[] { 
            "SELECT ", "FROM ", "WHERE ", "INSERT ", "UPDATE ", "DELETE ", 
            "CREATE TABLE", "JOIN ", "GROUP BY", "ORDER BY", "HAVING "
        },
        ["html"] = new[] { 
            "<!DOCTYPE", "<html", "<head", "<body", "<div", "<span", "<script",
            "</html>", "</body>", "<meta", "<link", "<style"
        },
        ["css"] = new[] { 
            "display:", "background:", "color:", "margin:", "padding:", "border:",
            "flex", "grid", "@media", "px", "rem", "vh", "vw"
        },
        ["json"] = new[] { 
            "\":", "\",", "null", "true", "false", "[", "]", "{", "}"
        },
        ["xml"] = new[] { 
            "<?xml", "xmlns", "<", "/>", "</", "version=", "encoding="
        },
        ["bash"] = new[] { 
            "#!/bin/bash", "echo ", "if [", "then", "fi", "for ", "done",
            "export ", "$", "chmod ", "grep ", "awk "
        },
        ["php"] = new[] {
            "<?php", "function ", "class ", "public ", "private ", "$",
            "echo ", "->", "=>", "namespace ", "use "
        },
        ["ruby"] = new[] {
            "def ", "end", "class ", "module ", "puts ", "attr_accessor",
            "do |", "each ", "map ", "select ", "@"
        },
        ["swift"] = new[] {
            "import Foundation", "func ", "let ", "var ", "class ", "struct ",
            "guard ", "if let", "switch ", "case ", "enum ", ": String", ": Int"
        },
        ["vue"] = new[] {
            "<template>", "</template>", "<script>", "export default", "data()",
            "methods:", "computed:", "v-if=", "v-for=", "@click=", "{{ "
        },
        ["react"] = new[] {
            "import React", "useState", "useEffect", "useContext", "export default",
            "return (", "className=", "onClick={", "props.", "JSX"
        }
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

        // Usar ML si está disponible, sino fallback a heurísticas
        // TEMPORALMENTE DESHABILITADO - causaba timeouts
        // if (_codeClassifier != null && IsCodeWithML(text))
        //     return ClipboardType.Code;
        if (IsCode(text))
            return ClipboardType.Code;

        if (IsRichText(text, mimeType))
            return ClipboardType.RichText;

        return ClipboardType.Text;
    }
    
    private bool IsCodeWithML(string text)
    {
        // Si el clasificador no está disponible o no inicializado, skip
        if (_codeClassifier == null)
            return false;
            
        try
        {
            // Verificar si está inicializado
            var isInitProperty = _codeClassifier.GetType().GetProperty("IsAvailable");
            if (isInitProperty != null)
            {
                var isAvailable = (bool?)isInitProperty.GetValue(_codeClassifier);
                if (isAvailable != true)
                    return false; // No inicializado aún, usar fallback
            }
            
            // Usar reflexión para llamar IsCodeAsync
            var method = _codeClassifier.GetType().GetMethod("IsCodeAsync");
            if (method != null)
            {
                var task = method.Invoke(_codeClassifier, new object[] { text });
                if (task is Task<bool> boolTask)
                {
                    // Timeout de 100ms - si tarda más, usar fallback
                    if (boolTask.Wait(100))
                    {
                        return boolTask.Result;
                    }
                    else
                    {
                        Console.WriteLine("⏱️  ML classifier timeout, usando fallback");
                        return false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error en ML classifier: {ex.Message}");
        }
        
        return false;
    }

    public string? DetectCodeLanguage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Intentar usar ML si está disponible (analiza TODO)
        if (_languageDetector != null)
        {
            try
            {
                var isAvailableProperty = _languageDetector.GetType().GetProperty("IsAvailable");
                if (isAvailableProperty != null)
                {
                    var isAvailable = (bool?)isAvailableProperty.GetValue(_languageDetector);
                    if (isAvailable == true)
                    {
                        var method = _languageDetector.GetType().GetMethod("DetectLanguageAsync");
                        if (method != null)
                        {
                            var task = method.Invoke(_languageDetector, new object[] { text });
                            if (task is Task<string?> stringTask)
                            {
                                // Timeout de 1000ms (1 segundo)
                                if (stringTask.Wait(1000))
                                {
                                    var result = stringTask.Result;
                                    // ML retorna null si score < 4.5 (no es código)
                                    return result;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Error en ML language detector: {ex.Message}");
            }
        }

        // Sin ML disponible, retornar null (se queda como texto)
        return null;
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
        text = text.Trim();
        
        Console.WriteLine($"🔍 IsCode evaluando texto ({text.Length} chars): {text.Substring(0, Math.Min(100, text.Length))}...");
        
        // Muy corto para ser código
        if (text.Length < 10)
        {
            Console.WriteLine("   ❌ Muy corto (<10 chars)");
            return false;
        }
        
        // Texto natural largo probablemente no es código
        if (text.Length > 100 && !text.Contains('\n'))
        {
            // Si tiene muchas palabras en español/inglés, probablemente es texto
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var naturalWords = words.Count(w => w.Length > 3 && !w.Contains('(') && !w.Contains('{'));
            if (naturalWords > words.Length * 0.7)
            {
                Console.WriteLine($"   ❌ Muchas palabras naturales: {naturalWords}/{words.Length}");
                return false;
            }
        }

        var codeIndicators = 0;
        var antiCodeIndicators = 0;

        // INDICADORES POSITIVOS (es código)
        
        // 1. Tiene palabras clave de programación MUY específicas
        var strongKeywords = new[]
        {
            "function ", "def ", "class ", "import ", "from ", "using ",
            "namespace ", "public ", "private ", "protected ", "static ",
            "const ", "let ", "var ", "async ", "await ", "return ",
            "if (", "for (", "while (", "switch (", "try {", "catch (",
            "print(", "println(", "console.log", "System.out"
        };
        
        var hasStrongKeywords = strongKeywords.Any(kw => 
            text.Contains(kw, StringComparison.OrdinalIgnoreCase));
        
        if (hasStrongKeywords)
        {
            codeIndicators += 5; // Peso MUY alto
            Console.WriteLine("   ✅ +5 Tiene palabras clave fuertes");
        }
        
        // 2. Tiene funciones con paréntesis: function(), print(), etc.
        var functionCalls = Regex.Matches(text, @"\w+\s*\([^\)]*\)").Count;
        if (functionCalls >= 2)
        {
            codeIndicators += 3;
            Console.WriteLine($"   ✅ +3 Tiene {functionCalls} llamadas a función");
        }
        
        // 3. Tiene llaves o corchetes balanceados Y múltiples
        var braceCount = text.Count(c => c == '{');
        if (braceCount >= 2 && text.Count(c => c == '}') >= 2)
        {
            codeIndicators += 3;
            Console.WriteLine($"   ✅ +3 Tiene {braceCount} pares de llaves");
        }

        // 4. Tiene punto y coma al final de líneas (múltiples)
        var semicolons = Regex.Matches(text, @";\s*$", RegexOptions.Multiline).Count;
        if (semicolons >= 2)
        {
            codeIndicators += 2;
            Console.WriteLine($"   ✅ +2 Tiene {semicolons} punto y coma al final de línea");
        }

        // 5. Tiene indentación consistente (múltiples líneas indentadas)
        var lines = text.Split('\n');
        if (lines.Length >= 3)
        {
            var indentedLines = lines.Count(l => l.StartsWith("    ") || l.StartsWith("\t"));
            if (indentedLines >= 3)
            {
                codeIndicators += 2;
                Console.WriteLine($"   ✅ +2 Tiene {indentedLines} líneas indentadas");
            }
        }
        
        // 6. Tiene operadores de programación múltiples
        var operators = Regex.Matches(text, @"[=<>!]+|&&|\|\||->|=>|\+=|-=|\*=|/=").Count;
        if (operators >= 3)
        {
            codeIndicators += 2;
            Console.WriteLine($"   ✅ +2 Tiene {operators} operadores");
        }

        // INDICADORES NEGATIVOS (NO es código)
        
        // 1. Tiene muchas palabras naturales seguidas (5+)
        if (Regex.IsMatch(text, @"(\b[a-záéíóúñA-ZÁÉÍÓÚÑ]{4,}\b\s+){5,}"))
        {
            antiCodeIndicators += 4;
            Console.WriteLine("   ❌ +4 anti: Muchas palabras naturales seguidas");
        }
        
        // 2. Tiene puntuación de texto natural abundante
        var naturalPunctuation = text.Count(c => c == '.' || c == ',' || c == '?' || c == '!');
        if (naturalPunctuation > text.Length * 0.05)
        {
            antiCodeIndicators += 3;
            Console.WriteLine($"   ❌ +3 anti: Mucha puntuación natural ({naturalPunctuation})");
        }
        
        // 3. Empieza con mayúscula y tiene estructura de oración
        if (char.IsUpper(text[0]) && (text.EndsWith('.') || text.EndsWith(':')) && !text.Contains('('))
        {
            antiCodeIndicators += 3;
            Console.WriteLine("   ❌ +3 anti: Estructura de oración");
        }
        
        // 4. Tiene palabras comunes de texto natural
        var commonWords = new[] { " para ", " con ", " que ", " the ", " and ", " for ", " with ", " is a ", " to " };
        var commonWordCount = commonWords.Count(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));
        if (commonWordCount >= 3)
        {
            antiCodeIndicators += 3;
            Console.WriteLine($"   ❌ +3 anti: {commonWordCount} palabras comunes de texto");
        }

        // DECISIÓN FINAL - MÁS ESTRICTO
        // Necesitamos evidencia FUERTE de código (6+ indicadores) Y superar anti-indicadores
        var isCode = codeIndicators >= 6 && codeIndicators > antiCodeIndicators * 2;
        Console.WriteLine($"   📊 RESULTADO: codeIndicators={codeIndicators}, antiCodeIndicators={antiCodeIndicators}, isCode={isCode}");
        
        return isCode;
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
