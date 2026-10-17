namespace Subsolo.AppHost.IntegrationTests;

/// <summary>
/// SPEC-0018 IT-02 (Emenda 2) — no <c>docker-compose.yaml</c> gerado por <c>aspire publish</c> do
/// <c>Subsolo.AppHost</c>, o serviço <c>api</c> recebe a seção <c>Notifications:Smtp</c> ligada ao Mailpit:
/// <c>Notifications__Smtp__Host</c> = o serviço <c>mailpit</c> e <c>Notifications__Smtp__Port</c> = a porta SMTP dele
/// (1025), efetivos com o <c>.env</c> gerado; <c>Notifications__Smtp__Security</c> declarado como
/// <c>${SMTP_SECURITY:-StartTls}</c>, que sem nada definido pelo operador vale <c>StartTls</c> (a API em Production
/// exige TLS; a pilha local do <c>compose-up.sh</c> define <c>SMTP_SECURITY=None</c>). Usa os auxiliares do
/// <see cref="ComposePublishTests"/> (mesma leitura do Compose e do <c>.env</c>). Exige a Aspire CLI no PATH.
/// </summary>
public sealed class NotificationsComposePublishTests
{
    private const string SecurityDeclaration = "${SMTP_SECURITY:-StartTls}";

    [Fact]
    [Trait("Spec", "SPEC-0018:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task PublishedComposeWiresTheApiSmtpToMailpitWithTlsByDefault()
    {
        var ct = TestContext.Current.CancellationToken;
        var output = Path.Combine(Path.GetTempPath(), $"subsolo-spec0018-it02-{Guid.NewGuid():N}");
        try
        {
            await ComposePublishTests.PublishAsync(output, ct);

            var compose = await File.ReadAllTextAsync(Path.Combine(output, "docker-compose.yaml"), ct);
            var envPath = Path.Combine(output, ".env");
            var env = File.Exists(envPath)
                ? ComposePublishTests.ParseEnvFile(await File.ReadAllLinesAsync(envPath, ct))
                : [];
            var services = ComposePublishTests.Services(compose);
            services.ShouldContain(service => service.Name == "mailpit", "serviço mailpit ausente no Compose");
            var api = services.SingleOrDefault(service => service.Name == "api")
                .ShouldNotBeNull($"serviço api ausente; serviços: {string.Join(", ", services.Select(s => s.Name))}");
            var listing = string.Join("\n", api.Environment.Select(variable => $"{variable.Key}={variable.Value}"))
                + $"\n.env: {ComposePublishTests.FormatEnv(env)}";

            Effective(api, "Notifications__Smtp__Host", env, listing).ShouldBe("mailpit", listing);
            Effective(api, "Notifications__Smtp__Port", env, listing).ShouldBe("1025", listing);
            api.Environment.GetValueOrDefault("Notifications__Smtp__Security").ShouldBe(SecurityDeclaration, listing);
            Effective(api, "Notifications__Smtp__Security", env, listing).ShouldBe("StartTls", listing);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    private static string Effective(
        ComposePublishTests.ComposeService api, string name, Dictionary<string, string> env, string listing)
    {
        api.Environment.TryGetValue(name, out var declared).ShouldBeTrue($"a api não declara {name} no Compose:\n{listing}");
        return ComposePublishTests.Effective(declared!, env);
    }
}
