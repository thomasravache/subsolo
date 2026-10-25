using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Subsolo.Tooling.Tests;

/// <summary>
/// SPEC-0041 UT-03 — <c>tools/tunnel.sh</c> abre o túnel rápido do Cloudflare para o Caddy do <c>aspire run</c>
/// (<c>https://localhost:8443</c>), com o cabeçalho <c>Host</c> e o nome TLS de <c>localhost</c> (para o Caddy casar o
/// site e o certificado), espera o endereço <c>https://*.trycloudflare.com</c> e mostra o endereço e o comando do
/// AppHost com <c>--Subsolo:PublicUrl</c>. Um <c>cloudflared</c> falso no <c>PATH</c> grava os argumentos e imprime, no
/// stderr como o real, o quadro com o endereço. Como no uso real, o script fica em primeiro plano até o Ctrl+C: o teste
/// lê a saída até o endereço aparecer e então manda o sinal de parada. Em toda parada (SIGTERM, SIGHUP, ou um segundo
/// Ctrl+C enquanto o AppHost demora a sair, H41-01) o <c>cloudflared</c> sai em poucos segundos, sem ficar órfão (o
/// endereço do túnel é público enquanto ele está no ar). Sem <c>cloudflared</c> no <c>PATH</c>, sai 1 com a mensagem
/// de instalação.
/// </summary>
public sealed class TunnelScriptTests
{
    private const string TunnelUrl = "https://teste-123.trycloudflare.com";
    private static readonly TimeSpan AddressTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Prazo para o cloudflared sair depois do sinal de parada (o endereço do túnel é público).</summary>
    private static readonly TimeSpan CloudflaredExitTimeout = TimeSpan.FromSeconds(5);

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

    /// <summary>
    /// O <c>dotnet</c> falso do <c>--run</c> (H41-01): grava o PID em <c>$FAKE_DOTNET_PID</c>, ignora o SIGINT (como
    /// um AppHost que demora a derrubar os contêineres) e só sai sozinho depois de uns 30 segundos.
    /// </summary>
    private const string FakeDotnet = """
        #!/usr/bin/env bash
        echo "$$" > "$FAKE_DOTNET_PID"
        trap '' INT
        for _ in $(seq 150); do sleep 0.2; done
        """;

    [Fact]
    [Trait("Spec", "SPEC-0041:UT-03")]
    [Trait("Kind", "Integration")]
    public async Task OpensTheQuickTunnelToTheCaddyAndShowsTheAppHostCommand()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fakes = new Fakes();

        var run = await RunAsync(
            fakes,
            [],
            text => text.Contains($"--Subsolo:PublicUrl={TunnelUrl}", StringComparison.Ordinal),
            ["TERM"],
            ct);

        run.ExitedEarly.ShouldBeNull($"tools/tunnel.sh saiu antes de mostrar o comando do AppHost (exit={run.ExitedEarly}):\n{run.Output}");
        File.Exists(fakes.CloudflaredArgs).ShouldBeTrue($"tools/tunnel.sh não chamou o cloudflared do PATH:\n{run.Output}");
        var arguments = await File.ReadAllLinesAsync(fakes.CloudflaredArgs, ct);
        var listing = string.Join(' ', arguments);
        arguments.ShouldContain("tunnel", $"o cloudflared não foi chamado com `tunnel` (argumentos: {listing})");
        HasOption(arguments, "--url", "https://localhost:8443")
            .ShouldBeTrue($"falta `--url https://localhost:8443` (argumentos: {listing})");
        HasOption(arguments, "--http-host-header", "localhost:8443")
            .ShouldBeTrue($"falta `--http-host-header localhost:8443` (argumentos: {listing})");
        HasOption(arguments, "--origin-server-name", "localhost")
            .ShouldBeTrue($"falta `--origin-server-name localhost` (argumentos: {listing})");

        run.Output.ShouldContain(TunnelUrl, Case.Sensitive, $"tools/tunnel.sh não mostrou o endereço do túnel:\n{run.Output}");
        run.Output.ShouldContain($"--Subsolo:PublicUrl={TunnelUrl}", Case.Sensitive,
            $"tools/tunnel.sh não mostrou o comando do AppHost com o endereço:\n{run.Output}");

