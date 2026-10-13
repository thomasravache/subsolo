namespace Subsolo.Tooling.Tests.Infrastructure;

/// <summary>Diretório temporário próprio de um teste, apagado no Dispose.</summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory(string prefix = "subsolo-tooling-")
    {
        Path = Directory.CreateTempSubdirectory(prefix).FullName;
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Limpeza de melhor esforço: o diretório fica no temp do sistema.
        }
        catch (UnauthorizedAccessException)
        {
            // Idem.
        }
    }

    public static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(System.IO.Path.Combine(destination, System.IO.Path.GetRelativePath(source, dir)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = System.IO.Path.Combine(destination, System.IO.Path.GetRelativePath(source, file));
            File.Copy(file, target, overwrite: true);
            CopyUnixMode(file, target);
        }
    }

    public static void CopyUnixMode(string source, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(target, File.GetUnixFileMode(source));
        }
    }
}
