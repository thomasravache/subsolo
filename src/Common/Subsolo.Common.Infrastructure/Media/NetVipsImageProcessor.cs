using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using NetVips;
using Subsolo.Common.Application.Media;
using Subsolo.Common.Domain;

namespace Subsolo.Common.Infrastructure.Media;

/// <summary>
/// SPEC-0022 (ADR-0015) — <see cref="IImageProcessor"/> com o NetVips (libvips). Confere o tipo pelo conteúdo (o
/// carregador que o libvips escolhe pelos bytes: JPEG, PNG ou WebP), os 10 MB e os 40 megapixels pelo cabeçalho, antes
/// de decodificar; decodifica uma vez só, já reduzida (shrink-on-load), com decodificação estrita (aviso do decodificador
/// vira erro → <see cref="MediaErrors.Unprocessable"/>), e gera <c>thumb</c> e <c>full</c> (WebP) e <c>og</c> (JPEG
/// 1200×630: o flyer inteiro sobre um fundo escuro desfocado dele mesmo), todos sem metadados (EXIF, GPS, XMP, ICC).
/// Um processamento por vez por instância (fila de 1, §5 da SPEC-0022): os outros pedidos esperam a vez.
/// SPEC-0023 (H22-04/H22-05): ao subir, bloqueia no libvips as operações não confiáveis e todos os carregadores fora
/// JPEG, PNG e WebP; os bytes soltos do cabeçalho do JPEG (aviso benigno) saem antes da decodificação estrita
/// (<see cref="JpegHeaderCleaner"/>), e dado corrompido ou arquivo truncado continuam erro; a espera
/// pela vez tem o tempo limite <see cref="MediaOptions.QueueTimeout"/> e, passado, devolve <see cref="MediaErrors.Busy"/>.
/// SPEC-0026 (H24-01): informa as medidas da variante <c>full</c> gerada (<see cref="FlyerImages.FullSize"/>).
/// </summary>
internal sealed class NetVipsImageProcessor : IImageProcessor, IDisposable
{
    private const string JpegLoader = "jpegload_buffer";

    private const string WebPLoader = "webpload_buffer";

    /// <summary>Maior redução na leitura do JPEG (1, 2, 4 ou 8).</summary>
    private const int MaxJpegShrink = 8;

    /// <summary>Carregadores aceitos (o libvips escolhe pelo conteúdo, nunca pela extensão).</summary>
    private static readonly HashSet<string> AcceptedLoaders =
        new([JpegLoader, "pngload_buffer", WebPLoader], StringComparer.Ordinal);

    /// <summary>Altura "sem limite" para reduzir só pela largura.</summary>
    private const int UnboundedHeight = 10_000_000;

    /// <summary>Tela pequena do fundo do <c>og</c>: desfocada e ampliada 5× para 1200×630.</summary>
    private const int BackgroundScale = 5;

    /// <summary>Classes do libvips dos carregadores aceitos (liberados depois de bloquear todos os outros).</summary>
    private static readonly string[] AllowedLoaderClasses = ["VipsForeignLoadJpeg", "VipsForeignLoadPng", "VipsForeignLoadWebp"];

    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly TimeSpan _queueTimeout;

    public NetVipsImageProcessor(IOptions<MediaOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _queueTimeout = options.Value.QueueTimeout;
        // Sem cache de operações do libvips: cada flyer é processado uma vez, e o cache só seguraria memória.
        Cache.Max = 0;
        BlockUnusedLoaders();
    }

    /// <summary>
    /// Bloqueia as operações marcadas como não confiáveis e todos os carregadores, e libera só JPEG, PNG e WebP (vale
    /// para o processo inteiro: o libvips só é usado aqui).
    /// </summary>
    internal static void BlockUnusedLoaders()
    {
        global::NetVips.NetVips.BlockUntrusted = true;
        Operation.Block("VipsForeignLoad", true);
        foreach (var loader in AllowedLoaderClasses)
        {
            Operation.Block(loader, false);
        }
    }

