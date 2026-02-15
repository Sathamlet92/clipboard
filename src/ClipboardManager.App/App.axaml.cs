using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using ClipboardManager.App.Services;
using ClipboardManager.App.ViewModels;
using ClipboardManager.App.Views;
using ClipboardManager.Core.Models;
using ClipboardManager.Core.Services;
using ClipboardManager.Data;
using ClipboardManager.Data.Repositories;
using DaemonClient = ClipboardManager.Daemon.Client.DaemonClient;
using CoreClipboardService = ClipboardManager.Core.Services.ClipboardService;
using CoreClassificationService = ClipboardManager.Core.Services.ClassificationService;
using CoreSecurityService = ClipboardManager.Core.Services.SecurityService;

namespace ClipboardManager.App;

public partial class App : Application
{
    private DaemonClient? _daemonClient;
    private CoreClipboardService? _clipboardService;
    private HotkeyService? _hotkeyService;
    private MainWindow? _mainWindow;
    private bool _ignoreNextClipboardChange = false;
    private ClipboardManager.ML.Services.EmbeddingService? _embeddingService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            
            // Database path
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".clipboard-manager",
                "clipboard.db"
            );
            
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            
            // Crear factory (thread-safe)
            var dbFactory = new ClipboardDbContextFactory(dbPath);
            
            // Inicializar servicios con factory
            InitializeServices(dbFactory);
            
            // Crear ventana principal
            var viewModel = new MainWindowViewModel(
                _clipboardService!, 
                new ClipboardRepository(dbFactory),
                new SearchRepository(dbFactory),
                _embeddingService); // Pasar embedding service al ViewModel
            
            _mainWindow = new MainWindow
            {
                DataContext = viewModel
            };
            
            desktop.MainWindow = _mainWindow;
            
            // Configurar hotkey service
            _hotkeyService = new HotkeyService(_mainWindow);
            _hotkeyService.RegisterHotkey(Avalonia.Input.Key.V, 
                Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Shift);
            _hotkeyService.HotkeyPressed += (s, e) =>
            {
                if (_mainWindow.IsVisible)
                {
                    _mainWindow.Hide();
                }
                else
                {
                    _mainWindow.Show();
                    _mainWindow.Activate();
                }
            };
            
            // Iniciar daemon client
            _ = StartDaemonClientAsync(viewModel);
            
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeServices(ClipboardDbContextFactory dbFactory)
    {
        // Configuración
        var config = new AppConfiguration
        {
            Security = new SecurityConfig
            {
                HandlePasswords = PasswordHandling.Encrypt,
                AutoDetectPasswords = true,
                PasswordTimeoutSeconds = 300
            },
            Performance = new PerformanceConfig
            {
                MaxItems = 1000,
                OcrEnabled = true, // OCR habilitado
                SemanticSearch = true // Embeddings habilitados
            }
        };

        // Repositorio con factory
        var clipboardRepo = new ClipboardRepository(dbFactory);

        // Servicios
        var classificationService = new CoreClassificationService();
        var securityService = new CoreSecurityService(config);
        
        // OCR Service y Queue
        var modelsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".clipboard-manager",
            "models",
            "tessdata"
        );
        var ocrService = new ClipboardManager.ML.Services.TesseractOcrService(modelsPath);
        var ocrQueueService = new OcrQueueService(ocrService, clipboardRepo);
        
        // Suscribirse al evento de OCR completado
        ocrQueueService.OcrCompleted += OnOcrCompleted;
        
        // Embedding Service (ML)
        var mlModelsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".clipboard-manager",
            "models",
            "ml"
        );
        _embeddingService = new ClipboardManager.ML.Services.EmbeddingService(mlModelsPath);
        
        _clipboardService = new CoreClipboardService(
            clipboardRepo,
            classificationService,
            securityService,
            config,
            ocrQueueService,
            _embeddingService); // Pasar embedding service
    }

    private void OnOcrCompleted(object? sender, OcrCompletedEventArgs e)
    {
        // Actualizar UI en el thread de UI
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_mainWindow?.DataContext is MainWindowViewModel viewModel)
            {
                viewModel.UpdateOcrText(e.ItemId, e.OcrText);
            }
        });
    }

    private async Task StartDaemonClientAsync(MainWindowViewModel viewModel)
    {
        try
        {
            _daemonClient = new DaemonClient("unix:///tmp/clipboard-daemon.sock");
            
            _daemonClient.ClipboardChanged += async (sender, evt) =>
            {
                try
                {
                    // Ignorar si nosotros generamos este cambio
                    if (_ignoreNextClipboardChange)
                    {
                        _ignoreNextClipboardChange = false;
                        Console.WriteLine("⏭️  Ignorando cambio generado por la app");
                        return;
                    }
                    
                    Console.WriteLine($"📋 Clipboard event: Type={evt.ContentType}, MimeType={evt.MimeType}, Size={evt.Data.Length} bytes");
                    
                    // Procesar evento de clipboard
                    await _clipboardService!.ProcessClipboardEventAsync(evt);
                    
                    // Actualizar UI (en el thread de UI)
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        var item = await _clipboardService.GetRecentItemsAsync(1);
                        if (item.Count > 0)
                        {
                            Console.WriteLine($"✅ Item agregado: Type={item[0].ContentType}, IsImage={item[0].IsImage}");
                            viewModel.AddItem(item[0]);
                        }
                    });
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
                {
                    // Duplicado, ignorar silenciosamente
                    Console.WriteLine("⏭️  Duplicado ignorado");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error procesando clipboard: {ex.Message}");
                    Console.WriteLine($"Stack: {ex.StackTrace}");
                }
            };

            await _daemonClient.StartStreamingAsync();
            Console.WriteLine("✅ Daemon client conectado");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  No se pudo conectar al daemon: {ex.Message}");
            Console.WriteLine("La aplicación funcionará sin captura automática de clipboard");
        }
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _daemonClient?.StopStreaming();
        _hotkeyService?.Dispose();
    }

    public void IgnoreNextClipboardChange()
    {
        _ignoreNextClipboardChange = true;
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}