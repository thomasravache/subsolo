using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Subsolo.Common.Application.Media;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application.Diagnostics;
using Subsolo.Modules.Events.Domain.Flyers;

namespace Subsolo.Modules.Events.Application.Features.UploadFlyer;

/// <summary>
/// SPEC-0022 — confere o tipo e o tamanho pelo começo do arquivo (<see cref="FlyerUploadRules"/>), calcula o id
/// (SHA-256 do original) e, se o flyer já existe, devolve o mesmo sem reprocessar. Senão, processa
/// (<see cref="IImageProcessor"/>: limite de pixels, metadados removidos, variantes), grava as três variantes
/// (<see cref="IFileStorage"/>) e a linha de <c>events.flyers</c>. Se algo falha depois de gravar uma variante, as
/// gravadas são apagadas: nada fica pela metade. O original não é guardado.
/// </summary>
internal sealed partial class UploadFlyerCommandHandler(
    IImageProcessor processor,
    IFileStorage storage,
    IFlyerRepository flyers,
    IEventsUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<UploadFlyerCommandHandler> logger)
    : ICommandHandler<UploadFlyerCommand, FlyerUploadResult>
{
    public async Task<Result<FlyerUploadResult>> Handle(UploadFlyerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var content = command.Content;

        var head = new byte[FlyerUploadRules.SignatureLength];
        var read = await content.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken);
        var rules = FlyerUploadRules.Check(head.AsSpan(0, read), command.Length);
        if (rules.IsFailure)
        {
            return Rejected(rules.Error);
        }

        content.Position = 0;
        var id = Convert.ToHexStringLower(await SHA256.HashDataAsync(content, cancellationToken));
        if (await flyers.ExistsAsync(id, cancellationToken))
        {
            return Existing(id);
        }

        content.Position = 0;
        var started = Stopwatch.GetTimestamp();
        var processed = await processor.ProcessFlyerAsync(content, cancellationToken);
        EventsTelemetry.Processed(Stopwatch.GetElapsedTime(started));
        if (processed.IsFailure)
        {
            return Rejected(ToEventsError(processed.Error));
        }

        var images = processed.Value;
        var saved = new List<string>();
        try
        {
            foreach (var (path, bytes) in new[]
            {
                (Flyer.VariantPath(id, Flyer.Thumb), images.Thumb),
                (Flyer.VariantPath(id, Flyer.Full), images.Full),
                (Flyer.VariantPath(id, Flyer.Og), images.Og),
            })
            {
                using var variant = new MemoryStream(bytes, writable: false);
                await storage.SaveAsync(path, variant, cancellationToken);
                saved.Add(path);
            }

            flyers.Add(Flyer.Create(id, command.UploadedBy, clock.GetUtcNow()));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateFlyerException)
        {
            // Outro envio do mesmo arquivo gravou a linha antes: as variantes (mesmo conteúdo) ficam.
            return Existing(id);
        }
        catch
        {
            await DeleteAsync(saved);
            throw;
        }

        LogFlyerUploaded(logger, command.UploadedBy, id);
        EventsTelemetry.Uploaded("created");
        return Result.Success(new FlyerUploadResult(Response(id), Created: true));
    }

    /// <summary>As URLs públicas das variantes de <paramref name="id"/>.</summary>
    internal static FlyerUploadedResponse Response(string id) => new(
        id,
        MediaPaths.PublicUrl(Flyer.VariantPath(id, Flyer.Thumb)),
        MediaPaths.PublicUrl(Flyer.VariantPath(id, Flyer.Full)),
        MediaPaths.PublicUrl(Flyer.VariantPath(id, Flyer.Og)));

    private static Result<FlyerUploadResult> Existing(string id)
    {
        EventsTelemetry.Uploaded("existing");
        return Result.Success(new FlyerUploadResult(Response(id), Created: false));
    }

    private static Result<FlyerUploadResult> Rejected(Error error)
    {
        EventsTelemetry.Uploaded(error.Code);
        return Result.Failure<FlyerUploadResult>(error);
    }

    private static Error ToEventsError(Error error) => error.Code switch
    {
        MediaErrors.InvalidType => EventsErrors.FlyerInvalidTypeError(),
        MediaErrors.TooLarge => EventsErrors.FlyerTooLargeError(),
        MediaErrors.TooManyPixels => EventsErrors.FlyerTooManyPixelsError(),
        MediaErrors.Unprocessable => EventsErrors.FlyerUnprocessableError(),
        _ => error,
    };

    private async Task DeleteAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            await storage.DeleteAsync(path, CancellationToken.None);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Auditoria: usuário {UserId} enviou o flyer {FlyerId}")]
    private static partial void LogFlyerUploaded(ILogger logger, Guid userId, string flyerId);
}
