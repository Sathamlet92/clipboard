using Grpc.Core;
using Grpc.Net.Client;

namespace ClipboardManager.Daemon.Client;

public class DaemonClient : IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly ClipboardService.ClipboardServiceClient _client;
    private CancellationTokenSource? _streamCts;
    private bool _disposed;

    public event EventHandler<ClipboardEventArgs>? ClipboardChanged;

    public DaemonClient(string address = "unix:///tmp/clipboard-daemon.sock")
    {
        // Configure channel for Unix domain sockets
        var socketHandler = new SocketsHttpHandler
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                var socket = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.Unix,
                    System.Net.Sockets.SocketType.Stream,
                    System.Net.Sockets.ProtocolType.Unspecified);

                var endpoint = new System.Net.Sockets.UnixDomainSocketEndPoint(
                    address.Replace("unix://", ""));

                await socket.ConnectAsync(endpoint, cancellationToken);
                return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
            }
        };

        _channel = GrpcChannel.ForAddress(
            "http://localhost", // Dummy address for Unix sockets
            new GrpcChannelOptions
            {
                HttpHandler = socketHandler
            });

        _client = new ClipboardService.ClipboardServiceClient(_channel);
    }

    public async Task StartStreamingAsync(CancellationToken cancellationToken = default)
    {
        _streamCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            using var call = _client.StreamClipboardEvents(
                new Empty(),
                cancellationToken: _streamCts.Token);

            await foreach (var clipboardEvent in call.ResponseStream.ReadAllAsync(_streamCts.Token))
            {
                OnClipboardChanged(clipboardEvent);
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
        {
            // Stream was cancelled, this is expected
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Stream error: {ex.Message}");
            throw;
        }
    }

    public void StopStreaming()
    {
        _streamCts?.Cancel();
    }

    public async Task<ClipboardContent?> GetCurrentContentAsync()
    {
        try
        {
            var response = await _client.GetClipboardContentAsync(new Empty());
            return response;
        }
        catch (RpcException ex)
        {
            Console.WriteLine($"Failed to get clipboard content: {ex.Message}");
            return null;
        }
    }

    private void OnClipboardChanged(ClipboardEvent clipboardEvent)
    {
        var args = new ClipboardEventArgs
        {
            Data = clipboardEvent.Data.ToByteArray(),
            SourceApp = clipboardEvent.SourceApp,
            WindowTitle = clipboardEvent.WindowTitle,
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(clipboardEvent.Timestamp).DateTime,
            MimeType = clipboardEvent.MimeType,
            ContentType = clipboardEvent.ContentType
        };

        ClipboardChanged?.Invoke(this, args);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _channel?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

public class ClipboardEventArgs : EventArgs
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string SourceApp { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public ContentType ContentType { get; set; }
}
