using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Subsolo.Common.Presentation;

namespace Subsolo.Api.IntegrationTests;

/// <summary>
/// Dublê de teste do SPEC-0039:UT-01 (H37-01): endpoint que só existe para o corpo
/// <see cref="AllowedValuesProbeRequest"/> entrar no documento OpenAPI e passar pelo <c>AllowedValuesSchemaTransformer</c>.
/// Registrado só com <c>ApiFactory(..., withTestEndpoints: true)</c>; o snapshot <c>docs/api/openapi.v1.json</c> não o vê.
/// </summary>
public sealed class AllowedValuesProbeEndpoint : IEndpoint
{
    public const string Route = "/__tests/allowed-values";

    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(Route, IResult (AllowedValuesProbeRequest request) => Results.NoContent()).AllowAnonymous();
}

/// <summary>
/// SPEC-0039:UT-01 — a mesma lista de valores numa propriedade anulável (<c>string?</c>) e numa não anulável
/// (<c>string</c>).
/// </summary>
public sealed record AllowedValuesProbeRequest(
    [property: AllowedValues("a", "b")] string? NullableChoice,
    [property: AllowedValues("a", "b")] string RequiredChoice);
