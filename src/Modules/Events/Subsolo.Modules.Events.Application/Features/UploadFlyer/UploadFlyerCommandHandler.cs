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
/// (<see cref="IImageProcessor"/>: limite de pixels, metadados removidos, variantes; SPEC-0023: o hash devolvido tem de
/// ser o do id, e a fila cheia além do tempo limite devolve <see cref="MediaErrors.Busy"/>, o 503), grava as três variantes
/// (<see cref="IFileStorage"/>) e a linha de <c>events.flyers</c>. Se algo falha depois de gravar uma variante, as
/// gravadas são apagadas: nada fica pela metade. O original não é guardado. SPEC-0024: grava as medidas da variante
/// <c>full</c> na linha; com a linha mas sem algum arquivo (H23-01), regrava as variantes; com a linha apagada pela
/// limpeza depois da leitura (H23-02), processa e grava de novo em vez de 500. SPEC-0026: as medidas do <c>full</c> vêm
/// do processador (<see cref="FlyerImages.FullSize"/>, H24-01), e a gravação dos arquivos e da linha acontece sob a trava
/// do id (<see cref="IFlyerLocks"/>, H24-02), a mesma da limpeza.
/// </summary>
internal sealed partial class UploadFlyerCommandHandler(
    IImageProcessor processor,
    IFileStorage storage,
    IFlyerRepository flyers,
    IEventsUnitOfWork unitOfWork,
    TimeProvider clock,
    ILogger<UploadFlyerCommandHandler> logger,
    IFlyerLocks? locks = null)
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
        var existing = await flyers.GetAsync(id, cancellationToken);
        if (existing is not null && await VariantsInPlaceAsync(id, cancellationToken))
        {
            // SPEC-0023 (H22-01): reenviar um flyer ainda sem show renova a contagem da limpeza.
            existing.Renew(clock.GetUtcNow());
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return Existing(id);
            }
            catch (MissingFlyerException)
            {
                // SPEC-0024 (H23-02): a limpeza apagou a linha depois da leitura; o arquivo é processado de novo.
                existing = null;
            }
        }

        // SPEC-0024 (H23-01): sem linha, ou com a linha mas sem algum arquivo (apagado por uma limpeza no meio), as
        // variantes são geradas e gravadas de novo.
        content.Position = 0;
        var started = Stopwatch.GetTimestamp();
        var processed = await processor.ProcessFlyerAsync(content, cancellationToken);
        EventsTelemetry.Processed(Stopwatch.GetElapsedTime(started));
        if (processed.IsFailure)
        {
            return Rejected(ToEventsError(processed.Error));
        }

        var images = processed.Value;
        if (!string.Equals(images.ContentHash, id, StringComparison.Ordinal))
        {
            // SPEC-0023 (H22-05): o processador leu outro conteúdo que não o do id; nada é gravado.
            return Rejected(EventsErrors.FlyerUnprocessableError());
        }

        var size = images.FullSize;
        var saved = new List<string>();
        var created = existing is null;
        await using var held = locks is null ? null : await locks.AcquireAsync(id, cancellationToken);
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

            if (existing is not null)
            {
                existing.Renew(clock.GetUtcNow());
                Measure(existing, size);
                try
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (MissingFlyerException)
                {
                    // A linha sumiu enquanto as variantes eram gravadas: grava a linha de novo.
                    created = true;
                    await AddAsync(id, command.UploadedBy, size, cancellationToken);
                }
            }
            else
            {
                await AddAsync(id, command.UploadedBy, size, cancellationToken);
            }

            if (held is not null)
            {
                await held.CommitAsync(cancellationToken);
            }
        }
        catch (DuplicateFlyerException)
        {
            // Outro envio do mesmo arquivo gravou a linha antes: as variantes (mesmo conteúdo) ficam.
            return Existing(id);
        }
        catch
        {
            if (created)
            {
                await DeleteAsync(saved);
            }

            throw;
        }

        if (!created)
        {
            return Existing(id);
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
        MediaErrors.Busy => error,
        _ => error,
    };

    private async Task AddAsync(string id, Guid uploadedBy, PixelSize? size, CancellationToken cancellationToken)
    {
        var flyer = Flyer.Create(id, uploadedBy, clock.GetUtcNow());
        Measure(flyer, size);
        flyers.Add(flyer);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void Measure(Flyer flyer, PixelSize? size)
    {
        if (size is { } full)
        {
            flyer.MeasureFull(full.Width, full.Height);
        }
    }

    /// <summary>As três variantes de <paramref name="id"/> estão gravadas.</summary>
    private async Task<bool> VariantsInPlaceAsync(string id, CancellationToken cancellationToken)
    {
        foreach (var path in Flyer.VariantPaths(id))
        {
            await using var file = await storage.OpenAsync(path, cancellationToken);
            if (file is null)
            {
                return false;
            }
        }

        return true;
    }

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
