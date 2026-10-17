using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Subsolo.Tooling.Tests.CiPolicy;

/// <summary>Um check do PR como o <c>gh pr checks --json</c> o devolve.</summary>
internal sealed record PullRequestCheck(string Name, string Bucket, string State, string Link)
{
    /// <summary>O check pertence ao job: nome igual ao id, ou "id (variante)" quando o job usa matrix.</summary>
    public bool BelongsTo(string jobId) =>
        Name == jobId || Name.StartsWith(jobId + " (", StringComparison.Ordinal);

    public bool Passed => Bucket == "pass";

    public bool Failed => Bucket == "fail";

    /// <summary>Id do job no Actions (= id do check run), extraído do link <c>.../actions/runs/R/job/J</c>.</summary>
    public string? JobId
    {
        get
        {
            var match = Regex.Match(Link, @"/job/(\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }
    }

    public override string ToString() => $"{Name}: {Bucket} ({State}) {Link}";
}

/// <summary>
/// Verificação real no GitHub (SPEC-0006 §7.3): monta uma branch descartável <c>ci-check/&lt;id&gt;-&lt;timestamp&gt;</c>
/// a partir do HEAD atual num clone temporário (nunca na worktree), aplica a mudança do cenário, publica, abre um PR
/// rascunho contra a main, espera o CI e, no fim, fecha o PR e apaga a branch remota.
/// Pré-requisitos: <c>gh</c> autenticado com permissão de push em thomasravache/subsolo, <c>git</c> e o SDK .NET.
/// </summary>
internal sealed class GitHubCiVerification : IAsyncDisposable
{
    public const string Repository = "thomasravache/subsolo";
    public const string EnableVariable = "SDD_GITHUB_VERIFY";
    public const string NotRunMessage = "verificação no GitHub não executada: defina SDD_GITHUB_VERIFY=1";

    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan PushTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ChecksRegisterTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ChecksWatchTimeout = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    private readonly TempDirectory _root = new("subsolo-ci-check-");
    private readonly Dictionary<string, string?> _gitEnvironment;
    private bool _pushed;

    private GitHubCiVerification(string testId)
    {
        var emptyGlobalConfig = _root.Combine("gitconfig-global");
        File.WriteAllText(emptyGlobalConfig, string.Empty);
        _gitEnvironment = new Dictionary<string, string?>
        {
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["GIT_CONFIG_GLOBAL"] = emptyGlobalConfig,
            ["GIT_TERMINAL_PROMPT"] = "0",
        };
        Branch = $"ci-check/{testId.ToLowerInvariant()}-{DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}";
    }

    public string Branch { get; }

    public string WorkTree => _root.Combine("clone");

    public int? PullRequestNumber { get; private set; }

    public string? PullRequestUrl { get; private set; }

    /// <summary>
    /// Falha (nunca pula) quando a verificação não foi habilitada: um teste pulado esconderia a falta de evidência.
    /// </summary>
    public static void RequireEnabled()
    {
        if (Environment.GetEnvironmentVariable(EnableVariable) != "1")
        {
            Assert.Fail(NotRunMessage);
        }
    }

    /// <summary>Clona o HEAD atual do repositório num diretório temporário e cria a branch de verificação.</summary>
    public static async Task<GitHubCiVerification> StartAsync(string testId, CancellationToken ct)
    {
        var auth = await Gh(["auth", "status"], RepositoryRoot.Path, ShortTimeout, ct);
        auth.ExitCode.ShouldBe(0, $"gh não autenticado (rode `gh auth login`):{Environment.NewLine}{auth}");

        var head = await ProcessRunner.RunAsync("git", ["rev-parse", "HEAD"], RepositoryRoot.Path, ShortTimeout, cancellationToken: ct);
        head.ExitCode.ShouldBe(0, $"git rev-parse HEAD falhou:{Environment.NewLine}{head}");
        var headSha = head.StandardOutput.Trim();

        var run = new GitHubCiVerification(testId);
        try
        {
            Directory.CreateDirectory(run.WorkTree);
            await run.GitOkAsync(["init", "-q", "-b", "ci-check-base"], ct);
            await run.GitOkAsync(["config", "user.name", "Subsolo CI Check"], ct);
            await run.GitOkAsync(["config", "user.email", "ci-check@subsolo.invalid"], ct);
            await run.GitOkAsync(["config", "commit.gpgsign", "false"], ct);
            await run.GitOkAsync(["fetch", "-q", "--no-tags", RepositoryRoot.Path, headSha], ct);
            await run.GitOkAsync(["checkout", "-q", "-b", run.Branch, "FETCH_HEAD"], ct);
            return run;
        }
        catch
        {
            await run.DisposeAsync();
            throw;
        }
    }

    public void WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(WorkTree, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void AppendToFile(string relativePath, string content)
    {
        var path = Path.Combine(WorkTree, relativePath);
        File.Exists(path).ShouldBeTrue($"{relativePath} ausente no HEAD verificado");
        File.AppendAllText(path, content);
    }

    public void ReplaceInFile(string relativePath, string oldText, string newText)
    {
        var path = Path.Combine(WorkTree, relativePath);
        File.Exists(path).ShouldBeTrue($"{relativePath} ausente no HEAD verificado");
        var text = File.ReadAllText(path);
        text.ShouldContain(oldText, Case.Sensitive, $"trecho esperado ausente em {relativePath}");
        File.WriteAllText(path, text.Replace(oldText, newText, StringComparison.Ordinal));
    }

    /// <summary>Roda um comando auxiliar (por exemplo, dotnet restore para atualizar lock files) dentro do clone.</summary>
    public async Task RunInCloneOkAsync(string fileName, IEnumerable<string> arguments, CancellationToken ct)
    {
        var args = arguments.ToArray();
        var result = await ProcessRunner.RunAsync(fileName, args, WorkTree, PushTimeout, _gitEnvironment, ct);
        result.ExitCode.ShouldBe(0, $"{fileName} {string.Join(' ', args)} falhou no clone:{Environment.NewLine}{result}");
    }

    /// <summary>Commita tudo, publica a branch e abre um PR rascunho contra a main.</summary>
    public async Task CommitPushAndOpenDraftPullRequestAsync(string title, string body, CancellationToken ct)
    {
        await GitOkAsync(["add", "-A"], ct);
        var messageFile = _root.Combine("commit-msg.txt");
        await File.WriteAllTextAsync(messageFile, $"test(ci): {Branch}{Environment.NewLine}{Environment.NewLine}Refs: SPEC-0006{Environment.NewLine}", ct);
        await GitOkAsync(["commit", "-q", "-F", messageFile], ct);

        var push = await ProcessRunner.RunAsync(
            "git",
            ["-c", "credential.helper=", "-c", "credential.helper=!gh auth git-credential",
             "push", "-q", $"https://github.com/{Repository}.git", $"HEAD:refs/heads/{Branch}"],
            WorkTree, PushTimeout, _gitEnvironment, ct);
        _pushed = true;
        push.ExitCode.ShouldBe(0, $"push de {Branch} falhou:{Environment.NewLine}{push}");

        var bodyFile = _root.Combine("pr-body.md");
        await File.WriteAllTextAsync(bodyFile, body, ct);
        var create = await Gh(
            ["pr", "create", "--repo", Repository, "--base", "main", "--head", Branch, "--draft", "--title", title, "--body-file", bodyFile],
            WorkTree, ShortTimeout, ct);
        create.ExitCode.ShouldBe(0, $"gh pr create falhou:{Environment.NewLine}{create}");

        PullRequestUrl = create.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(line => line.Contains("/pull/", StringComparison.Ordinal));
        PullRequestUrl.ShouldNotBeNull($"URL do PR não encontrada na saída do gh:{Environment.NewLine}{create}");
        PullRequestNumber = int.Parse(PullRequestUrl[(PullRequestUrl.LastIndexOf('/') + 1)..], CultureInfo.InvariantCulture);
        TestContext.Current.SendDiagnosticMessage($"{Branch}: PR {PullRequestUrl}");
    }

    /// <summary>
    /// Espera os checks dos 8 jobs aparecerem e terminarem (<c>gh pr checks --watch</c>, com tempo limite) e devolve
    /// o estado final de cada um.
    /// </summary>
    public async Task<IReadOnlyList<PullRequestCheck>> WaitForChecksAsync(CancellationToken ct)
    {
        var number = RequirePullRequest();
        var deadline = DateTimeOffset.UtcNow + ChecksRegisterTimeout;
        IReadOnlyList<PullRequestCheck> checks = [];
        while (DateTimeOffset.UtcNow < deadline)
        {
            checks = await ReadChecksAsync(number, ct);
            if (CiWorkflowDocument.ContractJobIds.All(job => checks.Any(c => c.BelongsTo(job))))
            {
                break;
            }

            await Task.Delay(PollInterval, ct);
        }

        // Sai com código ≠ 0 quando algum check falha: o resultado é lido depois, por check.
        await Gh(["pr", "checks", number, "--repo", Repository, "--watch", "--interval", "20"], WorkTree, ChecksWatchTimeout, ct);

        checks = await ReadChecksAsync(number, ct);
        checks.Where(c => c.Bucket == "pending").ShouldBeEmpty($"checks ainda pendentes após o --watch ({PullRequestUrl})");
        TestContext.Current.SendDiagnosticMessage(
            $"{Branch} ({PullRequestUrl}):{Environment.NewLine}{string.Join(Environment.NewLine, checks)}");
        return checks;
    }

    /// <summary>Todos os checks do job (um por variante de matrix), exigindo que exista ao menos um.</summary>
    public IReadOnlyList<PullRequestCheck> ChecksOf(IReadOnlyList<PullRequestCheck> checks, string jobId)
    {
        var ofJob = checks.Where(c => c.BelongsTo(jobId)).ToList();
        ofJob.ShouldNotBeEmpty($"check `{jobId}` não apareceu no PR {PullRequestUrl}; checks: {string.Join("; ", checks)}");
        return ofJob;
    }

    /// <summary>Log das etapas que falharam no job (<c>gh run view --job J --log-failed</c>).</summary>
    public async Task<string> FailedLogAsync(PullRequestCheck check, CancellationToken ct)
    {
        check.JobId.ShouldNotBeNull($"id do job não encontrado no link do check {check}");
        var log = await Gh(["run", "view", "--repo", Repository, "--job", check.JobId, "--log-failed"], WorkTree, ShortTimeout, ct);
        log.ExitCode.ShouldBe(0, $"não foi possível ler o log do job {check}:{Environment.NewLine}{log}");
        return log.StandardOutput;
    }

    /// <summary>
    /// Anotações (título e mensagem) do check run escritas pela ferramenta que falhou, sem a genérica do runner
    /// ("Process completed with exit code N") nem as genéricas do próprio workflow (título "SDD pr-check"/"SDD validate",
    /// que só dizem "veja as falhas acima") — SPEC-0013, item 7.
    /// </summary>
    public async Task<IReadOnlyList<(string Title, string Message)>> ToolAnnotationsAsync(PullRequestCheck check, CancellationToken ct)
    {
        check.JobId.ShouldNotBeNull($"id do check run não encontrado no link do check {check}");
        var result = await Gh(["api", $"repos/{Repository}/check-runs/{check.JobId}/annotations"], WorkTree, ShortTimeout, ct);
        result.ExitCode.ShouldBe(0, $"não foi possível ler as anotações do check {check}:{Environment.NewLine}{result}");
        using var json = JsonDocument.Parse(result.StandardOutput);
        return [.. json.RootElement.EnumerateArray()
            .Select(a => (
                Title: a.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty,
                Message: a.TryGetProperty("message", out var m) ? m.GetString() ?? string.Empty : string.Empty))
            .Where(a => a.Message.Length > 0
                && !a.Message.StartsWith("Process completed with exit code", StringComparison.Ordinal)
                && a.Title is not ("SDD pr-check" or "SDD validate"))];
    }

    /// <summary>
    /// Texto que identifica os achados do check: log das etapas que falharam mais título e mensagem de cada anotação
    /// (o título carrega o id da regra, ex.: "CodeQL cs/sql-injection", "Semgrep csharp.lang.security.sqli…").
    /// </summary>
    public async Task<string> FindingsTextAsync(PullRequestCheck check, CancellationToken ct)
    {
        check.JobId.ShouldNotBeNull($"id do check run não encontrado no link do check {check}");
        var result = await Gh(["api", $"repos/{Repository}/check-runs/{check.JobId}/annotations"], WorkTree, ShortTimeout, ct);
        result.ExitCode.ShouldBe(0, $"não foi possível ler as anotações do check {check}:{Environment.NewLine}{result}");
        using var json = JsonDocument.Parse(result.StandardOutput);
        var annotations = json.RootElement.EnumerateArray().Select(a =>
            $"{(a.TryGetProperty("title", out var t) ? t.GetString() : null)}: {(a.TryGetProperty("message", out var m) ? m.GetString() : null)}");
        return string.Join(Environment.NewLine, [.. annotations, await FailedLogAsync(check, ct)]);
    }

    /// <summary>Marca o PR como pronto (rascunho não tem estado de merge) e devolve o mergeStateStatus.</summary>
    public async Task<string> MarkReadyAndReadMergeStateAsync(CancellationToken ct)
    {
        var number = RequirePullRequest();
        var ready = await Gh(["pr", "ready", number, "--repo", Repository], WorkTree, ShortTimeout, ct);
        ready.ExitCode.ShouldBe(0, $"gh pr ready falhou:{Environment.NewLine}{ready}");

        var state = "UNKNOWN";
        var deadline = DateTimeOffset.UtcNow + ShortTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var view = await Gh(["pr", "view", number, "--repo", Repository, "--json", "mergeStateStatus"], WorkTree, ShortTimeout, ct);
            view.ExitCode.ShouldBe(0, $"gh pr view falhou:{Environment.NewLine}{view}");
            using var json = JsonDocument.Parse(view.StandardOutput);
            state = json.RootElement.GetProperty("mergeStateStatus").GetString() ?? "UNKNOWN";
            if (state != "UNKNOWN")
            {
                break;
            }

            await Task.Delay(PollInterval, ct);
        }

        return state;
    }

    /// <summary>
    /// Fecha o PR sem merge e apaga a branch remota; roda mesmo quando o teste falhou ou foi cancelado. Nunca lança:
    /// uma limpeza que falha só é registrada (saída de diagnóstico do teste e stderr) para remoção manual.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (PullRequestNumber is { } number)
            {
                await CleanupAsync(
                    $"fechar o PR #{number}",
                    ["pr", "close", number.ToString(CultureInfo.InvariantCulture), "--repo", Repository,
                     "--comment", "Verificação de CI da SPEC-0006 concluída; PR descartável fechado sem merge."]);
            }

            if (_pushed)
            {
                await CleanupAsync(
                    $"apagar a branch remota {Branch}",
                    ["api", "-X", "DELETE", $"repos/{Repository}/git/refs/heads/{Branch}"]);
            }
        }
        finally
        {
            _root.Dispose();
        }
    }

