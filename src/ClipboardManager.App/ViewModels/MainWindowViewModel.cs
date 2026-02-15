using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using ClipboardManager.App.Models;
using ClipboardManager.Core.Models;
using ClipboardManager.Core.Services;
using ClipboardManager.Data.Repositories;
using ReactiveUI;

namespace ClipboardManager.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ClipboardService _clipboardService;
    private readonly ClipboardRepository _clipboardRepository;
    private readonly SearchRepository _searchRepository;
    private readonly ClipboardManager.ML.Services.EmbeddingService? _embeddingService;
    private string _searchQuery = string.Empty;
    private ClipboardItemViewModel? _selectedItem;
    private bool _isLoading;
    private SearchMode _searchMode = SearchMode.Hybrid; // Por defecto híbrido

    public MainWindowViewModel(
        ClipboardService clipboardService, 
        ClipboardRepository clipboardRepository,
        SearchRepository searchRepository,
        ClipboardManager.ML.Services.EmbeddingService? embeddingService = null)
    {
        _clipboardService = clipboardService;
        _clipboardRepository = clipboardRepository;
        _searchRepository = searchRepository;
        _embeddingService = embeddingService;
        
        Items = new ObservableCollection<ClipboardItemViewModel>();
        
        SearchCommand = ReactiveCommand.CreateFromTask(async () => await SearchAsync());
        CopyToClipboardCommand = ReactiveCommand.CreateFromTask<ClipboardItemViewModel>(async item => await CopyItemAsync(item));
        DeleteItemCommand = ReactiveCommand.CreateFromTask<ClipboardItemViewModel>(async item => await DeleteItemAsync(item));
        RefreshCommand = ReactiveCommand.CreateFromTask(async () => await LoadItemsAsync());
        
        _ = LoadItemsAsync();
    }

    public ObservableCollection<ClipboardItemViewModel> Items { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchQuery, value);
            _ = SearchAsync();
        }
    }

    public SearchMode SearchMode
    {
        get => _searchMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchMode, value);
            _ = SearchAsync();
        }
    }

    public int SearchModeIndex
    {
        get => (int)_searchMode;
        set
        {
            SearchMode = (SearchMode)value;
        }
    }

    public ClipboardItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public ICommand SearchCommand { get; }
    public ICommand CopyToClipboardCommand { get; }
    public ICommand DeleteItemCommand { get; }
    public ICommand RefreshCommand { get; }

    private async Task LoadItemsAsync()
    {
        IsLoading = true;
        try
        {
            var items = await _clipboardRepository.GetRecentAsync(100);
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(new ClipboardItemViewModel(item));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            await LoadItemsAsync();
            return;
        }

        IsLoading = true;
        try
        {
            if (_searchMode == SearchMode.Semantic && _embeddingService?.IsAvailable == true)
            {
                // Búsqueda semántica pura
                var queryEmbedding = await _embeddingService.GetEmbeddingAsync(SearchQuery);
                if (queryEmbedding != null)
                {
                    var results = await _searchRepository.SemanticSearchAsync(queryEmbedding);
                    Items.Clear();
                    foreach (var result in results.Take(100))
                    {
                        Items.Add(new ClipboardItemViewModel(result.Item));
                    }
                    Console.WriteLine($"🔍 Búsqueda semántica: {results.Count} resultados");
                    return;
                }
            }
            else if (_searchMode == SearchMode.Hybrid && _embeddingService?.IsAvailable == true)
            {
                // Búsqueda híbrida (FTS5 + semántica)
                // Pesos: 70% FTS5 (texto exacto) + 30% semántica (significado)
                // Esto prioriza coincidencias exactas pero permite encontrar conceptos relacionados
                var queryEmbedding = await _embeddingService.GetEmbeddingAsync(SearchQuery);
                if (queryEmbedding != null)
                {
                    var results = await _searchRepository.HybridSearchAsync(
                        SearchQuery, 
                        queryEmbedding,
                        limit: 100,
                        textWeight: 0.7f,      // 70% peso a coincidencias exactas
                        semanticWeight: 0.3f); // 30% peso a similitud semántica
                    Items.Clear();
                    foreach (var result in results.Take(100))
                    {
                        Items.Add(new ClipboardItemViewModel(result.Item));
                    }
                    Console.WriteLine($"🔍 Búsqueda híbrida: {results.Count} resultados");
                    return;
                }
            }
            
            // Fallback a búsqueda FTS5
            var textResults = await _searchRepository.FullTextSearchAsync(SearchQuery);
            Items.Clear();
            foreach (var result in textResults.Take(100))
            {
                Items.Add(new ClipboardItemViewModel(result.Item));
            }
            Console.WriteLine($"🔍 Búsqueda FTS5: {textResults.Count} resultados");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task CopyItemAsync(ClipboardItemViewModel item)
    {
        if (item == null) return;
        
        try
        {
            // Marcar para ignorar el próximo evento de clipboard
            if (Application.Current is App app)
            {
                app.IgnoreNextClipboardChange();
            }
            
            // Obtener el clipboard desde la ventana principal
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = lifetime?.MainWindow;
            
            if (mainWindow?.Clipboard == null)
            {
                Console.WriteLine("❌ No se pudo acceder al clipboard");
                return;
            }
            
            if (item.IsImage)
            {
                // Copiar imagen usando wl-copy con stdin (workaround para Avalonia/Wayland)
                try
                {
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "wl-copy",
                            Arguments = "--type image/png",
                            RedirectStandardInput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    
                    process.Start();
                    await process.StandardInput.BaseStream.WriteAsync(item.Item.Content, 0, item.Item.Content.Length);
                    process.StandardInput.Close();
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        Console.WriteLine($"🖼️  Imagen copiada al clipboard ({item.Item.Content.Length} bytes)");
                    }
                    else
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        Console.WriteLine($"❌ Error en wl-copy para imagen: {error}");
                        
                        // Fallback a Avalonia
                        if (item.ImageSource != null)
                        {
                            await mainWindow.Clipboard.SetBitmapAsync(item.ImageSource);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error copiando imagen: {ex.Message}");
                    
                    // Fallback a Avalonia
                    if (item.ImageSource != null)
                    {
                        await mainWindow.Clipboard.SetBitmapAsync(item.ImageSource);
                    }
                }
            }
            else
            {
                // Copiar texto usando wl-copy para garantizar UTF-8 correcto
                var text = System.Text.Encoding.UTF8.GetString(item.Item.Content);
                
                try
                {
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "wl-copy",
                            RedirectStandardInput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            StandardInputEncoding = System.Text.Encoding.UTF8
                        }
                    };
                    
                    process.Start();
                    await process.StandardInput.WriteAsync(text);
                    process.StandardInput.Close();
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        Console.WriteLine($"📋 Texto copiado: {text.Substring(0, Math.Min(50, text.Length))}...");
                    }
                    else
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        Console.WriteLine($"❌ Error en wl-copy: {error}");
                        
                        // Fallback a Avalonia
                        await mainWindow.Clipboard.SetTextAsync(text);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error usando wl-copy: {ex.Message}");
                    
                    // Fallback a Avalonia
                    await mainWindow.Clipboard.SetTextAsync(text);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error copiando: {ex.Message}");
        }
    }

    public async Task DeleteItemAsync(ClipboardItemViewModel item)
    {
        if (item == null) return;
        
        await _clipboardService.DeleteItemAsync(item.Id);
        Items.Remove(item);
    }

    public async Task ClearAllAsync()
    {
        await _clipboardRepository.DeleteAllAsync();
        Items.Clear();
        Console.WriteLine("🗑️  Historial limpiado");
    }

    public async Task CopyOcrTextAsync(ClipboardItemViewModel item)
    {
        if (item == null || !item.IsImage) return;
        
        try
        {
            // Refrescar item de la DB para obtener OCR actualizado
            var freshItem = await _clipboardRepository.GetByIdAsync(item.Id);
            if (freshItem == null)
            {
                Console.WriteLine("❌ Item no encontrado en DB");
                return;
            }
            
            // Verificar si hay texto OCR
            if (string.IsNullOrWhiteSpace(freshItem.OcrText))
            {
                Console.WriteLine("⚠️  No hay texto OCR disponible. El OCR puede estar procesándose...");
                return;
            }
            
            // Marcar para ignorar el próximo evento de clipboard
            if (Application.Current is App app)
            {
                app.IgnoreNextClipboardChange();
            }
            
            // Obtener el clipboard desde la ventana principal
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = lifetime?.MainWindow;
            
            if (mainWindow?.Clipboard == null)
            {
                Console.WriteLine("❌ No se pudo acceder al clipboard");
                return;
            }
            
            // Copiar SOLO texto OCR usando wl-copy directamente (solución para Wayland UTF-8)
            Console.WriteLine($"📝 Copiando texto OCR ({freshItem.OcrText.Length} caracteres):");
            Console.WriteLine($"   Primeros 100 chars: {freshItem.OcrText.Substring(0, Math.Min(100, freshItem.OcrText.Length))}");
            
            try
            {
                // Usar wl-copy directamente para garantizar UTF-8 correcto en Wayland
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "wl-copy",
                        RedirectStandardInput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardInputEncoding = System.Text.Encoding.UTF8
                    }
                };
                
                process.Start();
                await process.StandardInput.WriteAsync(freshItem.OcrText);
                process.StandardInput.Close();
                await process.WaitForExitAsync();
                
                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"✅ Texto OCR copiado al clipboard (wl-copy)");
                }
                else
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    Console.WriteLine($"❌ Error en wl-copy: {error}");
                    
                    // Fallback a Avalonia
                    await mainWindow.Clipboard.SetTextAsync(freshItem.OcrText);
                    Console.WriteLine($"⚠️  Usando fallback de Avalonia");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error usando wl-copy: {ex.Message}");
                
                // Fallback a Avalonia
                await mainWindow.Clipboard.SetTextAsync(freshItem.OcrText);
                Console.WriteLine($"⚠️  Usando fallback de Avalonia");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error copiando texto OCR: {ex.Message}");
        }
    }

    public void AddItem(ClipboardItem item)
    {
        Items.Insert(0, new ClipboardItemViewModel(item));
        
        // Limitar a 100 items en memoria
        while (Items.Count > 100)
        {
            Items.RemoveAt(Items.Count - 1);
        }
    }

    public void UpdateOcrText(long itemId, string ocrText)
    {
        var item = Items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            // Actualizar el item en la colección
            var index = Items.IndexOf(item);
            if (index >= 0)
            {
                // Refrescar el item con el nuevo OCR text
                var freshItem = item.Item;
                freshItem.OcrText = ocrText;
                Items[index] = new ClipboardItemViewModel(freshItem);
                Console.WriteLine($"🔄 UI actualizada con OCR para item {itemId}");
            }
        }
    }

    public void UpdateLanguage(long itemId, string language)
    {
        var item = Items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            var index = Items.IndexOf(item);
            if (index >= 0)
            {
                var freshItem = item.Item;
                
                // ML detectó código - actualizar tipo y lenguaje
                freshItem.ContentType = ClipboardType.Code;
                freshItem.CodeLanguage = language;
                
                Items[index] = new ClipboardItemViewModel(freshItem);
                Console.WriteLine($"🔄 UI actualizada: Text → Code ({language}) para item {itemId}");
            }
        }
    }
}
