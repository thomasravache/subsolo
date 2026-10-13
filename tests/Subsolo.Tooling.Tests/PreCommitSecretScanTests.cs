namespace Subsolo.Tooling.Tests;

/// <summary>
/// SPEC-0004 IT-02 — o pre-commit (gitleaks, por binário ou imagem Docker) bloqueia um arquivo staged com uma
/// chave fictícia no formato AWS; sem o segredo, o commit passa.
/// </summary>
public sealed class PreCommitSecretScanTests
{
    // Chave fictícia: segue o formato real (AKIA + 16 caracteres [A-Z2-7]), mas não pertence a conta alguma.
    // Montada em partes para que o próprio arquivo de teste não seja apontado pelo scanner do repositório.
    private static readonly string FakeAwsAccessKeyId = "AKIA" + "Q3EGRTZ7" + "LNXWPV5K";

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
}
