using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Subsolo.Common.Domain;

namespace Subsolo.Common.Presentation;

/// <summary>
/// SPEC-0030 (H28-06) — validação no endpoint, antes do handler, compartilhada pelos módulos (sai do
/// <c>EndpointValidation</c> do Events e das cópias no Profiles e no <c>SiteEndpoints</c>): a resposta leva o código da
/// primeira falha e os campos inválidos, em vez do código genérico do decorator de validação.
/// </summary>
public static class EndpointValidation
{
    /// <summary>Valida <paramref name="request"/>: válido → <see langword="null"/>; senão, o ProblemDetails 400. Esqueleto (G1).</summary>
    public static Task<IResult?> ValidateAsync<T>(
        IValidator<T> validator,
        T request,
        string description,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException("SPEC-0030 (H28-06): EndpointValidation ainda não implementado.");

    /// <summary>Erro de validação com o código da primeira falha e as mensagens de cada campo inválido. Esqueleto (G1).</summary>
    public static Error ToError(ValidationResult validation, string description) =>
        throw new NotImplementedException("SPEC-0030 (H28-06): EndpointValidation ainda não implementado.");
}
