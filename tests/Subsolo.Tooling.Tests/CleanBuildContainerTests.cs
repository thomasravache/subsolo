namespace Subsolo.Tooling.Tests;

/// <summary>
/// SPEC-0004 IT-01 — num clone limpo dentro de um container com .NET 10 SDK e Node 24 (pnpm via corepack),
/// os comandos de build do contrato terminam com exit 0.
/// </summary>
public sealed class CleanBuildContainerTests
{
    private const string SdkImage = "mcr.microsoft.com/dotnet/sdk:10.0";
    private static readonly TimeSpan ContainerTimeout = TimeSpan.FromMinutes(40);

    /// <summary>
    /// Roda no container: copia o snapshot (montado só-leitura) para /work, instala o Node 24 oficial, habilita o pnpm
    /// fixado no packageManager via corepack e executa os comandos de build do contrato, nesta ordem.
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
        node_dist=https://nodejs.org/dist/latest-v24.x
        node_tarball=$(curl -fsSL "$node_dist/SHASUMS256.txt" | grep -m1 -o "node-v24[^ ]*-linux-$node_arch.tar.gz")
        curl -fsSL "$node_dist/$node_tarball" | tar -xz -C /usr/local --strip-components=1
        export COREPACK_ENABLE_DOWNLOAD_PROMPT=0
        corepack enable pnpm
        echo "node $(node --version) / pnpm $(pnpm --version) / dotnet $(dotnet --version)"
        echo '>>> dotnet build Subsolo.slnx -warnaserror'
        dotnet build Subsolo.slnx -warnaserror
        echo '>>> pnpm -C web install --frozen-lockfile'
        pnpm -C web install --frozen-lockfile
        echo '>>> pnpm -C web build'
        pnpm -C web build
        echo '>>> OK'
        """;

    [Fact]
    [Trait("Spec", "SPEC-0004:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task ContractBuildCommandsSucceedOnCleanCloneInContainer()
    {
        var ct = TestContext.Current.CancellationToken;
        File.Exists(RepositoryRoot.Combine("Subsolo.slnx")).ShouldBeTrue("Subsolo.slnx ausente na raiz do repositório");
        File.Exists(RepositoryRoot.Combine("web", "package.json")).ShouldBeTrue("web/package.json ausente (projeto Next.js não criado)");
        File.Exists(RepositoryRoot.Combine("web", "pnpm-lock.yaml")).ShouldBeTrue("web/pnpm-lock.yaml ausente (install --frozen-lockfile exige lockfile)");

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
                SdkImage,
                "bash", "-c", ContainerScript,
            ],
            snapshot.Path,
            ContainerTimeout,
            cancellationToken: ct);

        result.ExitCode.ShouldBe(0, $"comandos de build do contrato falharam no clone limpo:{Environment.NewLine}{result}");
        result.StandardOutput.ShouldContain(">>> OK");
    }
}
