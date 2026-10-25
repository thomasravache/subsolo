using System.Diagnostics;
using System.Text;

namespace Subsolo.Tooling.Tests;

/// <summary>
/// SPEC-0041 UT-03 — <c>tools/tunnel.sh</c> abre o túnel rápido do Cloudflare para o Caddy do <c>aspire run</c>
/// (<c>https://localhost:8443</c>), com o cabeçalho <c>Host</c> e o nome TLS de <c>localhost</c> (para o Caddy casar o
/// site e o certificado), espera o endereço <c>https://*.trycloudflare.com</c> e mostra o endereço e o comando do
/// AppHost com <c>--Subsolo:PublicUrl</c>. Um <c>cloudflared</c> falso no <c>PATH</c> grava os argumentos e imprime, no
/// stderr como o real, o quadro com o endereço. Como no uso real, o script fica em primeiro plano até o Ctrl+C: o teste
/// lê a saída até o endereço aparecer e então manda SIGTERM. Sem <c>cloudflared</c> no <c>PATH</c>, sai 1 com a
/// mensagem de instalação.
/// </summary>
public sealed class TunnelScriptTests
{
    private const string TunnelUrl = "https://teste-123.trycloudflare.com";
    private static readonly TimeSpan AddressTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// O <c>cloudflared</c> falso: grava cada argumento numa linha de <c>$FAKE_CLOUDFLARED_ARGS</c> e o PID em
    /// <c>$FAKE_CLOUDFLARED_PID</c>, imprime no stderr o quadro do túnel rápido no formato do log real e fica no ar até
    /// receber um sinal (como o real).
    /// </summary>
    private const string FakeCloudflared = """
        #!/usr/bin/env bash
        printf '%s\n' "$@" > "$FAKE_CLOUDFLARED_ARGS"
        echo "$$" > "$FAKE_CLOUDFLARED_PID"
        {
          echo "2026-10-25T12:00:00Z INF Thank you for trying Cloudflare Tunnel."
          echo "2026-10-25T12:00:00Z INF Requesting new quick Tunnel on trycloudflare.com..."
          echo "2026-10-25T12:00:01Z INF +--------------------------------------------------------------------------------------------+"
          echo "2026-10-25T12:00:01Z INF |  Your quick Tunnel has been created! Visit it at (it may take some time to be reachable):  |"
          echo "2026-10-25T12:00:01Z INF |  https://teste-123.trycloudflare.com                                                       |"
          echo "2026-10-25T12:00:01Z INF +--------------------------------------------------------------------------------------------+"
          echo "2026-10-25T12:00:01Z INF Registered tunnel connection connIndex=0 location=gru01 protocol=quic"
        } >&2
        trap 'exit 0' TERM INT
        while :; do sleep 0.2; done
        """;

    [Fact]
    [Trait("Spec", "SPEC-0041:UT-03")]
    [Trait("Kind", "Integration")]
    public async Task OpensTheQuickTunnelToTheCaddyAndShowsTheAppHostCommand()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = RepositoryRoot.Combine("tools", "tunnel.sh");
        File.Exists(script).ShouldBeTrue("tools/tunnel.sh ausente");

        using var temp = new TempDirectory("subsolo-tunnel-");
        var bin = temp.Combine("bin");
        Directory.CreateDirectory(bin);
        var fake = Path.Combine(bin, "cloudflared");
        await File.WriteAllTextAsync(fake, FakeCloudflared.ReplaceLineEndings("\n") + "\n", ct);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(fake, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        var argsFile = temp.Combine("cloudflared.args");
        var pidFile = temp.Combine("cloudflared.pid");

        var path = $"{bin}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}";
        var (output, exited) = await RunUntilAsync(
            script,
            new Dictionary<string, string>
            {
                ["PATH"] = path,
                ["FAKE_CLOUDFLARED_ARGS"] = argsFile,
                ["FAKE_CLOUDFLARED_PID"] = pidFile,
            },
            text => text.Contains($"--Subsolo:PublicUrl={TunnelUrl}", StringComparison.Ordinal),
            pidFile,
            ct);

        exited.ShouldBeNull($"tools/tunnel.sh saiu antes de mostrar o comando do AppHost (exit={exited}):\n{output}");
        File.Exists(argsFile).ShouldBeTrue($"tools/tunnel.sh não chamou o cloudflared do PATH:\n{output}");
        var arguments = await File.ReadAllLinesAsync(argsFile, ct);
        var listing = string.Join(' ', arguments);
        arguments.ShouldContain("tunnel", $"o cloudflared não foi chamado com `tunnel` (argumentos: {listing})");
        HasOption(arguments, "--url", "https://localhost:8443")
            .ShouldBeTrue($"falta `--url https://localhost:8443` (argumentos: {listing})");
        HasOption(arguments, "--http-host-header", "localhost:8443")
            .ShouldBeTrue($"falta `--http-host-header localhost:8443` (argumentos: {listing})");
        HasOption(arguments, "--origin-server-name", "localhost")
            .ShouldBeTrue($"falta `--origin-server-name localhost` (argumentos: {listing})");

        output.ShouldContain(TunnelUrl, Case.Sensitive, $"tools/tunnel.sh não mostrou o endereço do túnel:\n{output}");
        output.ShouldContain($"--Subsolo:PublicUrl={TunnelUrl}", Case.Sensitive,
            $"tools/tunnel.sh não mostrou o comando do AppHost com o endereço:\n{output}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0041:UT-03")]
    [Trait("Kind", "Integration")]
    public async Task WithoutCloudflaredExitsOneWithTheInstallationMessage()
    {
        var ct = TestContext.Current.CancellationToken;
        var script = RepositoryRoot.Combine("tools", "tunnel.sh");
        File.Exists(script).ShouldBeTrue("tools/tunnel.sh ausente");

        // O PATH do teste sem nenhum cloudflared (o desenvolvedor pode ter o real instalado): um diretório com links
        // para todos os executáveis do PATH atual, menos o cloudflared.
        using var temp = new TempDirectory("subsolo-tunnel-nocf-");
        var bin = temp.Combine("bin");
        Directory.CreateDirectory(bin);
        LinkExecutablesExcept(bin, "cloudflared");

        var result = await ProcessRunner.RunAsync(
            "/bin/bash",
            [script],
            RepositoryRoot.Path,
            AddressTimeout,
            new Dictionary<string, string?> { ["PATH"] = bin },
            ct);

        result.ExitCode.ShouldBe(1, $"tools/tunnel.sh sem cloudflared deveria sair com 1:\n{result}");
        result.Output.ShouldContain("cloudflared", Case.Sensitive, $"a mensagem deve citar o cloudflared:\n{result}");
        result.Output.ShouldContain("instal", Case.Insensitive, $"a mensagem deve citar a instalação:\n{result}");
        result.Output.ShouldContain("https://", Case.Sensitive, $"a mensagem deve trazer o link de instalação:\n{result}");
    }