    private static async Task CleanupAsync(string action, IReadOnlyList<string> arguments)
    {
        string? failure;
        try
        {
            var result = await Gh(arguments, RepositoryRoot.Path, ShortTimeout, CancellationToken.None);
            failure = result.ExitCode == 0 ? null : $"código {result.ExitCode}:{Environment.NewLine}{result}";
        }
#pragma warning disable CA1031 // limpeza best-effort: qualquer falha vira diagnóstico, nunca exceção no Dispose
        catch (Exception exception)
#pragma warning restore CA1031
        {
            failure = exception.ToString();
        }

        if (failure is not null)
        {
            ReportCleanupFailure($"limpeza da verificação de CI: falhou ao {action} (gh {string.Join(' ', arguments)}) — " +
                $"faça a remoção manualmente. {failure}");
        }
    }

    private static void ReportCleanupFailure(string message)
    {
        Console.Error.WriteLine(message);
#pragma warning disable CA1031 // sem contexto de teste ativo o diagnóstico fica só no stderr
        try
        {
            TestContext.Current.SendDiagnosticMessage(message);
            TestContext.Current.TestOutputHelper?.WriteLine(message);
        }
        catch (Exception)
        {
        }
#pragma warning restore CA1031
    }

    private string RequirePullRequest()
    {
        PullRequestNumber.ShouldNotBeNull("PR de verificação ainda não foi aberto");
        return PullRequestNumber.Value.ToString(CultureInfo.InvariantCulture);
    }

