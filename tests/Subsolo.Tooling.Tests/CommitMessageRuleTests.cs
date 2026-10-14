namespace Subsolo.Tooling.Tests;

/// <summary>
/// SPEC-0004 UT-01 — a regra de validação do hook commit-msg, avaliada diretamente sobre o script
/// <c>.githooks/commit-msg</c> (sem repositório git): aceita apenas mensagens Conventional Commits com <c>Refs: SPEC-NNNN</c>.
/// </summary>
public sealed class CommitMessageRuleTests
{
    private static readonly TimeSpan HookTimeout = TimeSpan.FromSeconds(30);

    public static TheoryData<string, string, bool> Messages => new()
    {
        { "conventional com escopo e Refs", "feat(events): add agenda\n\nRefs: SPEC-0004\n", true },
        { "sem tipo e sem Refs", "ajustes\n", false },
        { "tipo válido sem Refs", "feat: x\n", false },
        { "tipo inexistente com Refs", "feature: x\n\nRefs: SPEC-0004\n", false },
    };

    [Theory]
    [MemberData(nameof(Messages))]
    [Trait("Spec", "SPEC-0004:UT-01")]
    public async Task CommitMsgRuleAcceptsOnlyConventionalMessagesWithSpecReference(string scenario, string message, bool expectedValid)
    {
        var hook = RepositoryRoot.Combine(".githooks", "commit-msg");
        File.Exists(hook).ShouldBeTrue("hook .githooks/commit-msg ausente no repositório (esperado via `spec_graph.py vendor --hooks`)");

        using var temp = new TempDirectory();
        var messageFile = temp.Combine("COMMIT_EDITMSG");
        await File.WriteAllTextAsync(messageFile, message, TestContext.Current.CancellationToken);

        var result = await ProcessRunner.RunAsync(
            "bash",
            [hook, messageFile],
            temp.Path,
            HookTimeout,
            cancellationToken: TestContext.Current.CancellationToken);

        if (expectedValid)
        {
            result.ExitCode.ShouldBe(0, $"[{scenario}] mensagem válida rejeitada:{Environment.NewLine}{result}");
        }
        else
        {
            result.ExitCode.ShouldBe(1, $"[{scenario}] mensagem inválida deveria sair com exit 1:{Environment.NewLine}{result}");
            result.StandardError.ShouldNotBeNullOrWhiteSpace($"[{scenario}] o hook deve explicar a mensagem esperada");
        }
    }
}
