namespace Subsolo.Tooling.Tests.Infrastructure;

/// <summary>
/// Repositório git temporário com os hooks do repositório (<c>.githooks/</c>) instalados via <c>core.hooksPath</c>.
/// Isolado da configuração global/sistema do git da máquina para ser determinístico.
/// </summary>
public sealed class GitSandbox : IDisposable
{
    private static readonly TimeSpan GitTimeout = TimeSpan.FromMinutes(5);

    private readonly TempDirectory _root = new("subsolo-git-sandbox-");
    private readonly Dictionary<string, string?> _environment;

    private GitSandbox()
    {
        var emptyGlobalConfig = _root.Combine("gitconfig-global");
        File.WriteAllText(emptyGlobalConfig, string.Empty);
        Directory.CreateDirectory(WorkTree);
        _environment = new Dictionary<string, string?>
        {
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["GIT_CONFIG_GLOBAL"] = emptyGlobalConfig,
            ["GIT_TERMINAL_PROMPT"] = "0",
        };
    }

    public string WorkTree => _root.Combine("repo");

    /// <summary>
    /// Cria o repositório temporário e copia os hooks e a configuração do gitleaks do repositório real.
    /// Falha com mensagem clara quando o hook exigido ainda não existe.
    /// </summary>
    public static async Task<GitSandbox> CreateWithRepositoryHooksAsync(
        IEnumerable<string> requiredHooks,
        CancellationToken cancellationToken)
    {
        var hooksSource = RepositoryRoot.Combine(".githooks");
        foreach (var hook in requiredHooks)
        {
            File.Exists(Path.Combine(hooksSource, hook)).ShouldBeTrue(
                $"hook .githooks/{hook} ausente no repositório (esperado via `spec_graph.py vendor --hooks`)");
        }

        var sandbox = new GitSandbox();
        try
        {
            TempDirectory.CopyDirectory(hooksSource, Path.Combine(sandbox.WorkTree, ".githooks"));
            foreach (var config in new[] { ".gitleaks.toml", ".gitleaksignore" })
            {
                var source = RepositoryRoot.Combine(config);
                if (File.Exists(source))
                {
                    File.Copy(source, Path.Combine(sandbox.WorkTree, config));
                }
            }

            await sandbox.GitOkAsync(["init", "-q", "-b", "main"], cancellationToken).ConfigureAwait(false);
            await sandbox.GitOkAsync(["config", "user.name", "Subsolo Tooling Tests"], cancellationToken).ConfigureAwait(false);
            await sandbox.GitOkAsync(["config", "user.email", "tooling-tests@subsolo.invalid"], cancellationToken).ConfigureAwait(false);
            await sandbox.GitOkAsync(["config", "commit.gpgsign", "false"], cancellationToken).ConfigureAwait(false);
            await sandbox.GitOkAsync(["config", "core.hooksPath", ".githooks"], cancellationToken).ConfigureAwait(false);
            return sandbox;
        }
        catch
        {
            sandbox.Dispose();
            throw;
        }
    }

    public Task<ProcessResult> GitAsync(IEnumerable<string> arguments, CancellationToken cancellationToken) =>
        ProcessRunner.RunAsync("git", arguments, WorkTree, GitTimeout, _environment, cancellationToken);

    public async Task GitOkAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var args = arguments.ToArray();
        var result = await GitAsync(args, cancellationToken).ConfigureAwait(false);
        result.ExitCode.ShouldBe(0, $"git {string.Join(' ', args)} falhou:{Environment.NewLine}{result}");
    }

    public void WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(WorkTree, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>Faz <c>git add</c> e <c>git commit -F</c> com a mensagem dada, passando pelos hooks.</summary>
    public async Task<ProcessResult> AddAndCommitAsync(string relativePath, string message, CancellationToken cancellationToken)
    {
        await GitOkAsync(["add", "--", relativePath], cancellationToken).ConfigureAwait(false);
        var messageFile = _root.Combine($"commit-msg-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(messageFile, message, cancellationToken).ConfigureAwait(false);
        return await GitAsync(["commit", "-q", "-F", messageFile], cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CommitCountAsync(CancellationToken cancellationToken)
    {
        var result = await GitAsync(["rev-list", "--count", "--all"], cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 && int.TryParse(result.StandardOutput.Trim(), out var count) ? count : 0;
    }

    public void Dispose() => _root.Dispose();
}
