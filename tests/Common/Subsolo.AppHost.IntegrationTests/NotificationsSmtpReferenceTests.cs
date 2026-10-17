using System.Text.RegularExpressions;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Subsolo.AppHost.IntegrationTests;

/// <summary>
/// SPEC-0018 IT-02 — no modelo do <c>Subsolo.AppHost</c> (montado pelo <see cref="DistributedApplicationTestingBuilder"/>,
/// sem subir os recursos), a <c>api</c> recebe a referência <c>smtp</c> do Mailpit e a seção <c>Notifications:Smtp</c>
/// resolvida para ela: <c>Notifications__Smtp__Host</c> e <c>Notifications__Smtp__Port</c> apontam para o endpoint
/// <c>smtp</c> do recurso <c>mailpit</c> e <c>Notifications__Smtp__Security=None</c> (Mailpit no local e no CI). As
/// variáveis são lidas das anotações de ambiente da <c>api</c> no modo run, como expressões
/// (<c>{mailpit.bindings.smtp.host}</c>), sem alocar portas.
/// </summary>
public sealed partial class NotificationsSmtpReferenceTests
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    [Trait("Spec", "SPEC-0018:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task ApiGetsTheMailpitSmtpReferenceAndTheNotificationsSmtpSection()
    {
        var ct = TestContext.Current.CancellationToken;
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Subsolo_AppHost>(
            ["--Subsolo:EphemeralDatabase=true"], ct);
        await using var app = await appHost.BuildAsync(ct).WaitAsync(BuildTimeout, ct);

        var api = app.Services.GetRequiredService<DistributedApplicationModel>().Resources
            .OfType<ProjectResource>()
            .Single(resource => resource.Name == "api");
        var environment = await EnvironmentOfAsync(
            api, app.Services.GetRequiredService<DistributedApplicationExecutionContext>(), ct);
        var listing = string.Join("\n", environment.Select(variable => $"{variable.Key}={variable.Value}"));

        environment.ShouldContain(
            variable => variable.Key.StartsWith("services__mailpit__smtp__", StringComparison.Ordinal)
                && variable.Value.Contains("{mailpit.bindings.smtp.", StringComparison.Ordinal),
            $"a api não tem a referência smtp do Mailpit:\n{listing}");
        environment.GetValueOrDefault("Notifications__Smtp__Host")
            .ShouldBe("{mailpit.bindings.smtp.host}", $"Notifications:Smtp:Host da api:\n{listing}");
        environment.GetValueOrDefault("Notifications__Smtp__Port").ShouldNotBeNull($"Notifications:Smtp:Port ausente:\n{listing}")
            .ShouldMatch(SmtpPort().ToString(), $"Notifications:Smtp:Port da api:\n{listing}");
        environment.GetValueOrDefault("Notifications__Smtp__Security")
            .ShouldBe("None", $"Notifications:Smtp:Security da api:\n{listing}");
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

    [GeneratedRegex(@"^\{mailpit\.bindings\.smtp\.(?:port|targetPort)\}$")]
    private static partial Regex SmtpPort();
}
