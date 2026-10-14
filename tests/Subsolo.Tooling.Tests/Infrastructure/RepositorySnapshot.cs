namespace Subsolo.Tooling.Tests.Infrastructure;

/// <summary>
/// Copia o repositório como um clone novo o veria: arquivos versionados e arquivos novos não ignorados,
/// sem bin/, obj/, node_modules/ nem outros artefatos ignorados pelo .gitignore.
/// </summary>
public static class RepositorySnapshot
{
    public static async Task CopyToAsync(string destination, CancellationToken cancellationToken)
    {
        var listing = await ProcessRunner.RunAsync(
            "git",
            ["ls-files", "-z", "--cached", "--others", "--exclude-standard"],
            RepositoryRoot.Path,
            TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (listing.ExitCode != 0)
        {
            throw new InvalidOperationException($"git ls-files falhou na raiz do repositório:{Environment.NewLine}{listing}");
        }

        foreach (var relative in listing.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var cleanRelative = relative.Trim('\r', '\n');
            var source = Path.Combine(RepositoryRoot.Path, cleanRelative);
            if (!File.Exists(source))
            {
                continue; // apagado no working tree, ainda no índice
            }

            var target = Path.Combine(destination, cleanRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
            TempDirectory.CopyUnixMode(source, target);
        }
    }
}
