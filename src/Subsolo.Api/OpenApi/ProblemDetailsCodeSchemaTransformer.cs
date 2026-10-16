using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Subsolo.Common.Presentation;

namespace Subsolo.Api.OpenApi;

/// <summary>
/// SPEC-0008 — documenta no schema <c>ProblemDetails</c> a extensão <see cref="ApiResults.CodeExtension"/> (código
/// estável do erro, ex.: <c>agenda.city_not_found</c>) que toda resposta de erro da API carrega.
/// </summary>
internal sealed class ProblemDetailsCodeSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);
        if (context.JsonTypeInfo.Type == typeof(ProblemDetails))
        {
            schema.Properties ??= new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal);
            schema.Properties[ApiResults.CodeExtension] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Description = "Código estável do erro (ex.: agenda.invalid_slug, agenda.city_not_found).",
            };
        }

        return Task.CompletedTask;
    }
}
