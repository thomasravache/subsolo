using System.Diagnostics.CodeAnalysis;

namespace Subsolo.Common.Domain;

/// <summary>SPEC-0007 — erro de negócio com código estável (vai em <c>extensions.code</c> do ProblemDetails).</summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Nome fixado pelo contrato da SPEC-0007; o projeto é só C#.")]
public sealed record Error(string Code, string Description, ErrorType Type)
{
    /// <summary>Campos inválidos e suas mensagens; preenchido só em erros <see cref="ErrorType.Validation"/>.</summary>
    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; init; } = new Dictionary<string, string[]>();

    public static Error Failure(string code, string description) => throw new NotImplementedException();

    public static Error Validation(string code, string description, IReadOnlyDictionary<string, string[]> validationErrors) =>
        throw new NotImplementedException();

    public static Error NotFound(string code, string description) => throw new NotImplementedException();

    public static Error Conflict(string code, string description) => throw new NotImplementedException();

    public static Error Forbidden(string code, string description) => throw new NotImplementedException();

    public static Error Unauthorized(string code, string description) => throw new NotImplementedException();
}
