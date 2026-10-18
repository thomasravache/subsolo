namespace Subsolo.ArchitectureTests.ModularMonolith.MediaSamples.Shows.Application;

/// <summary>
/// SPEC-0022 IT-08 — dublê: um módulo que grava o flyer direto no disco com <c>System.IO.File</c>, sem o
/// <c>IFileStorage</c> (viola o ADR-0015). Só é avaliado pelo teste da violação.
/// </summary>
public static class DirectFlyerWriter
{
    public static Task SaveAsync(string path, byte[] content, CancellationToken ct) =>
        File.WriteAllBytesAsync(path, content, ct);
}
