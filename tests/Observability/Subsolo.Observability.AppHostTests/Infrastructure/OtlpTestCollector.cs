using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Subsolo.Observability.AppHostTests.Infrastructure;

/// <summary>Span recebido pelo <see cref="OtlpTestCollector"/>, com o <c>service.name</c> do recurso que o exportou.</summary>
internal sealed record CollectedSpan(
    string TraceId,
    string SpanId,
    string Name,
    string ServiceName,
    IReadOnlyDictionary<string, string> Attributes);

/// <summary>
/// Coletor OTLP/HTTP de teste, em memória: recebe <c>POST /v1/traces</c> em protobuf (<c>application/x-protobuf</c>)
/// ou JSON (<c>application/json</c>), com ou sem gzip, e guarda os spans; <c>/v1/logs</c> e <c>/v1/metrics</c> são
/// aceitos e descartados. Os serviços apontam para ele com <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> e
/// <c>OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf</c>.
/// </summary>
internal sealed class OtlpTestCollector : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentQueue<CollectedSpan> _spans = new();

    private OtlpTestCollector(WebApplication app) => _app = app;

    public Uri Endpoint { get; private set; } = null!;

    public IReadOnlyList<CollectedSpan> Spans => [.. _spans];

    public static async Task<OtlpTestCollector> StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var collector = new OtlpTestCollector(app);

        app.MapPost("/v1/traces", collector.ReceiveTracesAsync);
        app.MapPost("/v1/logs", Accept);
        app.MapPost("/v1/metrics", Accept);

        await app.StartAsync(cancellationToken);
        var address = app.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>()
            .Addresses.First();
        collector.Endpoint = new Uri(address);
        return collector;
    }

    /// <summary>Spans do trace <paramref name="traceId"/> (hexadecimal, 32 caracteres).</summary>
    public IReadOnlyList<CollectedSpan> Trace(string traceId) =>
        [.. _spans.Where(span => string.Equals(span.TraceId, traceId, StringComparison.OrdinalIgnoreCase))];

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private static IResult Accept() => Results.Bytes([], "application/x-protobuf");

    private async Task<IResult> ReceiveTracesAsync(HttpRequest request)
    {
        using var body = new MemoryStream();
        Stream input = request.Body;
        if (string.Equals(request.Headers.ContentEncoding, "gzip", StringComparison.OrdinalIgnoreCase))
        {
            input = new GZipStream(request.Body, CompressionMode.Decompress);
        }

        await input.CopyToAsync(body, request.HttpContext.RequestAborted);
        var isJson = request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true;
        foreach (var span in isJson ? ParseJson(body.ToArray()) : ParseProtobuf(body.ToArray()))
        {
            _spans.Enqueue(span);
        }

        return isJson ? Results.Json(new { }) : Accept();
    }

    // ExportTraceServiceRequest { repeated ResourceSpans resource_spans = 1; }
    private static IEnumerable<CollectedSpan> ParseProtobuf(ReadOnlyMemory<byte> message)
    {
        var request = new ProtobufReader(message);
        while (request.TryReadField(out var field))
        {
            if (field != 1)
            {
                request.Skip();
                continue;
            }

            foreach (var span in ParseResourceSpans(request.ReadBytes()))
            {
                yield return span;
            }
        }
    }

    // ResourceSpans { Resource resource = 1; repeated ScopeSpans scope_spans = 2; }
    private static List<CollectedSpan> ParseResourceSpans(ReadOnlyMemory<byte> message)
    {
        var reader = new ProtobufReader(message);
        var resource = new Dictionary<string, string>(StringComparer.Ordinal);
        var scopes = new List<ReadOnlyMemory<byte>>();
        while (reader.TryReadField(out var field))
        {
            switch (field)
            {
                case 1:
                    // Resource { repeated KeyValue attributes = 1; }
                    var resourceReader = new ProtobufReader(reader.ReadBytes());
                    while (resourceReader.TryReadField(out var resourceField))
                    {
                        if (resourceField == 1)
                        {
                            AddKeyValue(resource, resourceReader.ReadBytes());
                        }
                        else
                        {
                            resourceReader.Skip();
                        }
                    }

                    break;
                case 2:
                    scopes.Add(reader.ReadBytes());
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        var service = resource.GetValueOrDefault("service.name") ?? string.Empty;
        var spans = new List<CollectedSpan>();
        foreach (var scope in scopes)
        {
            // ScopeSpans { InstrumentationScope scope = 1; repeated Span spans = 2; }
            var scopeReader = new ProtobufReader(scope);
            while (scopeReader.TryReadField(out var scopeField))
            {
                if (scopeField == 2)
                {
                    spans.Add(ParseSpan(scopeReader.ReadBytes(), service));
                }
                else
                {
                    scopeReader.Skip();
                }
            }
        }

        return spans;
    }

    // Span { bytes trace_id = 1; bytes span_id = 2; string name = 5; repeated KeyValue attributes = 9; ... }
    private static CollectedSpan ParseSpan(ReadOnlyMemory<byte> message, string service)
    {
        var reader = new ProtobufReader(message);
        string traceId = string.Empty, spanId = string.Empty, name = string.Empty;
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.TryReadField(out var field))
        {
            switch (field)
            {
                case 1:
                    traceId = Convert.ToHexStringLower(reader.ReadBytes().Span);
                    break;
                case 2:
                    spanId = Convert.ToHexStringLower(reader.ReadBytes().Span);
                    break;
                case 5:
                    name = reader.ReadString();
                    break;
                case 9:
                    AddKeyValue(attributes, reader.ReadBytes());
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return new CollectedSpan(traceId, spanId, name, service, attributes);
    }

    // KeyValue { string key = 1; AnyValue value = 2; }
    // AnyValue { string string_value = 1; bool bool_value = 2; int64 int_value = 3; double double_value = 4; ... }
    private static void AddKeyValue(Dictionary<string, string> target, ReadOnlyMemory<byte> message)
    {
        var reader = new ProtobufReader(message);
        string key = string.Empty, value = string.Empty;
        while (reader.TryReadField(out var field))
        {
            if (field == 1)
            {
                key = reader.ReadString();
            }
            else if (field == 2)
            {
                var any = new ProtobufReader(reader.ReadBytes());
                while (any.TryReadField(out var anyField))
                {
                    value = anyField switch
                    {
                        1 => any.ReadString(),
                        2 => (any.ReadVarint() != 0).ToString(CultureInfo.InvariantCulture),
                        3 => ((long)any.ReadVarint()).ToString(CultureInfo.InvariantCulture),
                        4 => any.ReadDouble().ToString(CultureInfo.InvariantCulture),
                        _ => SkipAndKeep(any, value),
                    };
                }
            }
            else
            {
                reader.Skip();
            }
        }

        target[key] = value;
    }

    private static string SkipAndKeep(ProtobufReader reader, string value)
    {
        reader.Skip();
        return value;
    }

    // OTLP/JSON: {"resourceSpans":[{"resource":{"attributes":[...]},"scopeSpans":[{"spans":[...]}]}]}
    private static List<CollectedSpan> ParseJson(byte[] body)
    {
        using var document = JsonDocument.Parse(body);
        var spans = new List<CollectedSpan>();
        if (!document.RootElement.TryGetProperty("resourceSpans", out var resourceSpans))
        {
            return spans;
        }

        foreach (var resourceSpan in resourceSpans.EnumerateArray())
        {
            var resource = resourceSpan.TryGetProperty("resource", out var r) ? JsonAttributes(r) : [];
            var service = resource.GetValueOrDefault("service.name") ?? string.Empty;
            if (!resourceSpan.TryGetProperty("scopeSpans", out var scopeSpans))
            {
                continue;
            }

            foreach (var span in scopeSpans.EnumerateArray()
                         .Where(scope => scope.TryGetProperty("spans", out _))
                         .SelectMany(scope => scope.GetProperty("spans").EnumerateArray()))
            {
                spans.Add(new CollectedSpan(
                    span.TryGetProperty("traceId", out var t) ? t.GetString() ?? string.Empty : string.Empty,
                    span.TryGetProperty("spanId", out var s) ? s.GetString() ?? string.Empty : string.Empty,
                    span.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                    service,
                    JsonAttributes(span)));
            }
        }

        return spans;
    }

    private static Dictionary<string, string> JsonAttributes(JsonElement owner)
    {
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!owner.TryGetProperty("attributes", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return attributes;
        }

        foreach (var attribute in list.EnumerateArray())
        {
            var key = attribute.GetProperty("key").GetString() ?? string.Empty;
            var value = attribute.TryGetProperty("value", out var v) ? v.EnumerateObject().Select(p => p.Value.ToString()).FirstOrDefault() : null;
            attributes[key] = value ?? string.Empty;
        }

        return attributes;
    }
}
