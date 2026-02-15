using System;
using System.IO;
using Avalonia.Media.Imaging;
using ClipboardManager.Core.Models;

namespace ClipboardManager.App.Models;

public class ClipboardItemViewModel
{
    private readonly ClipboardItem _item;
    private Bitmap? _imageSource;

    public ClipboardItemViewModel(ClipboardItem item)
    {
        _item = item;
        LoadImageIfNeeded();
    }

    public ClipboardItem Item => _item;

    // Propiedades delegadas
    public long Id => _item.Id;
    public ClipboardType ContentType => _item.ContentType;
    public string SourceApp => _item.SourceApp;
    public DateTime Timestamp => _item.Timestamp;
    public string ContentPreview => _item.ContentPreview;
    public bool IsImage => _item.IsImage;
    public bool HasOcr => _item.HasOcr;
    public string? OcrText => _item.OcrText;
    
    // Propiedades para código
    public bool IsCode => _item.ContentType == ClipboardType.Code;
    public string? CodeLanguage => _item.CodeLanguage;
    public string? CodeContent => IsCode && _item.Content != null 
        ? System.Text.Encoding.UTF8.GetString(_item.Content) 
        : null;

    public Bitmap? ImageSource => _imageSource;

    private void LoadImageIfNeeded()
    {
        if (!_item.IsImage || _item.Content == null || _item.Content.Length == 0)
            return;

        try
        {
            using var ms = new MemoryStream(_item.Content);
            _imageSource = new Bitmap(ms);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error cargando imagen: {ex.Message}");
            Console.WriteLine($"   Tamaño: {_item.Content?.Length ?? 0} bytes");
            _imageSource = null;
        }
    }
}