        // H41-06 — o próprio script encerra o cloudflared no SIGTERM (o KillLeftover fica só como garantia).
        run.CloudflaredStopped.ShouldBeTrue(
            $"o cloudflared continuou no ar {CloudflaredExitTimeout.TotalSeconds:0}s depois do SIGTERM ao script:\n{run.Output}");
    }

    [Fact]
    [Trait("Spec", "SPEC-0041:UT-03")]
    [Trait("Kind", "Integration")]
    public async Task HangupStopsTheTunnel()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fakes = new Fakes();

        // H41-01 — o terminal fechado (SIGHUP) também encerra o túnel: o cloudflared roda num grupo próprio e não recebe
        // o hangup do terminal sozinho.
        var run = await RunAsync(
            fakes,
            [],
            text => text.Contains($"--Subsolo:PublicUrl={TunnelUrl}", StringComparison.Ordinal),
            ["HUP"],
            ct);

        run.ExitedEarly.ShouldBeNull($"tools/tunnel.sh saiu antes de mostrar o endereço (exit={run.ExitedEarly}):\n{run.Output}");
        run.CloudflaredStopped.ShouldBeTrue(
            $"o cloudflared continuou no ar {CloudflaredExitTimeout.TotalSeconds:0}s depois do SIGHUP ao script:\n{run.Output}");
    }

    [Theory]
    [Trait("Spec", "SPEC-0041:UT-03")]
    [Trait("Kind", "Integration")]
    [InlineData("INT,INT")]
    [InlineData("INT,TERM")]
    [InlineData("HUP")]
    public async Task WithRunTheTunnelStopsEvenWhileTheAppHostIsSlowToExit(string signals)
    {
        var ct = TestContext.Current.CancellationToken;
        using var fakes = new Fakes();

        // H41-01 — com --run, o AppHost demora a sair (ignora o SIGINT): o usuário aperta Ctrl+C de novo (ou manda
        // SIGTERM), ou fecha o terminal. O túnel não pode ficar no ar, órfão, depois que o script sai, nem esperar o
        // AppHost sair para ser encerrado.
        var run = await RunAsync(
            fakes,
            ["--run"],
            _ => File.Exists(fakes.DotnetPid),
            signals.Split(','),
            ct);

        run.ExitedEarly.ShouldBeNull($"tools/tunnel.sh saiu antes de subir o AppHost (exit={run.ExitedEarly}):\n{run.Output}");
        run.CloudflaredStopped.ShouldBeTrue(
            $"o cloudflared continuou no ar {CloudflaredExitTimeout.TotalSeconds:0}s depois de {signals} ao script " +
            $"(o AppHost falso ainda saindo):\n{run.Output}");
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
    /// Resultado de <see cref="RunAsync"/>: a saída do script, o código de saída se ele saiu sozinho antes do sinal, e
    /// se o cloudflared falso saiu até <see cref="CloudflaredExitTimeout"/> depois do último sinal.
    /// </summary>
    private sealed record TunnelRun(string Output, int? ExitedEarly, bool CloudflaredStopped);

    /// <summary>
    /// Roda o script pelo bash (stdout e stderr juntos) até <paramref name="ready"/> aceitar a saída, o processo sair ou
    /// passar o prazo; então manda ao script cada sinal de <paramref name="signals"/> (meio segundo entre eles), espera
    /// o cloudflared falso sair e depois o script sair; por garantia, mata o que sobrou (o script e os falsos, pelos PIDs
    /// que eles gravaram).
    /// </summary>
    private static async Task<TunnelRun> RunAsync(
        Fakes fakes, string[] arguments, Func<string, bool> ready, string[] signals, CancellationToken ct)
    {
        var script = RepositoryRoot.Combine("tools", "tunnel.sh");
        File.Exists(script).ShouldBeTrue("tools/tunnel.sh ausente");

        var startInfo = new ProcessStartInfo("/bin/bash")
        {
            WorkingDirectory = RepositoryRoot.Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(script);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (key, value) in fakes.Environment)
        {
            startInfo.Environment[key] = value;
        }

        var output = new StringBuilder();
        void OnData(object? _, DataReceivedEventArgs e)
        {
            if (e.Data is not null)
            {
                lock (output)
                {
                    output.AppendLine(e.Data);
                }
            }
        }

        string Snapshot()
        {
            lock (output)
            {
                return output.ToString();
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
        var cloudflaredStopped = false;
        try
        {
            var deadline = DateTimeOffset.UtcNow + AddressTimeout;
            while (!ready(Snapshot()) && !process.HasExited && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
            }

            if (process.HasExited)
            {
                exitedEarly = process.ExitCode;
            }
            else
            {
                for (var i = 0; i < signals.Length; i++)
                {
                    if (i > 0)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
                    }

                    await SignalAsync(process.Id, signals[i]);
                }

                var cloudflaredDeadline = DateTimeOffset.UtcNow + CloudflaredExitTimeout;
                while (IsAlive(fakes.CloudflaredPid) && DateTimeOffset.UtcNow < cloudflaredDeadline)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
                }

                cloudflaredStopped = !IsAlive(fakes.CloudflaredPid);
            }
        }
        finally
        {
            if (!process.HasExited)
            {
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

            KillLeftover(fakes.CloudflaredPid);
            KillLeftover(fakes.DotnetPid);
        }

        process.WaitForExit();
        return new TunnelRun(Snapshot(), exitedEarly, cloudflaredStopped);
    }

    private static Task<ProcessResult> SignalAsync(int pid, string signal) =>
        ProcessRunner.RunAsync(
            "kill", [$"-{signal}", pid.ToString(CultureInfo.InvariantCulture)],
            RepositoryRoot.Path, TimeSpan.FromSeconds(5), cancellationToken: CancellationToken.None);

    /// <summary>O PID gravado em <paramref name="pidFile"/>, se o arquivo existe.</summary>
    private static int? ReadPid(string pidFile) =>
        File.Exists(pidFile) && int.TryParse(File.ReadAllText(pidFile).Trim(), CultureInfo.InvariantCulture, out var pid)
            ? pid
            : null;

    /// <summary>O processo do PID gravado ainda está no ar (um zumbi, já encerrado e não colhido, conta como saído).</summary>
    private static bool IsAlive(string pidFile)
    {
        if (ReadPid(pidFile) is not { } pid)
        {
            return false;
        }

        try
        {
            var stat = File.ReadAllText($"/proc/{pid.ToString(CultureInfo.InvariantCulture)}/stat");
            var state = stat[(stat.LastIndexOf(')') + 2)..].Split(' ')[0];
            return state != "Z" && state != "X";
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Mata o falso se ele ainda estiver no ar (o script deveria tê-lo encerrado).</summary>
    private static void KillLeftover(string pidFile)
    {
        if (ReadPid(pidFile) is not { } pid)
        {
            return;
        }

        try
        {
            using var leftover = Process.GetProcessById(pid);
            if (leftover.ProcessName is "bash" or "cloudflared" or "dotnet")
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

    /// <summary>O diretório do teste com o <c>cloudflared</c> e o <c>dotnet</c> falsos à frente do <c>PATH</c>.</summary>
    private sealed class Fakes : IDisposable
    {
        private readonly TempDirectory temp = new("subsolo-tunnel-");

        public Fakes()
        {
            var bin = temp.Combine("bin");
            Directory.CreateDirectory(bin);
            Write(Path.Combine(bin, "cloudflared"), FakeCloudflared);
            Write(Path.Combine(bin, "dotnet"), FakeDotnet);
            Environment = new Dictionary<string, string>
            {
                ["PATH"] = $"{bin}{Path.PathSeparator}{System.Environment.GetEnvironmentVariable("PATH")}",
                ["FAKE_CLOUDFLARED_ARGS"] = CloudflaredArgs,
                ["FAKE_CLOUDFLARED_PID"] = CloudflaredPid,
                ["FAKE_DOTNET_PID"] = DotnetPid,
            };
        }

        public string CloudflaredArgs => temp.Combine("cloudflared.args");

        public string CloudflaredPid => temp.Combine("cloudflared.pid");

        public string DotnetPid => temp.Combine("dotnet.pid");

        public IReadOnlyDictionary<string, string> Environment { get; }

        public void Dispose() => temp.Dispose();

        private static void Write(string path, string content)
        {
            File.WriteAllText(path, content.ReplaceLineEndings("\n") + "\n");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
    }
}
