using System.Diagnostics;
using System.Text;

namespace Subsolo.Tooling.Tests.Infrastructure;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public string Output => StandardOutput + Environment.NewLine + StandardError;

    public override string ToString() =>
        $"exit={ExitCode}{Environment.NewLine}--- stdout ---{Environment.NewLine}{StandardOutput}" +
        $"{Environment.NewLine}--- stderr ---{Environment.NewLine}{StandardError}";
}

/// <summary>Executa um processo externo com tempo limite, sem shell implícito, capturando a saída.</summary>
public static class ProcessRunner
{
    /// <summary>Variáveis do git que, herdadas (por exemplo, quando a suíte roda dentro de um hook), apontariam para outro repositório.</summary>
    private static readonly string[] LeakingGitVariables =
    [
        "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY",
        "GIT_ALTERNATE_OBJECT_DIRECTORIES", "GIT_PREFIX", "GIT_COMMON_DIR",
    ];

    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string?>? environment = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var variable in LeakingGitVariables)
        {
            startInfo.Environment.Remove(variable);
        }

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                if (value is null)
                {
                    startInfo.Environment.Remove(key);
                }
                else
                {
                    startInfo.Environment[key] = value;
                }
            }
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (stdout)
                {
                    stdout.AppendLine(e.Data);
                }
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (stderr)
                {
                    stderr.AppendLine(e.Data);
                }
            }
        };

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new ProcessResult(127, string.Empty, $"não foi possível iniciar '{fileName}': {ex.Message}");
        }

        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException(
                $"'{fileName} {string.Join(' ', arguments)}' passou de {timeout} em {workingDirectory}." +
                $"{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
        }

        // Garante que os eventos assíncronos de saída foram todos entregues.
        process.WaitForExit();

        lock (stdout)
        {
            lock (stderr)
            {
                return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
            }
        }
    }

    /// <summary>Roda um script curto pelo bash (para comandos gravados como texto, como os do sdd-config).</summary>
    public static Task<ProcessResult> BashAsync(
        string script,
        string workingDirectory,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string?>? environment = null,
        CancellationToken cancellationToken = default) =>
        RunAsync("bash", ["-c", script], workingDirectory, timeout, environment, cancellationToken);
}
