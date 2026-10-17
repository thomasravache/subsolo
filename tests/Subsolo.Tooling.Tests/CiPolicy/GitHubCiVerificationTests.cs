using System.Text.RegularExpressions;

namespace Subsolo.Tooling.Tests.CiPolicy;

/// <summary>
/// SPEC-0006 IT-01..IT-06 — verificação do pipeline em execuções reais do GitHub Actions, com PRs descartáveis
/// (<c>ci-check/*</c>) abertos como rascunho contra a main e fechados sem merge.
/// <para>
/// Fora da execução padrão: só rodam com <c>SDD_GITHUB_VERIFY=1</c>. Sem a variável, FALHAM (não pulam) com
/// "verificação no GitHub não executada: defina SDD_GITHUB_VERIFY=1" — pular esconderia a falta de evidência.
/// Excluídas da suíte padrão pelo filtro <c>--filter-not-trait "Kind=GitHubVerification"</c>.
/// </para>
/// </summary>
public sealed partial class GitHubCiVerificationTests
{
    private const string ValidChangeFile = "tests/Subsolo.Tooling.Tests/CiPolicy/CiWorkflowDocument.cs";

    // Chave fictícia no formato AWS (AKIA + 16 caracteres [A-Z2-7]); não pertence a conta alguma.
    // Montada em partes para que este arquivo não seja apontado pelo scanner de segredos do repositório.
    private static readonly string FakeAwsAccessKeyId = "AKIA" + "Q3EGRTZ7" + "LNXWPV5K";

    [Fact]
    [Trait("Spec", "SPEC-0006:IT-01")]
    [Trait("Kind", "GitHubVerification")]
    public async Task HealthyPullRequestTurnsAllEightChecksGreen()
    {
        GitHubCiVerification.RequireEnabled();
        var ct = TestContext.Current.CancellationToken;
        await using var run = await GitHubCiVerification.StartAsync("IT-01", ct);
        run.AppendToFile(ValidChangeFile, $"{Environment.NewLine}// {run.Branch}: mudança válida de verificação (IT-01){Environment.NewLine}");

        await run.CommitPushAndOpenDraftPullRequestAsync(
            "test(ci): verifica pipeline com mudança válida [SPEC-0006]", SpecBody("IT-01"), ct);
        var checks = await run.WaitForChecksAsync(ct);

        foreach (var job in CiWorkflowDocument.ContractJobIds)
        {
            run.ChecksOf(checks, job).ShouldAllBe(
                c => c.Passed, $"check `{job}` deveria estar verde no PR saudável {run.PullRequestUrl}");
        }
    }

    [Fact]
    [Trait("Spec", "SPEC-0006:IT-02")]
    [Trait("Kind", "GitHubVerification")]
    public async Task FailingUnitTestTurnsTestCheckRedAndBlocksMerge()
    {
        GitHubCiVerification.RequireEnabled();
        var ct = TestContext.Current.CancellationToken;
        await using var run = await GitHubCiVerification.StartAsync("IT-02", ct);
        run.WriteFile(
            "tests/Subsolo.Tooling.Tests/CiPolicy/CiCheckDeliberateFailureTests.cs",
            """
            namespace Subsolo.Tooling.Tests.CiPolicy;

            public sealed class CiCheckDeliberateFailureTests
            {
                [Fact]
                [Trait("Spec", "SPEC-0006:IT-02")]
                public void CiCheckDeliberateFailure() => Assert.Fail("ci-check IT-02: teste propositalmente vermelho");
            }

            """);

        await run.CommitPushAndOpenDraftPullRequestAsync(
            "test(ci): verifica check test com teste vermelho [SPEC-0006]", SpecBody("IT-02"), ct);
        var checks = await run.WaitForChecksAsync(ct);

        var failed = run.ChecksOf(checks, "test").Where(c => c.Failed).ToList();
        failed.ShouldNotBeEmpty($"check `test` deveria falhar com o teste vermelho ({run.PullRequestUrl})");
        (await run.FailedLogAsync(failed[0], ct)).ShouldContain(
            "CiCheckDeliberateFailure", Case.Sensitive, "o log do job `test` deve citar o teste que falhou");
        (await run.MarkReadyAndReadMergeStateAsync(ct)).ShouldBe(
            "BLOCKED", $"com o check `test` vermelho a proteção da main deve bloquear o merge ({run.PullRequestUrl})");
    }

