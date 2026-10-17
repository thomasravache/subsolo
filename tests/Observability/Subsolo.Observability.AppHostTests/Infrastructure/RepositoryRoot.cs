namespace Subsolo.Observability.AppHostTests.Infrastructure;

internal static class RepositoryRoot
{
    /// <summary>Raiz do repositório (onde está o <c>Subsolo.slnx</c>), acima da pasta de saída do teste.</summary>
    public static string Path()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "Subsolo.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"Subsolo.slnx não encontrado acima de {AppContext.BaseDirectory}.");
    }
}
