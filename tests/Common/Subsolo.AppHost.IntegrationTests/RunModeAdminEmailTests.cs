using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Subsolo.AppHost.IntegrationTests;

/// <summary>
/// SPEC-0043 UT-01 e UT-02 — no modelo do <c>Subsolo.AppHost</c> em modo run (montado pelo
/// <see cref="DistributedApplicationTestingBuilder"/>, sem subir os recursos), a opção <c>Subsolo:AdminEmail</c> (o
/// e-mail de quem roda localmente) vira o <c>Users__AdminEmails__0</c> da <c>api</c>, que dá o papel admin. Sem a
/// opção, a <c>api</c> não recebe a variável. Um valor que não seja um único e-mail sem nome de exibição para a
/// montagem com um <see cref="InvalidOperationException"/> que cita a opção.
/// </summary>
public sealed class RunModeAdminEmailModelTests
{
    private const string AdminEmail = "admin@local.test";
    private const string AdminVariable = "Users__AdminEmails__0";
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    [Trait("Spec", "SPEC-0043:UT-01")]
    [Trait("Kind", "Integration")]
    public async Task AdminEmailOptionBecomesTheApiAdminEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        var api = await ApiEnvironmentAsync(
            ["--Subsolo:EphemeralDatabase=true", $"--Subsolo:AdminEmail={AdminEmail}"], ct);

        api.Environment.GetValueOrDefault(AdminVariable).ShouldBe(AdminEmail, $"{AdminVariable} da api:\n{api.Listing}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0043:UT-01")]
    [Trait("Kind", "Integration")]
    public async Task WithoutTheOptionTheApiHasNoAdminEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        var api = await ApiEnvironmentAsync(["--Subsolo:EphemeralDatabase=true"], ct);

        api.Environment.ContainsKey(AdminVariable).ShouldBeFalse(
            $"sem Subsolo:AdminEmail a api não deve receber {AdminVariable}:\n{api.Listing}");
    }

    [Theory]
    [Trait("Spec", "SPEC-0043:UT-02")]
    [Trait("Kind", "Integration")]
    [InlineData("nao-e-email")]
    [InlineData("Fulano <a@b.c>")]
    [InlineData("a@b.c,d@e.f")]
    public async Task InvalidAdminEmailStopsTheAppHostWithAClearMessage(string value)
    {
        var ct = TestContext.Current.CancellationToken;

        var exception = await Should.ThrowAsync<Exception>(
            () => ApiEnvironmentAsync(["--Subsolo:EphemeralDatabase=true", $"--Subsolo:AdminEmail={value}"], ct),
            $"o AppHost aceitou --Subsolo:AdminEmail={value} (deveria parar na montagem)");

        // O erro da montagem é um InvalidOperationException cuja mensagem cita a opção. Ele pode vir embrulhado pelo
        // teste do Aspire, por isso a busca percorre a cadeia de exceções (como no SPEC-0041 UT-01).
        var chain = new List<Exception>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            chain.Add(current);
            if (current is AggregateException aggregate)
            {
                chain.AddRange(aggregate.Flatten().InnerExceptions);
            }
        }

        chain.OfType<InvalidOperationException>()
            .Any(invalid => invalid.Message.Contains("Subsolo:AdminEmail", StringComparison.Ordinal))
            .ShouldBeTrue($"nenhum InvalidOperationException com 'Subsolo:AdminEmail' na mensagem:\n{exception}");
    }

    private static async Task<(Dictionary<string, string> Environment, string Listing)> ApiEnvironmentAsync(
        string[] args, CancellationToken ct)
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Subsolo_AppHost>(args, ct);
        await using var app = await appHost.BuildAsync(ct).WaitAsync(BuildTimeout, ct);
        var executionContext = app.Services.GetRequiredService<DistributedApplicationExecutionContext>();
        var resources = app.Services.GetRequiredService<DistributedApplicationModel>().Resources;
        return await RunModeCaddyModelTests.EnvironmentOfAsync(
            resources.Single(resource => resource.Name == "api"), executionContext, ct);
    }
}

/// <summary>
/// SPEC-0043 UT-03 — no <c>docker-compose.yaml</c> gerado por <c>aspire publish</c> com <c>Subsolo:AdminEmail</c>
/// definido (como variável de ambiente <c>Subsolo__AdminEmail</c> do processo), nenhum serviço recebe
/// <c>Users__AdminEmails__0</c> nem o e-mail da opção (nem o <c>.env</c> gerado): a opção é só do run e a produção não muda (o operador define
/// <c>Users__AdminEmails__N</c> no Compose, como já faz). Usa os auxiliares do <see cref="ComposePublishTests"/>.
/// Exige a Aspire CLI no PATH.
/// </summary>
public sealed class AdminEmailComposePublishTests
{
    private const string AdminEmail = "admin@local.test";

    [Fact]
    [Trait("Spec", "SPEC-0043:UT-03")]
    [Trait("Kind", "Integration")]
    public async Task PublishedComposeIgnoresTheLocalAdminEmailOption()
    {
        var ct = TestContext.Current.CancellationToken;
        var output = Path.Combine(Path.GetTempPath(), $"subsolo-spec0043-ut03-{Guid.NewGuid():N}");
        try
        {
            await ComposePublishTests.PublishAsync(
                output, ct, new Dictionary<string, string> { ["Subsolo__AdminEmail"] = AdminEmail });

            var compose = await File.ReadAllTextAsync(Path.Combine(output, "docker-compose.yaml"), ct);
            var services = ComposePublishTests.Services(compose);
            services.ShouldContain(service => service.Name == "api", "serviço api ausente no Compose");

            var problems = services
                .SelectMany(service => service.Environment
                    .Where(variable => variable.Key == "Users__AdminEmails__0"
                        || variable.Value.Contains(AdminEmail, StringComparison.OrdinalIgnoreCase))
                    .Select(variable => $"{service.Name}: {variable.Key}={variable.Value}"))
                .ToList();
            var envPath = Path.Combine(output, ".env");
            if (File.Exists(envPath))
            {
                problems.AddRange(ComposePublishTests.ParseEnvFile(await File.ReadAllLinesAsync(envPath, ct))
                    .Where(entry => entry.Value.Contains(AdminEmail, StringComparison.OrdinalIgnoreCase))
                    .Select(entry => $".env: {entry.Key}={entry.Value}"));
            }

            problems.ShouldBeEmpty("o Compose publicado não deve receber o admin local do Subsolo:AdminEmail");
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }
}