    [Fact]
    [Trait("Spec", "SPEC-0006:IT-03")]
    [Trait("Kind", "GitHubVerification")]
    public async Task FakeSecretTurnsSecurityCheckRedCitingTheFile()
    {
        GitHubCiVerification.RequireEnabled();
        var ct = TestContext.Current.CancellationToken;
        await using var run = await GitHubCiVerification.StartAsync("IT-03", ct);
        const string secretFile = "tests/Subsolo.Tooling.Tests/CiPolicy/ci-check-credentials.ini";
        run.WriteFile(secretFile, $"[default]{Environment.NewLine}aws_access_key_id = {FakeAwsAccessKeyId}{Environment.NewLine}");

        await run.CommitPushAndOpenDraftPullRequestAsync(
            "test(ci): verifica gitleaks com chave fictícia [SPEC-0006]", SpecBody("IT-03"), ct);
        var checks = await run.WaitForChecksAsync(ct);

        var failed = run.ChecksOf(checks, "security").Where(c => c.Failed).ToList();
        failed.ShouldNotBeEmpty($"check `security` deveria falhar com a chave fictícia ({run.PullRequestUrl})");
        (await run.FailedLogAsync(failed[0], ct)).ShouldContain(
            "ci-check-credentials.ini", Case.Sensitive, "o achado do gitleaks no job `security` deve citar o arquivo");
    }

    [Fact]
    [Trait("Spec", "SPEC-0006:IT-04")]
    [Trait("Kind", "GitHubVerification")]
    public async Task HighSeverityVulnerablePackageTurnsSecurityCheckRed()
    {
        GitHubCiVerification.RequireEnabled();
        var ct = TestContext.Current.CancellationToken;
        await using var run = await GitHubCiVerification.StartAsync("IT-04", ct);

        // Newtonsoft.Json 12.0.1: GHSA-5crp-9r3c-p9vr, severidade High (corrigida na 13.0.1).
        const string vulnerablePackage = "Newtonsoft.Json";
        var propsPath = Path.Combine(run.WorkTree, "Directory.Packages.props");
        var props = await File.ReadAllTextAsync(propsPath, ct);
        var firstVersion = props.IndexOf("<PackageVersion ", StringComparison.Ordinal);
        firstVersion.ShouldBeGreaterThan(0, "Directory.Packages.props sem PackageVersion");
        await File.WriteAllTextAsync(
            propsPath,
            props.Insert(firstVersion, $"<PackageVersion Include=\"{vulnerablePackage}\" Version=\"12.0.1\" />{Environment.NewLine}    "),
            ct);
        run.ReplaceInFile(
            "tests/Subsolo.Tooling.Tests/Subsolo.Tooling.Tests.csproj",
            "<PackageReference Include=\"YamlDotNet\" />",
            $"<PackageReference Include=\"YamlDotNet\" />{Environment.NewLine}    <PackageReference Include=\"{vulnerablePackage}\" />");
        // Atualiza o packages.lock.json para que a falha venha da auditoria, não de lock desatualizado.
        await run.RunInCloneOkAsync(
            "dotnet",
            ["restore", "tests/Subsolo.Tooling.Tests/Subsolo.Tooling.Tests.csproj", "--force-evaluate", "-p:NuGetAudit=false"],
            ct);

        await run.CommitPushAndOpenDraftPullRequestAsync(
            "test(ci): verifica auditoria com pacote vulnerável [SPEC-0006]", SpecBody("IT-04"), ct);
        var checks = await run.WaitForChecksAsync(ct);

        var failed = run.ChecksOf(checks, "security").Where(c => c.Failed).ToList();
        failed.ShouldNotBeEmpty($"check `security` deveria falhar com {vulnerablePackage} 12.0.1 ({run.PullRequestUrl})");
        (await run.FailedLogAsync(failed[0], ct)).ShouldContain(
            vulnerablePackage, Case.Insensitive, "o job `security` deve citar o pacote vulnerável");
    }

