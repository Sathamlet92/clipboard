using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Diagnostics;
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
    private ClipboardManager.ML.Services.LanguageDetectionService? _languageDetector;
    private ThemeService? _themeService;

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
            
            // Aplicar tema a la ventana
            if (_themeService != null)
            {
                var theme = _themeService.CurrentTheme;
                _mainWindow.Width = theme.Window.Width;
                _mainWindow.Height = theme.Window.Height;
                _mainWindow.Opacity = theme.Window.Opacity;
            }
            
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
                    _mainWindow.Focus();
                }
            };
            
            // Crear archivo de lock para instancia única
            var lockFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".clipboard-manager",
                "app.lock"
            );
            
            try
            {
                File.WriteAllText(lockFile, Process.GetCurrentProcess().Id.ToString());
            }
            catch { }
            
            // Iniciar daemon client
            _ = StartDaemonClientAsync(viewModel);
            
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeServices(ClipboardDbContextFactory dbFactory)
    {
        // Cargar tema
        _themeService = new ThemeService();
        var theme = _themeService.LoadTheme();
        
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
        
        // Embedding Service (ML) - carga en background sin bloquear
        var mlModelsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".clipboard-manager",
            "models",
            "ml"
        );
        _embeddingService = new ClipboardManager.ML.Services.EmbeddingService(mlModelsPath);
        
        // Language Detection Service (ML) - carga en background sin bloquear
        var langModelsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".clipboard-manager",
            "models",
            "language-detection"
        );
        _languageDetector = new ClipboardManager.ML.Services.LanguageDetectionService(langModelsPath);
        
        // Cargar modelos en background después de iniciar la UI
        _ = Task.Run(() =>
        {
            try
            {
                Console.WriteLine("🔄 Cargando modelos ML en background...");
                // Forzar carga de modelos
                if (_embeddingService?.IsAvailable == true)
                {
                    Console.WriteLine("✅ Embedding service disponible");
                }
                if (_languageDetector != null)
                {
                    Console.WriteLine("✅ Language detector disponible");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Error cargando modelos ML: {ex.Message}");
            }
        });
        
        // Code Classifier Service (ML) - inicializar en background sin bloquear
        var codeClassifier = new ClipboardManager.ML.Services.CodeClassifierService(_embeddingService);
        _ = Task.Run(async () => 
        {
            try
            {
                await codeClassifier.InitializeAsync();
                Console.WriteLine("✅ Code classifier inicializado");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Code classifier no disponible: {ex.Message}");
            }
        });

        // Servicios - pasar code classifier y language detector
        var classificationService = new CoreClassificationService(codeClassifier, _languageDetector);
        var securityService = new CoreSecurityService(config);
        
        // OCR Service y Queue
        var modelsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".clipboard-manager",
            "models",
            "tessdata"
        );
        var ocrService = new ClipboardManager.ML.Services.TesseractOcrService(modelsPath, _languageDetector);
        var ocrQueueService = new OcrQueueService(ocrService, clipboardRepo, _languageDetector);
        
        // Suscribirse al evento de OCR completado
        ocrQueueService.OcrCompleted += OnOcrCompleted;
        
        _clipboardService = new CoreClipboardService(
            clipboardRepo,
            classificationService,
            securityService,
            config,
            ocrQueueService,
            _embeddingService); // Pasar embedding service
        
        // Suscribirse al evento de lenguaje detectado
        _clipboardService.LanguageDetected += OnLanguageDetected;
    }

    private void OnOcrCompleted(object? sender, OcrCompletedEventArgs e)
    {
        // Actualizar UI en el thread de UI
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (_mainWindow?.DataContext is MainWindowViewModel viewModel)
            {
                // Si se detectó código, UpdateLanguageAsync traerá el item completo de la DB
                // incluyendo el OcrText, ContentType y CodeLanguage actualizados
                if (e.IsCode && !string.IsNullOrEmpty(e.CodeLanguage))
                {
                    await viewModel.UpdateLanguageAsync(e.ItemId, e.CodeLanguage);
                }
                else
                {
                    // Solo texto OCR sin código detectado
                    viewModel.UpdateOcrText(e.ItemId, e.OcrText);
                }
            }
        });
    }

    private void OnLanguageDetected(object? sender, Core.Services.LanguageDetectedEventArgs e)
    {
        // Actualizar UI en el thread de UI
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (_mainWindow?.DataContext is MainWindowViewModel viewModel)
            {
                await viewModel.UpdateLanguageAsync(e.ItemId, e.Language);
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
                    var item = await _clipboardService!.ProcessClipboardEventAsync(evt);
                    
                    // Actualizar UI (en el thread de UI)
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Console.WriteLine($"✅ Item agregado: Type={item.ContentType}, IsImage={item.IsImage}");
                        viewModel.AddItem(item);
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