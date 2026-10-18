using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Subsolo.AppHost.IntegrationTests;

/// <summary>
/// SPEC-0016 IT-10 (Emenda 4, §7.6 j) — no modelo do <c>Subsolo.AppHost</c> em modo run (montado pelo
/// <see cref="DistributedApplicationTestingBuilder"/>, sem subir os recursos), a <c>api</c> recebe
/// <c>App__PublicUrl</c> com o endereço do recurso <c>web</c> (a base do link mágico no <c>aspire run</c>), lido das
/// anotações de ambiente como expressão (<c>{web.bindings.…}</c>), sem alocar portas.
/// </summary>
public sealed class PublicUrlReferenceTests
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    [Trait("Spec", "SPEC-0016:IT-10")]
    [Trait("Kind", "Integration")]
    public async Task ApiGetsAppPublicUrlWithTheWebEndpointInRunMode()
    {
        var ct = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Subsolo_AppHost>(
            ["--Subsolo:EphemeralDatabase=true"], ct);
        await using var app = await appHost.BuildAsync(ct).WaitAsync(BuildTimeout, ct);

        var resources = app.Services.GetRequiredService<DistributedApplicationModel>().Resources;
        resources.ShouldContain(resource => resource.Name == "web", "o AppHost não tem o recurso web");
        var api = resources.OfType<ProjectResource>().Single(resource => resource.Name == "api");
        var environment = await EnvironmentOfAsync(
            api, app.Services.GetRequiredService<DistributedApplicationExecutionContext>(), ct);
        var listing = string.Join("\n", environment.Select(variable => $"{variable.Key}={variable.Value}"));

        var publicUrl = environment.GetValueOrDefault("App__PublicUrl");
        publicUrl.ShouldNotBeNull($"a api não recebe App__PublicUrl no modo run:\n{listing}");
        publicUrl.ShouldContain("{web.bindings.", Case.Sensitive, $"App__PublicUrl não aponta para o endpoint do web:\n{listing}");
    }

    /// <summary>
    /// Variáveis de ambiente de <paramref name="resource"/> como o orquestrador as montaria no modo run: cada valor
    /// que não é texto vira a expressão de manifesto (<see cref="IManifestExpressionProvider.ValueExpression"/>).
    /// </summary>
    private static async Task<Dictionary<string, string>> EnvironmentOfAsync(
        IResource resource, DistributedApplicationExecutionContext executionContext, CancellationToken ct)
    {
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        if (resource.TryGetEnvironmentVariables(out var callbacks))
        {
            var context = new EnvironmentCallbackContext(executionContext, resource, values, ct);
            foreach (var callback in callbacks)
            {
                await callback.Callback(context);
            }
        }

        return values.ToDictionary(
            variable => variable.Key,
            variable => variable.Value switch
            {
                string text => text,
                IManifestExpressionProvider expression => expression.ValueExpression,
                _ => variable.Value?.ToString() ?? string.Empty,
            },
            StringComparer.Ordinal);
    }
}