    private async Task<IReadOnlyList<PullRequestCheck>> ReadChecksAsync(string number, CancellationToken ct)
    {
        // Código 8 = checks pendentes; "no checks reported" sai com código 1 e sem JSON.
        var result = await Gh(["pr", "checks", number, "--repo", Repository, "--json", "name,bucket,state,link"], WorkTree, ShortTimeout, ct);
        var output = result.StandardOutput.Trim();
        if (!output.StartsWith('['))
        {
            return [];
        }

        using var json = JsonDocument.Parse(output);
        return [.. json.RootElement.EnumerateArray().Select(c => new PullRequestCheck(
            c.GetProperty("name").GetString() ?? string.Empty,
            c.GetProperty("bucket").GetString() ?? string.Empty,
            c.GetProperty("state").GetString() ?? string.Empty,
            c.GetProperty("link").GetString() ?? string.Empty))];
    }

    private async Task GitOkAsync(IEnumerable<string> arguments, CancellationToken ct)
    {
        var args = arguments.ToArray();
        var result = await ProcessRunner.RunAsync("git", args, WorkTree, PushTimeout, _gitEnvironment, ct);
        result.ExitCode.ShouldBe(0, $"git {string.Join(' ', args)} falhou no clone:{Environment.NewLine}{result}");
    }

    private static Task<ProcessResult> Gh(IEnumerable<string> arguments, string workingDirectory, TimeSpan timeout, CancellationToken ct) =>
        ProcessRunner.RunAsync("gh", arguments, workingDirectory, timeout, new Dictionary<string, string?> { ["GH_PROMPT_DISABLED"] = "1" }, ct);
}
