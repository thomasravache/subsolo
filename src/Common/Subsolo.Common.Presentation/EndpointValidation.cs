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
    /// <summary>Valida <paramref name="request"/>: válido → <see langword="null"/>; senão, o ProblemDetails 400.</summary>
    public static async Task<IResult?> ValidateAsync<T>(
        IValidator<T> validator,
        T request,
        string description,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(validator);
        var validation = await validator.ValidateAsync(request, cancellationToken);
        return validation.IsValid ? null : ApiResults.Problem(Result.Failure(ToError(validation, description)));
    }

    /// <summary>Erro de validação com o código da primeira falha e as mensagens de cada campo inválido (sem repetir).</summary>
    public static Error ToError(ValidationResult validation, string description)
    {
        ArgumentNullException.ThrowIfNull(validation);
        var fields = validation.Errors
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        return Error.Validation(validation.Errors[0].ErrorCode, description, fields);
    }
}
