using System.Net;
using System.Net.Sockets;

namespace Subsolo.Notifications.IntegrationTests.Infrastructure;

/// <summary>
/// Servidor TCP em <c>127.0.0.1</c> que aceita conexões e nunca responde (nem a saudação SMTP): o cliente fica
/// esperando até o próprio timeout. <see cref="Accepted"/> prova que a conexão chegou a ser aceita.
/// </summary>
internal sealed class SilentSmtpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly List<TcpClient> _clients = [];
    private readonly Task _acceptLoop;
    private int _accepted;

    private SilentSmtpServer()
    {
        _listener.Start();
        _acceptLoop = AcceptAsync();
    }

    public const string Host = "127.0.0.1";

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public int Accepted => Volatile.Read(ref _accepted);

    public static SilentSmtpServer Start() => new();

    /// <summary>Uma porta de <c>127.0.0.1</c> sem ninguém escutando (conexão recusada).</summary>
    public static int ClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        listener.Dispose();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try
        {
            await _acceptLoop;
        }
        catch (OperationCanceledException)
        {
        }

        lock (_clients)
        {
            foreach (var client in _clients)
            {
                client.Dispose();
            }

            _clients.Clear();
        }

        _listener.Dispose();
        _stop.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            lock (_clients)
            {
                _clients.Add(client);
            }

            Interlocked.Increment(ref _accepted);
        }
    }
}
