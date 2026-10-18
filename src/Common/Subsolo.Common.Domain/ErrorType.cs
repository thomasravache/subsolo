namespace Subsolo.Common.Domain;

/// <summary>
/// SPEC-0007 — tipo do <see cref="Error"/>, que define o status HTTP: Validation 400 | NotFound 404 |
/// Conflict 409 | Unauthorized 401 | Forbidden 403 | Failure 500.
/// SPEC-0022 (Emenda v2) — Unprocessable 422: a entrada é bem formada, mas não pôde ser processada (ex.: imagem corrompida).
/// </summary>
public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Forbidden = 4,
    Unauthorized = 5,
    Unprocessable = 6,
}
