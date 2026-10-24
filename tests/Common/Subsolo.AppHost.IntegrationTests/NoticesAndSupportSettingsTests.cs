using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Subsolo.AppHost.IntegrationTests;

/// <summary>
/// SPEC-0030 — no modelo do <c>Subsolo.AppHost</c> em modo run (montado pelo <see cref="DistributedApplicationTestingBuilder"/>,
/// sem subir os recursos), como o <c>tools/compose-up.sh</c> faz na pilha do Compose:
/// <list type="bullet">
/// <item>H28-05 (herança da revisão da SPEC-0028) — o <c>web</c> recebe <c>Support__PixKey</c>, <c>Support__PixName</c>,
/// <c>Support__PixCity</c> e <c>Site__ContactEmail</c> com valores de desenvolvimento, só do servidor (nenhuma variável
/// <c>NEXT_PUBLIC_</c>);</item>
/// <item>IT-04 — a <c>api</c> recebe as chaves VAPID de desenvolvimento (<c>Notifications__WebPush__PublicKey</c>,
/// <c>__PrivateKey</c>, <c>__Subject</c>);</item>
/// <item>IT-05 — a <c>api</c> recebe a chave do token de descadastro (<c>Notifications__Unsubscribe__SigningKey</c>).</item>
/// </list>
/// Os valores podem ser texto ou expressões de parâmetro (segredo gerado); só não podem faltar nem ser vazios.
/// </summary>
public sealed class NoticesAndSupportSettingsTests
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    [Trait("Spec", "SPEC-0030:H28-05")]
    [Trait("Kind", "Integration")]
    public async Task WebGetsThePixAndContactSettingsOnlyOnTheServer()
    {
        var ct = TestContext.Current.CancellationToken;
        var (environment, listing) = await EnvironmentOfAsync("web", ct);

        foreach (var name in new[] { "Support__PixKey", "Support__PixName", "Support__PixCity", "Site__ContactEmail" })
        {
            environment.GetValueOrDefault(name).ShouldNotBeNullOrWhiteSpace($"o web não recebe {name} no modo run:\n{listing}");
        }

        environment.Keys.ShouldNotContain(
            key => key.StartsWith("NEXT_PUBLIC_", StringComparison.Ordinal),
            $"variável NEXT_PUBLIC_ no web (chegaria ao navegador):\n{listing}");
    }

    [Theory]
    [Trait("Spec", "SPEC-0030:IT-04")]
    [Trait("Kind", "Integration")]
    [InlineData("Notifications__WebPush__PublicKey")]
    [InlineData("Notifications__WebPush__PrivateKey")]
    [InlineData("Notifications__WebPush__Subject")]
    public async Task ApiGetsTheDevelopmentVapidKeys(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var (environment, listing) = await EnvironmentOfAsync("api", ct);

        environment.GetValueOrDefault(name).ShouldNotBeNullOrWhiteSpace($"a api não recebe {name} no modo run:\n{listing}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:IT-05")]
    [Trait("Kind", "Integration")]
    public async Task ApiGetsTheUnsubscribeSigningKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var (environment, listing) = await EnvironmentOfAsync("api", ct);

        environment.GetValueOrDefault("Notifications__Unsubscribe__SigningKey")
            .ShouldNotBeNullOrWhiteSpace($"a api não recebe Notifications__Unsubscribe__SigningKey no modo run:\n{listing}");
    }

    /// <summary>
    /// Variáveis de ambiente do recurso <paramref name="name"/> como o orquestrador as montaria no modo run: cada valor
    /// que não é texto vira a expressão de manifesto (<see cref="IManifestExpressionProvider.ValueExpression"/>).
    /// </summary>
    private static async Task<(Dictionary<string, string> Environment, string Listing)> EnvironmentOfAsync(
        string name, CancellationToken ct)
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Subsolo_AppHost>(
            ["--Subsolo:EphemeralDatabase=true"], ct);
        await using var app = await appHost.BuildAsync(ct).WaitAsync(BuildTimeout, ct);

        var resource = app.Services.GetRequiredService<DistributedApplicationModel>().Resources
            .Single(candidate => candidate.Name == name);
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        if (resource.TryGetEnvironmentVariables(out var callbacks))
        {
            var context = new EnvironmentCallbackContext(
                app.Services.GetRequiredService<DistributedApplicationExecutionContext>(), resource, values, ct);
            foreach (var callback in callbacks)
            {
                await callback.Callback(context);
            }
        }

        var environment = values.ToDictionary(
            variable => variable.Key,
            variable => variable.Value switch
            {
                string text => text,
                IManifestExpressionProvider expression => expression.ValueExpression,
                _ => variable.Value?.ToString() ?? string.Empty,
            },
            StringComparer.Ordinal);
        return (environment, string.Join("\n", environment.Select(variable => $"{variable.Key}={variable.Value}")));
    }
}
