namespace Subsolo.Common.Application.Media;

/// <summary>
/// SPEC-0022 (ADR-0015) — armazenamento de arquivos de mídia por caminho relativo (disco local no MVP, object storage
/// no futuro só por configuração).
/// </summary>
public interface IFileStorage
{
    Task SaveAsync(string path, Stream content, CancellationToken ct);

    Task<Stream?> OpenAsync(string path, CancellationToken ct);

    Task DeleteAsync(string path, CancellationToken ct);
}
