using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Subsolo.Api.OpenApi;

/// <summary>
/// SPEC-0037 (H33-07) — a propriedade de texto marcada com <see cref="AllowedValuesAttribute"/> sai no OpenAPI como o
/// <c>enum</c> desses valores (ex.: <c>ReportShowRequest.reason</c>). Só documenta: o valor fora da lista continua
/// recusado pelo validador do módulo, com o código de erro dele. SPEC-0039 (H37-01): a propriedade anulável em C#
/// (<c>string?</c>) mantém o <c>null</c> — tipo <c>string | null</c> e <c>null</c> no <c>enum</c>; a não anulável sai só
/// <c>string</c>, com só os valores da lista.
/// </summary>
internal sealed class AllowedValuesSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);
        var property = context.JsonPropertyInfo;
        var allowed = property?.AttributeProvider?
            .GetCustomAttributes(typeof(AllowedValuesAttribute), inherit: true)
            .OfType<AllowedValuesAttribute>()
            .FirstOrDefault();
        if (allowed is not null)
        {
            var values = allowed.Values.OfType<string>().ToList();
            if (values.Count > 0)
            {
                // A anulabilidade vem do tipo .NET da propriedade (anotações de nulo do C#), a mesma que o gerador usa.
                var nullable = property!.IsGetNullable || property.IsSetNullable;
                schema.Type = nullable ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String;
                List<JsonNode> allowedValues = [.. values.Select(value => (JsonNode)JsonValue.Create(value))];
                if (nullable)
                {
                    // O null do JSON é o nó nulo do System.Text.Json.Nodes.
                    allowedValues.Add(null!);
                }

                schema.Enum = allowedValues;
            }
        }

        return Task.CompletedTask;
    }
}
