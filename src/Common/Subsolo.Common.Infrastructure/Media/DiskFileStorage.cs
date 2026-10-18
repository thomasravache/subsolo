using Microsoft.Extensions.Options;
using Subsolo.Common.Application.Media;

namespace Subsolo.Common.Infrastructure.Media;

/// <summary>
/// SPEC-0022 (ADR-0015) — <see cref="IFileStorage"/> em disco, sob <c>Media:Root</c>. Os caminhos são relativos à raiz
/// (sem <c>..</c>, sem caminho absoluto); a gravação vai para um arquivo temporário no mesmo diretório e troca de nome
/// no fim (atômico): quem lê nunca vê um arquivo pela metade, e uma falha não deixa nada para trás.
/// </summary>
internal sealed class DiskFileStorage(IOptions<MediaOptions> options) : IFileStorage
{
    private const int BufferSize = 81920;

    private readonly Lazy<string> _root = new(() => RootOf(options.Value));

    public async Task SaveAsync(string path, Stream content, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        var target = Resolve(path);
        var directory = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var file = new FileStream(
                temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous))
            {
                await content.CopyToAsync(file, ct);
                await file.FlushAsync(ct);
                file.Flush(flushToDisk: true);
            }

            File.Move(temporary, target, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    public Task<Stream?> OpenAsync(string path, CancellationToken ct)
    {
        var target = Resolve(path);
        Stream? stream = File.Exists(target)
            ? new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        File.Delete(Resolve(path));
        return Task.CompletedTask;
    }

    /// <summary>O caminho absoluto de <paramref name="path"/> sob a raiz; recusa o que sairia dela.</summary>
    private string Resolve(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (Path.IsPathRooted(path)
            || path.Contains('\\', StringComparison.Ordinal)
            || path.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException($"Caminho de mídia inválido: \"{path}\".", nameof(path));
        }

        var root = _root.Value;
        var full = Path.GetFullPath(Path.Combine(root, path));
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? full
            : throw new ArgumentException($"Caminho de mídia fora de Media:Root: \"{path}\".", nameof(path));
    }

    internal static string RootOf(MediaOptions options) =>
        string.IsNullOrWhiteSpace(options.Root)
            ? throw new InvalidOperationException(
                $"Diretório de mídia não configurado ({MediaOptions.SectionName}:{nameof(MediaOptions.Root)}).")
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.Root));

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Temporário: o que sobrar não é servido (nome com ponto) nem lido.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
