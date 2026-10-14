namespace Subsolo.Tooling.Tests;

/// <summary>
/// SPEC-0004 IT-04 — num repositório git temporário com os hooks instalados, o commit-msg rejeita mensagem
/// fora de Conventional Commits/sem <c>Refs: SPEC-NNNN</c> e aceita a mensagem correta.
/// </summary>
public sealed class CommitMsgHookTests
{
    [Fact]
    [Trait("Spec", "SPEC-0004:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task CommitWithMessageOutsideTheConventionIsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var sandbox = await GitSandbox.CreateWithRepositoryHooksAsync(["commit-msg", "pre-commit"], ct);
        sandbox.WriteFile("notes.txt", "agenda\n");

        var result = await sandbox.AddAndCommitAsync("notes.txt", "ajustes\n", ct);

        result.ExitCode.ShouldNotBe(0, $"commit com a mensagem \"ajustes\" deveria ser rejeitado:{Environment.NewLine}{result}");
        (await sandbox.CommitCountAsync(ct)).ShouldBe(0, "nenhum commit deveria ter sido criado");
        result.Output.ShouldContain("Conventional Commits", Case.Insensitive, $"o hook deve dizer qual é a mensagem esperada:{Environment.NewLine}{result}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0004:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task CommitWithConventionalMessageAndSpecReferenceIsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        using var sandbox = await GitSandbox.CreateWithRepositoryHooksAsync(["commit-msg", "pre-commit"], ct);
        sandbox.WriteFile("notes.txt", "agenda\n");

        var result = await sandbox.AddAndCommitAsync(
            "notes.txt",
            "feat(events): add agenda\n\nRefs: SPEC-0004\n",
            ct);

        result.ExitCode.ShouldBe(0, $"commit no padrão deveria ser aceito:{Environment.NewLine}{result}");
        (await sandbox.CommitCountAsync(ct)).ShouldBe(1);
    }
}
