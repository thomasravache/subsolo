namespace Subsolo.Common.Infrastructure.Storage;

/// <summary>SPEC-0007 — armazenamento de arquivos (disco local; usado pela feature de flyer, ADR-0015).</summary>
public interface IFileStorage
{
    /// <summary>Grava o conteúdo em <paramref name="path"/> e devolve o caminho relativo armazenado.</summary>
    Task<string> SaveAsync(Stream content, string path, string contentType, CancellationToken cancellationToken);

    /// <summary>Abre o arquivo para leitura; <see langword="null"/> quando não existe.</summary>
    Task<Stream?> OpenAsync(string path, CancellationToken cancellationToken);
}