    [Fact]
    [Trait("Spec", "SPEC-0006:IT-05")]
    [Trait("Kind", "GitHubVerification")]
    public async Task PullRequestWithoutApprovedSpecTurnsSddCheckRedWithAnnotation()
    {
        GitHubCiVerification.RequireEnabled();
        var ct = TestContext.Current.CancellationToken;
        await using var run = await GitHubCiVerification.StartAsync("IT-05", ct);
        run.AppendToFile(ValidChangeFile, $"{Environment.NewLine}// {run.Branch}: PR sem spec (IT-05){Environment.NewLine}");

        // Título e corpo sem nenhuma referência de spec (o pr-check também procura referências no corpo).
        await run.CommitPushAndOpenDraftPullRequestAsync(
            "test(ci): verifica pipeline sem citar spec",
            "Verificação descartável do CI: PR propositalmente sem spec aprovada (IT-05). Fechado sem merge.\n",
            ct);
        var checks = await run.WaitForChecksAsync(ct);

        var failed = run.ChecksOf(checks, "sdd").Where(c => c.Failed).ToList();
        failed.ShouldNotBeEmpty($"check `sdd` deveria falhar no PR sem spec ({run.PullRequestUrl})");
        // SPEC-0013 (item 7): a anotação genérica do workflow ("SDD pr-check") não basta — o próprio pr-check precisa
        // explicar o motivo, citando a spec ou a regra violada.
        var annotations = await run.ToolAnnotationsAsync(failed[0], ct);
        annotations.Where(a => a.Message.Contains("spec", StringComparison.OrdinalIgnoreCase)).ShouldNotBeEmpty(
            "o check `sdd` deve ter uma anotação do pr-check citando a spec ou a regra violada (a genérica do workflow, " +
            $"\"SDD pr-check\", e a de exit code não contam). Anotações: {string.Join(" | ", annotations.Select(a => $"{a.Title}: {a.Message}"))}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0006:IT-06")]
    [Trait("Kind", "GitHubVerification")]
    public async Task SqlInjectionTurnsCodeqlAndSemgrepRed()
    {
        GitHubCiVerification.RequireEnabled();
        var ct = TestContext.Current.CancellationToken;
        await using var run = await GitHubCiVerification.StartAsync("IT-06", ct);
        const string project = "ci-check/SqlInjectionSample/SqlInjectionSample.csproj";
        run.WriteFile(
            project,
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <IsPackable>false</IsPackable>
                <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
              </PropertyGroup>
            </Project>

            """);
        run.WriteFile(
            "ci-check/SqlInjectionSample/Program.cs",
            """
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllers();
            var app = builder.Build();
            app.MapControllers();
            app.Run();

            """);
        run.WriteFile(
            "ci-check/SqlInjectionSample/UsersController.cs",
            """
            using System.Data;
            using Microsoft.AspNetCore.Mvc;

            namespace CiCheck.SqlInjectionSample;

            [ApiController]
            [Route("users")]
            public sealed class UsersController(IDbConnection connection) : ControllerBase
            {
                [HttpGet]
                public IActionResult Find([FromQuery] string name)
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT id FROM users WHERE name = '" + name + "'";
                    return Ok(command.ExecuteScalar());
                }
            }

            """);
        await run.RunInCloneOkAsync("dotnet", ["sln", "Subsolo.slnx", "add", project], ct);
        await run.RunInCloneOkAsync("dotnet", ["restore", project], ct);

        await run.CommitPushAndOpenDraftPullRequestAsync(
            "test(ci): verifica SAST com injeção de SQL [SPEC-0006]", SpecBody("IT-06"), ct);
        var checks = await run.WaitForChecksAsync(ct);

        var codeql = run.ChecksOf(checks, "codeql").Where(c => c.Failed).ToList();
        codeql.ShouldNotBeEmpty($"check `codeql` deveria falhar com a injeção de SQL ({run.PullRequestUrl})");
        // Pela regra (não por "sql", que casaria com o caminho SqlInjectionSample do próprio cenário).
        (await run.FindingsTextAsync(codeql[0], ct)).ShouldContain(
            "cs/sql-injection", Case.Sensitive, "o job `codeql` deve citar o alerta pela regra cs/sql-injection");

        var security = run.ChecksOf(checks, "security").Where(c => c.Failed).ToList();
        security.ShouldNotBeEmpty($"o Semgrep no check `security` deveria falhar com a injeção de SQL ({run.PullRequestUrl})");
        var semgrepFindings = await run.FindingsTextAsync(security[0], ct);
        SemgrepSqlInjectionRule().IsMatch(semgrepFindings).ShouldBeTrue(
            $"o Semgrep deve citar o achado pelo id de regra de SQL injection (…sqli…, anotação \"Semgrep <regra>\"):{Environment.NewLine}{semgrepFindings}");
    }

    /// <summary>
    /// Corpo de PR que declara as specs (linha <c>Refs:</c>). <c>SDD_GITHUB_VERIFY_REFS</c> permite citar mais specs
    /// quando o HEAD verificado carrega mudanças de outras specs ainda não integradas (escopo do pr-check).
    /// </summary>
    private static string SpecBody(string testId)
    {
        var refs = Environment.GetEnvironmentVariable("SDD_GITHUB_VERIFY_REFS");
        return $"""
            ## Resumo
            Verificação descartável do CI da SPEC-0006 ({testId}); fechada sem merge após registrar a evidência.

            ## Como foi testado
            Execução do próprio pipeline neste PR.

            Refs: {(string.IsNullOrWhiteSpace(refs) ? "SPEC-0006" : refs)}

            """;
    }

    /// <summary>Id de regra do Semgrep (pontuado, minúsculo) com "sqli", ex.: csharp.lang.security.sqli.csharp-sqli.</summary>
    [GeneratedRegex(@"\bSemgrep (?:[a-z0-9_-]+\.)+[a-z0-9_.-]*sqli[a-z0-9_.-]*")]
    private static partial Regex SemgrepSqlInjectionRule();
}