    public async Task<Result<FlyerImages>> ProcessFlyerAsync(Stream original, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (!await _gate.WaitAsync(_queueTimeout, ct))
        {
            return Result.Failure<FlyerImages>(MediaErrors.BusyError());
        }

        try
        {
            var buffer = await ReadUpToLimitAsync(original, ct);
            if (buffer is null)
            {
                return Result.Failure<FlyerImages>(MediaErrors.TooLargeError());
            }

            return await Task.Run(() => Process(buffer), ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private static Result<FlyerImages> Process(byte[] buffer)
    {
        var inspected = Inspect(buffer);
        if (inspected.IsFailure)
        {
            return Result.Failure<FlyerImages>(inspected.Error);
        }

        var header = inspected.Value;
        var sizes = FlyerVariantSizing.For(header.Display);
        try
        {
            // Uma decodificação, já na maior largura que as variantes usam (sem passar da original).
            var masterWidth = Math.Min(header.Display.Width, Math.Max(sizes.Full.Width, sizes.OgFlyer.Width));
            using var master = LoadMaster(buffer, header, masterWidth);

            var (thumb, _) = Variant(master, sizes.Thumb);
            var (full, fullSize) = Variant(master, sizes.Full);
            var og = OpenGraph(master, sizes);
            return Result.Success(new FlyerImages(thumb, full, og, Convert.ToHexStringLower(SHA256.HashData(buffer)), fullSize));
        }
        catch (VipsException)
        {
            return Result.Failure<FlyerImages>(MediaErrors.UnprocessableError());
        }
    }

    /// <summary>Só o cabeçalho: o tipo pelo carregador, os pixels e a orientação (EXIF 5–8 troca largura e altura).</summary>
    private static Result<Header> Inspect(byte[] buffer)
    {
        Image image;
        try
        {
            image = Image.NewFromBuffer(buffer, access: Enums.Access.Sequential);
        }
        catch (VipsException)
        {
            return Result.Failure<Header>(MediaErrors.InvalidTypeError());
        }

        using (image)
        {
            if (image.Get("vips-loader") is not string loader || !AcceptedLoaders.Contains(loader))
            {
                return Result.Failure<Header>(MediaErrors.InvalidTypeError());
            }

            if ((long)image.Width * image.Height > MediaErrors.MaxPixels)
            {
                return Result.Failure<Header>(MediaErrors.TooManyPixelsError());
            }

            var rotated = image.Contains("orientation") && image.Get("orientation") is int orientation && orientation is >= 5 and <= 8;
            var stored = new PixelSize(image.Width, image.Height);
            return Result.Success(new Header(loader, stored, rotated ? new PixelSize(stored.Height, stored.Width) : stored));
        }
    }

    /// <summary>
    /// Decodifica o original com <paramref name="masterWidth"/> de largura (na orientação de exibição), em sRGB 8 bits, na
    /// memória. Decodificação estrita: no JPEG, qualquer aviso do decodificador (dados corrompidos, arquivo truncado) é
    /// erro, depois de tirar os bytes soltos do cabeçalho (SPEC-0023, H22-04: aviso benigno; com <c>FailOn.Truncated</c>
    /// o JPEG corrompido do IT-05 da SPEC-0022 passaria); no PNG e no WebP, os erros. Redução já na leitura quando o formato permite (JPEG <c>shrink</c>, WebP
    /// <c>scale</c>); o resto, depois, por blocos.
    /// </summary>
    private static Image LoadMaster(byte[] buffer, Header header, int masterWidth)
    {
        var ratio = (double)header.Display.Width / masterWidth;
        var options = new VOption();
        var failOn = Enums.FailOn.Error;
        if (header.Loader == JpegLoader)
        {
            failOn = Enums.FailOn.Warning;
            buffer = JpegHeaderCleaner.WithoutExtraneousHeaderBytes(buffer);
            var shrink = 1;
            while (shrink < MaxJpegShrink && shrink * 2 <= ratio)
            {
                shrink *= 2;
            }

            options.Add("shrink", shrink);
        }
        else if (header.Loader == WebPLoader && ratio > 1)
        {
            options.Add("scale", 1 / ratio);
        }

        using var loaded = Image.NewFromBuffer(buffer, access: Enums.Access.Sequential, failOn: failOn, kwargs: options);
        var storedWidth = Math.Max(1, (int)Math.Round(header.Stored.Width / ratio));
        using var resized = loaded.Width > storedWidth
            ? loaded.ThumbnailImage(storedWidth, height: UnboundedHeight, size: Enums.Size.Down, noRotate: true)
            : loaded.Copy();
        using var decoded = resized.CopyMemory();
        using var rotated = decoded.Autorot();
        return ToSrgb(rotated);
    }

    /// <summary>A variante WebP em <paramref name="size"/> e as medidas da imagem gerada (SPEC-0026, H24-01).</summary>
    private static (byte[] Bytes, PixelSize Size) Variant(Image master, PixelSize size)
    {
        using var resized = master.ThumbnailImage(size.Width, height: size.Height, size: Enums.Size.Force);
        return (resized.WebpsaveBuffer(q: 82, keep: Enums.ForeignKeep.None), new PixelSize(resized.Width, resized.Height));
    }

    /// <summary>1200×630: o flyer contido e centralizado sobre ele mesmo cobrindo a tela, desfocado e escurecido.</summary>
    private static byte[] OpenGraph(Image master, FlyerVariantSizes sizes)
    {
        var canvas = sizes.OgCanvas;
        using var small = master.ThumbnailImage(
            canvas.Width / BackgroundScale, height: canvas.Height / BackgroundScale, crop: Enums.Interesting.Centre);
        using var smallOpaque = Opaque(small);
        using var blurred = smallOpaque.Gaussblur(3);
        using var darker = blurred * 0.4;
        using var dark = darker.Cast(Enums.BandFormat.Uchar);
        using var enlarged = dark.Resize(BackgroundScale, kernel: Enums.Kernel.Linear);
        using var background = enlarged.Embed(0, 0, canvas.Width, canvas.Height, extend: Enums.Extend.Copy);

        using var flyer = master.ThumbnailImage(sizes.OgFlyer.Width, height: sizes.OgFlyer.Height, size: Enums.Size.Force);
        using var flyerOpaque = Opaque(flyer);
        using var composed = background.Insert(
            flyerOpaque, (canvas.Width - flyerOpaque.Width) / 2, (canvas.Height - flyerOpaque.Height) / 2);
        return composed.JpegsaveBuffer(q: 85, optimizeCoding: true, keep: Enums.ForeignKeep.None);
    }

    private static Image ToSrgb(Image image)
    {
        if (image.Interpretation == Enums.Interpretation.Srgb && image.Format == Enums.BandFormat.Uchar)
        {
            return image.Copy();
        }

        using var srgb = image.Colourspace(Enums.Interpretation.Srgb);
        return srgb.Cast(Enums.BandFormat.Uchar);
    }

    /// <summary>Sem canal alfa (o JPEG não tem): achatado sobre preto.</summary>
    private static Image Opaque(Image image) =>
        image.HasAlpha() ? image.Flatten(background: [0, 0, 0]) : image.Copy();

    /// <summary>O cabeçalho do original: carregador, medidas como gravadas e na orientação de exibição.</summary>
    private sealed record Header(string Loader, PixelSize Stored, PixelSize Display);

    /// <summary>O conteúdo inteiro, ou <see langword="null"/> se passar de <see cref="MediaErrors.MaxBytes"/> (sem ler além).</summary>
    private static async Task<byte[]?> ReadUpToLimitAsync(Stream original, CancellationToken ct)
    {
        if (original.CanSeek)
        {
            var length = original.Length - original.Position;
            if (length > MediaErrors.MaxBytes)
            {
                return null;
            }

            var content = new byte[length];
            await original.ReadExactlyAsync(content, ct);
            return content;
        }

        using var memory = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await original.ReadAsync(chunk, ct)) > 0)
        {
            if (memory.Length + read > MediaErrors.MaxBytes)
            {
                return null;
            }

            memory.Write(chunk, 0, read);
        }

        return memory.ToArray();
    }
}
