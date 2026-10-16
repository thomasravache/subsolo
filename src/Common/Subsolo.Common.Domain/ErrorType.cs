namespace Subsolo.Common.Domain;

/// <summary>
/// SPEC-0007 — tipo do <see cref="Error"/>, que define o status HTTP: Validation 400 | NotFound 404 |
/// Conflict 409 | Unauthorized 401 | Forbidden 403 | Failure 500.
/// </summary>
public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Forbidden = 4,
    Unauthorized = 5,
}
