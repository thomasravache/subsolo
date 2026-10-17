using System.Globalization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Subsolo.Common.Presentation.Security;

namespace Subsolo.Api.Security;

/// <summary>
/// SPEC-0014 — no OpenAPI, a descrição de cada resposta de erro declarada com <c>ProducesProblemCode</c> passa a citar o
/// código estável dela (ex.: <c>Unauthorized (code=auth.required)</c>).
/// </summary>
internal sealed class ProblemCodeOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);
        var codes = context.Description.ActionDescriptor.EndpointMetadata.OfType<ProblemCodeMetadata>();
        foreach (var code in codes)
        {
            var status = code.StatusCode.ToString(CultureInfo.InvariantCulture);
            if (operation.Responses?.TryGetValue(status, out var response) == true && response is OpenApiResponse documented)
            {
                documented.Description = $"{documented.Description} (code={code.Code})".TrimStart();
            }
        }

        return Task.CompletedTask;
    }
}
