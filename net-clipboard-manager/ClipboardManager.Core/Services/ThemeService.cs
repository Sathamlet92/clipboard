using System;
using System.IO;
using System.Text.Json;
using ClipboardManager.Core.Models;

namespace ClipboardManager.Core.Services;

/// <summary>
/// Servicio para cargar y gestionar temas visuales.
/// </summary>
public class ThemeService
{
    private readonly string _configPath;
    private ThemeConfig? _currentTheme;

    public ThemeService()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "clipboard-manager"
        );
        Directory.CreateDirectory(configDir);
        _configPath = Path.Combine(configDir, "theme.json");
    }

    /// <summary>
    /// Carga el tema desde el archivo de configuración o crea uno por defecto.
    /// </summary>
    public ThemeConfig LoadTheme()
    {
        if (_currentTheme != null)
            return _currentTheme;

        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                _currentTheme = JsonSerializer.Deserialize<ThemeConfig>(json);
                if (_currentTheme != null)
                {
                    Console.WriteLine($"✅ Tema cargado desde {_configPath}");
                    return _currentTheme;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Error cargando tema: {ex.Message}");
            }
        }

        // Crear tema por defecto
        _currentTheme = new ThemeConfig();
        SaveTheme(_currentTheme);
        return _currentTheme;
    }

    /// <summary>
    /// Guarda el tema en el archivo de configuración.
    /// </summary>
    public void SaveTheme(ThemeConfig theme)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(theme, options);
            File.WriteAllText(_configPath, json);
            Console.WriteLine($"✅ Tema guardado en {_configPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error guardando tema: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtiene el tema actual.
    /// </summary>
    public ThemeConfig CurrentTheme => _currentTheme ?? LoadTheme();
}
