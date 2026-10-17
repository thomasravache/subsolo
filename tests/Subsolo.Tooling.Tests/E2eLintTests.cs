namespace Subsolo.Tooling.Tests;

/// <summary>
/// SPEC-0013 IT-04 — com um arquivo TypeScript em <c>e2e/</c> que tem erro de tipo, <c>./tools/lint.sh</c> termina com
/// exit ≠ 0 e o passo <c>e2e-lint</c> (<c>tsc --noEmit</c> do projeto do Playwright) falha apontando o arquivo.
/// Antes da correção a pasta <c>e2e/</c> fica fora do lint: o erro só aparece ao rodar os testes E2E.
/// </summary>
public sealed class E2eLintTests
{
    private static readonly TimeSpan LintTimeout = TimeSpan.FromMinutes(20);

    private const string TypeErrorFileName = "lint-probe-type-error.ts";

    [Fact]
    [Trait("Spec", "SPEC-0013:IT-04")]
    [Trait("Kind", "Integration")]
    public async Task LintFailsTheE2eLintStepOnTypeErrorInE2eTypeScript()
    {
        var ct = TestContext.Current.CancellationToken;
        File.Exists(RepositoryRoot.Combine("tools", "lint.sh")).ShouldBeTrue("tools/lint.sh ausente no repositório");
        File.Exists(RepositoryRoot.Combine("e2e", "package.json")).ShouldBeTrue("e2e/package.json ausente");

        using var temp = new TempDirectory("subsolo-e2e-lint-");
        await RepositorySnapshot.CopyToAsync(temp.Path, ct);

        // TS sintaticamente válido e formatado, mas com erro de tipo (string atribuída a number).
        await File.WriteAllTextAsync(
            Path.Combine(temp.Path, "e2e", "tests", TypeErrorFileName),
            "export const lintProbe: number = \"não é número\";\n",
            ct);

        await ProcessRunner.RunAsync("git", ["init", "-q"], temp.Path, TimeSpan.FromMinutes(1), cancellationToken: ct);
        await ProcessRunner.RunAsync("git", ["add", "-A"], temp.Path, TimeSpan.FromMinutes(1), cancellationToken: ct);

        var result = await ProcessRunner.RunAsync(
            "bash",
            ["./tools/lint.sh"],
            temp.Path,
            LintTimeout,
            new Dictionary<string, string?> { ["CI"] = "true" },
            ct);

        result.ExitCode.ShouldNotBe(0, $"lint deveria falhar com erro de tipo em e2e/:{Environment.NewLine}{result}");
        result.Output.ShouldContain("<<< e2e-lint: FALHOU", Case.Sensitive,
            $"o passo e2e-lint do tools/lint.sh deve falhar:{Environment.NewLine}{result}");
        result.Output.ShouldContain(TypeErrorFileName, Case.Sensitive,
            $"o e2e-lint deve apontar o arquivo com erro de tipo:{Environment.NewLine}{result}");
    }
}
