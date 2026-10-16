namespace Subsolo.Tooling.Tests;

/// <summary>
/// SPEC-0004 IT-02 — o pre-commit (gitleaks, por binário ou imagem Docker) bloqueia um arquivo staged com uma
/// chave fictícia no formato AWS; sem o segredo, o commit passa. Também confere que o <c>.gitignore</c> da raiz cobre
/// <c>.env*</c> e <c>appsettings.*.local.json</c> (seção 5 da spec). SPEC-0012 IT-01/IT-02 acrescentam a chave no formato Google
/// API, o <c>.env.example</c> versionável e a pasta local <c>.claude/</c>.
/// </summary>
public sealed class PreCommitSecretScanTests
{
    // Chave fictícia: segue o formato real (AKIA + 16 caracteres [A-Z2-7]), mas não pertence a conta alguma.
    // Montada em partes para que o próprio arquivo de teste não seja apontado pelo scanner do repositório.
    private static readonly string FakeAwsAccessKeyId = "AKIA" + "Q3EGRTZ7" + "LNXWPV5K";

    // Chave fictícia no formato Google API (AIza + 35 caracteres [0-9A-Za-z_-]); não pertence a projeto algum.
    // Montada em partes pelo mesmo motivo da chave AWS acima.
    private static readonly string FakeGoogleApiKey = "AI" + "za" + "SyD4k9QmZr7Xv2LpT8" + "nWc3HbJ6fGe1Yu0Ks";

    private const string ValidMessage = "test(tooling): sandbox commit\n\nRefs: SPEC-0004\n";

    [Fact]
    [Trait("Spec", "SPEC-0004:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task PreCommitBlocksStagedFileWithFakeAwsKey()
    {
        var ct = TestContext.Current.CancellationToken;
        using var sandbox = await GitSandbox.CreateWithRepositoryHooksAsync(["pre-commit", "commit-msg"], ct);
        sandbox.WriteFile("config/credentials.ini", $"[default]\naws_access_key_id = {FakeAwsAccessKeyId}\n");

        var result = await sandbox.AddAndCommitAsync("config/credentials.ini", ValidMessage, ct);

        result.ExitCode.ShouldNotBe(0, $"o pre-commit deveria bloquear o segredo staged:{Environment.NewLine}{result}");
        (await sandbox.CommitCountAsync(ct)).ShouldBe(0, "nenhum commit deveria ter sido criado com o segredo");
        result.Output.ShouldContain("leak", Case.Insensitive, $"a saída deve trazer o achado do gitleaks:{Environment.NewLine}{result}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0012:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task PreCommitBlocksStagedFileWithFakeGoogleApiKey()
    {
        var ct = TestContext.Current.CancellationToken;
        using var sandbox = await GitSandbox.CreateWithRepositoryHooksAsync(["pre-commit", "commit-msg"], ct);
        sandbox.WriteFile("config/maps.ini", $"[maps]\napi_key = {FakeGoogleApiKey}\n");

        var result = await sandbox.AddAndCommitAsync("config/maps.ini", ValidMessage, ct);

        result.ExitCode.ShouldNotBe(0, $"o pre-commit deveria bloquear a chave Google staged:{Environment.NewLine}{result}");
        (await sandbox.CommitCountAsync(ct)).ShouldBe(0, "nenhum commit deveria ter sido criado com o segredo");
        result.Output.ShouldContain("leak", Case.Insensitive, $"a saída deve trazer o achado do gitleaks:{Environment.NewLine}{result}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0004:IT-02")]
    [Trait("Kind", "Integration")]
    public async Task PreCommitAllowsCleanFile()
    {
        var ct = TestContext.Current.CancellationToken;
        using var sandbox = await GitSandbox.CreateWithRepositoryHooksAsync(["pre-commit", "commit-msg"], ct);
        sandbox.WriteFile("config/settings.ini", "[default]\nregion = sa-east-1\n");

        var result = await sandbox.AddAndCommitAsync("config/settings.ini", ValidMessage, ct);

        result.ExitCode.ShouldBe(0, $"arquivo sem segredo deveria ser commitado:{Environment.NewLine}{result}");
        (await sandbox.CommitCountAsync(ct)).ShouldBe(1);
    }

    [Theory]
    [Trait("Spec", "SPEC-0004:IT-02")]
    [Trait("Kind", "Integration")]
    [InlineData(".env", true)]
    [InlineData(".env.local", true)]
    [InlineData("web/.env.production", true)]
    [InlineData("src/Subsolo.Api/appsettings.Development.local.json", true)]
    [InlineData("web/package.json", false)]
    [InlineData("src/Subsolo.Api/appsettings.Development.json", false)]
    public Task GitignoreCoversEnvFilesAndLocalSettings(string path, bool shouldBeIgnored) =>
        AssertGitignoreVerdictAsync(path, shouldBeIgnored);

    [Theory]
    [Trait("Spec", "SPEC-0012:IT-01")]
    [Trait("Kind", "Integration")]
    [InlineData(".env.example", false)]
    [InlineData("web/.env.example", false)]
    [InlineData(".env", true)]
    [InlineData(".env.local", true)]
    [InlineData(".claude/worktrees/x", true)]
    public Task GitignoreKeepsEnvExampleVersionedAndIgnoresClaudeLocalState(string path, bool shouldBeIgnored) =>
        AssertGitignoreVerdictAsync(path, shouldBeIgnored);

    private static async Task AssertGitignoreVerdictAsync(string path, bool shouldBeIgnored)
    {
        var ct = TestContext.Current.CancellationToken;

        // --no-index: avalia só as regras, mesmo que o caminho não exista ou já esteja rastreado.
        // core.excludesFile vazio: o gitignore global da máquina não pode mascarar uma lacuna do .gitignore do repositório.
        var result = await ProcessRunner.RunAsync(
            "git",
            ["-c", "core.excludesFile=", "check-ignore", "-q", "--no-index", "--", path],
            RepositoryRoot.Path,
            TimeSpan.FromMinutes(1),
            cancellationToken: ct);

        result.ExitCode.ShouldBeOneOf([0, 1], $"git check-ignore falhou para '{path}':{Environment.NewLine}{result}");
        if (shouldBeIgnored)
        {
            result.ExitCode.ShouldBe(0, $"'{path}' deveria ser ignorado pelo .gitignore (segredos/configuração local, ADR-0008)");
        }
        else
        {
            result.ExitCode.ShouldBe(1, $"'{path}' é fonte versionável e não deveria ser ignorado pelo .gitignore");
        }
    }
}
