using System.Diagnostics;
using Subsolo.Observability.AppHostTests.Infrastructure;
using YamlDotNet.RepresentationModel;

namespace Subsolo.Observability.AppHostTests;

/// <summary>
/// SPEC-0009 IT-04 — no <c>docker-compose.yaml</c> gerado pelo <c>aspire publish</c> do <c>Subsolo.AppHost</c>, o
/// serviço <c>seq</c> existe e não publica nenhuma porta no host (acesso só por túnel). Exige a Aspire CLI no PATH.
/// </summary>
public sealed class SeqNotExposedTests
{
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromMinutes(8);

    [Fact]
    [Trait("Spec", "SPEC-0009:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task PublishedComposeDoesNotPublishSeqPorts()
    {
        var ct = TestContext.Current.CancellationToken;
        var output = Path.Combine(Path.GetTempPath(), $"subsolo-spec0009-it04-{Guid.NewGuid():N}");
        try
        {
            await PublishAsync(output, ct);

            var yaml = new YamlStream();
            using (var reader = new StringReader(await File.ReadAllTextAsync(Path.Combine(output, "docker-compose.yaml"), ct)))
            {
                yaml.Load(reader);
            }

            var root = (YamlMappingNode)yaml.Documents[0].RootNode;
            var services = (YamlMappingNode)root.Children[new YamlScalarNode("services")];
            var names = services.Children.Keys.OfType<YamlScalarNode>().Select(key => key.Value).ToList();
            services.Children.TryGetValue(new YamlScalarNode("seq"), out var seqNode)
                .ShouldBeTrue($"serviço seq ausente no Compose; serviços: {string.Join(", ", names)}");

            var seq = (YamlMappingNode)seqNode!;
            if (seq.Children.TryGetValue(new YamlScalarNode("ports"), out var ports))
            {
                var published = ports is YamlSequenceNode list
                    ? string.Join(", ", list.Children.Select(port => port.ToString()))
                    : ports.ToString();
                published.ShouldBeEmpty("o serviço seq publica portas no host");
            }
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    private static async Task PublishAsync(string output, CancellationToken ct)
    {
        var root = RepositoryRoot.Path();
        var start = new ProcessStartInfo("aspire")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[]
        {
            "publish", "--apphost", Path.Combine(root, "src", "Subsolo.AppHost", "Subsolo.AppHost.csproj"),
            "-o", output, "--non-interactive", "--nologo",
        })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("não foi possível iniciar a Aspire CLI (aspire)");
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(PublishTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        process.ExitCode.ShouldBe(0, $"aspire publish falhou:\n{await stdout}\n{await stderr}");
    }
}
