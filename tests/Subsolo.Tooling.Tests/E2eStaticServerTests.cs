using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Subsolo.Tooling.Tests;

/// <summary>
/// SPEC-0013 IT-01 — o servidor estático do E2E (<c>e2e/static-server.mjs</c>) responde 400 a uma URL com
/// percent-encoding malformado (<c>GET /%E0%A4%A</c>) e continua no ar, servindo as páginas do harness com 200.
/// Antes da correção, o <c>decodeURIComponent</c> fora de <c>try</c> lança <c>URIError</c> e derruba o processo.
/// <para>
/// O pedido é escrito cru no socket: clientes HTTP de alto nível normalizam a URL e não mandariam o caminho malformado.
/// A página servida é <c>/harness/acessivel.html</c> (o <c>e2e/static</c> não tem <c>index.html</c> na raiz).
/// </para>
/// </summary>
public sealed class E2eStaticServerTests
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Process, StringBuilder> Outputs = [];

    [Fact]
    [Trait("Spec", "SPEC-0013:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task MalformedPercentEncodingGetsBadRequestAndServerKeepsServing()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = RepositoryRoot.Combine("e2e", "static-server.mjs");
        File.Exists(script).ShouldBeTrue("e2e/static-server.mjs ausente");
        var port = FreePort();

        using var server = StartServer(port);
        try
        {
            await WaitUntilListeningAsync(server, port, ct);

            var malformed = await RawGetStatusAsync(port, "/%E0%A4%A", ct);
            malformed.ShouldBe(400, $"GET /%E0%A4%A deveria responder 400 (URL malformada).{Diagnostics(server)}");

            server.HasExited.ShouldBeFalse($"o servidor caiu depois de GET /%E0%A4%A.{Diagnostics(server)}");
            var page = await RawGetStatusAsync(port, "/harness/acessivel.html", ct);
            page.ShouldBe(200, $"o servidor deveria continuar servindo /harness/acessivel.html com 200.{Diagnostics(server)}");
        }
        finally
        {
            if (!server.HasExited)
            {
                server.Kill(entireProcessTree: true);
                await server.WaitForExitAsync(CancellationToken.None);
            }
        }
    }

    private static Process StartServer(int port)
    {
        var startInfo = new ProcessStartInfo("node")
        {
            WorkingDirectory = RepositoryRoot.Combine("e2e"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("static-server.mjs");
        startInfo.Environment["E2E_STATIC_PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => Append(output, e.Data);
        process.ErrorDataReceived += (_, e) => Append(output, e.Data);
        Outputs.Add(process, output);
        process.Start().ShouldBeTrue("não foi possível iniciar `node static-server.mjs`");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static void Append(StringBuilder output, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (output)
        {
            output.AppendLine(line);
        }
    }

    private static string Diagnostics(Process server)
    {
        var exited = server.HasExited ? $"processo encerrado (exit {server.ExitCode})" : "processo no ar";
        var output = Outputs.TryGetValue(server, out var builder) ? builder.ToString() : string.Empty;
        return $"{Environment.NewLine}{exited}; saída do servidor:{Environment.NewLine}{output}";
    }

    private static async Task WaitUntilListeningAsync(Process server, int port, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + StartupTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            server.HasExited.ShouldBeFalse($"o servidor encerrou antes de abrir a porta {port}.{Diagnostics(server)}");
            try
            {
                using var probe = new TcpClient();
                await probe.ConnectAsync(IPAddress.Loopback, port, ct);
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
            }
        }

        throw new TimeoutException($"o servidor não abriu a porta {port} em {StartupTimeout}.{Diagnostics(server)}");
    }

    /// <summary>Status HTTP de um GET escrito cru no socket; 0 quando a conexão fecha sem resposta.</summary>
    private static async Task<int> RawGetStatusAsync(int port, string path, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
            await using var stream = client.GetStream();
            var request = Encoding.ASCII.GetBytes($"GET {path} HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(request, timeout.Token);

            using var reader = new StreamReader(stream, Encoding.ASCII);
            var statusLine = await reader.ReadLineAsync(timeout.Token);
            var parts = statusLine?.Split(' ', 3);
            return parts is { Length: >= 2 } && int.TryParse(parts[1], out var status) ? status : 0;
        }
        catch (Exception ex) when ((ex is IOException or SocketException or OperationCanceledException) && !ct.IsCancellationRequested)
        {
            return 0;
        }
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
