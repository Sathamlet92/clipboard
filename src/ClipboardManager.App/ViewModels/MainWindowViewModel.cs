using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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

/// <summary>
/// ViewModel principal de la ventana de la aplicación.
/// Gestiona la lista de items del clipboard, búsqueda y operaciones CRUD.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ClipboardService _clipboardService;
    private readonly ClipboardRepository _clipboardRepository;
    private readonly SearchRepository _searchRepository;
    private readonly ClipboardManager.ML.Services.EmbeddingService? _embeddingService;
    private string _searchQuery = string.Empty;
    private ClipboardItemViewModel? _selectedItem;
    private bool _isLoading;
    private SearchMode _searchMode = SearchMode.Hybrid;
    private CancellationTokenSource? _searchCancellation;

    /// <summary>
    /// Inicializa una nueva instancia del ViewModel principal.
    /// </summary>
    /// <param name="clipboardService">Servicio de gestión del clipboard</param>
    /// <param name="clipboardRepository">Repositorio de items del clipboard</param>
    /// <param name="searchRepository">Repositorio de búsqueda</param>
    /// <param name="embeddingService">Servicio de embeddings para búsqueda semántica (opcional)</param>
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

    /// <summary>
    /// Colección observable de items del clipboard para mostrar en la UI.
    /// </summary>
    public ObservableCollection<ClipboardItemViewModel> Items { get; }

    /// <summary>
    /// Query de búsqueda actual. No dispara búsqueda automáticamente.
    /// </summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set => this.RaiseAndSetIfChanged(ref _searchQuery, value);
    }

    /// <summary>
    /// Modo de búsqueda actual (Texto, Semántico, Híbrido).
    /// </summary>
    public SearchMode SearchMode
    {
        get => _searchMode;
        set => this.RaiseAndSetIfChanged(ref _searchMode, value);
    }

    /// <summary>
    /// Índice del modo de búsqueda para binding con ComboBox.
    /// </summary>
    public int SearchModeIndex
    {
        get => (int)_searchMode;
        set
        {
            SearchMode = (SearchMode)value;
        }
    }

    /// <summary>
    /// Item actualmente seleccionado en la UI.
    /// </summary>
    public ClipboardItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
    }

    /// <summary>
    /// Indica si hay una operación en progreso.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    /// <summary>
    /// Comando para ejecutar búsqueda.
    /// </summary>
    public ICommand SearchCommand { get; }
    
    /// <summary>
    /// Comando para copiar un item al clipboard.
    /// </summary>
    public ICommand CopyToClipboardCommand { get; }
    
    /// <summary>
    /// Comando para eliminar un item.
    /// </summary>
    public ICommand DeleteItemCommand { get; }
    
    /// <summary>
    /// Comando para refrescar la lista de items.
    /// </summary>
    public ICommand RefreshCommand { get; }

    /// <summary>
    /// Carga los items más recientes del historial del clipboard.
    /// </summary>
    private async Task LoadItemsAsync()
    {
        try
        {
            // Cargar solo 20 items inicialmente para carga rápida
            var items = await _clipboardRepository.GetRecentAsync(20);
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(new ClipboardItemViewModel(item));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error cargando items: {ex.Message}");
        }
    }

    /// <summary>
    /// Ejecuta una búsqueda asíncrona sin bloquear el UI.
    /// Cancela búsquedas anteriores si aún están en progreso.
    /// </summary>
    public async Task SearchAsync()
    {
        // Cancelar búsqueda anterior si existe
        _searchCancellation?.Cancel();
        _searchCancellation = new CancellationTokenSource();
        var cancellationToken = _searchCancellation.Token;

        // Si no hay query, cargar todos los items
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            await LoadItemsAsync();
            return;
        }

        // NO bloquear el UI - ejecutar en background
        try
        {
            List<SearchResult> results;

            if (_searchMode == SearchMode.Semantic && _embeddingService?.IsAvailable == true)
            {
                // Búsqueda semántica pura
                var queryEmbedding = await Task.Run(() => 
                    _embeddingService.GetEmbeddingAsync(SearchQuery), cancellationToken);
                
                if (queryEmbedding != null && !cancellationToken.IsCancellationRequested)
                {
                    results = await _searchRepository.SemanticSearchAsync(queryEmbedding);
                }
                else
                {
                    return;
                }
            }
            else if (_searchMode == SearchMode.Hybrid && _embeddingService?.IsAvailable == true)
            {
                // Búsqueda híbrida: texto + semántico
                var queryEmbedding = await Task.Run(() => 
                    _embeddingService.GetEmbeddingAsync(SearchQuery), cancellationToken);
                
                if (queryEmbedding != null && !cancellationToken.IsCancellationRequested)
                {
                    results = await _searchRepository.HybridSearchAsync(
                        SearchQuery, 
                        queryEmbedding,
                        limit: 50,
                        textWeight: 0.7f,
                        semanticWeight: 0.3f);
                }
                else
                {
                    return;
                }
            }
            else
            {
                // Fallback a búsqueda FTS5 (más rápida)
                results = await _searchRepository.FullTextSearchAsync(SearchQuery, limit: 50);
            }

            // Actualizar UI solo si no fue cancelado
            if (!cancellationToken.IsCancellationRequested)
            {
                Items.Clear();
                foreach (var result in results)
                {
                    Items.Add(new ClipboardItemViewModel(result.Item));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Búsqueda cancelada, ignorar
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error en búsqueda: {ex.Message}");
        }
    }

    /// <summary>
    /// Copia un item al clipboard del sistema.
    /// Usa wl-copy en Wayland, fallback a Avalonia clipboard API.
    /// </summary>
    /// <param name="item">Item a copiar</param>
    public async Task CopyItemAsync(ClipboardItemViewModel item)
    {
        if (item == null) return;
        
        try
        {
            if (Application.Current is App app)
            {
                app.IgnoreNextClipboardChange();
            }
            
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = lifetime?.MainWindow;
            
            if (mainWindow?.Clipboard == null) return;
            
            if (item.IsImage)
            {
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
                    
                    if (process.ExitCode != 0 && item.ImageSource != null)
                    {
                        await mainWindow.Clipboard.SetBitmapAsync(item.ImageSource);
                    }
                }
                catch
                {
                    if (item.ImageSource != null)
                    {
                        await mainWindow.Clipboard.SetBitmapAsync(item.ImageSource);
                    }
                }
            }
            else
            {
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
                    
                    if (process.ExitCode != 0)
                    {
                        await mainWindow.Clipboard.SetTextAsync(text);
                    }
                }
                catch
                {
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
        SearchQuery = ""; // Limpiar búsqueda también
        Console.WriteLine("✅ Historial limpiado");
    }

    public async Task CopyOcrTextAsync(ClipboardItemViewModel item)
    {
        if (item == null || !item.IsImage) return;
        
        try
        {
            var freshItem = await _clipboardRepository.GetByIdAsync(item.Id);
            if (freshItem == null || string.IsNullOrWhiteSpace(freshItem.OcrText))
            {
                return;
            }
            
            if (Application.Current is App app)
            {
                app.IgnoreNextClipboardChange();
            }
            
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            var mainWindow = lifetime?.MainWindow;
            
            if (mainWindow?.Clipboard == null) return;
            
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
                await process.StandardInput.WriteAsync(freshItem.OcrText);
                process.StandardInput.Close();
                await process.WaitForExitAsync();
                
                if (process.ExitCode != 0)
                {
                    await mainWindow.Clipboard.SetTextAsync(freshItem.OcrText);
                }
            }
            catch
            {
                await mainWindow.Clipboard.SetTextAsync(freshItem.OcrText);
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
        
        // Limitar a 50 items en memoria para mejor rendimiento
        while (Items.Count > 50)
        {
            Items.RemoveAt(Items.Count - 1);
        }
    }

    public void UpdateOcrText(long itemId, string ocrText)
    {
        var item = Items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            var index = Items.IndexOf(item);
            if (index >= 0)
            {
                var freshItem = item.Item;
                freshItem.OcrText = ocrText;
                Items[index] = new ClipboardItemViewModel(freshItem);
            }
        }
    }

    public async Task UpdateLanguageAsync(long itemId, string language)
    {
        var item = Items.FirstOrDefault(i => i.Id == itemId);
        if (item != null)
        {
            var index = Items.IndexOf(item);
            if (index >= 0)
            {
                // Obtener item fresco de la base de datos
                var freshItem = await _clipboardRepository.GetByIdAsync(itemId);
                if (freshItem != null)
                {
                    // Asegurar que está marcado como código
                    if (freshItem.ContentType != ClipboardType.Code)
                    {
                        freshItem.ContentType = ClipboardType.Code;
                    }
                    if (freshItem.CodeLanguage != language)
                    {
                        freshItem.CodeLanguage = language;
                    }
                    
                    // Remover y agregar de nuevo para forzar recreación del control
                    Items.RemoveAt(index);
                    Items.Insert(index, new ClipboardItemViewModel(freshItem));
                }
            }
        }
    }
}
