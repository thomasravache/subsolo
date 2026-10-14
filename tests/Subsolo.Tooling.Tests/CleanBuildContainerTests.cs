namespace Subsolo.Tooling.Tests;

/// <summary>
/// SPEC-0004 IT-01 (Emenda v2) — num clone limpo dentro de um container com .NET 10 SDK, Node 24 e corepack habilitado
/// (sem pnpm global fixado/preparado), <c>./tools/build.sh</c> termina com exit 0 usando o pnpm fixado em
/// <c>web/package.json</c>. O teste não pode depender de o pnpm padrão/mais recente do corepack coincidir com o fixado.
/// </summary>
public sealed class CleanBuildContainerTests
{
    /// <summary>Imagem com tag fixa (ADR-0009), na mesma faixa de SDK do global.json (10.0.4xx).</summary>
    private const string SdkImage = "mcr.microsoft.com/dotnet/sdk:10.0.402";

    /// <summary>Node.js 24 LTS "Krypton" fixado (ADR-0009); o tarball é conferido contra o SHASUMS256.txt oficial.</summary>
    private const string NodeVersion = "24.21.0";
    private static readonly TimeSpan ContainerTimeout = TimeSpan.FromMinutes(40);

    /// <summary>
    /// Roda no container: copia o snapshot (montado só-leitura) para /work, instala o Node fixado (<c>NODE_VERSION</c>)
    /// após conferir o SHA-256 do tarball contra o SHASUMS256.txt oficial da versão, habilita o
    /// corepack sem preparar/ativar nenhuma versão de pnpm e executa <c>./tools/build.sh</c>. Depois confere, de dentro
    /// de web/, qual pnpm o corepack resolve (deve ser o fixado no <c>packageManager</c>).
    /// </summary>
    private const string ContainerScript = """
        set -euo pipefail
        mkdir -p /work
        cp -a /src/. /work/
        cd /work
        case "$(uname -m)" in
          x86_64) node_arch=x64 ;;
          aarch64) node_arch=arm64 ;;
          *) echo "arquitetura não suportada: $(uname -m)" >&2; exit 2 ;;
        esac
        node_dist="https://nodejs.org/dist/v$NODE_VERSION"
        node_tarball="node-v$NODE_VERSION-linux-$node_arch.tar.gz"
        node_tmp=$(mktemp -d)
        curl -fsSL -o "$node_tmp/SHASUMS256.txt" "$node_dist/SHASUMS256.txt"
        curl -fsSL -o "$node_tmp/$node_tarball" "$node_dist/$node_tarball"
        (cd "$node_tmp" && grep -E "^[0-9a-f]{64}  $node_tarball\$" SHASUMS256.txt > expected.sha256 && sha256sum -c expected.sha256)
        tar -xzf "$node_tmp/$node_tarball" -C /usr/local --strip-components=1
        rm -rf "$node_tmp"
        export COREPACK_ENABLE_DOWNLOAD_PROMPT=0
        corepack enable
        echo "node $(node --version) / dotnet $(dotnet --version)"
        echo '>>> ./tools/build.sh'
        ./tools/build.sh
        echo ">>> pnpm em web/: $(cd web && pnpm --version)"
        echo '>>> OK'
        """;

    [Fact]
    [Trait("Spec", "SPEC-0004:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task ContractBuildCommandsSucceedOnCleanCloneInContainer()
    {
        var ct = TestContext.Current.CancellationToken;
        File.Exists(RepositoryRoot.Combine("tools", "build.sh")).ShouldBeTrue("tools/build.sh ausente (comando `build` do contrato v2)");
        var pinnedPnpm = ReadPinnedPnpmVersion();

        var docker = await ProcessRunner.RunAsync("docker", ["version", "--format", "{{.Server.Version}}"], RepositoryRoot.Path, TimeSpan.FromMinutes(1), cancellationToken: ct);
        docker.ExitCode.ShouldBe(0, $"Docker indisponível — IT-01 exige um daemon Docker local:{Environment.NewLine}{docker}");

        using var snapshot = new TempDirectory("subsolo-clean-clone-");
        await RepositorySnapshot.CopyToAsync(snapshot.Path, ct);

        var result = await ProcessRunner.RunAsync(
            "docker",
            [
                "run", "--rm",
                "-v", $"{snapshot.Path}:/src:ro",
                "-e", "DOTNET_CLI_TELEMETRY_OPTOUT=1",
                "-e", "DOTNET_NOLOGO=1",
                "-e", "NEXT_TELEMETRY_DISABLED=1",
                "-e", "CI=true",
                "-e", $"NODE_VERSION={NodeVersion}",
                SdkImage,
                "bash", "-c", ContainerScript,
            ],
            snapshot.Path,
            ContainerTimeout,
            cancellationToken: ct);

        result.ExitCode.ShouldBe(0, $"./tools/build.sh falhou no clone limpo:{Environment.NewLine}{result}");
        result.StandardOutput.ShouldContain(">>> OK");
        result.StandardOutput.ShouldContain($"node v{NodeVersion} / dotnet ", Case.Sensitive, $"o container não usou o Node fixado ({NodeVersion}):{Environment.NewLine}{result}");
        result.StandardOutput.ShouldContain($">>> pnpm em web/: {pinnedPnpm}", Case.Sensitive, $"o corepack não resolveu o pnpm fixado ({pinnedPnpm}) em web/:{Environment.NewLine}{result}");
    }

    /// <summary>Lê a versão do pnpm fixada no campo <c>packageManager</c> de web/package.json (ex.: <c>pnpm@12.11.2</c>).</summary>
    private static string ReadPinnedPnpmVersion()
    {
        var packageJson = RepositoryRoot.Combine("web", "package.json");
        File.Exists(packageJson).ShouldBeTrue("web/package.json ausente (projeto Next.js não criado)");
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(packageJson));
        document.RootElement.TryGetProperty("packageManager", out var packageManager)
            .ShouldBeTrue("web/package.json sem o campo packageManager (pnpm não fixado)");
        var value = packageManager.GetString() ?? string.Empty;
        value.ShouldStartWith("pnpm@", customMessage: $"packageManager deveria fixar o pnpm, mas é '{value}'");
        var version = value["pnpm@".Length..];
        var hashIndex = version.IndexOf('+', StringComparison.Ordinal);
        return hashIndex >= 0 ? version[..hashIndex] : version;
    }
}
