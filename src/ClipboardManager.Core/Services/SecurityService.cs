using ClipboardManager.Core.Models;
using System.Security.Cryptography;
using System.Text;

namespace ClipboardManager.Core.Services;

public class SecurityService
{
    private readonly AppConfiguration _config;
    private readonly byte[] _masterKey;

    public SecurityService(AppConfiguration config)
    {
        _config = config;
        _masterKey = GetOrCreateMasterKey();
    }

    public bool IsPassword(string text, string? sourceApp = null, string? windowTitle = null)
    {
        if (!_config.Security.AutoDetectPasswords)
            return false;

        // 1. Detección por contexto (ventana/app)
        if (IsPasswordContext(sourceApp, windowTitle))
            return true;

        // 2. Heurística de características
        return IsPasswordLike(text);
    }

    public async Task<byte[]> EncryptAsync(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = _masterKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        
        // Escribir IV primero
        await ms.WriteAsync(aes.IV);
        
        // Encriptar datos
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            await cs.WriteAsync(data);
        }

        return ms.ToArray();
    }

    public async Task<byte[]> DecryptAsync(byte[] encryptedData)
    {
        using var aes = Aes.Create();
        aes.Key = _masterKey;

        // Leer IV (primeros 16 bytes)
        var iv = new byte[16];
        Array.Copy(encryptedData, 0, iv, 0, 16);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(encryptedData, 16, encryptedData.Length - 16);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var output = new MemoryStream();
        
        await cs.CopyToAsync(output);
        return output.ToArray();
    }

    public string ComputeHash(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToBase64String(hash);
    }

    private bool IsPasswordContext(string? sourceApp, string? windowTitle)
    {
        if (string.IsNullOrEmpty(windowTitle))
            return false;

        var passwordIndicators = new[]
        {
            "password", "passwd", "pwd", "pass",
            "contraseña", "clave", "secret",
            "login", "sign in", "authentication"
        };

        var lowerTitle = windowTitle.ToLowerInvariant();
        return passwordIndicators.Any(indicator => lowerTitle.Contains(indicator));
    }

    private bool IsPasswordLike(string text)
    {
        // Longitud típica de password
        if (text.Length < 6 || text.Length > 128)
            return false;

        // Passwords raramente tienen espacios
        if (text.Contains(' '))
            return false;

        // Passwords raramente tienen saltos de línea
        if (text.Contains('\n') || text.Contains('\r'))
            return false;

        // Analizar características
        var hasUpper = text.Any(char.IsUpper);
        var hasLower = text.Any(char.IsLower);
        var hasDigit = text.Any(char.IsDigit);
        var hasSpecial = text.Any(c => !char.IsLetterOrDigit(c));

        // Si tiene 3 o más características, probablemente es password
        var characteristics = new[] { hasUpper, hasLower, hasDigit, hasSpecial }.Count(x => x);
        
        return characteristics >= 3;
    }

    private byte[] GetOrCreateMasterKey()
    {
        // En producción, esto debería usar el keyring del sistema
        // Por ahora, usamos un archivo local (SOLO PARA DESARROLLO)
        var keyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".clipboard-manager",
            "master.key"
        );

        if (File.Exists(keyPath))
        {
            return File.ReadAllBytes(keyPath);
        }

        // Generar nueva clave
        var key = new byte[32]; // 256 bits
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(key);
        }

        // Guardar clave
        Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
        File.WriteAllBytes(keyPath, key);
        
        // Establecer permisos restrictivos (solo en Unix)
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return key;
    }
}