    /// <summary><paramref name="name"/> seguido de <paramref name="value"/>, ou na forma <c>name=value</c>.</summary>
    private static bool HasOption(string[] arguments, string name, string value)
    {
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] == $"{name}={value}" || (arguments[i] == name && i + 1 < arguments.Length && arguments[i + 1] == value))
            {
                return true;
            }
        }

        return false;
    }

    private static void LinkExecutablesExcept(string bin, string excluded)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var name = Path.GetFileName(file);
                if (name == excluded || !seen.Add(name))
                {
                    continue;
                }

                File.CreateSymbolicLink(Path.Combine(bin, name), file);
            }
        }
    }

    /// <summary>
    /// Roda o script pelo bash (stdout e stderr juntos) até <paramref name="done"/> aceitar a saída, o processo sair ou
    /// passar o prazo; então manda SIGTERM, espera a saída e, por garantia, mata o que sobrou (o script e o
    /// cloudflared falso, pelo PID que ele gravou). Devolve a saída e o código de saída se o script saiu sozinho antes.
    /// </summary>
    private static async Task<(string Output, int? ExitedEarly)> RunUntilAsync(
        string script,
        IReadOnlyDictionary<string, string> environment,
        Func<string, bool> done,
        string fakePidFile,
        CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo("/bin/bash")
        {
            WorkingDirectory = RepositoryRoot.Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(script);
        foreach (var (key, value) in environment)
        {
            startInfo.Environment[key] = value;
        }

        var output = new StringBuilder();
        var matched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnData(object? _, DataReceivedEventArgs e)
        {
            if (e.Data is null)
            {
                return;
            }

            lock (output)
            {
                output.AppendLine(e.Data);
                if (done(output.ToString()))
                {
                    matched.TrySetResult();
                }
            }
        }

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += OnData;
        process.ErrorDataReceived += OnData;
        process.Start();
        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        int? exitedEarly = null;
        try
        {
            var exit = process.WaitForExitAsync(ct);
            var first = await Task.WhenAny(matched.Task, exit, Task.Delay(AddressTimeout, ct));
            if (first == exit && !matched.Task.IsCompleted)
            {
                exitedEarly = process.ExitCode;
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                await ProcessRunner.RunAsync(
                    "kill", ["-TERM", process.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)],
                    RepositoryRoot.Path, TimeSpan.FromSeconds(5), cancellationToken: CancellationToken.None);
                using var exitCts = new CancellationTokenSource(ExitTimeout);
                try
                {
                    await process.WaitForExitAsync(exitCts.Token);
                }
                catch (OperationCanceledException)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }
            }

            KillLeftover(fakePidFile);
        }

        lock (output)
        {
            return (output.ToString(), exitedEarly);
        }
    }

    /// <summary>Mata o cloudflared falso se ele ainda estiver no ar (o script deveria tê-lo encerrado).</summary>
    private static void KillLeftover(string pidFile)
    {
        if (!File.Exists(pidFile)
            || !int.TryParse(File.ReadAllText(pidFile).Trim(), System.Globalization.CultureInfo.InvariantCulture, out var pid))
        {
            return;
        }

        try
        {
            using var leftover = Process.GetProcessById(pid);
            if (leftover.ProcessName is "bash" or "cloudflared")
            {
                leftover.Kill(entireProcessTree: true);
            }
        }
        catch (ArgumentException)
        {
            // Já saiu.
        }
        catch (InvalidOperationException)
        {
            // Idem.
        }
    }
}
