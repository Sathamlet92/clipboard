using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using ClipboardManager.Daemon.Client;

namespace ClipboardManager.WaylandDaemon;

/// <summary>
/// Daemon simple que usa wl-paste (igual que cliphist) para capturar clipboard.
/// Mucho más confiable que el daemon C++ con wlr-data-control.
/// </summary>
class Program
{
    private static readonly Server _grpcServer = new Server();
    private static readonly ClipboardServiceImpl _service = new ClipboardServiceImpl();
    private static Process? _wlPasteProcess;
    private static bool _running = true;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Clipboard Manager Daemon (wl-paste) v2.0");
        Console.WriteLine("=========================================");

        // Iniciar servidor gRPC
        _grpcServer.Services.Add(ClipboardService.BindService(_service));
        _grpcServer.Ports.Add(new ServerPort("unix:///tmp/clipboard-daemon.sock", ServerCredentials.Insecure));
        _grpcServer.Start();
        Console.WriteLine("✅ gRPC server started");

        // Iniciar wl-paste --watch
        StartWlPasteMonitor();

        // Esperar señal de cierre
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            _running = false;
        };

        while (_running)
        {
            await Task.Delay(100);
        }

        // Cleanup
        _wlPasteProcess?.Kill();
        await _grpcServer.ShutdownAsync();
        Console.WriteLine("Daemon stopped");
    }

    static void StartWlPasteMonitor()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = "-c \"wl-paste --watch bash -c 'echo CLIPBOARD_CHANGED'\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _wlPasteProcess = new Process { StartInfo = startInfo };
        
        _wlPasteProcess.OutputDataReceived += async (s, e) =>
        {
            if (e.Data == "CLIPBOARD_CHANGED")
            {
                await HandleClipboardChange();
            }
        };

        _wlPasteProcess.Start();
        _wlPasteProcess.BeginOutputReadLine();
        Console.WriteLine("✅ wl-paste monitor started");
    }

    static async Task HandleClipboardChange()
    {
        try
        {
            // Obtener tipos MIME
            var mimeTypes = await RunCommand("wl-paste", "--list-types");
            if (string.IsNullOrWhiteSpace(mimeTypes))
            {
                return;
            }

            var mimeType = mimeTypes.Split('\n')[0].Trim();

            // Obtener contenido
            byte[] content;
            ContentType contentType;

            if (mimeType.StartsWith("image/"))
            {
                content = await RunCommandBytes("wl-paste", $"--type {mimeType}");
                contentType = ContentType.Image;
            }
            else
            {
                var text = await RunCommand("wl-paste", $"--type {mimeType}");
                content = Encoding.UTF8.GetBytes(text);
                contentType = ContentType.Text;
            }

            Console.WriteLine($"📋 Clipboard: {mimeType} ({content.Length} bytes)");

            // Enviar evento a clientes
            var clipboardEvent = new ClipboardEvent
            {
                Data = Google.Protobuf.ByteString.CopyFrom(content),
                MimeType = mimeType,
                ContentType = contentType,
                SourceApp = "wayland",
                WindowTitle = "",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _service.BroadcastClipboardChange(clipboardEvent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }

    static async Task<string> RunCommand(string command, string args)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }

    static async Task<byte[]> RunCommandBytes(string command, string args)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        using var ms = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(ms);
        await process.WaitForExitAsync();
        return ms.ToArray();
    }
}

class ClipboardServiceImpl : ClipboardService.ClipboardServiceBase
{
    private readonly List<IServerStreamWriter<ClipboardEvent>> _clients = new();

    public override async Task StreamClipboardEvents(Empty request, IServerStreamWriter<ClipboardEvent> responseStream, ServerCallContext context)
    {
        _clients.Add(responseStream);
        Console.WriteLine($"✅ Client connected ({_clients.Count} total)");

        try
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, context.CancellationToken);
            }
        }
        finally
        {
            _clients.Remove(responseStream);
            Console.WriteLine($"❌ Client disconnected ({_clients.Count} remaining)");
        }
    }

    public override Task<ClipboardContent> GetClipboardContent(Empty request, ServerCallContext context)
    {
        return Task.FromResult(new ClipboardContent());
    }

    public void BroadcastClipboardChange(ClipboardEvent clipboardEvent)
    {
        foreach (var client in _clients.ToList())
        {
            try
            {
                client.WriteAsync(clipboardEvent).Wait();
            }
            catch
            {
                _clients.Remove(client);
            }
        }
    }
}
