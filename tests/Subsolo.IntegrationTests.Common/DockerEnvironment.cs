using System.Net.Sockets;
using DotNet.Testcontainers.Builders;

namespace Subsolo.IntegrationTests.Common;

/// <summary>
/// SPEC-0005 — verificação do Docker antes de subir containers. Sem ela, um <c>DOCKER_HOST</c> inválido faria o
/// Testcontainers procurar outro endpoint (ex.: <c>/var/run/docker.sock</c>) ou falhar com uma mensagem genérica.
/// </summary>
internal static class DockerEnvironment
{
    /// <summary>Mensagem do contrato da SPEC-0005 quando não há Docker disponível.</summary>
    public const string NotFoundMessage = "Docker não encontrado: inicie o Docker para rodar testes de integração";

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Falha logo, com <see cref="NotFoundMessage"/>, se o <c>DOCKER_HOST</c> informado não responde.</summary>
    public static async Task EnsureAvailableAsync()
    {
        var host = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (string.IsNullOrWhiteSpace(host))
        {
            return; // Descoberta automática do Testcontainers; a falha dela é traduzida por StartAsync.
        }

        if (!Uri.TryCreate(host, UriKind.Absolute, out var endpoint))
        {
            throw NotFound($"DOCKER_HOST inválido: \"{host}\"");
        }

        try
        {
            using var timeout = new CancellationTokenSource(ConnectTimeout);
            switch (endpoint.Scheme)
            {
                case "unix":
                    using (var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
                    {
                        await socket.ConnectAsync(new UnixDomainSocketEndPoint(endpoint.AbsolutePath), timeout.Token);
                    }

                    break;
                case "tcp":
                case "http":
                case "https":
                    using (var client = new TcpClient())
                    {
                        await client.ConnectAsync(endpoint.Host, endpoint.Port, timeout.Token);
                    }

                    break;
                default:
                    return; // npipe e outros: deixa o Testcontainers decidir.
            }
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or IOException)
        {
            throw NotFound($"DOCKER_HOST={host} não responde", ex);
        }
    }

    /// <summary>Sobe o container, traduzindo a ausência do Docker para a mensagem do contrato.</summary>
    public static async Task<T> StartAsync<T>(Func<T> build)
        where T : DotNet.Testcontainers.Containers.IContainer
    {
        await EnsureAvailableAsync();
        try
        {
            var container = build();
            await container.StartAsync();
            return container;
        }
        catch (DockerUnavailableException ex)
        {
            throw NotFound(ex.Message, ex);
        }
    }

    private static InvalidOperationException NotFound(string detail, Exception? inner = null) =>
        new($"{NotFoundMessage} ({detail}).", inner);
}
