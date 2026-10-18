using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Subsolo.Events.IntegrationTests.Infrastructure;

/// <summary>
/// SPEC-0023 (H22-03) — dublê do limite de corpo do Kestrel para o <c>TestServer</c> (que não implementa
/// <see cref="IHttpMaxRequestBodySizeFeature"/>): cada requisição ganha o recurso, com o padrão do Kestrel
/// (<see cref="KestrelDefault"/>), que o endpoint pode trocar antes de começar a ler; passar do limite durante a leitura
/// lança <see cref="BadHttpRequestException"/> 413, como o Kestrel, com ou sem <c>Content-Length</c>. Guarda o limite
/// em vigor quando a leitura começou (<see cref="LimitsAtFirstRead"/>).
/// </summary>
internal sealed class EmulatedKestrelBodyLimit : IStartupFilter
{
    /// <summary><c>KestrelServerLimits.MaxRequestBodySize</c> padrão (~28,6 MiB).</summary>
    public const long KestrelDefault = 30_000_000;

    /// <summary>O limite em vigor na primeira leitura do corpo de cada requisição a <c>/api/v1/flyers</c>.</summary>
    public ConcurrentQueue<long?> LimitsAtFirstRead { get; } = new();

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            var feature = new BodySizeFeature(context.Request.Body, context.Request.Path.StartsWithSegments("/api/v1/flyers") ? LimitsAtFirstRead : null);
            context.Features.Set<IHttpMaxRequestBodySizeFeature>(feature);
            context.Request.Body = feature.Body;
            await nextMiddleware(context);
        });
        next(app);
    };

    private sealed class BodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        private long? _limit = KestrelDefault;

        public BodySizeFeature(Stream inner, ConcurrentQueue<long?>? record) => Body = new LimitedStream(inner, this, record);

        public Stream Body { get; }

        public bool Started { get; set; }

        public bool IsReadOnly => Started;

        public long? MaxRequestBodySize
        {
            get => _limit;
            set
            {
                if (Started)
                {
                    throw new InvalidOperationException("O limite do corpo não pode mudar depois que a leitura começou.");
                }

                _limit = value;
            }
        }
    }

    private sealed class LimitedStream(Stream inner, BodySizeFeature feature, ConcurrentQueue<long?>? record) : Stream
    {
        private long _read;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _read;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Count(await inner.ReadAsync(PrepareBuffer(buffer), cancellationToken));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private Memory<byte> PrepareBuffer(Memory<byte> buffer)
        {
            if (!feature.Started)
            {
                feature.Started = true;
                record?.Enqueue(feature.MaxRequestBodySize);
            }

            return buffer;
        }

        private int Count(int read)
        {
            PrepareBuffer(Memory<byte>.Empty);
            _read += read;
            if (feature.MaxRequestBodySize is { } limit && _read > limit)
            {
                throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);
            }

            return read;
        }
    }
}
