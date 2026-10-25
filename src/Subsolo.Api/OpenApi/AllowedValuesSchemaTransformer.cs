using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Subsolo.Api.OpenApi;

/// <summary>
/// SPEC-0037 (H33-07) — a propriedade de texto marcada com <see cref="AllowedValuesAttribute"/> sai no OpenAPI como o
/// <c>enum</c> desses valores (ex.: <c>ReportShowRequest.reason</c>). Só documenta: o valor fora da lista continua
/// recusado pelo validador do módulo, com o código de erro dele.
/// </summary>
internal sealed class AllowedValuesSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);
        var allowed = context.JsonPropertyInfo?.AttributeProvider?
            .GetCustomAttributes(typeof(AllowedValuesAttribute), inherit: true)
            .OfType<AllowedValuesAttribute>()
            .FirstOrDefault();
        if (allowed is not null)
        {
            var values = allowed.Values.OfType<string>().ToList();
            if (values.Count > 0)
            {
                schema.Type = JsonSchemaType.String;
                schema.Enum = [.. values.Select(value => (JsonNode)JsonValue.Create(value))];
            }
        }

        return Task.CompletedTask;
    }
}
