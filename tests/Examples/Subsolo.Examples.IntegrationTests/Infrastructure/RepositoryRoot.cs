namespace Subsolo.Examples.IntegrationTests.Infrastructure;

/// <summary>
/// Raiz do repositório: o primeiro diretório acima do assembly de teste que contém a entrada <c>.git</c>
/// (diretório num clone, arquivo numa worktree).
/// </summary>
public static class RepositoryRoot
{
    private static readonly Lazy<string> Root = new(Locate);

    public static string Path => Root.Value;

    public static string Combine(params string[] parts) =>
        System.IO.Path.Combine([Path, .. parts]);

    private static string Locate()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var gitEntry = System.IO.Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(gitEntry) || File.Exists(gitEntry))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Raiz do repositório não encontrada: nenhuma entrada .git acima de {AppContext.BaseDirectory}");
    }
}
